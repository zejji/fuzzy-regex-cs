#!/usr/bin/env python
"""Screens wave rows for upstream reading memory it never initialised, under MemorySanitizer.

Why this exists
---------------
A row on which upstream reads uninitialised memory has no ground truth: its answer is whatever
that memory held. The 2026-09-24 CI failure was one - ledger entry 5's carried slice reaching an
uninitialised `RE_Position new_position` in `basic_match` (`_regex.c:11820`) - and the same row
crashed on Linux and answered wrongly, without crashing, on Windows. Differential testing keeps
such inputs out of the comparison rather than scoring against them: Csmith refuses to generate
them, CsmithEdge generates loosely and filters them with sanitisers. MemorySanitizer is the
detector for this class; ASan and Windows page heap cannot see it (measured 2026-09-24: both
silent on the row, MSan and Valgrind both name it).

What it screens
---------------
Only the rows whose verdict depends on upstream's answer: the consumer's `fault`, `diverge` and
`expected` rows, whose numbers it writes to TestResults/oracle/screen-candidates.txt. An AGREEING
row is not screened: the port has no uninitialised memory, so it can only agree with a garbage
answer by coincidence. Screening every row is what was measured and rejected - 5 to 12 times the
recording time, and 15.2 GB where the plain build needs 1.4 GB on the same generator
(`/usr/bin/time -v`, seed 648375957, 2026-09-25), more than a GitHub Linux runner has.

How
---
In the image tools/msan/Dockerfile builds: CPython 3.12 with MSan, and upstream's extension
compiled from THIS checkout's submodule on every run, so the screen always tests the pinned code.
The candidate rows are re-recorded through record-oracle.py's crash supervisor, each tagged on
stderr, so every MSan report is tied to its row and a row that crashes does not take the rest.

Each row with a report gets an `undefinedBehaviour` annotation in the wave: the origins MSan
names, and the ledger entry tools/msan/known-undefined.json attributes them to - or null, which
the consumer turns into a failing `fault`.

The registry is pinned to an upstream commit
--------------------------------------------
Upstream may fix a defect in a later release, and a registry that went on calling rows "known"
after that would pin today's behaviour for ever. So the registry records the upstream commit its
reproductions last reproduced on, and when the submodule is anywhere else the screen REFUSES to
attribute anything and says to run `--reverify`, which re-screens every reproduction: one that
still shows its origin moves the commit forward, one that does not is reported as possibly fixed
upstream, for its ledger entry to be re-judged. The pattern is pytest's `xfail(strict=True)` and
rustc's `known-bug` tests: a known defect is re-checked, not remembered.

Usage::

    python tools/screen-undefined.py --wave TestResults/oracle/wave.jsonl
    python tools/screen-undefined.py --reverify
    python tools/screen-undefined.py --self-check
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
REGISTRY = REPO_ROOT / "tools" / "msan" / "known-undefined.json"
CANDIDATES = REPO_ROOT / "TestResults" / "oracle" / "screen-candidates.txt"
IMAGE = "fuzzyregex-msan:cpython-3.12.11"

# The worker's row tag, written to its stderr before each row when ORACLE_TAG_ROWS is set - see
# `_worker` in record-oracle.py. The key is the row's `_screenKey`, which this script assigns.
_TAG = re.compile(r"^@@SCREEN (\S+)$")
_WARNING = re.compile(r"WARNING: MemorySanitizer: (\S+)")
_STACK_ORIGIN = re.compile(r"created by an allocation of '([^']+)' in the stack frame")
_HEAP_ORIGIN = re.compile(r"created by a heap allocation")
_FRAME = re.compile(r"^\s*#\d+ 0x[0-9a-f]+ in (\S+) (\S+)")
_SUMMARY = re.compile(r"SUMMARY: MemorySanitizer: (\S+) (\S+)(?: in (\S+))?")


def parse_reports(log: str) -> dict[str, list[str]]:
    """Every MSan finding in a tagged worker log, as origins keyed by the row they happened on.

    An origin is "<local> in <function>" for a stack allocation, "heap allocation in <function>"
    for a heap one - the first upstream frame, not malloc - and, for a report MSan could not trace
    or a SEGV it caught with no uninitialised read before it, "<kind> at <function> <file:line>".
    Deduplicated and sorted per row, so one row's answer is the same however often it was asked.
    """
    found: dict[str, set[str]] = {}
    row: str | None = None
    lines = log.splitlines()
    index = 0
    while index < len(lines):
        line = lines[index]
        tag = _TAG.match(line)
        if tag:
            row = tag.group(1)
            index += 1
            continue

        warning = _WARNING.search(line)
        summary = _SUMMARY.search(line)
        if row is not None and warning and warning.group(1) != "use-of-uninitialized-value":
            # A SEGV or other fault MSan caught itself: no uninitialised read to attribute.
            found.setdefault(row, set())
        if row is not None and warning and warning.group(1) == "use-of-uninitialized-value":
            origin = None
            use = None
            scan = index + 1
            while scan < len(lines) and not _WARNING.search(lines[scan]) and not _TAG.match(lines[scan]):
                frame = _FRAME.match(lines[scan])
                if frame and use is None:
                    use = f"{frame.group(1)} {frame.group(2).rsplit('/', 1)[-1]}"
                stack = _STACK_ORIGIN.search(lines[scan])
                if stack:
                    below = _FRAME.match(lines[scan + 1]) if scan + 1 < len(lines) else None
                    origin = f"{stack.group(1)} in {below.group(1) if below else '?'}"
                    break
                if _HEAP_ORIGIN.search(lines[scan]):
                    for later in lines[scan + 1:]:
                        frame = _FRAME.match(later)
                        if frame and "_regex" in frame.group(2):
                            origin = f"heap allocation in {frame.group(1)}"
                            break
                    origin = origin or "heap allocation outside upstream"
                    break
                if _SUMMARY.search(lines[scan]):
                    break
                scan += 1
            found.setdefault(row, set()).add(origin or f"untraced use at {use or '?'}")
        elif row is not None and summary and summary.group(1) != "use-of-uninitialized-value":
            where = summary.group(3) or "?"
            found.setdefault(row, set()).add(f"{summary.group(1)} at {where} {summary.group(2).rsplit('/', 1)[-1]}")
        index += 1
    return {key: sorted(origins) for key, origins in found.items() if origins}


def attribute(origins: list[str], registry: dict) -> str | None:
    """The ledger entry that accounts for EVERY origin, or None if any origin is unknown."""
    ledgers = {d["origin"]: d["ledger"] for d in registry["defects"]}
    named = {ledgers.get(origin) for origin in origins}
    if None in named or len(named) != 1:
        return None
    return named.pop()


def _upstream_commit() -> str:
    return subprocess.run(["git", "-C", str(REPO_ROOT / "upstream"), "rev-parse", "HEAD"],
                          capture_output=True, text=True, check=True).stdout.strip()


def _load_registry() -> dict:
    return json.loads(REGISTRY.read_text(encoding="utf-8"))


def _check_registry_current(registry: dict) -> None:
    commit = _upstream_commit()
    if registry["verifiedAtUpstreamCommit"] != commit:
        raise SystemExit(
            "UPSTREAM HAS MOVED since the known-undefined registry was last verified.\n"
            f"  registry verified at {registry['verifiedAtUpstreamCommit']}\n"
            f"  upstream submodule   {commit}\n"
            "  A defect it lists may be fixed in the new upstream, so nothing is attributed until\n"
            "  its reproductions are re-screened. Run:\n"
            "      python tools/screen-undefined.py --reverify\n"
            "  and act on what it reports: each reproduction that still shows its origin moves the\n"
            "  registry forward; one that does not is a defect upstream may have fixed - re-judge its\n"
            "  ledger entry and the ExpectedDivergences that cite it before removing it here."
        )


# --------------------------------------------------------------------------------------------
# Running rows under MSan
# --------------------------------------------------------------------------------------------


def _docker_available() -> bool:
    if shutil.which("docker") is None:
        return False
    return subprocess.run(["docker", "info"], capture_output=True).returncode == 0


def _ensure_image() -> None:
    # Cached by Docker's layer cache after the first build (about two minutes: CPython with MSan).
    subprocess.run(["docker", "build", "-q", "-t", IMAGE, str(REPO_ROOT / "tools" / "msan")],
                   check=True, stdout=subprocess.DEVNULL)


def _host_path(path: Path) -> str:
    # Docker Desktop on Windows takes a Windows path; everywhere else the path as it is.
    return str(path.resolve())


def screen_rows(rows: list[dict]) -> dict[str, list[str]]:
    """Records `rows` under MSan in the image and returns the findings keyed by `_screenKey`."""
    _ensure_image()
    with tempfile.TemporaryDirectory(dir=REPO_ROOT / "TestResults") as tmp:
        work = Path(tmp)
        (work / "rows.jsonl").write_text("".join(json.dumps(r) + "\n" for r in rows),
                                         encoding="ascii", newline="")
        env = [
            "-e", "ORACLE_TAG_ROWS=1",
            "-e", "ORACLE_WORKER_LOG=/work/worker.log",
            # The submodule's commit, measured here: a worktree's submodule `.git` points at a
            # host path the container cannot follow, so `git rev-parse` inside it fails.
            "-e", f"ORACLE_UPSTREAM_COMMIT={_upstream_commit()}",
            # halt_on_error=0 reports every row; exitcode=0 so a worker that only REPORTED exits
            # cleanly (in recover mode MSan otherwise exits non-zero, which the supervisor rightly
            # reads as a harness failure); handle_segv=0 so a SEGV stays a real signal and is
            # recorded as a crash, instead of MSan's handler exiting with that same code 0.
            "-e", "MSAN_OPTIONS=halt_on_error=0:print_stacktrace=1:exitcode=0:handle_segv=0",
        ]
        process = subprocess.run(
            ["docker", "run", "--rm", "--security-opt", "seccomp=unconfined", *env,
             "-v", f"{_host_path(REPO_ROOT)}:/src:ro", "-v", f"{_host_path(work)}:/work",
             "-v", "fuzzyregex-msan-cache:/cache",
             IMAGE, "sh", "/src/tools/msan/record-under-msan.sh"],
            capture_output=True, text=True, errors="replace")
        log = (work / "worker.log").read_text(encoding="utf-8", errors="replace") \
            if (work / "worker.log").exists() else ""
        if process.returncode != 0:
            raise SystemExit("the MSan recording failed (exit "
                             f"{process.returncode}):\n{process.stderr[-3000:]}")
        return parse_reports(log)


# --------------------------------------------------------------------------------------------
# Commands
# --------------------------------------------------------------------------------------------

_ROW_INPUTS_DROPPED = ("outcome",)


def screen_wave(wave: Path, candidates: Path) -> int:
    registry = _load_registry()
    _check_registry_current(registry)

    lines = wave.read_text(encoding="ascii").split("\n")
    header, body = lines[0], [line for line in lines[1:] if line.strip()]
    numbers = sorted({int(n) for n in candidates.read_text(encoding="ascii").split()} if candidates.exists() else set())
    if not numbers:
        print("screen: no row's verdict depends on upstream's answer - nothing to screen")
        return 0

    rows = []
    for number in numbers:
        row = json.loads(body[number - 1])
        # Asked again from its inputs: everything the recorder wrote is dropped except what it
        # reads back, which is the whole row - record-oracle's --rows path ignores the answers.
        rows.append({**{k: v for k, v in row.items() if k not in _ROW_INPUTS_DROPPED}, "_screenKey": str(number)})
    findings = screen_rows(rows)

    for key, origins in findings.items():
        number = int(key)
        row = json.loads(body[number - 1])
        row["undefinedBehaviour"] = {"origins": origins, "known": attribute(origins, registry)}
        body[number - 1] = json.dumps(row)
    wave.write_text("\n".join([header, *body]) + "\n", encoding="ascii", newline="")

    known = sum(1 for o in findings.values() if attribute(o, registry))
    print(f"screen: {len(rows)} rows screened under MSan, {len(findings)} read uninitialised memory "
          f"({known} from a known origin, {len(findings) - known} from an unknown one)")
    return 0


def reverify() -> int:
    registry = _load_registry()
    rows = []
    for d_index, defect in enumerate(registry["defects"]):
        for r_index, row in enumerate(defect["reproductions"]):
            rows.append({**row, "_screenKey": f"{d_index}.{r_index}"})
    findings = screen_rows(rows)

    stale = []
    for d_index, defect in enumerate(registry["defects"]):
        for r_index, _ in enumerate(defect["reproductions"]):
            origins = findings.get(f"{d_index}.{r_index}", [])
            if defect["origin"] not in origins:
                stale.append((defect, r_index, origins))

    commit = _upstream_commit()
    if stale:
        for defect, r_index, origins in stale:
            print(f"NO LONGER REPRODUCES: {defect['ledger']} ({defect['origin']}), reproduction "
                  f"{r_index + 1}, on upstream {commit}: MSan now reports {origins or 'nothing'}.",
                  file=sys.stderr)
        print("  Upstream may have fixed it. Re-judge the ledger entry and every ExpectedDivergences "
              "entry that cites it, then update or remove the defect in tools/msan/known-undefined.json. "
              "The registry was NOT moved forward.", file=sys.stderr)
        return 1

    registry["verifiedAtUpstreamCommit"] = commit
    REGISTRY.write_text(json.dumps(registry, indent=2, ensure_ascii=True) + "\n", encoding="utf-8", newline="\n")
    print(f"reverify: all {len(rows)} reproductions still show their origin on upstream {commit}; "
          "the registry now records that commit - commit tools/msan/known-undefined.json")
    return 0


def _self_check() -> int:
    """The parser is the part that can be silently wrong, so it is checked on known MSan output."""
    failures = []
    log = "\n".join([
        "@@SCREEN 7",
        "==42==WARNING: MemorySanitizer: use-of-uninitialized-value",
        "    #0 0x7ff in basic_match /src/upstream/src/_regex.c:13876:54",
        "    #1 0x7ff in do_match /src/upstream/src/_regex.c",
        "  Uninitialized value was stored to memory at",
        "    #0 0x7ff in basic_match /src/upstream/src/_regex.c:11835:29",
        "  Uninitialized value was created by an allocation of 'new_position' in the stack frame",
        "    #0 0x7ff in basic_match /src/upstream/src/_regex.c:11820:13",
        "@@SCREEN 8",
        "@@SCREEN 9",
        "==43==WARNING: MemorySanitizer: use-of-uninitialized-value",
        "    #0 0x7ff in try_match /src/upstream/src/_regex.c:900:1",
        "  Uninitialized value was created by a heap allocation",
        "    #0 0x7ff in malloc /llvm/msan_interceptors.cpp:1",
        "    #1 0x7ff in re_alloc /src/upstream/src/_regex.c:700:5",
        "==43==WARNING: MemorySanitizer: use-of-uninitialized-value",
        "    #0 0x7ff in basic_match /src/upstream/src/_regex.c:13876:54",
        "  Uninitialized value was created by an allocation of 'new_position' in the stack frame",
        "    #0 0x7ff in basic_match /src/upstream/src/_regex.c:11820:13",
        "@@SCREEN 10",
        "==44==ERROR: MemorySanitizer: SEGV on unknown address",
        "SUMMARY: MemorySanitizer: SEGV /src/upstream/src/_regex.c:764:12 in bytes1_char_at",
    ])
    got = parse_reports(log)
    expected = {
        "7": ["new_position in basic_match"],
        "9": ["heap allocation in re_alloc", "new_position in basic_match"],
        "10": ["SEGV at bytes1_char_at _regex.c:764:12"],
    }
    if got != expected:
        failures.append(f"parse_reports gave {got}, expected {expected}")

    registry = {"defects": [{"origin": "new_position in basic_match", "ledger": "ledger 5"}]}
    if attribute(["new_position in basic_match"], registry) != "ledger 5":
        failures.append("a known origin is not attributed to its ledger entry")
    if attribute(["heap allocation in re_alloc", "new_position in basic_match"], registry) is not None:
        failures.append("a row with one unknown origin beside a known one is attributed as known")

    # THE COMMIT GATE, the thing that stops today's behaviour being pinned for ever: a registry
    # verified at any other upstream commit must refuse to attribute, and say what to run.
    try:
        _check_registry_current({"verifiedAtUpstreamCommit": "0" * 40})
    except SystemExit as e:
        if "--reverify" not in str(e):
            failures.append(f"the stale-registry refusal does not say what to run: {e}")
    else:
        failures.append("a registry verified at another upstream commit was trusted")
    try:
        _check_registry_current({"verifiedAtUpstreamCommit": _upstream_commit()})
    except SystemExit as e:
        failures.append(f"a registry verified at the current commit was refused: {e}")

    for failure in failures:
        print("self-check: " + failure, file=sys.stderr)
    if failures:
        return 1
    print("self-check: the MSan log parser, the attribution rule and the upstream-commit gate all hold")
    return 0


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--wave", type=Path, default=REPO_ROOT / "TestResults" / "oracle" / "wave.jsonl")
    parser.add_argument("--candidates", type=Path, default=CANDIDATES)
    parser.add_argument("--reverify", action="store_true",
                        help="re-screen the registry's reproductions against the current upstream")
    parser.add_argument("--self-check", action="store_true", help="check the log parser on known output")
    parser.add_argument("--check-registry", action="store_true",
                        help="fail fast if the registry was verified at another upstream commit")
    parser.add_argument("--if-available", action="store_true",
                        help="exit 0 with a notice, instead of failing, when Docker is not available")
    args = parser.parse_args(argv)

    if args.self_check:
        return _self_check()
    if args.check_registry:
        # No Docker needed: the gate compares two commit hashes, so CI runs it before recording
        # anything and a moved pin fails in seconds with the instruction, not after a wave.
        _check_registry_current(_load_registry())
        print("screen: the known-undefined registry was verified at the current upstream commit")
        return 0
    if not _docker_available():
        message = "screen: Docker is not available, so no row was screened under MSan"
        if args.if_available:
            print(message + " - crashed rows stay `fault`")
            return 0
        raise SystemExit(message)
    if args.reverify:
        return reverify()
    return screen_wave(args.wave, args.candidates)


if __name__ == "__main__":
    sys.exit(main())
