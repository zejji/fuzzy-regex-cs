# S54 sittings

Per-sitting notes. The slice file stays its spec.

## Sitting 1, 2026-09-16 18:04-18:37 - LOST, partially recoverable

Reached ratchet GREEN at 6278 tests and ended without committing, so the driver stashed its tracked
changes as `stash@{0}` ("slice-rescue S54-benchmark-baselines-and-edge-pins 2026-09-16 18:37:49")
and reset the tree. **Its new files were untracked and are therefore NOT in the stash and are
gone**: the benchmark classes, the trap tests, `tools/compare-benchmarks.ps1`, and two probes it
quotes numbers from (`port-benchmark-sizing.ps1`, `upstream-baseline-timings.py`). What survived is
five tracked files' worth of edits - `.editorconfig`, the deletion of `MatchingBenchmarks.cs`,
`STATUS.md`, `DECISIONS.md` and `OPTIMISATION-NOTES.md`.

Sitting 2 read the stash rather than applying it, because three of its conclusions had to be
re-decided and its measurements could not be reproduced from what it left:

- **Taken as-is**: the deletion of `MatchingBenchmarks.cs` into the new suite, and its reasoning.
- **Re-measured, not inherited**: `(a+)+b` versus `(a|a)*b`, and the lazy walk's cost. Sitting 1's
  figures (26.5 s against 153 ms at 1 MB) came from a probe that no longer exists, so by the
  slice rule they are COULD NOT RUN. Sitting 2 re-measured with a committed `sizing` mode in the
  benchmark project and recorded its own numbers (12,643 ms against 111 ms). The mechanism sitting 1
  identified was right both times.
- **Superseded**: `--inProcess`. Sitting 1 concluded BenchmarkDotNet "exposes no project-path
  option" and ran in-process with fixed iteration counts, which costs process isolation and rules
  out `--runtimes net10.0 net11.0`. Sitting 2 read `CsProjGenerator.GetProjectFilePath` and found
  the search starts at the WORKING DIRECTORY's nearest solution file, so a one-project
  `bench/FuzzyRegex.Benchmarks.slnx` plus a working directory fixes it on the default toolchain.
  Verified: 32 of 32 benchmarks ran.

## Sitting 2, 2026-09-16 from 18:40

Order of work: benchmark suite, then the upstream probe, then the trap tests, then the compare
script, then the baseline run last so nothing contends with it.

Landed before the checkpoint commit:

- `bench/FuzzyRegex.Benchmarks/` - `Corpus.cs` (shared 1 MB, 100 KB and short subjects, and the
  large pattern), `WorkloadBenchmarks.cs` (17), `ReferenceBenchmarks.cs` (12, this port beside
  interpreted and compiled `Regex` on the four workloads it can express), `Sizing.cs` (the one-shot
  stopwatch mode that sizes the rest), and `FuzzyRegex.Benchmarks.slnx`.
- `tools/probes/upstream-optimiser-traps.py` - every expected value in the trap tests that upstream
  can express, run against `regex` 2026.9.10 on 2026-09-16.
- `tests/FuzzyRegex.Tests/Gaps/Engine/OptimiserTrapsTests.cs` - 35 test cases, all green.
- `tools/compare-benchmarks.ps1`.

Two things the suite found that were nothing to do with benchmarking:

- **`Replace` takes upstream's `\1` template syntax, not .NET's `$1`** - `ReplaceFormat` is the
  one that takes `$1`. The first trap test asserted 1,048,631 characters and got 643,485, because
  `"$2 $1"` had been substituted as a five-character literal. Caught by the pin, not by the
  benchmark, which would have measured the wrong work silently.
- **The 100 KB subject exists because of the measurement.** Both the benchmark and the test that
  drain the lazy walk to the end were written against the megabyte first; at 12.6 seconds an
  operation that is a benchmark nobody re-runs and a test nobody keeps.
