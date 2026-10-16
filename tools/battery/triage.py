"""Builds a battery run's triage table: one row per disagreement cluster.

    python tools/battery/triage.py .scratch/battery/RUN_ID [--no-reduce]

Reads what the parts wrote (battery.py's docstring lists them) and writes, next to them:
  triage.md        the table, witnesses and the counts behind it
  findings.jsonl   every interesting row, with its cluster key and every reference's verdict

A row is interesting when the port disagrees with every reference that answered it, when the
references disagree among themselves and the port sides with one of them, or when a self-check
fails (a Debug.Assert or exception, memo on or off answering differently, a partial answer its own
continuations contradict, a call answering differently from its written-out body).

References per row, in order of authority: upstream regex 2026.9.10 (C1, through the oracle
consumer, which also applies ExpectedDivergences and the judged rows), PCRE2 and Perl together (C7),
Python `re` (from C7's survey), the fuzzy reference matcher (C6), TRE (C8), and the guards-off
control (G: the same pattern inside a fuzzy section that can make no edit, which turns the repeat
guards off and nothing else on the rows it is applied to).

Attribution is automatic only where an existing screen decided it: an ExpectedDivergences entry or
a judged row (C1 "expected"), MemorySanitizer (oracle UNDEFINED), the stale controls. A cluster that
matches a known signature gets a HINT naming the register row, which is a hypothesis for the
orchestrator, not an attribution. Everything else is UNJUDGED with a minimal witness: the smallest
row of the cluster, shrunk by a batch delta reducer (one probe run per round for every witness at
once) while the port still differs from upstream in the same way and the row keeps its constructs.
"""

from __future__ import annotations

import collections
import importlib.util
import json
import queue
import re
import subprocess
import sys
import threading
from pathlib import Path

import regex

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "matrix"))
import tagger  # noqa: E402

sys.path.insert(0, str(HERE))
import f6model  # noqa: E402

RISKY = ("fuzzy", "backref", "lookahead", "neg-lookahead", "lookbehind", "neg-lookbehind", "atomic", "possessive",
         "verb", "conditional", "keep", "call", "flag-r", "partial", "slice", "flag-b", "flag-e", "flag-p")
REFS = ("C1", "C7", "re", "C6", "C8", "G")
SELF = ("C2", "C3", "C4", "C5")

# Known signatures: (name, test(row-tags, check, kind), why). A HINT, never an attribution.
HINTS = [
    ("D90?", lambda t, c, k: "partial" in t and {"lookahead", "lookbehind", "neg-lookahead", "neg-lookbehind"} & t
     and {"atomic", "possessive", "verb"} & t and "capture" in t,
     "partial + lookaround setting a group + a cut (D90, open)"),
    ("F63/F64 (phantom partial)", lambda t, c, k: c == "C4" and k == "phantom",
     "a partial match no continuation completes: the decided partial contract"),
    ("D51 (call captures)", lambda t, c, k: c == "C2" and "groups" in k, "captures made inside a call"),
]
# A self-check "failure" that is the port refusing the pattern at compile time (a left-recursion
# refusal, for example): a decided rule to look up in DIVERGENCES, not a crash.
REFUSAL = "FuzzyRegexParseException"


def load(path: Path) -> list[dict]:
    return [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()] if path.exists() else []


def risky(tags) -> str:
    return "+".join(t for t in RISKY if t in set(tags)) or "-"


def tags_of(row: dict) -> set[str]:
    if row.get("tags"):
        return set(row["tags"])
    try:
        return tagger.confirmed({"flags": 0, "namedLists": {}, **row})
    except Exception:  # noqa: BLE001
        return set()


def hint(tags: set[str], check: str, kind: str) -> str:
    for name, test, _ in HINTS:
        try:
            if test(tags, check, kind):
                return name
        except Exception:  # noqa: BLE001
            pass
    return ""


# ----------------------------------------------------------------------------------------------
# Answers in the probe's format, and upstream's in a comparable normal form


