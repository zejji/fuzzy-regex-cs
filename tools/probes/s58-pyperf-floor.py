"""S58: the Python side's own noise floor, measured the way pyperf documents.

The .NET floor (`bench/baselines/<machine-id>/noise-floor.md`) says how much two identical
BenchmarkDotNet runs differ on this machine. The v1.0 gate compares our median against upstream
`regex`'s median, so the Python side needs the same number, measured the same way: run this
workload twice with nothing else happening, then `python -m pyperf compare_to run1.json run2.json`.
A difference inside that band is the machine, not the library.

The workloads mirror `bench/FuzzyRegex.Benchmarks/WorkloadBenchmarks.cs` on the same corpus shapes
as `Corpus.cs`, so the two floors are about comparable work. Workloads with no Python equivalent
(the span overloads) are absent by design.

Usage:
    python tools/probes/s58-pyperf-floor.py -o run1.json
    python tools/probes/s58-pyperf-floor.py -o run2.json
    python -m pyperf compare_to run1.json run2.json --table
    python -m pyperf check run1.json
"""

import os
import site
import sys

import pyperf
import regex

# pyperf measures in spawned worker processes, and on this machine `pyperf` and `regex` are a
# per-user install (`%APPDATA%\Roaming\Python\Python314\site-packages`) that the workers do not
# see: the worker dies with `ModuleNotFoundError: No module named 'pyperf'` and the run fails with
# `RuntimeError: python.exe failed with exit code 1`. pyperf passes only a whitelist of environment
# variables to a worker, so PYTHONPATH has to be both set and named on `--inherit-environ`.
# Measured 2026-09-19; harmless where the packages are installed system-wide.
_USER_SITE = site.getusersitepackages()
if _USER_SITE and os.path.isdir(_USER_SITE):
    _existing = os.environ.get("PYTHONPATH", "")
    if _USER_SITE not in _existing.split(os.pathsep):
        os.environ["PYTHONPATH"] = (
            _USER_SITE if not _existing else _USER_SITE + os.pathsep + _existing
        )
    if not any(arg.startswith("--inherit-environ") for arg in sys.argv[1:]):
        sys.argv.append("--inherit-environ=PYTHONPATH")

# Corpus.cs:21 - the same filler sentence, so the subjects are the same shape on both sides.
SENTENCE = "the quick brown fox jumps over the lazy dog "
MEGABYTE = 1024 * 1024


def pad(tail, size=MEGABYTE):
    """Build a subject of at least `size` filler characters plus a distinguishing tail."""
    parts = []
    length = 0
    while length < size:
        parts.append(SENTENCE)
        length += len(SENTENCE)
    parts.append(tail)
    return "".join(parts)


LONG = pad("a needle in a haystack.")  # Corpus.Long
LONG_NO_MATCH = pad("a pin in a haystack.")  # Corpus.LongNoMatch
FUZZY = SENTENCE + "and finds a haystakc."  # Corpus.Fuzzy

# Compiled once, as the C# side holds its FuzzyRegex instances in static fields: the gate measures
# matching, not compilation. CompileLargePattern is the one workload that measures compilation and
# it is deliberately not mirrored here.
LITERAL = regex.compile("needle")
CLASS_HEAVY = regex.compile("[a-z]{3}[^a-z]")
WORDS = regex.compile(r"\w+")
FUZZY_ONE = regex.compile("(?:needle){e<=1}")
BEST = regex.compile("(?b)(?:haystack){e<=3}")


def literal_match():
    """WorkloadBenchmarks.LiteralMatch: one literal at the end of a megabyte, so a full scan."""
    return LITERAL.search(LONG).start()


def class_scan():
    """WorkloadBenchmarks.ClassScan: two classes and a counted repeat, matching densely."""
    return len(CLASS_HEAVY.findall(LONG))


def words_findall():
    """WorkloadBenchmarks.MatchesToEnd: many short matches, walked to the end."""
    return len(WORDS.findall(LONG))


def fuzzy_long():
    """WorkloadBenchmarks.FuzzyLong: one error allowed, the match at the very end."""
    return FUZZY_ONE.search(LONG).start()


def fuzzy_no_match_long():
    """WorkloadBenchmarks.FuzzyNoMatchLong: the 2026-09-18 addition - a fuzzy scan that fails."""
    return FUZZY_ONE.search(LONG_NO_MATCH) is None


def best_match():
    """WorkloadBenchmarks.BestMatch: BESTMATCH explores the whole space, the likeliest regression."""
    return BEST.search(FUZZY).span()


if __name__ == "__main__":
    runner = pyperf.Runner()
    runner.metadata["description"] = "S58 Python-side noise floor, mirroring WorkloadBenchmarks"
    runner.bench_func("literal_match", literal_match)
    runner.bench_func("class_scan", class_scan)
    runner.bench_func("words_findall", words_findall)
    runner.bench_func("fuzzy_long", fuzzy_long)
    runner.bench_func("fuzzy_no_match_long", fuzzy_no_match_long)
    runner.bench_func("best_match", best_match)
