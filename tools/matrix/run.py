"""The complete matrix's driver: judge every row by every check that applies, chunk by chunk.

    python tools/matrix/run.py ROWS.jsonl --run NAME [--chunk 500] [--max-minutes 40] [--ablate X]
    python tools/matrix/run.py --run NAME --judge-only

Results go to .scratch/matrix/results/NAME/ (git-ignored). The rows are cut into chunks; each chunk
runs these stages, each writing its own file and a `.done` marker, so a stopped run resumes at the
first unfinished stage (a half-written port or Python stage resumes at its next row):

  port   tools/matrix/port-runner.cs on a DEBUG build (asserts throw): C2, C3, C4 (port judge), C5,
         and the base answers C1x and C6 are compared with;
  c1     tools/record-oracle.py --rows, then the oracle consumer's The_wave_agrees_with_upstream
         (Release), for every row the recorder can ask;
  c1x    upstream's own finditer for the rows it cannot (partial or pos/endpos);
  c4up   tools/matrix/d11-brute-judge.py, upstream-based, for partial rows;
  c6     tools/probes/fuzzy-reference-matcher.py, for rows inside its subset.

A run stops launching stages after --max-minutes (default 40), so no chunk of wall time passes 45
minutes; launch it again to continue. It also waits while the machine has under 4 GB free and
kills a stage that takes over its cap. Once every chunk is done it judges: `cells.csv` (per check,
per cell: rows, n/a, failures), `failures.jsonl` (row, check, kind, both answers), `summary.md`
(the Part B table, with the smallest failing row per check as its witness) and `cost.json`.

A check that does not apply to a row is n/a, never a pass.
"""

from __future__ import annotations

import argparse
import collections
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

import psutil

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
RESULTS = REPO / ".scratch" / "matrix" / "results"
sys.path.insert(0, str(HERE))
import gen  # noqa: E402

STAGES = ("port", "c1", "c1x", "c4up", "c6")
STAGE_CAP_SECONDS = 30 * 60
MIN_FREE_BYTES = 4 * 1024**3
ORACLE_KEYS = ("pattern", "flags", "namedLists", "subject", "operation", "partial", "pos", "endpos")
CHECKS = ("C1", "C1x", "C2", "C3", "C4", "C5", "C6")
UNANSWERED = ("ERR Timeout", "ERR StepLimit", "ERR RowBudget", "ERR Hang", "ERR Crash")


def log(msg: str) -> None:
    print(time.strftime("%H:%M:%S ") + msg, flush=True)


def free_bytes() -> int:
    return psutil.virtual_memory().available


def run_stage(cmd: list[str], env: dict | None, cwd: Path, logfile: Path) -> tuple[int, float, int]:
    """Runs one stage, returns (exit code, seconds, peak RSS of its process tree in bytes)."""
    start = time.time()
    peak = 0
    with open(logfile, "ab") as lf:
        proc = psutil.Popen(cmd, cwd=cwd, env=env, stdin=subprocess.DEVNULL, stdout=lf, stderr=subprocess.STDOUT)
        while True:
            try:
                code = proc.wait(timeout=1)
                break
            except psutil.TimeoutExpired:
                pass
            try:
                rss = proc.memory_info().rss + sum(c.memory_info().rss for c in proc.children(recursive=True))
                peak = max(peak, rss)
            except psutil.Error:
                pass
            if free_bytes() < MIN_FREE_BYTES // 2 or time.time() - start > STAGE_CAP_SECONDS:
                log(f"  killing stage: free {free_bytes() >> 20} MB, {time.time() - start:.0f} s")
                for c in proc.children(recursive=True):
                    c.kill()
                proc.kill()
                code = -9
                break
    return code, time.time() - start, peak


def lines(path: Path) -> int:
    return sum(1 for line in open(path, encoding="utf-8") if line.strip()) if path.exists() else 0


def prepare(rows_path: Path, out: Path, chunk: int) -> list[Path]:
    out.mkdir(parents=True, exist_ok=True)
    rows = [json.loads(line) for line in open(rows_path, encoding="utf-8") if line.strip()]
    if not (out / "rows.jsonl").exists():
        shutil.copyfile(rows_path, out / "rows.jsonl")
    chunks = []
    for k in range(0, len(rows), chunk):
        path = out / f"chunk-{k // chunk:03}.jsonl"
        if not path.exists():
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                for row in rows[k:k + chunk]:
                    f.write(json.dumps(row, ensure_ascii=True) + "\n")
        chunks.append(path)
    return chunks