SPAN = re.compile(r"^\((\d+),(\d+)\) \((\d+),(\d+),(\d+)\)")
GROUP = re.compile(r" g(\d+)=((?:\(\d+,\d+\),?)*)")
CAP = re.compile(r"\((\d+),(\d+)\)")


def norm_probe_one(ans: str):
    if ans == "None":
        return None
    m = SPAN.match(ans)
    if not m:
        return ("ERR", ans.split(":")[0])
    groups = tuple(tuple(sorted((int(a), int(b)) for a, b in CAP.findall(g.group(2)))) for g in GROUP.finditer(ans))
    return ((int(m.group(1)), int(m.group(2))), tuple(int(m.group(i)) for i in (3, 4, 5)), " partial" in ans, groups)


def norm_probe(ans: str, op: str = ""):
    if op == "finditer" or ans.startswith("[") or " | " in ans:
        return tuple(norm_probe_one(a) for a in ([] if ans == "[]" else ans.split(" | ")))
    return norm_probe_one(ans)


def norm_regex_match(m):
    if m is None:
        return None
    groups = tuple(tuple(sorted(tuple(s) for s in m.spans(i))) for i in range(1, (m.re.groups or 0) + 1))
    return (m.span(), tuple(m.fuzzy_counts), bool(m.partial), groups)


def upstream(row: dict):
    """Upstream's answer to a probe row in the normal form, or ("ERR", type). In this process: call it
    only through upstream_many, since some rows crash `regex` itself (ledger entries 9 and 56)."""
    try:
        p = regex.compile(row["pattern"], int(row.get("flags") or 0), **(row.get("namedLists") or {}))
        kw = {"partial": bool(row.get("partial")), "timeout": 2}
        s = row["subject"]
        args = [s] + ([row["pos"]] if "pos" in row else []) + ([row["endpos"]] if "endpos" in row else [])
        op = row["operation"]
        if op == "finditer":
            return tuple(norm_regex_match(m) for m in p.finditer(*args, **kw))
        if op == "match":
            return norm_regex_match(p.match(*args, **kw))
        if op == "fullmatch":
            return norm_regex_match(p.fullmatch(*args, **kw))
        return norm_regex_match(p.search(*args, **kw))
    except Exception as e:  # noqa: BLE001
        return ("ERR", type(e).__name__)


def _tuples(x):
    return tuple(_tuples(v) for v in x) if isinstance(x, list) else x


UPSTREAM_ROW_SECONDS = 20


def _pump(stream, lines: queue.Queue) -> None:
    for line in stream:
        lines.put(line)
    lines.put(None)


def write_jsonl(path: Path, rows) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        for r in rows:
            fh.write(json.dumps(r, ensure_ascii=True) + "\n")


def upstream_many(rows: list[dict], work: Path) -> list:
    """upstream() for every row, in child processes. A row that kills the child (battery run
    20261016-1: `(?:(?P<g2>(?P<g1>(?:ba(?2)b|ab)))){d<=1}(?&g2)` match 'abbababa' partial, ledger
    56, exit 0xC0000005 took the whole triage with it) or hangs it is answered ("ERR", "Crash") or
    ("ERR", "Hang"), and the next child starts after it."""
    src = work / "upstream-in.jsonl"
    write_jsonl(src, rows)
    out, start = [], 0
    while start < len(rows):
        proc = subprocess.Popen([sys.executable, str(Path(__file__)), "--upstream-worker", str(src), str(start)],
                                stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                text=True, encoding="utf-8")
        lines: queue.Queue = queue.Queue()
        threading.Thread(target=_pump, args=(proc.stdout, lines), daemon=True).start()
        while True:
            try:
                line = lines.get(timeout=UPSTREAM_ROW_SECONDS)
            except queue.Empty:
                proc.kill()
                out.append(("ERR", "Hang"))
                start += 1
                break
            if line is None:
                proc.wait()
                if start < len(rows):
                    out.append(("ERR", "Crash"))
                    start += 1
                break
            out.append(_tuples(json.loads(line)))
            start += 1
        proc.wait()
    return out


