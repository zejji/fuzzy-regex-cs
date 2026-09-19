# The noise floor of this machine

**Machine:** `windows-x64-13th-gen-intel-core-i7-13850hx` (the id `tools/compare-benchmarks.ps1`
derives from what BenchmarkDotNet reports, so this folder and the numbers in it cannot describe
different hardware).
**Taken:** 2026-09-19, S58, at commit `69c4dc6` plus S58's benchmark additions (no `src/` change).
**Job:** `--job medium` = `MediumRun(IterationCount=15, LaunchCount=2, WarmupCount=10)`, read off the
run log rather than from documentation.
**BenchmarkDotNet:** 0.15.8. **Runtime:** .NET 10, Release.

## What a noise floor is, and why this file exists

`tools/compare-benchmarks.ps1` calls a benchmark regressed when it is more than `-Threshold` times
the baseline. That is only meaningful if the machine can tell a real change of that size from its own
variation. So: **two runs of an UNCHANGED tree**, A recorded as a baseline and B compared against it.
Every ratio in that comparison is the machine's noise and nothing else, because there is no code
difference for it to be measuring. The largest per-workload ratio is the floor.

Per workload, never an average. The gate is per workload by design, and a mean would let one badly
regressed case hide behind twenty unchanged ones - so the floor has to be sized the same way.

## How these two runs were taken

```powershell
pwsh -File tools/compare-benchmarks.ps1 -Job medium -Filter '*' `
  -ArtifactsPath artifacts/bench/2026-09-19-S58-noise-A -UpdateBaseline `
  -BaselinePath bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json

pwsh -File tools/compare-benchmarks.ps1 -Job medium -Filter '*' `
  -ArtifactsPath artifacts/bench/2026-09-19-S58-noise-C -UpdateBaseline `
  -BaselinePath bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-C.json

# and then the comparison itself, from the committed files:
pwsh -File tools/compare-benchmarks.ps1 -UseExisting -Job medium `
  -ArtifactsPath artifacts/bench/2026-09-19-S58-noise-C `
  -BaselinePath bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json
```

The second run is called C because the first attempt at it, B, was thrown away. Its baseline is
committed beside the two that count, as `2026-09-19-S58-noise-B-discarded.json`, because a run that
had to be discarded is evidence about the method - the next section is what it taught.

Both JSON files are committed beside this one, so the comparison is re-runnable from committed
evidence; the raw BenchmarkDotNet output under `artifacts/` is not, because `artifacts/` is
gitignored and regenerable.

## The state of the machine while they ran

Recorded rather than claimed, because "the machine was quiet" is the assumption every number here
rests on.

- **No driver, no Stryker, no test run, no build.** Nothing was started in either window except the
  run itself.
- Idle leftovers from earlier sessions, left running under the owner's do-not-kill rule and using no
  CPU: `python -m http.server` on ports 8090/8092/8137, a Vite dev server (PID 34120), and a **hung
  `VBCSCompiler.exe` (PID 16364, ~700 MB, 0% CPU)**. The compiler daemon is hung, not busy - it
  blocks builds that try to use it and consumes nothing while idle, and BenchmarkDotNet passes
  `/p:UseSharedCompilation=false` to its own builds, so it does not touch these numbers.
- Run A (08:22:24 - 08:56): 49 benchmarks, 33 m 38 s, **0** rows with min/median below 0.85 (the
  contention signal), 4 with a multimodality warning: `ReferenceBenchmarks.BacktrackingBcl`,
  `SpanOverloadBenchmarks.CountSpanMegabyte`, `WorkloadBenchmarks.ReverseFailedScan`,
  `WorkloadBenchmarks.SplitLong`.
- Run C (09:44 - 10:23): see below.

## Run B, which was discarded, and what it taught

B ran 08:56:27 - 09:35, immediately after A, on what every monitor available said was an idle
machine. It came back with **19 of 49 rows below 0.85** and medians up to **2.39x** A's. A floor
taken from it would have been a floor of about 2.4x - wide enough to pass any regression this
project could plausibly introduce, which is the failure mode this whole document exists to prevent.

The shape of the damage is the interesting part. Each row is min/median within its own run, so a
number well under 1 means that benchmark's own iterations disagreed:

| Benchmark (in run order) | A min/med | B min/med | B/A |
|---|---:|---:|---:|
| `GroupCountStateBenchmarks.StateByGroupCount(1..32)` | 0.97-0.99 | 0.94-0.98 | 1.02-1.08 |
| `ReferenceBenchmarks.LiteralBcl` | 0.98 | **0.79** | **1.38** |
| `ReferenceBenchmarks.ReplaceBcl` | 0.97 | **0.76** | **1.90** |
| `ReferenceBenchmarks.BacktrackingBclCompiled` | 0.98 | **0.52** | **1.87** |
| `SpanOverloadBenchmarks.StringShort` | 0.99 | **0.58** | **1.77** |
| `SpanOverloadBenchmarks.SpanMegabyte` | 0.95 | 0.92 | **2.39** |
| `SubjectLengthStateBenchmarks.StateBySubjectLength(16384)` | 0.99 | **0.77** | **2.10** |
| `WorkloadBenchmarks.LiteralMatch` | 0.98 | **0.76** | **1.48** |
| `WorkloadBenchmarks.ClassScan` | 0.98 | **0.71** | **1.43** |
| `WorkloadBenchmarks.CaseFoldedScan` and every row after it (17 rows) | 0.90-0.99 | 0.93-0.99 | 0.92-1.03 |

Reproduce it from the committed baselines with `tools/probes/compare-two-baselines.ps1`.

