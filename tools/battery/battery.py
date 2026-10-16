"""The correctness battery's driver (plan to 1.0, section 3). Start it with tools/battery/run.sh.

    python tools/battery/battery.py all RUN_ID [--commit REV] [--smoke]   # every part, then triage
    python tools/battery/battery.py part NAME RUN_ID                     # one part (all calls these)
    python tools/battery/battery.py triage RUN_ID
    python tools/battery/battery.py clean RUN_ID                         # remove the run's worktrees

Output: .scratch/battery/RUN_ID/ in the main checkout. `status` there is the progress log (one line
per event); `config.json` fixes the commit, seeds and sizes on first launch, so a re-launch with the
same RUN_ID resumes the same run. Every part writes its results as it goes and a `<part>/DONE`
marker at the end; a re-launch skips finished parts and finished steps inside a part.

The commit under test is checked out in detached worktrees, one per part that builds, so parts
running at once never share a build folder: .claude/worktrees/bat-RUN_ID-{oracle,oracle2,probe,pa,pb,matrix}.

Parts (run in parallel, each behind the resource gate):
  oracle   tools/sweep-seeds.ps1, one seed at a time, Debug consumer, as two parts (oracle and
           oracle2) on alternate seeds; diverging rows re-asked in Release (a Debug consumer's timeout is not a divergence, run-oracle.ps1 -Configuration);
           a seed whose consumer dies (a failed Debug.Assert ends the test host) gets its wave run
           through tools/matrix/port-runner.cs, whose listener names the asserting rows.
  probe    the D85 real-pattern bed through tools/battery/probe.cs (Debug, asserts reported per
           row), then the guards-off control over the pair and matrix rows it applies to.
  pa, pb   the feature-pair rows (tools/battery/gen/pairs.py), split in two, each through
           tools/matrix/run.py (checks C1-C8).
  matrix   tools/matrix/gen.py at two new seeds, each through tools/matrix/run.py.

Resource gate, before every heavy step: wait while .scratch/test-lock is held (a timing run), while
less than 6 GB plus the step's own need is free, or while the CPU is over 85% busy.
"""

from __future__ import annotations

import argparse
import datetime
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

import psutil

HERE = Path(__file__).resolve().parent
TOOLS_REPO = HERE.parent.parent  # the checkout this script lives in (its generators and probe)
MAIN = Path(subprocess.run(["git", "-C", str(HERE), "rev-parse", "--path-format=absolute", "--git-common-dir"],
                           capture_output=True, text=True, check=True).stdout.strip()).parent
OUT_ROOT = MAIN / ".scratch" / "battery"
LOCK = MAIN / ".scratch" / "test-lock"
# BATTERY_FREE_FLOOR_GB lowers the floor for a resumed run whose remaining parts are light (owner's
# call; 2026-10-16 the last pair chunks peaked at 190 MB while an IDE held the machine under 6 GB).
FREE_FLOOR_GB = float(os.environ.get("BATTERY_FREE_FLOOR_GB", "6"))
PARTS = ("oracle", "oracle2", "probe", "pa", "pb", "matrix")
RUN_PY_CHUNK = 500
RUN_PY_MINUTES = 10  # run.py returns after this, so the gate is re-checked between its stretches
BED = MAIN / ".scratch" / "s1016" / "d85" / "bed"
GUARD_WRAP = "{s<=1:[\\x00]}"  # a fuzzy section that can make no edit: guards off, answer unchanged


# ----------------------------------------------------------------------------------------------
# Plumbing


def now() -> str:
    return datetime.datetime.now().strftime("%H:%M:%S")