def upstream_worker(src: str, start: int) -> int:
    rows = [json.loads(line) for line in open(src, encoding="utf-8") if line.strip()]
    for row in rows[start:]:
        print(json.dumps(upstream(row)), flush=True)
    return 0


def read_probe(path: Path) -> list[str]:
    out = []
    if path.exists():
        for line in open(path, encoding="utf-8", errors="replace"):
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= 5 and parts[-1].endswith("ms"):
                out.append(parts[3])
    return out


# ----------------------------------------------------------------------------------------------
# Sources


class Findings:
    def __init__(self):
        self.rows = []
        self.notes = []
        self.known = collections.Counter()

    def add(self, source, category, check, kind, row, port, other, refs=None, cluster=None, tags=None):
        tags = tags if tags is not None else tags_of(row)
        key = cluster or f"{source} | {category} | {check} {kind} | {risky(tags)}"
        self.rows.append({"cluster": key, "source": source, "category": category, "check": check, "kind": kind,
                          "row": {k: row[k] for k in ("pattern", "flags", "subject", "operation", "partial", "pos",
                                                       "endpos", "namedLists", "cell", "generator") if k in row},
                          "port": port, "other": other, "refs": refs or {}, "tags": sorted(tags),
                          "hint": hint(tags, check, kind)})


def oracle_source(run: Path, f: Findings) -> None:
    out = run / "oracle"
    if not out.exists():
        f.notes.append("oracle: no output")
        return
    log = {e["seed"]: e for p in sorted(out.glob("sweep-*.jsonl")) for e in load(p)}
    # Per oracle part: which (seed, row) went into its Release re-check, and whether it still diverged.
    recheck_state = {}
    for rep in sorted(out.glob("recheck-*-report.txt")):
        slot = rep.name[len("recheck-"):-len("-report.txt")]
        text = rep.read_text(encoding="utf-8")
        still = {int(m.group(1)) for m in re.finditer(r"^(?:DIVERGE|FAULT) row (\d+) ", text, re.M)}
        tally = re.search(r"^agree .*$", text, re.M)
        f.notes.append(f"{slot} Release re-check of the diverging rows: {tally.group(0) if tally else text[:120].strip()}")
        for n, o in enumerate(load(out / f"recheck-{slot}-origin.jsonl"), start=1):
            if tally:
                recheck_state[(o["seed"], o["row"])] = n in still
    for d in sorted(p for p in out.glob("sweep-*") if p.is_dir()):
        seed = int(d.name.split("-")[1])
        e = log.get(seed)
        f.notes.append(f"oracle seed {seed}: " + (e["tally"] if e else (d / "crashed.txt").read_text(encoding="utf-8").strip()
                                                  if (d / "crashed.txt").exists() else "no result"))
        for a in load(d / "asserts.jsonl"):
            f.add("oracle", "self", "C3", a["answer"].split(" ")[0], a, a["answer"], None)
        report = d / "report.txt"
        if not report.exists():
            continue
        wave = load(d / "wave.jsonl")
        wave = wave[1:] if wave and wave[0].get("kind") == "header" else wave
        for block in report.read_text(encoding="utf-8").split("\n\n"):
            m = re.match(r"(DIVERGE|FAULT|UNDEFINED|EXPECTED (\S+)) row (\d+) \(([^)]+)\)", block.strip())
            if not m:
                continue
            n = int(m.group(3))
            if m.group(1).startswith("EXPECTED"):
                f.known[f"oracle: ExpectedDivergences/judged {m.group(2)}"] += 1
                continue
            if m.group(1) == "UNDEFINED":
                f.known["oracle: UNDEFINED (MemorySanitizer screen)"] += 1
                continue
            row = dict(wave[n - 1]) if 1 <= n <= len(wave) else {"pattern": "?", "subject": "?", "operation": "?"}
            up = next((l.strip() for l in block.splitlines() if l.strip().startswith("upstream")), "")
            mine = next((l.strip() for l in block.splitlines() if l.strip().startswith("port")), "")
            if recheck_state.get((seed, n)) is False:
                f.known["oracle: diverged in Debug only (agrees in Release: a Debug timeout)"] += 1
                continue
            f.add("oracle", "C1 only", "C1", m.group(1).lower(), row, mine, up,
                  cluster=f"oracle | {m.group(4)} | {m.group(1)} | {risky(tags_of(row))}")


