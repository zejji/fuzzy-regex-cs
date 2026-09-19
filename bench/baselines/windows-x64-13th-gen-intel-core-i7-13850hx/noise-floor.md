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
  -ArtifactsPath artifacts/bench/2026-09-19-S58-noise-<N> -UpdateBaseline `
  -BaselinePath bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-<N>.json

# and then the comparison itself, from the committed baselines alone:
pwsh -File tools/probes/compare-two-baselines.ps1 `
  -BaselineA bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json `
  -BaselineB bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-<N>.json
```

A is the first of the pair. Its partner took several attempts, and **every discarded attempt is
committed beside it** - `-noise-B-discarded.json`, `-noise-D-discarded.json` and the two
`-load.log` files - because a run that had to be thrown away is evidence about the method. The two
sections below are what they taught. The letter of the partner that finally counted is in "The
floor" at the end.

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
- Runs B, C and D: discarded, each for a named and measured cause - see the two sections below.

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
| `WorkloadBenchmarks.CaseFoldedScan` and every row after it (17 rows) | 0.90-0.99 | 0.83-0.99 | 0.92-1.03 |

Reproduce it from the committed baselines with `tools/probes/compare-two-baselines.ps1`.

Read that column downwards: the first class is clean, then everything from `LiteralBcl` to
`WorkloadBenchmarks.ClassScan` is hurt, and then it stops - dead - and the last seventeen rows are
inside 1.03x. Their contention signal is nearly clean too, with one exception worth stating rather
than rounding away: `MatchesFirstTwo` reads 0.83 in B, below the 0.85 line, while its ratio is
1.01. That is not thermal throttling (which would get worse
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
pass per interval, under 0.2 s of CPU a sample, which at 15-second spacing is 1.3% of one core -
the script's own 0.7% figure is quoted for its default 30-second interval.

## Run D, which was discarded for the same cause with a different culprit

D ran 11:16:18 - 11:51 and came back with **6 of 49 rows below 0.85** and a widest ratio of
**2.06x** (`GroupCountStateBenchmarks.StateByGroupCount(Groups: 32)`). Read in run order it is B's
shape again, and this time the damage is at the *front*:

| Benchmark (in run order) | A min/med | D min/med | D/A |
|---|---:|---:|---:|
| `GroupCountStateBenchmarks.StateByGroupCount(1..32)` | 0.97-0.99 | **0.54-0.97** | **1.10-2.06** |
| `ReferenceBenchmarks.LiteralPort` | 0.96 | **0.66** | **1.55** |
| `ReferenceBenchmarks.LiteralBcl` | 0.98 | 0.88 | **1.39** |
| `ReferenceBenchmarks.LiteralBclCompiled` | 0.94 | **0.60** | **1.51** |
| `ReferenceBenchmarks.ClassScanPort` | 0.97 | **0.77** | **1.58** |
| `ReferenceBenchmarks.ClassScanBcl` | 0.98 | **0.70** | **1.45** |
| `ReferenceBenchmarks.ClassScanBclCompiled` and every row after it (38 rows) | 0.90-0.99 | 0.89-0.99 | 0.90-1.07 |

Eleven damaged rows, then it stops dead and the remaining 38 are inside 1.07x. An episode at the
start of the run rather than the middle of it.

**The culprit is named in D's own load log, and it was the measuring session.** The sampler this
method gained after B recorded, at 11:21:19 and 11:21:34, `python(21184) 54.5s` and `64.1s` in
15-second windows - **3.6 and 4.3 cores** - from the Headroom context proxy. That is the same PID
run C was thrown away for. The Claude session was orienting: reading state files, sampling the
machine and enumerating processes, all of it through the proxy.

**Why it did that while a run was in flight** is the part worth keeping, because it is a process
fault and not a carelessness fault. The sitting that launched D was killed a minute later, at
11:17, without writing down that it had launched anything: `STATE.md` said "take the one missing
noise run", which reads as *start one*, and the next sitting started by doing exactly what a
sitting should do - orient, check the machine is quiet - which was itself the contention. The
sampler log's first four lines (`pwsh(30380)`, `msedge`, `python`) are that dead sitting's own
launch traffic. So:

- **A detached run must be recorded in `STATE.md` before it is launched, with its PID, its log path
  and its expected finish time**, because the session that knows about it can be killed at any
  moment and the next one inherits nothing else. D was found only because the orchestrator said so.
- **Orientation is not free.** "Check the machine is quiet" costs 3.6 cores through the proxy, so on
  this machine the check and the thing it checks for are the same event. Read the state files, and
  if they say a run is in flight, stop reading and block.

Both discarded runs are kept: `2026-09-19-S58-noise-D-discarded.json` and its
`-discarded-load.log`. Between them B and D make the point twice over that the *detector* works -
BDN's min/median flagged both without being told anything - and that what fails is always the
watchman.

## Taking a run so it counts

Every line here was paid for on 2026-09-19.

1. **Nothing else may build or test anywhere on this machine** - including another git worktree.
   A worktree is a separate directory but not a separate CPU.
2. **The measuring session must be silent while the run is in flight.** No file writing, no
   searching, no subagents: its own LLM traffic costs 2.6 cores through the Headroom proxy per
   request. Block on one long wait per poll; do the writing before the run or after it, never
   during. Pausing Stryker is necessary and not sufficient.
3. **Run `tools/probes/sample-machine-load.ps1` beside the run** and read its log before trusting
   the numbers. Judge it on **how much CPU a non-benchmark process took**, not on whether one is
   named at all: the kept run E's log names `msedge`, `msedgewebview2`, `python` and
   `VBCSCompiler`, and its busiest such sample is 4.5 s in 15 - 0.3 of a core. Every discarded run
   is an order of magnitude past that (C: `python` 2.6 cores, D: `python` 3.6 and 4.3 cores), so
   the two populations do not overlap and nothing here fixes a line between them. Treat 0.3 of a
   core as demonstrably harmless, 2.6 as demonstrably fatal, and anything in between as unproven -
   re-take the run rather than argue about it. `dotnet` at 14.5 s in 15 is the benchmark working,
   and `csc` bursts to ten cores are BenchmarkDotNet building the next benchmark class, which is
   expected and sequential with the measurement.
4. **Read the run's own contention line.** Any row with min/median below 0.85 makes the whole run
   suspect - the floor is a maximum over rows, so one contended row sets it.
5. Back-to-back runs are fine on this machine: A and the discarded B agreed within 0.92-1.03x on
   the seventeen rows the episode missed, so there is no evidence of thermal drift between
   consecutive runs.
6. **Write the run into `STATE.md` before launching it** - PID, log path, expected finish - and
   launch it detached. A session can be killed mid-run; the next one must be able to find the run
   from committed files alone, and must block rather than orient. Run D was lost to this.

## The floor

| | Floor | Set by | Observed span |
|---|---:|---|---|
| **Time** | **1.13** | `WorkloadBenchmarks.ReverseFailedScan`, ratio **0.88916** (1/0.88916 = 1.1247) | 0.88916 - 1.0768 |
| **Allocation** | **1.0001** | `WorkloadBenchmarks.SplitLong`, 11,060,299 B to 11,059,992 B | 0.99997 - 1.00001 |

Measured 2026-09-19 from **run A** (08:22-08:56) and **run E** (11:56:31-12:29:55), `--job medium`,
49 benchmarks, an unchanged tree - `src/` last touched at `dfa8767` the previous evening and the
benchmark sources at 08:00-08:02, both before A started. Both baselines are committed here, so the
comparison re-runs from committed files alone:

```powershell
pwsh -File tools/probes/compare-two-baselines.ps1 `
  -BaselineA bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json `
  -BaselineB bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-E.json
```

E was the fourth attempt at A's partner and the first to pass its own gate: **0 of 49 rows below
0.85**, against 19 for B and 6 for D. Its sampler log
(`2026-09-19-S58-noise-E-load.log`) names no process other than the benchmark itself above 0.3 of a
core for a single sample; `dotnet` reaches 14.5 s of CPU in a 15 s window, which is the run working.

**The time floor is set by the "improvement" side, and that is deliberate.** The widest regression
direction was 1.0768 (`StateByGroupCount(Groups: 32)`); the widest movement in either direction was
`ReverseFailedScan` running 11% *faster* in E than in A with no code between them. The band is
two-sided because an unexplained improvement inside the floor is the same measurement artefact as a
regression inside it, so the floor is the wider side: 1.1247, rounded up to 1.13.

**How to tighten it, for a later slice that wants a sharper gate.** That row is the one to suspect
rather than the machine: `ReverseFailedScan` was one of run A's three shakiest rows on the
contention signal (min/median 0.9148, behind `MatchesToEndDense` at 0.9008 and `MatchesFirstTwo` at
0.9102; 0.99 in E) and one of A's four multimodal rows. A multimodal benchmark's median jumps
between its two modes, which is real variation the floor has to cover - but it is variation in one
row, not a property of the suite. Drop that row and the floor is 1.08. Re-measuring A would very
likely buy back those five points; re-measuring it *after* stabilising the benchmark would be
better still.

**Allocation is nearly deterministic here, and that is the useful half of the result.** Forty-five
rows allocate; the largest disagreement between two runs was 307 bytes in 11.06 MB, and 38 rows
matched to the byte. So an allocation change of almost any size is *measurable* here, which matters,
because allocation is Phase 7's first optimisation lever and the lazy-walk and span decisions both
turn on allocated bytes rather than nanoseconds. The 1.0001 floor exists only to absorb that 2.8e-5
wobble.

**Measurable is not the same as gated, and at these numbers it is not gated.** A floor in
`tools/compare-benchmarks.ps1` can only ever *excuse* a ratio, never fail one: what fails a run is
`-Threshold`, which defaults to 1.25 and governs both axes, and a ratio over `-Threshold` is
forgiven if it is inside the floor. So a floor above `-Threshold` does change the verdict - a row
doctored to 1.20x on both axes, run at `-Threshold 1.05`, is RED with the shipped floors and GREEN
with `-NoiseFloor 1.25 -AllocationNoiseFloor 1.25` (both, because both axes moved) - but a floor
*below* `-Threshold` forgives nothing that `-Threshold` would have failed. Both
of these floors are below 1.25, so they move no verdict: a benchmark allocating 1.20x its baseline
reports GREEN, byte-level determinism or not.

Pinned as a test, `tools/tests/CompareBenchmarks.Tests.ps1`, "does NOT fail a run for an allocation
change between the allocation floor and -Threshold", which is the reproduction that needs nothing
but committed files. Confirmed on real data too, with `tools/probes/gate-scale-one-row.py`: scaling
`WorkloadBenchmarks.SplitLong` to 1.20x on both axes inside a copy of run A's artifacts and
comparing that against run A's committed baseline printed `1.20x 1.20x` on the row and `GREEN`
overall. (That one needs the artifacts folder, which is gitignored.) That
is pre-existing behaviour, not something these floors changed, and picking the allocation threshold
is a gate decision rather than a measurement one, so it is left to **S63** (see its scope item 7).
What this measurement establishes is the evidence S63 needs: an allocation threshold anywhere above
about 1.0001 is justified by the machine, not forced by it.

**Still not done:** scope item 1's second pair ("repeat the pair once after a reboot"). Rebooting
this machine would kill the owner's driver, Stryker and night-shift processes, which is the owner's
call and not a slice's. It stays on the slice.

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