class Run:
    def __init__(self, run_id: str):
        self.id = run_id
        self.out = OUT_ROOT / run_id
        self.out.mkdir(parents=True, exist_ok=True)
        self.cfg_path = self.out / "config.json"
        self.cfg = json.loads(self.cfg_path.read_text(encoding="utf-8")) if self.cfg_path.exists() else None

    def status(self, msg: str) -> None:
        line = f"{now()} {msg}"
        with open(self.out / "status", "a", encoding="utf-8", newline="\n") as f:
            f.write(line + "\n")
        print(line, flush=True)

    def sut(self, slot: str) -> Path:
        return MAIN / ".claude" / "worktrees" / f"bat-{self.id}-{slot}"

    def ensure_sut(self, slot: str) -> Path:
        path = self.sut(slot)
        commit = self.cfg["commit"]
        if not path.exists():
            self.status(f"{slot}: creating worktree at {commit[:8]}")
            sh(["git", "-C", str(MAIN), "worktree", "add", "--detach", str(path), commit])
            sh(["git", "-C", str(path), "submodule", "update", "--init", "upstream"])
        head = sh(["git", "-C", str(path), "rev-parse", "HEAD"]).strip()
        if head != commit:
            raise SystemExit(f"{path} is at {head}, not {commit}")
        return path


def sh(cmd: list[str], **kw) -> str:
    return subprocess.run(cmd, capture_output=True, text=True, check=True, **kw).stdout