def oracle_row(row: dict) -> bool:
    return not (row["operation"] == "finditer" and (row.get("partial") or row.get("pos") is not None))


def stage(name: str, chunk: Path, out: Path, ablate: str) -> tuple[int, float, int]:
    stem = chunk.with_suffix("")
    env = dict(os.environ)
    env["PYTHONDONTWRITEBYTECODE"] = "1"
    logfile = out / "stages.log"
    if name == "port":
        target = stem.with_name(stem.name + ".port.jsonl")
        env["DOTNET_GCHeapHardLimit"] = "0xC0000000"  # 3 GiB: a runaway row throws, it does not swap
        cmd = ["dotnet", "run", "-c", "Debug", str(HERE / "port-runner.cs"), "--", str(chunk), str(target),
               "--start", str(lines(target)), "--ablate", ablate]
        return run_stage(cmd, env, REPO, logfile)
    if name in ("c1x", "c4up", "c6"):
        target = stem.with_name(stem.name + f".{name}.jsonl")
        return run_stage([sys.executable, str(HERE / "pyworker.py"), name, str(chunk), str(target)], env, REPO, logfile)
    # c1: the oracle's own recorder and comparer.
    rows = [json.loads(line) for line in open(chunk, encoding="utf-8") if line.strip()]
    asked = [r for r in rows if oracle_row(r)]
    q = stem.with_name(stem.name + ".oracle-rows.jsonl")
    with open(q, "w", encoding="utf-8", newline="\n") as f:
        for r in asked:
            row = {k: r[k] for k in ORACLE_KEYS if k in r}
            row["generator"] = "rows"
            f.write(json.dumps(row, ensure_ascii=True) + "\n")
    with open(stem.with_name(stem.name + ".oracle-ids.json"), "w", encoding="utf-8") as f:
        json.dump([r["id"] for r in asked], f)
    wave = stem.with_name(stem.name + ".wave.jsonl")
    report = stem.with_name(stem.name + ".report.txt")
    code, s1, p1 = run_stage([sys.executable, str(REPO / "tools" / "record-oracle.py"), "--rows", str(q),
                              "--output", str(wave)], env, REPO, logfile)
    if code != 0:
        return code, s1, p1
    env["FUZZYREGEX_ORACLE_WAVE_PATH"] = str(wave)
    env["FUZZYREGEX_ORACLE_REPORT_PATH"] = str(report)
    code, s2, p2 = run_stage(["dotnet", "test", "--project", str(REPO / "tests" / "FuzzyRegex.OracleTests" /
                                                                 "FuzzyRegex.OracleTests.csproj"),
                              "-c", "Release", "--treenode-filter", "/*/*/OracleWaveTests/The_wave_agrees_with_upstream"],
                             env, REPO, logfile)
    # A red test is the divergences; only a missing report is a failed stage.
    return (0 if report.exists() else code or 1), s1 + s2, max(p1, p2)


def drive(out: Path, chunks: list[Path], max_minutes: float, ablate: str) -> bool:
    began = time.time()
    timings = out / "timings.jsonl"
    for chunk in chunks:
        for name in STAGES:
            marker = chunk.with_suffix(f".{name}.done")
            if marker.exists():
                continue
            if (time.time() - began) / 60 > max_minutes:
                log(f"stopping at {max_minutes} min; launch again to resume at {chunk.name} {name}")
                return False
            while free_bytes() < MIN_FREE_BYTES:
                log(f"  waiting: {free_bytes() >> 20} MB free")
                time.sleep(30)
            log(f"{chunk.name} {name} ...")
            code, seconds, peak = stage(name, chunk, out, ablate)
            with open(timings, "a", encoding="utf-8") as f:
                f.write(json.dumps({"chunk": chunk.name, "stage": name, "seconds": round(seconds, 1),
                                    "peakMB": peak >> 20, "exit": code, "rows": lines(chunk)}) + "\n")
            log(f"{chunk.name} {name} exit {code} in {seconds:.0f} s, peak {peak >> 20} MB")
            complete = code == 0
            if name == "port":
                complete = lines(chunk.with_name(chunk.stem + ".port.jsonl")) >= lines(chunk)
            if name in ("c1x", "c4up", "c6"):
                complete = lines(chunk.with_name(chunk.stem + f".{name}.jsonl")) >= lines(chunk)
            if not complete:
                log(f"  {chunk.name} {name} incomplete; stopping so it can resume")
                return False
            marker.write_text("done\n")
    return True