def bed_source(run: Path, f: Findings, bed_dir: Path) -> None:
    out = run / "bed"
    probe = out / "probe-all.txt"
    if not probe.exists():
        f.notes.append("bed: no output")
        return
    spec = importlib.util.spec_from_file_location("bedcompare", bed_dir / "compare.py")
    cmpm = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(cmpm)
    rows = load(bed_dir / "rows.jsonl")
    exp = load(bed_dir / "expected.jsonl")
    key = {(r["operation"], r["pattern"], r["subject"]): i for i, r in enumerate(rows)}
    if (out / "release-recheck.txt").exists():
        f.notes.append("bed Release re-check: " + (out / "release-recheck.txt").read_text(encoding="utf-8").strip())
    if (out / "compare.txt").exists():
        f.notes.append("bed compare.py: " + " / ".join(
            l.strip() for l in (out / "compare.txt").read_text(encoding="utf-8").splitlines()[:7]))
    for line in open(probe, encoding="utf-8", errors="replace"):
        parts = line.rstrip("\n").split("\t")
        if len(parts) < 5:
            continue
        try:
            i = key.get((parts[0], json.loads(parts[1]), json.loads(parts[2])))
        except ValueError:
            continue
        if i is None:
            continue
        e, ans, row = exp[i], parts[3], rows[i]
        ng = max(len(next((x["groups"] for x in (e["re"], e["regex"]) if x), [])), cmpm._group_count(row["pattern"]))
        want, like, got = cmpm._norm(e["re"], ng), cmpm._norm(e["regex"], ng), cmpm.parse_answer(ans, ng)
        if got == want:
            continue
        if not isinstance(got, str):
            # KNOWN-DEFECTS F6: an empty iteration that changes a tested group is progress
            # (upstream's rule, kept). f6model's A12u is a guard-free model of that rule; a port
            # answer equal to it is the decided rule (run 20261016-1: 187 such rows came back
            # UNJUDGED, 182 where regex agrees and 5 where regex's D85 tail guard breaks its own rule).
            model = f6model.safe_run(row["pattern"], row["subject"], row["operation"], "A12u")
            if (model is None or isinstance(model, dict)) and cmpm._norm(model, ng) == got:
                f.known["bed: F6 (port = the model of upstream's empty-iteration rule, A12u)"] += 1
                continue
        tags = {"backref"} | ({"conditional"} if "(?(" in row["pattern"] else set())
        if isinstance(got, str):
            what = "timeout (Debug 5 s, then Release 30 s)" if "Timeout" in got else got[7:].split(":")[0]
            f.add("bed", "self" if "Assert" in got else "error", "C3" if "Assert" in got else "ERR", what, row, ans,
                  None, cluster=f"bed | {what} | {e['source']}", tags=tags)
        elif got == like:
            f.add("bed", "like upstream", "re", "port = regex != re", row, ans, f"re {cmpm._show(e['re'])}",
                  refs={"re": "fail", "C1": "pass"}, cluster=f"bed | port = regex != re | {e['cls']} | {e['family']}",
                  tags=tags)
        else:
            f.add("bed", "all refs", "re", "port != re and != regex", row, ans,
                  f"re {cmpm._show(e['re'])}; regex {cmpm._show(e['regex'])}", refs={"re": "fail", "C1": "fail"},
                  cluster=f"bed | port != re, regex | {e['cls']} | {e['family']}", tags=tags)


