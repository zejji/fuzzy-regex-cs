#!/usr/bin/env python
"""Re-runs the Phase 3 negative controls from the slices' closing notes.

S26's job: every control a Phase 3 slice recorded, re-run at its recorded seed *and* at a seed
that slice never used. A control that no longer reproduces means either the generator has lost
its teeth or the notes are wrong, and both are findings.

The shape is S25's own control runner, which its closing notes describe: mutate one
file, format it (an unformatted mutation fails the build on IDE0055 and the harness then reports
RED with no verdict line, which looks exactly like a control that fired), record each wave once
and reuse it across mutations, and never pipe a wave into a buffering filter.

The controls themselves live in `tools/controls.json`, one object per control:

    {"id": "S19-A", "name": "greedy-min",
     "file": "src/FuzzyRegex/Engine/Matcher.cs",
     "anchor": "case Opcode.GreedyRepeatOne:", "anchorNth": 2,
     "before": "...", "after": "...",
     "generator": "quantifiers", "count": 600, "seeds": [7, 20260901]}

`anchor`/`anchorNth` locate the *site*: the mutation is applied to the first occurrence of
`before` at or after the nth occurrence of `anchor`. Two sites in this engine share their text
(the forward and the backtrack half of the same opcode), so a bare string replace would silently
mutate the wrong one - which is a control that measures nothing.

Usage::

    python tools/run-controls.py --check              # resolve every site, build nothing
    python tools/run-controls.py --slices S19,S20     # run those slices' controls
    python tools/run-controls.py --ids S19-A          # run one
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

# Tracked rather than left in .scratch/, which slice sessions clear. S18's controls were lost
# that way and are permanently unreproducible; tools/launch-slice.ps1 and tools/heartbeat.sh
# were promoted for the same reason on 2026-09-01. The waves and the consumer log this
# writes are scratch and still go to .scratch/.
REPO = Path(__file__).resolve().parent.parent
CONTROLS = Path(__file__).resolve().parent / "controls.json"
WAVES = REPO / ".scratch" / "control-waves"
LIVE_WAVE = REPO / "TestResults" / "oracle" / "wave.jsonl"
REPORT = REPO / "TestResults" / "oracle" / "report.txt"
SUMMARY = re.compile(r"agree (\d+)\s+unsupported (\d+)\s+diverge (\d+)\s+of (\d+) rows")
CONSUME_TIMEOUT = 240


def load() -> list[dict]:
    return json.loads(CONTROLS.read_text(encoding="utf-8"))


def resolve(control: dict) -> tuple[Path, str, int]:
    """The file's text and the offset of the `before` to replace, or raises."""
    path = REPO / control["file"]
    text = path.read_text(encoding="utf-8")

    at = 0
    for _ in range(control.get("anchorNth", 1)):
        at = text.find(control["anchor"], at)
        if at < 0:
            raise LookupError(f"anchor {control['anchor']!r} not found often enough")
        at += 1
    at -= 1

    found = text.find(control["before"], at)
    if found < 0:
        raise LookupError("the 'before' text does not appear after the anchor")

    # A mutation whose 'before' also appears *before* the anchor is not wrong, but one that
    # appears twice after it is ambiguous about which site it means.
    if text.find(control["before"], found + 1) >= 0 and control.get("anchorNth", 1) == 1 and control["anchor"] == "":
        raise LookupError("the 'before' text is ambiguous: it appears more than once")

    return path, text, found


def mutate(control: dict) -> tuple[Path, str]:
    """Applies the mutation and returns the file and its original text."""
    path, text, at = resolve(control)
    mutated = text[:at] + control["after"] + text[at + len(control["before"]) :]
    path.write_text(mutated, encoding="utf-8", newline="\n")
    return path, text


def run(command: list[str], **kwargs) -> subprocess.CompletedProcess:
    return subprocess.run(command, cwd=REPO, capture_output=True, text=True, check=False, **kwargs)


def wave_for(generator: str, count: int, seed: int) -> Path:
    """Records the wave once and caches it: the rows do not depend on our own code."""
    WAVES.mkdir(parents=True, exist_ok=True)
    path = WAVES / f"{generator}-{count}-{seed}.jsonl"
    if path.exists():
        return path

    result = run([
        sys.executable, "tools/record-oracle.py",
        "--generator", generator, "--count", str(count), "--seed", str(seed),
        "--output", str(path),
    ])
    if result.returncode != 0:
        path.unlink(missing_ok=True)
        raise SystemExit(f"recording {generator} seed {seed} failed:\n{result.stdout}\n{result.stderr}")
    return path