# ----------------------------------------------------------------------------------------------
# Judging


def head(answer: str) -> str:
    """Span, partial marker and fuzzy counts: what a written-out form must reproduce."""
    if answer.startswith("["):
        return "[" + " ; ".join(" ".join(m.split(" ")[:2]) for m in answer[1:-1].split(" ; ") if m) + "]"
    return " ".join(answer.split(" ")[:2]) if answer.startswith("(") else answer


def answered(a: str | None) -> bool:
    return a is not None and (a == "None" or a.startswith("(") or a.startswith("["))


def verdict(answer: str) -> str:
    """A single-match answer in the partial judges' notation: None, P(s,e) or F(s,e)."""
    if answer == "None":
        return "None"
    m = re.match(r"\((\d+),(\d+)\)(P?)", answer)
    return ("P" if m.group(3) else "F") + f"({m.group(1)},{m.group(2)})" if m else answer


def reference_form(answer: str) -> str:
    if answer == "None":
        return "None"
    parts = answer.split(" ")
    return f"{parts[0].rstrip('P')} {parts[1]} {parts[3]}"


def only_changes_leaked(base: str, up: str, leak_free: str | None) -> bool:
    """ExpectedDivergences.OnlyTheChangePositionsLeaked for a finditer answer: every differing match
    agrees apart from the change positions, with equal counts, the port's positions agree with its
    counts, and upstream's leak-free answer, where it could be asked ("?" where not), is the port's."""
    if leak_free is None:
        return False
    mine, theirs, free = (a[1:-1].split(" ; ") if a != "[]" else [] for a in (base, up, leak_free))
    if not len(mine) == len(theirs) == len(free):
        return False
    for m, t, f in zip(mine, theirs, free):
        if m == t:
            continue
        pm, pt = m.split(" "), t.split(" ")
        if len(pm) != len(pt) or pm[:2] != pt[:2] or pm[3:] != pt[3:]:
            return False
        changes = [x.split(",") if x else [] for x in pm[2][1:-1].split("|")]
        if [len(c) for c in changes] != [int(n) for n in pm[1].split(",")]:
            return False
        if f != "?" and f != m:
            return False
    return True


def c2_verdict(base: str, written: str, depth: str) -> tuple:
    """A call means its body written out (D40), and a capture made inside a call is discarded on
    return (owner ruling D51 (a), 2026-09-30), which is what the non-capturing copy does. So the
    whole answer must agree; the kind says whether the span or counts differ, or only the groups."""
    if written == base:
        return ("pass", "", base, written)
    if head(written) != head(base):
        return ("fail", "span or counts differ from the written-out form" + depth, base, written)
    return ("fail", "groups or captures differ from the written-out form" + depth, base, written)