def control_verdicts(run: Path) -> dict:
    """{(source, id): (status, plain, guards-off)} from the probe part's control output."""
    origin = load(run / "control" / "origin.jsonl")
    answers = read_probe(run / "control" / "probe-all.txt")
    out = {}
    for n, o in enumerate(origin):
        if 2 * n + 1 >= len(answers):
            break
        plain, off = answers[2 * n], answers[2 * n + 1]
        if not (plain == "None" or plain.startswith(("(", "["))) or not (off == "None" or off.startswith(("(", "["))):
            status = "n/a"
        else:
            status = "pass" if norm_probe(plain) == norm_probe(off) else "fail"
        out[(o["source"], o["id"])] = (status, plain, off)
    return out


def harness_source(run: Path, f: Findings, label: str, results: Path, rows_path: Path, control: dict, src: str) -> None:
    """A tools/matrix/run.py result: every row judged again with run.py's own judge_row."""
    sut_matrix = results.parents[3] / "tools" / "matrix"  # <sut>/.scratch/matrix/results/<name>
    spec = importlib.util.spec_from_file_location(f"runpy_{label}", sut_matrix / "run.py")
    sys.path.insert(0, str(sut_matrix))
    runpy = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(runpy)
    import c7  # noqa: E402 - the SUT's, now first on the path
    rows = load(results / "rows.jsonl")
    port, extra, c1 = {}, collections.defaultdict(dict), {}
    for chunk in sorted(results.glob("chunk-[0-9][0-9][0-9].jsonl")):
        stem = chunk.with_suffix("")
        port.update(runpy.read_by_id(stem.with_name(stem.name + ".port.jsonl")))
        for task in ("c1x", "c4up", "c6", "c7", "c8"):
            for rid, d in runpy.read_by_id(stem.with_name(stem.name + f".{task}.jsonl")).items():
                extra[rid][task] = d[task]
                if "c1xLeakFree" in d:
                    extra[rid]["c1xLeakFree"] = d["c1xLeakFree"]
        if stem.with_name(stem.name + ".report.txt").exists():
            c1.update(runpy.oracle_verdicts(chunk))
    counts = collections.Counter()
    for row in rows:
        rid = row["id"]
        if rid not in port:
            counts["not answered by the port stage"] += 1
            continue
        v = runpy.judge_row(row, port[rid], extra.get(rid, {}), c1.get(rid))
        refs = {c: v[c][0] for c in ("C1", "C6", "C7", "C8")}
        res = (extra.get(rid, {}).get("c7") or {}).get("results") or {}
        mine = c7.port_key(port[rid].get("base"))
        rk = c7._engine_key(res.get("re"))
        refs["re"] = "n/a" if rk is None or mine is None else ("pass" if rk == mine else "fail")
        g = control.get((src, rid))
        refs["G"] = g[0] if g else "n/a"
        if refs["C1"] == "expected":
            f.known[f"{label}: C1 expected {v['C1'][1]}"] += 1
        for check in SELF:
            status, kind, a, b = v[check]
            if status == "expected":
                # run.py's own screens for the partial contract: C2 partial starts, C4 F66 starts.
                f.known[f"{label}: {check} {kind}"] += 1
                continue
            if status == "phantom":
                # A partial no continuation completes is the decided partial contract (KNOWN-DEFECTS
                # F63/F64: a run-out presumes the rest of the pattern). Run 20261016-1: 755 of 799
                # pair phantoms gave upstream's answer exactly and the other 44 were known
                # divergences, which C1 reports in its own right.
                counts[f"{check} phantom"] += 1
                f.known[f"{label}: C4 phantom partial (F63/F64 partial contract)"] += 1
                continue
            if status == "fail":
                kind_s = kind if check != "C3" else (a or "").split(" ")[0]
                counts[f"{check} {status}"] += 1
                if check == "C3" and REFUSAL in (a or "") and "(compile)" in (a or ""):
                    msg = re.sub(r"group '[^']*'|the pattern", "<group>", (a or "").split(") ", 1)[-1].split(" @ ")[0])
                    f.known[f"{label}: the port refuses the pattern at compile time: {msg[:90]}"] += 1
                    continue
                f.add(label, "self", check, kind_s or status, row, a, b, refs)
        failing = [c for c in REFS if refs[c] == "fail"]
        passing = [c for c in REFS if refs[c] == "pass"]
        if not failing:
            counts["no reference fails"] += 1
            continue
        detail = {"C1": v["C1"], "C7": v["C7"], "C6": v["C6"], "C8": v["C8"]}
        other = "; ".join(f"{c}: {detail[c][3]}" for c in failing if c in detail and detail[c][3])
        if "G" in failing:
            other += f"; G guards-off: {g[2]}"
        cat = "all refs" if not passing else "refs disagree"
        sig = f"fails {'+'.join(failing)}" + (f"; passes {'+'.join(passing)}" if passing else "")
        counts[cat] += 1
        cell = row.get("cell", "?")
        cluster = f"{label} | {cat} | {sig} | {cell if src == 'pairs' else risky(row.get('tags', []))}"
        f.add(label, cat, failing[0], sig, row, port[rid].get("base"), other, refs, cluster=cluster,
              tags=set(row.get("tags", [])))
    sys.path.remove(str(sut_matrix))
    for m in ("c7", "c8", "gen", "tagger", "survey"):
        if m in sys.modules and str(sut_matrix) in str(getattr(sys.modules[m], "__file__", "")):
            del sys.modules[m]
    f.notes.append(f"{label}: {len(rows)} rows; " + ", ".join(f"{k} {n}" for k, n in sorted(counts.items())))
    if (results / "controls.txt").exists():
        f.notes.append(f"{label}: controls " + (results / "controls.txt").read_text(encoding="utf-8")[:200])


