"""What happened to the mutants inside each Stryker chunk's own windows?

A chunk's `mutation-report.json` counts every mutant in every file it touched, including the ones
outside that chunk's `File.cs{start..end}` windows, which another chunk owns. Over a chunked queue
the raw totals over-count several-fold. This maps each mutant's line and column to a character
offset in the report's own embedded source, keeps only the mutants inside that chunk's windows,
and reports the status totals plus the enclosing method of everything that was not simply killed.

    python tools/stryker-inwindow-summary.py                # every chunk in the queue
    python tools/stryker-inwindow-summary.py engine-rand    # chunks whose name starts with this

Produces the tables in `docs/plan/mutation/2026-09-20-engine.md`.
"""

import json
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = os.path.join("TestResults", "stryker")
PLACEHOLDER = "File ignored by mutate filter"

# The `?` in the character class is load-bearing: without it `internal static string?[] Split(`
# is not a declaration and its mutants are charged to the method above it (S56, blind review).
# The optional group is what lets a constructor - `internal CharacterIndex(MatchState state)`,
# which has no return type - be recognised as a declaration too.
# Known limitation: a mutant on a field initialiser has no enclosing method, and is charged to
# whichever method precedes it. Two such mutants exist in the engine reports, both in
# `MatchState.cs`; neither is Killed, Survived, Timeout or RuntimeError.
SIGNATURE = re.compile(
    r"^\s*(?:internal|private|public|protected)\s(?:[\w\s<>,\[\]\.\?]*?\s)?(\w+)\s*(?:<[^>]*>)?\("
)


def enclosing_method(lines, line_number):
    for i in range(line_number - 1, -1, -1):
        hit = SIGNATURE.match(lines[i])
        if hit:
            return hit.group(1)
    return "?"


def main(prefix):
    with open(os.path.join("tools", "stryker-queue.json"), encoding="utf-8") as fh:
        queue = {e["chunk"]: e["mutate"] for e in json.load(fh) if "chunk" in e}

    totals = Counter()
    methods = defaultdict(Counter)
    ignored_reasons = Counter()
    missing = []

    for chunk, windows in queue.items():
        if not chunk.startswith(prefix):
            continue
        report = os.path.join(ROOT, chunk, "reports", "mutation-report.json")
        if not os.path.exists(report):
            missing.append(chunk)
            continue

        spans = defaultdict(list)
        for window in windows:
            name, rng = window.split("{")
            lo, hi = rng.rstrip("}").split("..")
            spans[name.split("/")[-1]].append((int(lo), int(hi)))

        with open(report, encoding="utf-8") as fh:
            files = json.load(fh)["files"]
        for fname, info in files.items():
            src = info.get("source", "")
            base = fname.replace("\\", "/").split("/")[-1]
            if src == PLACEHOLDER or base not in spans:
                continue
            lines = src.split("\n")
            starts, pos = [], 0
            for line in lines:
                starts.append(pos)
                pos += len(line) + 1

            for mutant in info["mutants"]:
                loc = mutant["location"]["start"]
                offset = starts[loc["line"] - 1] + loc["column"] - 1
                end = mutant["location"]["end"]
                end_offset = starts[end["line"] - 1] + end["column"] - 1
                # Stryker takes a mutant only when its whole span is inside the window, not just
                # its first character. Testing the start alone counts 165 engine mutants that
                # Stryker itself rejected with "Removed by mutate filter" (S56, second review).
                if not any(lo <= offset and end_offset <= hi for lo, hi in spans[base]):
                    continue
                status = mutant["status"]
                totals[status] += 1
                if status != "Killed":
                    methods[status][f"{base}::{enclosing_method(lines, loc['line'])}"] += 1
                if status == "Ignored":
                    ignored_reasons[(mutant.get("statusReason") or "")[:60]] += 1

    if missing:
        print(f"no report for {len(missing)} chunk(s): {', '.join(sorted(missing))}\n")

    print(f"mutants inside the '{prefix}' chunks' own windows:")
    for status, n in sorted(totals.items(), key=lambda kv: -kv[1]):
        print(f"  {status:14s} {n}")
    tested = sum(totals[s] for s in ("Killed", "Timeout", "RuntimeError", "Survived"))
    print(f"  tested = {tested}, survivors = {totals['Survived']}")

    print("\nIgnored reasons:")
    for reason, n in ignored_reasons.most_common():
        print(f"  {n:6d}  {reason!r}")

    for status in ("Survived", "NoCoverage", "Timeout", "RuntimeError", "CompileError"):
        if not methods[status]:
            continue
        print(f"\n{status} by enclosing method:")
        for name, n in methods[status].most_common(25):
            print(f"  {n:4d}  {name}")
        print(f"  {len(methods[status])} distinct method(s), {sum(methods[status].values())} mutants")


if __name__ == "__main__":
    os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    main(sys.argv[1] if len(sys.argv) > 1 else "")