Read that column downwards: the first class is clean, then everything from `LiteralBcl` to
`WorkloadBenchmarks.ClassScan` is hurt, and then it stops - dead - and the last seventeen rows are
inside 1.03x with a clean contention signal. That is not thermal throttling (which would get worse
as the run went on, not better) and it is not a slow machine. It is an **episode**: something had
the machine for roughly 25 minutes in the middle of the run and then stopped.

**What was watching, and what it missed.** `.scratch/load-guard.log` sampled through that whole
window and reported `avg=1% peak=7%` five-minute summaries throughout. It was not lying: this is a
20-core machine, so one core taken for ninety seconds is under 1% of a five-minute average across
all of them. A mean over cores and over minutes cannot see the thing that ruins a benchmark, which
is a competitor for *one* core for *seconds*.

**The cause, confirmed rather than inferred.** The orchestrator reported at 09:55 that a review fix
pass in the demo worktree had run npm builds, vitest, Pester, a TUnit Debug run and
csharpier/ReSharper inspections **between about 08:47 and 09:50** - which brackets B almost exactly
(08:56-09:35) and leaves A (08:22-08:56) all but untouched. That is the episode, and its end at
09:50 is why B's last seventeen rows are clean.

**The method did detect it**, which is the part worth keeping: BenchmarkDotNet's own min/median
signal flagged 19 of 49 rows without knowing anything about the worktree, and
`tools/compare-benchmarks.ps1` refused to be read as a gate ("THE MACHINE WAS NOT QUIET ... re-take
this baseline"). What failed was not the detector but the *watchman*: the load guard's five-minute
averages said 1%.

**And there is a second competitor, found by the new sampler, that nobody had thought of: this
project's own tooling.** During the replacement run the sampler recorded
`python(21184) 39.1s/15s` - 2.6 cores - from `headroom.cli proxy --port 8787`, the context proxy
every Claude session here routes through. That log is committed as
`2026-09-19-S58-noise-C-discarded-load.log`; the `dotnet` lines in it are the benchmark itself and
the `csc(17108) 151.6s/15s` line is BenchmarkDotNet building the next benchmark class. Every request the *measuring session itself* sends
compresses context on this machine. So a session that writes documents while its own benchmark run
is in flight is measuring its own typing. The rule that follows is in the next section, and it is
not optional.

**The method changed because of it.** `tools/probes/sample-machine-load.ps1` now runs alongside a
noise run, samples every 15 seconds, and logs the per-process CPU *delta* with the process named. A
run whose sampler log is quiet is a run that can say so with evidence; a run whose sampler names a
competitor is a run to throw away, knowing why. Its cost is measured, not assumed: one `Get-Process`
pass per interval, under 0.2 s of CPU a sample, which is below 0.7% of one core at 15-second
spacing.

## Taking a run so it counts

Every line here was paid for on 2026-09-19.

1. **Nothing else may build or test anywhere on this machine** - including another git worktree.
   A worktree is a separate directory but not a separate CPU.
2. **The measuring session must be silent while the run is in flight.** No file writing, no
   searching, no subagents: its own LLM traffic costs 2.6 cores through the Headroom proxy per
   request. Block on one long wait per poll; do the writing before the run or after it, never
   during. Pausing Stryker is necessary and not sufficient.
3. **Run `tools/probes/sample-machine-load.ps1` beside the run** and read its log before trusting
   the numbers. A run whose log names any process other than `dotnet`, `csc`, `MSBuild` and the
   sampler is a run to discard. `csc` bursts to ten cores are BenchmarkDotNet building the next
   benchmark class, which is expected and sequential with the measurement.
4. **Read the run's own contention line.** Any row with min/median below 0.85 makes the whole run
   suspect - the floor is a maximum over rows, so one contended row sets it.
5. Back-to-back runs are fine on this machine: A and the discarded B agreed to 1.02-1.08x on every
   row the episode missed, so there is no evidence of thermal drift between consecutive runs.

## The floor

**NOT YET MEASURED.** Run A is taken, sound and committed. Its partner is not: B was contaminated
by the worktree episode above, and the replacement run C, started 09:44, had its first benchmark
class inside the same episode's tail (which ran to 09:50) and was stopped rather than finished, so
there is no C baseline. What the next sitting does, and nothing else, before it touches anything
else on this slice:

- Take **one** fresh run against the committed A, with the five rules above obeyed and the sampler
  log committed beside it. One run is what is missing; A does not need re-taking, and the tree is
  unchanged since it.
- Then set `-NoiseFloor` and `-AllocationNoiseFloor` in `tools/compare-benchmarks.ps1` from the
  largest per-workload time ratio and the largest allocation ratio that comparison prints, and
  replace this section with those two numbers and the row each came from.

Until that exists, the parameters stay at `1.0` - no floor at all - which is the honest default:
it never excuses a regression, it only over-reports.

Scope item 1's second pair ("repeat the pair once after a reboot") is **not done and not scheduled
here**: rebooting this machine would kill the owner's driver, Stryker and night-shift processes,
which is the owner's call and not a slice's. It stays on the slice.

## Using it

`tools/compare-benchmarks.ps1` defaults `-NoiseFloor` and `-AllocationNoiseFloor` to the numbers
above. A ratio inside the band reads `same` and is never a regression - and the band is two-sided,
because an unexplained improvement inside the floor is the same measurement artefact as a regression
inside it.

**These are not knobs.** A floor raised to make a red run green has stopped measuring the machine
and started hiding the change. Re-measure instead - two runs, unchanged tree, same job - and update
this file with the new date and the new numbers.

**The floor is specific to this machine, this job and this benchmark set.** A different job has a
different spread before any code changes (that is why the baseline records its job and the script
warns on a mismatch). A benchmark added after these runs has no floor of its own; the number here is
the suite's worst case, which is the conservative thing to apply to it, but a new workload with a
much wider spread would deserve its own measurement.