def judge_row(row: dict, port: dict, extra: dict, c1: dict | None) -> dict:
    """{check: (status, kind, port answer, other answer)} with status pass / fail / expected / n/a."""
    out = {}
    base = port.get("base")
    # C1: the oracle. EXPECTED means an ExpectedDivergences entry accounts for the row.
    if c1 is None:
        out["C1"] = ("n/a", "not asked through the oracle", None, None)
    else:
        out["C1"] = c1
    # C1x: upstream's own finditer where the recorder cannot ask.
    up = extra.get("c1x", "n/a")
    if up == "n/a":
        out["C1x"] = ("n/a", "", None, None)
    elif not answered(up) or not answered(base):
        out["C1x"] = ("n/a", "unanswered", base, up)
    elif up == base:
        out["C1x"] = ("pass", "", base, up)
    else:
        # As C1 does through ExpectedDivergences: a difference that one of the oracle's fuzzy
        # ablations takes away (port-runner's `pinned` variants), or that is only upstream's leaked
        # fuzzy_changes (pyworker's anchored re-ask), is accounted for. C1x compared the raw strings
        # and so counted pinned divergences as failures (matrix triage 2026-09-30).
        pinned = [v for v, a in (port.get("pinned") or {}).items() if a == up]
        if pinned:
            out["C1x"] = ("expected", "pinned divergence, taken away by " + pinned[0], base, up)
        elif only_changes_leaked(base, up, extra.get("c1xLeakFree")):
            out["C1x"] = ("expected", "fuzzy-changes-leaked-from-an-abandoned-attempt", base, up)
        elif (row.get("partial") and (row["flags"] & 1024 or row["pattern"].startswith("(?r)"))
              and (row.get("pos") or 0) > 0 and up == "[]"
              and re.fullmatch(rf"\[\({row['pos']},\d+\)P [^;]*\]", base)):
            # ExpectedDivergences' reversed-partial-runs-out-at-the-slice-start (ledger 24): upstream
            # finds nothing, the port a partial that starts where the reversed text runs out.
            out["C1x"] = ("expected", "reversed-partial-runs-out-at-the-slice-start", base, up)
        else:
            out["C1x"] = ("fail", "differs from upstream", base, up)
    # C2: written-out equivalence.
    wo = port.get("wo")
    if not wo:
        out["C2"] = ("n/a", "", None, None)
    elif not answered(base):
        out["C2"] = ("n/a", "base unanswered", base, None)
    elif "8" in wo:
        w = wo["8"]
        if not answered(w):
            out["C2"] = ("n/a", "written-out unanswered", base, w)
        else:
            out["C2"] = c2_verdict(base, w, "")
    else:
        w4, w5, w6 = (wo.get(d) for d in ("4", "5", "6"))
        if not all(answered(w) for w in (w4, w5, w6)) or not head(w4) == head(w5) == head(w6):
            out["C2"] = ("n/a", "written-out forms not stable at depths 4-6", base, w6)
        else:
            out["C2"] = c2_verdict(base, w6, " (depth 6)")
    # C3: asserts and crashes anywhere in the port's answers.
    everything = [port.get("base"), port.get("off"), port.get("on"), port.get("judge")] + list((wo or {}).values())
    bad = [a for a in everything if a and (a.startswith("ASSERT") or a.startswith("EXC"))]
    out["C3"] = ("fail", bad[0].split(" ")[0], bad[0], None) if bad else ("pass", "", None, None)
    # C4: partial self-consistency, both judges.
    pj, uj = port.get("judge"), extra.get("c4up", "n/a")
    if pj is None and uj == "n/a":
        out["C4"] = ("n/a", "", None, None)
    elif not answered(base):
        out["C4"] = ("n/a", "base unanswered", base, pj)
    else:
        ans = verdict(base)
        kind = "ok"
        if pj and pj.startswith("F"):
            kind = "ok" if ans == pj else "complete-mismatch"
        elif pj and pj.startswith("P"):
            kind = "miss" if ans == "None" else ("ok" if ans == pj else "span-diff")
        elif pj == "None":
            kind = "phantom" if ans != "None" else "ok"
        if kind in ("ok", "phantom") and uj.startswith("P") and ans == "None":
            kind = "miss (upstream judge only)"
        if (kind == "span-diff" and "\\K" in row["pattern"] and pj
                and re.fullmatch(r"P\((\d+),(\d+)\)", ans) and ans.split(",")[1] == pj.split(",")[1]
                and int(ans[2:].split(",")[0]) >= int(pj[2:].split(",")[0])):
            # Both judges report a match/fullmatch partial as P(0,|t|), the attempt's start; with a
            # \K passed before the edge the reported start is where \K was passed, as upstream's own
            # partial answer has it (measured 2026-09-30: `(?:ba)*+.\K\w.` match 'baab' partial is
            # P(3,4) upstream and here). A judge convention, not a disagreement (matrix triage).
            kind = "ok"
        if kind == "ok":
            out["C4"] = ("pass", "", ans, f"port judge {pj}; upstream judge {uj}")
        elif kind == "phantom":
            out["C4"] = ("phantom", kind, ans, f"port judge {pj}; upstream judge {uj}")
        else:
            out["C4"] = ("fail", kind, ans, f"port judge {pj}; upstream judge {uj}")
    # C5: memo off and memo on from the first call answer as the default does.
    off, on = port.get("off"), port.get("on")
    if not all(answered(a) for a in (base, off, on)):
        out["C5"] = ("n/a", "unanswered", base, None)
    elif off != base:
        out["C5"] = ("fail", "memo off differs", base, off)
    elif on != base:
        out["C5"] = ("fail", "memo on differs", base, on)
    else:
        out["C5"] = ("pass", "", base, None)
    # C6: the reference matcher.
    ref = extra.get("c6", "n/a")
    if ref == "n/a" or not answered(ref) and ref != "None":
        out["C6"] = ("n/a", "", None, None)
    elif not answered(base):
        out["C6"] = ("n/a", "base unanswered", base, ref)
    else:
        mine = reference_form(base)
        out["C6"] = ("pass", "", mine, ref) if mine == ref else ("fail", "differs from the reference matcher", mine, ref)
    return out


