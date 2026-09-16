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
  Verified: every benchmark in the suite ran - 32 of 32 at the moment that check was made, which
  was before `MatchingBenchmarks` (3) was deleted and `MatchesToEndDense` (1) added. The committed
  suite is 30, and the DECISIONS entry's "32 of 32" is the count at that earlier verification.

## Sitting 2, 2026-09-16 from 18:40

Order of work: benchmark suite, then the upstream probe, then the trap tests, then the compare
script, then the baseline run last so nothing contends with it.

Landed before the checkpoint commit:

- `bench/FuzzyRegex.Benchmarks/` - `Corpus.cs` (shared 1 MB, 100 KB and short subjects, and the
  large pattern), `WorkloadBenchmarks.cs` (18), `ReferenceBenchmarks.cs` (12, this port beside
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

### The blind review, and why the baselines were measured twice

The first baseline was taken before the blind review. The review found three defects in what the
suite MEASURED, so the numbers it had produced described code that no longer existed, and the whole
suite was re-run against the committed tree afterwards (32 minutes, 30 benchmarks). That is the same
rule the negative controls have carried since S22, applied to a benchmark: a measurement taken
partway through a slice measures an instrument the slice no longer has.

Controls on `tools/compare-benchmarks.ps1` itself, all on doctored copies of the real run
(`.scratch/doctor-artifacts.py`, gitignored; the doctoring is one multiplication). **Every one was
re-run after the second review's fixes**, because two of those fixes were in the comparison itself:

| Control | Result |
|---|---|
| unmodified report | GREEN, exit 0, every row 1.00x |
| every median x2 | RED, exit 1, all 30 rows `slower (2x)` |
| every `BytesAllocatedPerOperation` x100 | RED, exit 1, 26 rows `allocates more (100x)` - the other four allocate nothing in the baseline, so x100 is still nothing |
| every `BytesAllocatedPerOperation` set to 0 | RED, exit 1, 26 rows `allocation no longer measured (diagnoser lost?)` |
| one benchmark's `Memory` node deleted | recorded as "not measured", compared as neither better nor worse |
| one baselined benchmark deleted from the report | RED, `missing (baselined benchmark not in this run)` |
| `-Threshold 0.5` against an unmodified report | RED, exit 1 (proves the threshold is read) |
| the run log renamed as BenchmarkDotNet names a single-class run | 3 multimodal rows still found (0 before the glob fix) |
| a benchmark entry with no `Statistics` at all | RED, exit 1, 2 rows `produced no measurement at all` |
| fresh independent run of `*WorkloadBenchmarks.Fuzzy*` | **UNSTABLE - see below** |

That last row is the honest one and it is worth reading twice. The same three benchmarks, the same
binary, re-measured four times over an hour on this machine:

| attempt | FuzzyShort | FuzzyLong | FuzzyBudgetThree | verdict |
|---|---:|---:|---:|---|
| mine | 1.00x | 1.17x | 1.09x | GREEN |
| verifier 1 | - | - | - | did not run: BenchmarkDotNet's own boilerplate build timed out |
| verifier 2 | 1.00x | 1.78x | 1.89x | **RED** |
| verifier 3 | 0.57x | 1.04x | 1.10x | GREEN |

**An unchanged binary produced a RED.** The spread on this machine is 0.57x-1.89x, not the "up to
17%" an earlier draft of this file recorded from a single re-run, and the verifier is what caught
that. The conclusion is in the next section and it is not "raise the threshold": it is that the
baseline has to be re-taken on a quiet machine before the script is trusted to gate anything.

Verifier attempt 1 also found a real defect rather than a flaky number: with the run incomplete,
the script died on `The property 'Median' cannot be found on this object`. It now reports which
benchmarks produced no measurement and exits RED, which is the same rule `check-ratchet.ps1`
applies to a missing test report.

### The baselines are usable and they are not yet a tight gate

The second blind pass proved what the run's own numbers already implied: **13 of the 30 committed
rows have a minimum far below their median** (0.53-0.76), which means those iterations were being
descheduled and the medians record this machine's contention as much as the code. A pessimistic
median makes the 1.25x threshold looser than it looks - a row recorded at 0.55 min/median can get
nearly twice as slow and still compare GREEN.

Rather than leave that as prose nobody re-derives, the script now measures it: it records `minNs`
per row, lists every row below a 0.85 min/median floor in a `contended` array in the baseline, and
prints a CAUTION banner on every comparison against such a baseline. The slice's own scope provides
for this - "the orchestrator runs the suite detached overnight after the slice lands if the
session's own run was contended; the numbers the slice commits are labelled with how they were
taken" - so **the numbers are labelled, and the quiet re-run is the orchestrator's**. Re-taking it
is one command and the file it overwrites is self-describing.

**Until that re-run happens, read a RED from `compare-benchmarks.ps1` on this machine as "measure
it again", not as "the change regressed".** The verifier demonstrated a RED on an unchanged binary.
The script is correct; the baseline under it is noisy, and the two failure modes look identical
from the outside, which is exactly why the `contended` array and the CAUTION banner exist.

One measurement limit found while checking the controls and not worth engineering around: at about
a kilobyte per operation and only a few operations per iteration, BenchmarkDotNet's
bytes-per-operation figure quantises, so the allocation ratio for the small-allocation benchmarks
carries some noise of its own (`FuzzyLong` re-measured at 1.17x on unchanged code). It is well
inside the threshold; it would matter if anyone tightened the allocation gate below about 1.2x.