# ----------------------------------------------------------------------------------------------
# Witness reduction


def candidates(row: dict) -> list[dict]:
    """One-step reductions: drop a subject character, drop a pattern character or bracketed piece."""
    out = []
    s, p = row["subject"], row["pattern"]
    for i in range(len(s)):
        out.append({**row, "subject": s[:i] + s[i + 1:]})
    for i in range(len(p)):
        out.append({**row, "pattern": p[:i] + p[i + 1:]})
        if p[i] == "(":
            depth = 0
            for j in range(i, len(p)):
                depth += {"(": 1, ")": -1}.get(p[j], 0) if (j == 0 or p[j - 1] != "\\") else 0
                if depth == 0:
                    out.append({**row, "pattern": p[:i] + p[j + 1:]})
                    inner = p[i + 1:j]
                    if inner.startswith("?:"):
                        out.append({**row, "pattern": p[:i] + inner[2:] + p[j + 1:]})
                    break
    good = []
    for c in out:
        try:
            regex.compile(c["pattern"], int(c.get("flags") or 0), **(c.get("namedLists") or {}))
        except Exception:  # noqa: BLE001
            continue
        good.append(c)
    return good


def reduce_witnesses(witnesses: dict, probe_run, work: Path, rounds: int = 12) -> dict:
    """{cluster: row} -> {cluster: reduced row}. The predicate: the port still differs from upstream
    and the row keeps its risky constructs. probe_run(rows) -> list of probe answers."""
    def differs(row, ans, up):
        return (norm_probe(ans, row["operation"]) != up and not ans.startswith(("RegexMatchTimeout", "CRASH"))
                and up not in (("ERR", "Crash"), ("ERR", "Hang")))

    current = {}
    first_rows = list(witnesses.values())
    first = probe_run(first_rows)
    for (k, row), ans, up in zip(witnesses.items(), first, upstream_many(first_rows, work)):
        if differs(row, ans, up):
            current[k] = (row, risky(tags_of(row)), ans, up)
    start = {k: v[0] for k, v in current.items()}
    for _ in range(rounds):
        batch, owner = [], []
        for k, (row, sig, _a, _u) in current.items():
            for c in candidates(row):
                batch.append(c)
                owner.append(k)
        if not batch:
            break
        answers = probe_run(batch)
        ups = upstream_many(batch, work)
        improved = False
        best = {}
        for k, c, ans, up in zip(owner, batch, answers, ups):
            size = len(c["pattern"]) + len(c["subject"])
            if (k not in best or size < best[k][0]) and differs(c, ans, up) and risky(tags_of(c)) == current[k][1]:
                best[k] = (size, c, ans, up)
        for k, (size, c, ans, up) in best.items():
            row, sig, _a, _u = current[k]
            if size < len(row["pattern"]) + len(row["subject"]):
                current[k] = (c, sig, ans, up)
                improved = True
        if not improved:
            break
    # Only witnesses that actually shrank; each with the port's answer and upstream's normal form.
    return {k: (row, ans, up) for k, (row, _s, ans, up) in current.items() if row is not start[k]}