def oracle_verdicts(chunk: Path) -> dict:
    stem = chunk.with_suffix("")
    ids = json.load(open(stem.with_name(stem.name + ".oracle-ids.json"), encoding="utf-8"))
    wave = [json.loads(line) for line in open(stem.with_name(stem.name + ".wave.jsonl"), encoding="utf-8")]
    rows = [w for w in wave if w.get("kind") != "header"]
    report = stem.with_name(stem.name + ".report.txt").read_text(encoding="utf-8")
    blocks = {}
    for block in report.split("\n\n"):
        m = re.match(r"(DIVERGE|EXPECTED (\S+)|\S+) row (\d+) ", block)
        if m:
            blocks[int(m.group(3))] = (m.group(1), block)
    out = {}
    for n, (rid, w) in enumerate(zip(ids, rows), start=1):
        kind = (w.get("outcome") or {}).get("kind")
        if n in blocks:
            heading, block = blocks[n]
            upstream = next((l.strip() for l in block.splitlines() if l.strip().startswith("upstream")), "")
            mine = next((l.strip() for l in block.splitlines() if l.strip().startswith("port")), "")
            if heading == "DIVERGE":
                out[rid] = ("fail", "diverges from upstream", mine, upstream)
            elif heading.startswith("EXPECTED"):
                out[rid] = ("expected", heading.split(" ", 1)[1], mine, upstream)
            else:
                out[rid] = ("fail", heading, mine, upstream)
        elif kind in ("match", "nomatch", "matches", "error"):
            out[rid] = ("pass", "", None, None)
        else:
            out[rid] = ("n/a", f"upstream {kind}", None, None)
    return out


def read_by_id(path: Path) -> dict:
    out = {}
    if path.exists():
        for line in open(path, encoding="utf-8"):
            if line.strip():
                d = json.loads(line)
                out[d["id"]] = d
    return out


