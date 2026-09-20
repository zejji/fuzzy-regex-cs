"""S56: apply one of the queue's Timeout / RuntimeError mutants and see what the engine does.

The Stryker queue reports a mutant as Timeout (the suite hung) or RuntimeError (the test host
crashed) without saying why. This applies the mutation to the working tree, runs
tools/probes/s56-mutant-behaviour.cs against it, restores the file, and prints the verdict.

Usage (from the repository root, PowerShell or bash):

    python tools/probes/s56-mutant-behaviour.py            # all three
    python tools/probes/s56-mutant-behaviour.py optimiser  # one

Line numbers below are HEAD's. Where HEAD has drifted from the revision the queue mutated, the
report's own line number is given as "report line" - see docs/plan/mutation/2026-09-20-engine.md.
Files are read and written as bytes: this repository is LF-only and a text-mode write would turn
the file CRLF and fail the build.
"""

import os
import subprocess
import sys

MUTANTS = {
    # name: (file, HEAD line, expected text on that line, mutated text, probe case, report)
    "groupcall": (
        "src/FuzzyRegex/Engine/NodeCompiler.cs",
        1105,
        "        args.Code += 2;",
        "        args.Code -= 2;",
        "groupcall",
        "engine-rand-41, RuntimeError, report line 1079 (BuildGroupCall)",
    ),
    "optimiser": (
        "src/FuzzyRegex/Engine/Optimiser.cs",
        186,
        "                        node.Status |= NodeStatus.VisitedAg | NodeStatus.Max3(result, bodyResult, tailResult);",
        "                        node.Status &= NodeStatus.VisitedAg | NodeStatus.Max3(result, bodyResult, tailResult);",
        "optimiser",
        "engine-rand-51, Timeout, report line 186 (GreedyRepeat arm)",
    ),
    "string": (
        "src/FuzzyRegex/Engine/NodeCompiler.cs",
        1632,
        "        args.Code += (int)(3 + length);",
        "        args.Code -= (int)(3 + length);",
        "string",
        "engine-rand-14, RuntimeError, report line 1596 (BuildString)",
    ),
    "bytestack": (
        "src/FuzzyRegex/Engine/ByteStack.cs",
        113,
        "                newCapacity *= 2;",
        "                newCapacity /= 2;",
        "bytestack",
        "engine-rand-44, Timeout, report line 113 (PushBlock growth loop)",
    ),
}

DEADLINE = "60"


def read_lines(path):
    with open(path, "rb") as fh:
        return fh.read().split(b"\n")


def write_lines(path, lines):
    with open(path, "wb") as fh:
        fh.write(b"\n".join(lines))


def run(name):
    path, lineno, expected, mutated, case, report = MUTANTS[name]
    lines = read_lines(path)
    actual = lines[lineno - 1].decode("utf-8")
    if actual != expected:
        print(f"{name}: SKIPPED - {path}:{lineno} reads {actual!r}, not {expected!r}")
        return
    original = list(lines)
    lines[lineno - 1] = mutated.encode("utf-8")
    write_lines(path, lines)
    try:
        print(f"\n=== {name} ===")
        print(f"    {report}")
        print(f"    {path}:{lineno}  {expected.strip()}  ->  {mutated.strip()}")
        result = subprocess.run(
            ["dotnet", "run", "tools/probes/s56-mutant-behaviour.cs", "--", case, DEADLINE],
            capture_output=True, text=True, check=False,
        )
        out = (result.stdout + result.stderr).strip().split("\n")
        # The first line carries the verdict (an exception's own line, or the answer); a stack
        # trace after it is noise here.
        for line in out[:2] + (["    ..."] if len(out) > 3 else []) + out[-1:]:
            print(f"    {line[:160]}")
        verdict = {0: "COMPLETED (mutant survives this probe)", 3: "THREW", 124: "HANG"}
        print(f"    exit {result.returncode}: {verdict.get(result.returncode, 'other')}")
    finally:
        write_lines(path, original)
        print(f"    restored {path}")


if __name__ == "__main__":
    os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    names = sys.argv[1:] or list(MUTANTS)
    for chosen in names:
        run(chosen)