def make_probe_runner(run: Path):
    cfg = json.loads((run / "config.json").read_text(encoding="utf-8"))
    main = run.parent.parent.parent
    sut = main / ".claude" / "worktrees" / f"bat-{run.name}-probe"
    probe = sut / ".battery" / "probe.cs"
    work = run / "reduce"
    work.mkdir(exist_ok=True)
    counter = [0]

    def go(rows):
        counter[0] += 1
        src, dst = work / f"round-{counter[0]:02}.jsonl", work / f"round-{counter[0]:02}.out.txt"
        with open(src, "w", encoding="utf-8", newline="\n") as fh:
            for r in rows:
                q = {k: r[k] for k in ("pattern", "flags", "subject", "operation", "partial", "pos", "endpos",
                                       "namedLists") if k in r}
                fh.write(json.dumps(q, ensure_ascii=True) + "\n")
        with open(dst, "wb") as out:
            subprocess.run(["dotnet", "run", "-c", "Debug", str(probe), "--", str(src), "2"], cwd=sut, stdout=out,
                           stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL)
        answers = read_probe(dst)
        return answers + ["?"] * (len(rows) - len(answers))

    return go if probe.exists() and cfg else None


# ----------------------------------------------------------------------------------------------


def main(argv: list[str]) -> int:
    run = Path(argv[0]).resolve()
    do_reduce = "--no-reduce" not in argv
    main_repo = run.parent.parent.parent
    bed_dir = main_repo / ".scratch" / "s1016" / "d85" / "bed"
    f = Findings()
    oracle_source(run, f)
    bed_source(run, f, bed_dir)
    control = control_verdicts(run)
    g = collections.Counter(v[0] for v in control.values())
    f.notes.append(f"guards-off control: {dict(g)}")
    for half in ("a", "b"):
        p = run / "pairs" / half / "results-path.txt"
        if p.exists():
            harness_source(run, f, f"pairs-{half}", Path(p.read_text(encoding="utf-8").strip()),
                           run / "rows" / f"pairs-{half}.jsonl", control, "pairs")
    for d in sorted((run / "matrix").glob("*")) if (run / "matrix").exists() else []:
        p = d / "results-path.txt"
        if p.exists():
            harness_source(run, f, f"matrix-{d.name}", Path(p.read_text(encoding="utf-8").strip()),
                           run / "rows" / f"matrix-{d.name}.jsonl", control, f"matrix-{d.name}")

    clusters = collections.defaultdict(list)
    for x in f.rows:
        # The pair halves are one source: merge their clusters.
        x["cluster"] = x["cluster"].replace("pairs-a |", "pairs |").replace("pairs-b |", "pairs |")
        clusters[x["cluster"]].append(x)
    with open(run / "findings.jsonl", "w", encoding="utf-8", newline="\n") as fh:
        for x in f.rows:
            fh.write(json.dumps(x, ensure_ascii=True) + "\n")

    witness = {}
    for k, xs in clusters.items():
        w = min(xs, key=lambda x: (len(x["row"].get("pattern", "")) + len(x["row"].get("subject", "")),
                                   x["row"].get("pattern", "")))
        witness[k] = w
    # The table is written first, without reductions, so a reducer failure can never lose it.
    write_report(run, f, clusters, witness, {}, "not reduced yet" if do_reduce else "reduction off (--no-reduce)")
    if not do_reduce:
        return 0
    try:
        runner = make_probe_runner(run)
        eligible = {k: w["row"] for k, w in witness.items()
                    if runner and w["category"] != "self"
                    and w["row"].get("operation") in ("search", "match", "fullmatch", "finditer")
                    and all(ord(ch) < 128 for ch in w["row"].get("pattern", "") + w["row"].get("subject", ""))}
        reduced = reduce_witnesses(eligible, runner, run / "reduce") if eligible else {}
        note = f"{len(reduced)} of {len(eligible)} eligible witnesses reduced"
    except Exception as e:  # noqa: BLE001 - a reducer failure degrades to "not reduced"
        reduced, note = {}, f"NOT REDUCED: the reducer failed ({type(e).__name__}: {str(e)[:200]})"
    write_report(run, f, clusters, witness, reduced, note)
    return 0