def judge(out: Path, constructs: dict) -> None:
    rows = [json.loads(line) for line in open(out / "rows.jsonl", encoding="utf-8") if line.strip()]
    pairs, triples = gen.cells_of(constructs)
    cells = pairs + triples
    fam = gen.family_members(constructs)
    port, extra, c1 = {}, collections.defaultdict(dict), {}
    for chunk in sorted(out.glob("chunk-[0-9][0-9][0-9].jsonl")):
        stem = chunk.with_suffix("")
        port.update(read_by_id(stem.with_name(stem.name + ".port.jsonl")))
        for task in ("c1x", "c4up", "c6"):
            for rid, d in read_by_id(stem.with_name(stem.name + f".{task}.jsonl")).items():
                extra[rid][task] = d[task]
                if "c1xLeakFree" in d:
                    extra[rid]["c1xLeakFree"] = d["c1xLeakFree"]
        if stem.with_name(stem.name + ".report.txt").exists():
            c1.update(oracle_verdicts(chunk))
    table = collections.defaultdict(lambda: collections.Counter())
    totals = {c: collections.Counter() for c in CHECKS}
    witness = {}
    controls = []
    with open(out / "failures.jsonl", "w", encoding="utf-8", newline="\n") as f:
        for row in rows:
            rid = row["id"]
            if rid not in port:
                continue
            verdicts = judge_row(row, port[rid], extra.get(rid, {}), c1.get(rid) if oracle_row(row) else None)
            if "expect" in row:
                check, want = row["expect"]
                got = verdicts[check]
                controls.append(f"{'OK  ' if got[0] == want else 'BAD '} {check} want {want} got {got[0]} "
                                f"({got[1]}) {row['pattern']!r} {row['subject']!r} port {got[2]} other {got[3]}")
            tags = set(row["tags"])
            covered = [c for c in cells if gen.covers(tags, c, fam)]
            for check, (status, kind, mine, other) in verdicts.items():
                totals[check][status] += 1
                for c in covered:
                    table[(gen.cell_name(c), check)][status] += 1
                if status in ("fail", "expected", "phantom"):
                    question = {k: row[k] for k in ORACLE_KEYS if k in row}
                    f.write(json.dumps({"id": rid, "check": check, "status": status, "kind": kind, **question,
                                        "cell": row["cell"], "port": mine, "other": other}, ensure_ascii=True) + "\n")
                    if status == "fail":
                        size = len(row["pattern"]) + len(row["subject"])
                        key = (check, kind)
                        if key not in witness or size < witness[key][0]:
                            witness[key] = (size, row, mine, other)
    with open(out / "cells.csv", "w", encoding="utf-8", newline="\n") as f:
        f.write("cell,kind,check,rows,applicable,na,fail,expected,phantom\n")
        for c in cells:
            name = gen.cell_name(c)
            kind = "triple" if c[0] in ("family", "ids") else "pair"
            for check in CHECKS:
                t = table[(name, check)]
                rows_ = sum(t.values())
                f.write(f"{name},{kind},{check},{rows_},{rows_ - t['n/a']},{t['n/a']},{t['fail']},{t['expected']},{t['phantom']}\n")
    judged = sum(1 for r in rows if r["id"] in port)
    lines_ = [f"Rows judged: {judged} of {len(rows)}.", "",
              "| Check | Applicable rows | n/a | Failures | Accounted (EXPECTED) | Phantoms | Cells with a failure |",
              "|---|---|---|---|---|---|---|"]
    for check in CHECKS:
        t = totals[check]
        failing_cells = sum(1 for c in cells if table[(gen.cell_name(c), check)]["fail"])
        lines_.append(f"| {check} | {sum(t.values()) - t['n/a']} | {t['n/a']} | {t['fail']} | {t['expected']} | {t['phantom']} | {failing_cells} |")
    lines_ += ["", "Smallest failing row per check and kind (raw, not triaged):", ""]
    for (check, kind), (_, row, mine, other) in sorted(witness.items()):
        q = {k: row[k] for k in ORACLE_KEYS if k in row and k != "namedLists"}
        lines_.append(f"- {check} {kind}: `{json.dumps(q, ensure_ascii=True)}` port `{mine}` other `{other}`")
    if controls:
        (out / "controls.txt").write_text("\n".join(controls) + "\n", encoding="utf-8")
        print("\n".join(controls))
    (out / "summary.md").write_text("\n".join(lines_) + "\n", encoding="utf-8")
    print("\n".join(lines_[:len(CHECKS) + 4]))


def cost(out: Path) -> None:
    timings = [json.loads(line) for line in open(out / "timings.jsonl", encoding="utf-8")] if (out / "timings.jsonl").exists() else []
    per = collections.defaultdict(lambda: {"seconds": 0.0, "rows": 0, "peakMB": 0})
    for t in timings:
        p = per[t["stage"]]
        p["seconds"] += t["seconds"]
        p["rows"] += t["rows"] if t["exit"] == 0 else 0
        p["peakMB"] = max(p["peakMB"], t["peakMB"])
    disk = sum(p.stat().st_size for p in out.rglob("*") if p.is_file())
    summary = {"stages": per, "diskMB": round(disk / 2**20, 1)}
    (out / "cost.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")
    for name, p in per.items():
        rate = p["rows"] / p["seconds"] if p["seconds"] else 0
        print(f"  {name}: {p['seconds']:.0f} s for {p['rows']} rows ({rate:.1f} rows/s), peak {p['peakMB']} MB")
    print(f"  disk {summary['diskMB']} MB")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("rows", type=Path, nargs="?")
    ap.add_argument("--run", required=True)
    ap.add_argument("--chunk", type=int, default=500)
    ap.add_argument("--max-minutes", type=float, default=40)
    ap.add_argument("--ablate", default="none")
    ap.add_argument("--judge-only", action="store_true")
    ap.add_argument("--constructs", type=Path, default=None)
    args = ap.parse_args(argv)
    out = RESULTS / args.run
    constructs = gen.load_constructs(args.constructs)
    if not args.judge_only:
        chunks = prepare(args.rows, out, args.chunk)
        log(f"run {args.run}: {len(chunks)} chunks of up to {args.chunk} rows, ablate {args.ablate}")
        if not drive(out, chunks, args.max_minutes, args.ablate):
            cost(out)
            return 3
    judge(out, constructs)
    cost(out)
    log("done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