def consume(wave: Path) -> tuple[str, str]:
    """Runs the consumer against one wave and returns its summary line."""
    LIVE_WAVE.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(wave, LIVE_WAVE)
    # Deleted first, so a build failure cannot leave the previous run's report to be read as this
    # run's answer - which is how an unformatted mutation gets recorded as a control that fired.
    REPORT.unlink(missing_ok=True)

    # Bounded, because a mutation can make a row backtrack catastrophically rather than answer
    # wrongly: S17-B ran for six minutes on a 1200-row wave that the honest engine answers in one
    # second. An unbounded wait there looks exactly like a slow build.
    # Redirected to a file rather than captured through a pipe. `dotnet test` spawns MSBuild nodes
    # and a test host that inherit the handles, so on a timeout `subprocess.run` kills the direct
    # child and then blocks for ever draining a pipe those grandchildren still hold open - which is
    # how a 240-second bound sat there for six minutes without firing (measured 2026-09-01).
    log = REPO / ".scratch" / "control-consume.log"
    with open(log, "w", encoding="utf-8") as handle:
        try:
            subprocess.run(
                ["dotnet", "test", "tests/FuzzyRegex.OracleTests/FuzzyRegex.OracleTests.csproj",
                 "--configuration", "Debug"],
                cwd=REPO, stdout=handle, stderr=subprocess.STDOUT, check=False,
                timeout=CONSUME_TIMEOUT,
            )
        except subprocess.TimeoutExpired:
            return "TIMEOUT", f"the consumer did not finish within {CONSUME_TIMEOUT}s"

    if not REPORT.exists():
        tail = "\n".join(log.read_text(encoding="utf-8", errors="replace").splitlines()[-12:])
        return "NO REPORT", tail

    first = REPORT.read_text(encoding="utf-8").splitlines()[0]
    return first, ""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="resolve every site and build nothing")
    parser.add_argument("--ids", default="", help="comma-separated control ids")
    parser.add_argument("--slices", default="", help="comma-separated slice prefixes")
    parser.add_argument("--record-only", action="store_true", help="record the waves and stop")
    args = parser.parse_args()

    # The two selectors are a union, not an intersection: "these ids and those slices" is what a
    # caller means by giving both, and intersecting them selects nothing.
    controls = load()
    if args.ids or args.slices:
        wanted = {i.strip() for i in args.ids.split(",") if i.strip()}
        prefixes = tuple(s.strip() for s in args.slices.split(",") if s.strip())
        controls = [
            c for c in controls
            if c["id"] in wanted or (prefixes and c["id"].startswith(prefixes))
        ]
    if not controls:
        raise SystemExit("no controls selected")

    if args.check:
        bad = 0
        for control in controls:
            try:
                path, text, at = resolve(control)
            except LookupError as e:
                print(f"FAIL {control['id']:8} {control['name']:34} {e}")
                bad += 1
                continue
            line = text.count("\n", 0, at) + 1
            print(f"ok   {control['id']:8} {control['name']:34} {control['file']}:{line}")
        return 1 if bad else 0

    for control in controls:
        for seed in control["seeds"]:
            wave_for(control["generator"], control["count"], seed)
    if args.record_only:
        return 0

    for control in controls:
        started = time.time()
        path, original = mutate(control)
        try:
            fmt = run(["dotnet", "csharpier", "format", str(path)])
            if fmt.returncode != 0:
                print(f"{control['id']:8} {control['name']:34} FORMAT FAILED {fmt.stderr.strip()[:200]}")
                continue

            for seed in control["seeds"]:
                summary, tail = consume(wave_for(control["generator"], control["count"], seed))
                match = SUMMARY.search(summary)
                if match:
                    agree, unsupported, diverge, rows = match.groups()
                    print(f"{control['id']:8} {control['name']:34} seed {seed:>9}  "
                          f"agree {agree:>5}  unsupported {unsupported:>4}  diverge {diverge:>5}  of {rows}")
                else:
                    print(f"{control['id']:8} {control['name']:34} seed {seed:>9}  {summary} {tail[:300]}")
                sys.stdout.flush()
        finally:
            path.write_text(original, encoding="utf-8", newline="\n")
        print(f"         ({time.time() - started:.0f}s, {path.name} restored)")
        sys.stdout.flush()

    return 0


if __name__ == "__main__":
    sys.exit(main())