def write_report(run: Path, f: Findings, clusters: dict, witness: dict, reduced: dict, note: str) -> None:
    lines = [f"# Battery triage: {run.name}", ""]
    cfg = json.loads((run / "config.json").read_text(encoding="utf-8")) if (run / "config.json").exists() else {}
    lines += [f"Commit {cfg.get('commit', '?')}, master seed {cfg.get('master', '?')}, smoke {cfg.get('smoke')}.", "",
              "Attribution: KNOWN = decided by an existing screen; HINT = a signature match, to be judged; "
              "UNJUDGED = nothing matched. Witness: the smallest row of the cluster, with its answers as the "
              "harness recorded them. Reduced: the witness shrunk by the delta reducer while the port still "
              "differed from upstream in a rough normal form (span, counts, partial, sorted captures) and kept "
              "its risky constructs; it ignores ExpectedDivergences, so it can drift onto a known divergence "
              "and must be checked before use.", "",
              "| # | Cluster | Rows | Attribution | Witness (pattern, subject, op) | Port | Other | Reduced: row; port; upstream |",
              "|---|---|---|---|---|---|---|---|"]
    order = sorted(clusters, key=lambda k: (-len(clusters[k]), k))

    def question(r):
        q = f"`{r.get('pattern')!s}` `{r.get('subject')!s}` {r.get('operation')}{' partial' if r.get('partial') else ''}"
        if "pos" in r or "endpos" in r:
            q += f" pos={r.get('pos')} endpos={r.get('endpos')}"
        if r.get("flags"):
            q += f" flags={r['flags']}"
        return q

    def cell(s, n=160):
        return str(s).replace("|", "\\|").replace("\n", " ")[:n]

    for n, k in enumerate(order, start=1):
        xs, w = clusters[k], witness[k]
        hints = sorted({x["hint"] for x in xs if x["hint"]})
        attribution = ("HINT " + ", ".join(hints)) if hints else "UNJUDGED"
        red = ""
        if k in reduced:
            rr, pa, up = reduced[k]
            red = f"{cell(question(rr))}; port {cell(pa, 80)}; upstream {cell(up, 80)}"
        lines.append(f"| {n} | {cell(k)} | {len(xs)} | {attribution} | {cell(question(w['row']))} | {cell(w['port'])} | "
                     f"{cell(w['other'])} | {red} |")
    lines += ["", "## Known (accounted for by an existing screen)", "", "| What | Rows |", "|---|---|"]
    lines += [f"| {k} | {n} |" for k, n in sorted(f.known.items(), key=lambda kv: -kv[1])]
    lines += ["", "## Counts and notes", ""] + [f"- {x}" for x in f.notes] + [f"- witness reduction: {note}"]
    (run / "triage.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(clusters)} clusters, {len(f.rows)} interesting rows, {sum(f.known.values())} known; {note}")


if __name__ == "__main__":
    if sys.argv[1:2] == ["--upstream-worker"]:
        sys.exit(upstream_worker(sys.argv[2], int(sys.argv[3])))
    sys.exit(main(sys.argv[1:]))