def env() -> dict:
    e = dict(os.environ)
    e["PATH"] = r"C:\Program Files\dotnet" + os.pathsep + e.get("PATH", "")
    e["PYTHONDONTWRITEBYTECODE"] = "1"
    e["PYTHONUNBUFFERED"] = "1"
    e["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    if not shutil.which("node", path=e["PATH"]):
        # SHORTCUT: C7's survey runs node. On 2026-10-16 an nvm migration left node.exe in
        # C:\ProgramData\nvm\v<version> without its C:\nvm4w\nodejs link, and run.py's c7 stage then
        # failed every chunk ("WinError 2"); nvm v2 then put its shims in a new folder that shells
        # started earlier do not have on PATH. Upgrade path: delete this once PATH has node again.
        shim = Path.home() / "AppData" / "Local" / "Author Software" / "nvm" / ".shim" / "node.exe"
        found = [shim] if shim.exists() else sorted(Path(r"C:\ProgramData\nvm").glob("v*/node.exe"))
        if found:
            e["PATH"] = str(found[-1].parent) + os.pathsep + e["PATH"]
    return e


def preflight(run: Run) -> None:
    """Every outside engine the parts call, found or not, in the status file before anything runs."""
    e = env()
    missing = [x for x in ("dotnet", "pwsh", "python", "perl", "node", "wsl") if not shutil.which(x, path=e["PATH"])]
    run.status("preflight: " + (f"MISSING {', '.join(missing)}" if missing else "dotnet, pwsh, python, perl, node, wsl found"))
    if {"dotnet", "pwsh", "node", "perl"} & set(missing):
        raise SystemExit(f"cannot run without {', '.join(missing)}")


def gate(run: Run, who: str, need_gb: float) -> None:
    """Waits until no timing run holds the lock, enough memory is free and the CPU is not saturated."""
    said = 0.0
    while True:
        reasons = []
        if LOCK.exists():
            reasons.append("test lock held")
        free = psutil.virtual_memory().available / 2**30
        if free < FREE_FLOOR_GB + need_gb:
            reasons.append(f"{free:.1f} GB free")
        cpu = psutil.cpu_percent(interval=2)
        if cpu > 85:
            reasons.append(f"CPU {cpu:.0f}%")
        if not reasons:
            return
        if time.time() - said > 300:
            run.status(f"{who}: waiting ({', '.join(reasons)})")
            said = time.time()
        time.sleep(20)


def call(cmd: list[str], log: Path, cwd: Path, extra_env: dict | None = None, stdout: Path | None = None) -> int:
    e = env()
    if extra_env:
        e.update(extra_env)
    with open(log, "ab") as lf:
        out = open(stdout, "wb") if stdout else lf
        try:
            return subprocess.run(cmd, cwd=cwd, env=e, stdin=subprocess.DEVNULL, stdout=out,
                                  stderr=lf).returncode
        finally:
            if stdout:
                out.close()


def lines(path: Path) -> int:
    return sum(1 for line in open(path, encoding="utf-8", errors="replace") if line.strip()) if path.exists() else 0


def probe_lines(path: Path) -> int:
    """Answer lines in a probe output; `dotnet run` may print build messages to the same stream."""
    if not path.exists():
        return 0
    return sum(1 for line in open(path, encoding="utf-8", errors="replace") if is_answer(line))


def is_answer(line: str) -> bool:
    return line.rstrip("\n").endswith("ms") and line.count("\t") >= 4


def jsonl(path: Path) -> list[dict]:
    return [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()] if path.exists() else []


def write_jsonl(path: Path, rows) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for r in rows:
            f.write(json.dumps(r, ensure_ascii=True) + "\n")


# ----------------------------------------------------------------------------------------------
# Configuration


def configure(run: Run, commit: str | None, smoke: bool) -> None:
    if run.cfg:
        if commit and sh(["git", "-C", str(MAIN), "rev-parse", commit]).strip() != run.cfg["commit"]:
            raise SystemExit(f"run {run.id} is fixed at {run.cfg['commit']}; use a new run id")
        return
    rev = sh(["git", "-C", str(MAIN), "rev-parse", commit or "main"]).strip()
    # The master seed is the run id's digits: 20261016-1 gives 202610161 (the brief's "date+N").
    digits = "".join(ch for ch in run.id if ch.isdigit())
    master = int(digits[-9:]) if digits else 1
    seeds = [int(s) for s in sh(["pwsh", "-NoProfile", "-Command",
                                 f"$d=[System.Random]::new({master}); 1..10 | % {{ $d.Next(1 -shl 30) }}"]).split()]
    run.cfg = {
        "commit": rev, "smoke": smoke, "master": master,
        # sweep-seeds.ps1 draws its seeds the same way from -MasterSeed; they are drawn here so each
        # seed can run (and fail) on its own, and recorded so the run can be reproduced.
        "oracleSeeds": seeds[:1] if smoke else seeds,
        "oracleCount": 4 if smoke else 2000,
        "bedRows": 400 if smoke else 0,  # 0 = all 276,591
        "pairsPerCell": 12 if smoke else 2500,
        "matrixSeeds": [master + 1] if smoke else [master + 1, master + 2],
        "matrixSample": 60 if smoke else 0,  # 0 = the complete matrix
        "created": datetime.datetime.now().isoformat(timespec="seconds"),
    }
    run.cfg_path.write_text(json.dumps(run.cfg, indent=1), encoding="utf-8")
    run.status(f"configured: commit {rev[:8]}, master seed {master}, smoke {smoke}")


def generate_rows(run: Run) -> None:
    """Pair rows and matrix rows, written once; the probe part needs both for its control."""
    d = run.out / "rows"
    d.mkdir(exist_ok=True)
    pairs = d / "pairs.jsonl"
    if not pairs.exists():
        tmp = d / "pairs.tmp"
        rc = call([sys.executable, str(HERE / "gen" / "pairs.py"), "--seed", str(run.cfg["master"]), "--per-cell",
                   str(run.cfg["pairsPerCell"]), "--out", str(tmp), "--counts", str(d / "pairs-counts.csv")],
                  d / "gen.log", TOOLS_REPO)
        if rc != 0:
            run.status(f"rows: pairs.py exit {rc} (a cell below its count; see rows/gen.log), kept")
        rows = jsonl(tmp)
        write_jsonl(d / "pairs-a.jsonl", (r for i, r in enumerate(rows) if i % 2 == 0))
        write_jsonl(d / "pairs-b.jsonl", (r for i, r in enumerate(rows) if i % 2 == 1))
        tmp.replace(pairs)
        run.status(f"rows: {len(rows)} pair rows")
    for seed in run.cfg["matrixSeeds"]:
        f = d / f"matrix-{seed}.jsonl"
        if f.exists():
            continue
        tmp = d / f"matrix-{seed}.tmp"
        cmd = [sys.executable, str(TOOLS_REPO / "tools" / "matrix" / "gen.py"), "--out", str(tmp), "--seed", str(seed),
               "--counts", str(d / f"matrix-{seed}-counts.csv")]
        if run.cfg["matrixSample"]:
            cmd += ["--sample", str(run.cfg["matrixSample"])]
        rc = call(cmd, d / "gen.log", TOOLS_REPO)
        if rc != 0 or not tmp.exists():
            run.status(f"rows: matrix gen.py seed {seed} exit {rc}; see rows/gen.log")
            if not tmp.exists():
                raise SystemExit(1)
        tmp.replace(f)
        run.status(f"rows: matrix seed {seed}: {lines(f)} rows")


# ----------------------------------------------------------------------------------------------
# Part: oracle sweep


def slot_seeds(run: Run, slot: str) -> list[int]:
    """The two oracle parts take alternate seeds, each in its own worktree."""
    return run.cfg["oracleSeeds"][0 if slot == "oracle" else 1::2]


def part_oracle(run: Run, slot: str) -> None:
    out = run.out / "oracle"
    out.mkdir(exist_ok=True)
    sut = run.ensure_sut(slot)
    count = run.cfg["oracleCount"]
    log = out / f"sweep-{slot}.jsonl"
    question = f"{count}||Debug"
    for seed in slot_seeds(run, slot):
        done = {e["seed"] for e in jsonl(log) if e.get("question") == question}
        keep = out / f"sweep-{seed}"
        if seed in done or (keep / "crashed.txt").exists():
            continue
        gate(run, slot, 1.5)
        run.status(f"{slot}: seed {seed} ({count} rows a generator, Debug) starting")
        t = time.time()
        rc = call(["pwsh", "-NoProfile", "-File", str(sut / "tools" / "sweep-seeds.ps1"), "-Seeds", str(seed),
                   "-Count", str(count), "-Configuration", "Debug", "-Log", str(log)],
                  out / f"seed-{seed}.log", sut)
        keep.mkdir(exist_ok=True)
        src = sut / "TestResults" / "oracle" / f"sweep-{seed}"
        for name in ("report.txt", "wave.jsonl"):
            if (src / name).exists():
                shutil.copy2(src / name, keep / name)
        entry = next((e for e in jsonl(log) if e.get("seed") == seed and e.get("question") == question), None)
        if entry is None:
            # No log line: the consumer produced no report. The one known cause is a failed
            # Debug.Assert, which ends the test host; keep the wave for the assert hunt.
            wave = sut / "TestResults" / "oracle" / f"wave-{seed}.jsonl"
            if wave.exists():
                shutil.copy2(wave, keep / "wave.jsonl")
            (keep / "crashed.txt").write_text(f"sweep-seeds exit {rc}; no log line; see ../seed-{seed}.log\n",
                                              encoding="utf-8")
            run.status(f"{slot}: seed {seed} produced no verdict (exit {rc}) in {time.time() - t:.0f} s")
        else:
            run.status(f"{slot}: seed {seed} {'green' if entry['green'] else 'RED'} in {time.time() - t:.0f} s: "
                       f"{entry['tally']}")
    for seed in slot_seeds(run, slot):
        keep = out / f"sweep-{seed}"
        if (keep / "crashed.txt").exists() and not (keep / "asserts.jsonl").exists():
            assert_hunt(run, sut, keep)
    recheck_in_release(run, sut, out, slot)
    (run.out / f"{slot}.DONE").write_text(now() + "\n", encoding="utf-8")
    run.status(f"{slot}: DONE")


def wave_rows(wave: Path) -> list[dict]:
    rows = jsonl(wave)
    return rows[1:] if rows and rows[0].get("kind") == "header" else rows


def diverging_rows(report: Path) -> list[int]:
    import re
    if not report.exists():
        return []
    return [int(m.group(1)) for m in re.finditer(r"^(?:DIVERGE|FAULT) row (\d+) ", report.read_text(encoding="utf-8"),
                                                re.M)]


def recheck_in_release(run: Run, sut: Path, out: Path, slot: str) -> None:
    """Every DIVERGE/FAULT row of the slot's seeds, re-recorded and compared with a Release consumer."""
    target = out / f"recheck-{slot}-report.txt"
    if target.exists():
        return
    keys = ("generator", "pattern", "flags", "namedLists", "subject", "operation", "partial", "pos", "endpos")
    rows, origin = [], []
    for seed in slot_seeds(run, slot):
        keep = out / f"sweep-{seed}"
        wave = wave_rows(keep / "wave.jsonl")
        for n in diverging_rows(keep / "report.txt"):
            if 1 <= n <= len(wave):
                rows.append({k: wave[n - 1][k] for k in keys if k in wave[n - 1]})
                origin.append({"seed": seed, "row": n})
    write_jsonl(out / f"recheck-{slot}-origin.jsonl", origin)
    if not rows:
        target.write_text("no diverging rows to re-check\n", encoding="utf-8")
        return
    write_jsonl(out / f"recheck-{slot}-rows.jsonl", rows)
    gate(run, slot, 1.5)
    run.status(f"{slot}: re-checking {len(rows)} diverging rows in Release")
    legacy = sut / "TestResults" / "oracle" / "report.txt"
    if legacy.exists():
        legacy.unlink()
    call(["pwsh", "-NoProfile", "-File", str(sut / "tools" / "run-oracle.ps1"), "-Rows",
          str(out / f"recheck-{slot}-rows.jsonl"), "-Configuration", "Release"], out / f"recheck-{slot}.log", sut)
    if legacy.exists():
        shutil.copy2(legacy, target)
    else:
        target.write_text(f"the Release re-check produced no report; see recheck-{slot}.log\n", encoding="utf-8")


def assert_hunt(run: Run, sut: Path, keep: Path) -> None:
    """A seed whose Debug consumer died: run its wave through port-runner.cs (Debug, asserts throw)."""
    wave = wave_rows(keep / "wave.jsonl")
    rows = []
    for i, w in enumerate(wave, start=1):
        r = {k: w[k] for k in ("pattern", "flags", "namedLists", "subject", "operation", "partial", "pos", "endpos")
             if k in w}
        r.update({"id": i, "cell": w.get("generator", "?"), "tags": []})
        rows.append(r)
    write_jsonl(keep / "hunt-rows.jsonl", rows)
    gate(run, "oracle", 2)
    run.status(f"oracle: assert hunt over {len(rows)} rows of {keep.name}")
    target = keep / "hunt.port.jsonl"
    call(["dotnet", "run", "-c", "Debug", str(sut / "tools" / "matrix" / "port-runner.cs"), "--",
          str(keep / "hunt-rows.jsonl"), str(target), "--start", str(lines(target))],
         keep / "hunt.log", sut, {"DOTNET_GCHeapHardLimit": "0xC0000000"})
    found = []
    for d in jsonl(target):
        bad = [v for v in (d.get("base"), d.get("off"), d.get("on")) if isinstance(v, str) and v.startswith(("ASSERT", "EXC"))]
        if bad:
            found.append({"row": d["id"], **{k: rows[d["id"] - 1].get(k) for k in ("pattern", "subject", "operation",
                                                                                    "flags", "partial")},
                          "answer": bad[0]})
    write_jsonl(keep / "asserts.jsonl", found)
    run.status(f"oracle: assert hunt {keep.name}: {len(found)} rows assert or throw")


# ----------------------------------------------------------------------------------------------
# Part: probe (the bed, then the guards-off control)


def install_probe(sut: Path) -> Path:
    d = sut / ".battery"
    d.mkdir(exist_ok=True)
    shutil.copy2(HERE / "probe.cs", d / "probe.cs")
    return d / "probe.cs"


def probe_chunks(run: Run, sut: Path, probe: Path, rows: list[dict], d: Path, chunk: int, who: str) -> list[Path]:
    """Runs rows through the probe in chunks; a chunk with a complete output is not run again."""
    d.mkdir(parents=True, exist_ok=True)
    outs = []
    for k in range(0, len(rows), chunk):
        part = rows[k:k + chunk]
        src = d / f"chunk-{k // chunk:03}.jsonl"
        dst = d / f"chunk-{k // chunk:03}.out.txt"
        outs.append(dst)
        if probe_lines(dst) >= len(part):
            continue
        write_jsonl(src, part)
        gate(run, who, 1)
        t = time.time()
        rc = call(["dotnet", "run", "-c", "Debug", str(probe), "--", str(src), "5"], d / "probe.log", sut,
                  {"DOTNET_GCHeapHardLimit": "0xC0000000"}, stdout=dst)
        got = probe_lines(dst)
        run.status(f"{who}: {src.name} exit {rc}, {got}/{len(part)} answers in {time.time() - t:.0f} s")
        if got < len(part):
            # A crashed probe (out of memory, a stack overflow): finish the chunk a row at a time
            # so one bad row costs one row. The row that kills the process is recorded as such.
            kept = [ln for ln in open(dst, encoding="utf-8", errors="replace") if is_answer(ln)]
            with open(dst, "w", encoding="utf-8", newline="\n") as f:
                f.writelines(kept)
            with open(dst, "a", encoding="utf-8", newline="\n") as f:
                for row in part[got:]:
                    one = d / "one.jsonl"
                    write_jsonl(one, [row])
                    tmp = d / "one.out.txt"
                    call(["dotnet", "run", "-c", "Debug", str(probe), "--", str(one), "5"], d / "probe.log", sut,
                         {"DOTNET_GCHeapHardLimit": "0xC0000000"}, stdout=tmp)
                    found = [ln for ln in (tmp.read_text(encoding="utf-8", errors="replace").splitlines()
                                           if tmp.exists() else []) if is_answer(ln)]
                    txt = found[-1] if found else ""
                    if not txt:
                        txt = "\t".join([row["operation"], json.dumps(row["pattern"]), json.dumps(row["subject"]),
                                         "CRASH: the probe process died on this row", "0ms"])
                    f.write(txt.splitlines()[-1] + "\n")
    return outs


def part_probe(run: Run) -> None:
    out = run.out / "bed"
    out.mkdir(exist_ok=True)
    sut = run.ensure_sut("probe")
    probe = install_probe(sut)
    if not (out / "DONE").exists():
        rows = jsonl(BED / "rows.jsonl")
        if run.cfg["bedRows"]:
            rows = rows[:run.cfg["bedRows"]]
        run.status(f"bed: {len(rows)} rows")
        outs = probe_chunks(run, sut, probe, rows, out / "chunks", 20000, "bed")
        with open(out / "probe-all.txt", "w", encoding="utf-8", newline="\n") as f:
            for o in outs:
                f.write(o.read_text(encoding="utf-8", errors="replace"))
        call([sys.executable, str(BED / "compare.py"), str(out / "probe-all.txt"), "--show", "0"], out / "compare.log",
             BED, stdout=out / "compare.txt")
        (out / "DONE").write_text(now() + "\n", encoding="utf-8")
        run.status("bed: DONE")
    part_control(run, sut, probe)
    (run.out / "probe.DONE").write_text(now() + "\n", encoding="utf-8")


def control_pattern(pattern: str) -> str | None:
    """The pattern inside a fuzzy section that can make no edit, leading inline flags kept outside."""
    import re
    m = re.match(r"^((?:\(\?[a-zA-Z0-9]+\))*)(.*)$", pattern, re.S)
    head, rest = m.group(1), m.group(2)
    if not rest:
        return None
    return f"{head}(?:{rest}){GUARD_WRAP}"


def control_applies(row: dict) -> bool:
    """Rows where a fuzzy section changes nothing but the guards: no fuzzy section of its own (its
    guards are off already), no verb or \\K (both behave differently inside a fuzzy section, D33,
    ledger entries on verbs in fuzzy sections), not partial, no flags outside the pattern (the probe
    cannot pass them), no named lists and no group calls."""
    tags = set(row.get("tags") or [])
    return (not row.get("flags") and not row.get("partial") and not row.get("namedLists")
            and not tags & {"fuzzy", "fuzzy-min", "fuzzy-cost", "verb", "keep", "call", "flag-p", "flag-b", "flag-e"}
            and "(*" not in row["pattern"] and "\\K" not in row["pattern"])


def part_control(run: Run, sut: Path, probe: Path) -> None:
    out = run.out / "control"
    if (out / "DONE").exists():
        return
    out.mkdir(exist_ok=True)
    rows, origin = [], []
    sources = [("pairs", run.out / "rows" / "pairs.jsonl")] + [
        (f"matrix-{s}", run.out / "rows" / f"matrix-{s}.jsonl") for s in run.cfg["matrixSeeds"]]
    for name, path in sources:
        for r in jsonl(path):
            if not control_applies(r):
                continue
            wrapped = control_pattern(r["pattern"])
            if wrapped is None:
                continue
            q = {k: r[k] for k in ("subject", "operation", "pos", "endpos") if k in r}
            rows.append({"pattern": r["pattern"], **q})
            rows.append({"pattern": wrapped, **q})
            origin.append({"source": name, "id": r["id"]})
    write_jsonl(out / "origin.jsonl", origin)
    run.status(f"control: {len(origin)} rows, each plain and guards-off")
    outs = probe_chunks(run, sut, probe, rows, out / "chunks", 20000, "control")
    with open(out / "probe-all.txt", "w", encoding="utf-8", newline="\n") as f:
        for o in outs:
            f.write(o.read_text(encoding="utf-8", errors="replace"))
    (out / "DONE").write_text(now() + "\n", encoding="utf-8")
    run.status("control: DONE")


# ----------------------------------------------------------------------------------------------
# Parts: the matrix harness (pa, pb, matrix)


def run_py(run: Run, slot: str, rows: Path, name: str) -> Path:
    """tools/matrix/run.py to completion, in stretches of RUN_PY_MINUTES with the gate between."""
    sut = run.ensure_sut(slot)
    results = sut / ".scratch" / "matrix" / "results" / name
    log = run.out / f"{slot}.log"
    failures = stalled = 0
    last = -1
    while True:
        if (results / "summary.md").exists() and (results / "cost.json").exists() and (results / "judged.ok").exists():
            return results
        gate(run, slot, 2)
        rc = call([sys.executable, str(sut / "tools" / "matrix" / "run.py"), str(rows), "--run", name, "--chunk",
                   str(RUN_PY_CHUNK), "--max-minutes", str(RUN_PY_MINUTES)], log, sut)
        done = len(list(results.glob("chunk-*.c8.done")))
        total = len(list(results.glob("chunk-[0-9][0-9][0-9].jsonl")))
        run.status(f"{slot}: run.py {name} exit {rc}, {done}/{total} chunks complete")
        if rc == 0:
            (results / "judged.ok").write_text(now() + "\n", encoding="utf-8")
            return results
        markers = len(list(results.glob("chunk-*.done")))
        stalled = stalled + 1 if markers == last else 0
        last = markers
        if rc != 3 or stalled >= 3:
            failures += 1
            if failures >= 3:
                run.status(f"{slot}: run.py {name} failed three times; see {log.name}")
                raise SystemExit(1)


def copy_results(results: Path, dest: Path) -> None:
    dest.mkdir(parents=True, exist_ok=True)
    for name in ("summary.md", "cells.csv", "failures.jsonl", "c7-failures.jsonl", "c7-others.jsonl", "cost.json",
                 "controls.txt"):
        if (results / name).exists():
            shutil.copy2(results / name, dest / name)
    (dest / "results-path.txt").write_text(str(results) + "\n", encoding="utf-8")


def part_pairs(run: Run, slot: str) -> None:
    half = "a" if slot == "pa" else "b"
    results = run_py(run, slot, run.out / "rows" / f"pairs-{half}.jsonl", f"bat-{run.id}-p{half}")
    copy_results(results, run.out / "pairs" / half)
    (run.out / f"{slot}.DONE").write_text(now() + "\n", encoding="utf-8")
    run.status(f"{slot}: DONE")


def part_matrix(run: Run) -> None:
    for seed in run.cfg["matrixSeeds"]:
        results = run_py(run, "matrix", run.out / "rows" / f"matrix-{seed}.jsonl", f"bat-{run.id}-m{seed}")
        copy_results(results, run.out / "matrix" / str(seed))
    (run.out / "matrix.DONE").write_text(now() + "\n", encoding="utf-8")
    run.status("matrix: DONE")


# ----------------------------------------------------------------------------------------------
# Commands


def part_done(run: Run, name: str) -> bool:
    return (run.out / f"{name}.DONE").exists()


def cmd_part(run: Run, name: str) -> None:
    try:
        {"oracle": lambda r: part_oracle(r, "oracle"), "oracle2": lambda r: part_oracle(r, "oracle2"), "probe": part_probe, "matrix": part_matrix,
         "pa": lambda r: part_pairs(r, "pa"), "pb": lambda r: part_pairs(r, "pb")}[name](run)
    except SystemExit:
        raise
    except Exception as e:  # noqa: BLE001 - recorded in the status file, then re-raised
        run.status(f"{name}: FAILED {type(e).__name__}: {e}")
        raise


def cmd_all(run: Run, commit: str | None, smoke: bool) -> int:
    configure(run, commit, smoke)
    (run.out / "driver.pid").write_text(str(os.getpid()) + "\n", encoding="utf-8")
    run.status(f"driver: pid {os.getpid()}, commit {run.cfg['commit'][:8]}")
    preflight(run)
    generate_rows(run)
    procs = {}
    for name in PARTS:
        if part_done(run, name):
            run.status(f"{name}: already done, skipped")
            continue
        log = open(run.out / f"part-{name}.log", "ab")
        procs[name] = subprocess.Popen([sys.executable, str(Path(__file__)), "part", name, run.id], cwd=TOOLS_REPO,
                                       env=env(), stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT)
        run.status(f"{name}: started, pid {procs[name].pid}")
        time.sleep(30)  # stagger the first builds
    for name, p in procs.items():
        rc = p.wait()
        run.status(f"{name}: exit {rc}")
    missing = [n for n in PARTS if not part_done(run, n)]
    if missing:
        run.status(f"driver: parts not finished: {', '.join(missing)}; launch again to resume")
        return 1
    rc = cmd_triage(run)
    run.status(f"driver: FINISHED (triage exit {rc})")
    return rc


def cmd_triage(run: Run) -> int:
    rc = call([sys.executable, str(HERE / "triage.py"), str(run.out)], run.out / "triage.log", TOOLS_REPO)
    run.status(f"triage: exit {rc}, {run.out / 'triage.md'}")
    return rc


def cmd_clean(run: Run) -> None:
    for slot in PARTS:
        path = run.sut(slot)
        if path.exists():
            # A worktree with a checked-out submodule: `git worktree remove` refuses, so delete the
            # folder and prune (never `submodule deinit`, which edits the shared .git/config).
            shutil.rmtree(path, ignore_errors=True)
            run.status(f"clean: removed {path.name}")
    sh(["git", "-C", str(MAIN), "worktree", "prune"])


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("command", choices=("all", "part", "triage", "clean"))
    ap.add_argument("args", nargs="+")
    ap.add_argument("--commit", default=None)
    ap.add_argument("--smoke", action="store_true")
    a = ap.parse_args(argv)
    if a.command == "part":
        name, run_id = a.args
        run = Run(run_id)
        cmd_part(run, name)
        return 0
    run = Run(a.args[0])
    if a.command == "all":
        return cmd_all(run, a.commit, a.smoke)
    if a.command == "triage":
        return cmd_triage(run)
    cmd_clean(run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
