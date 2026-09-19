# How to get numbers Phase 7 can be trusted with

Research for Phase 7, gathered 2026-09-16. Every external claim carries the URL it came from and
the date it was fetched. Claims marked **UNVERIFIED** could not be confirmed from a primary source
and must be probed before anything is built on them.

The owner's brief is the frame: *that the first step, before making any changes, is to consider how to properly benchmark and profile*, and *that all changes need to be accurately benchmarked or they will not be trusted*. BenchmarkDotNet is not a silver bullet - it controls the measurement, it
does not control the machine, and it cannot tell you whether the two things you compared were the
same question.

---

## 0. The three numbers, and what each is for

| Number | Produced by | What it decides |
|---|---|---|
| Our median, per workload | BenchmarkDotNet | Did this change make it faster? |
| Allocated bytes/op, per workload | BenchmarkDotNet `MemoryDiagnoser` | Did this change allocate less? (the second half of the owner's goal) |
| Python `regex` median, per workload | pyperf, same machine, same session | Are we at parity? (the v1.0 gate, `.claude/skills/benchmark/SKILL.md`) |

A fourth, `System.Text.RegularExpressions`, is not in the gate but is the number a .NET user will
actually compare against (spec section 11).

---

## 1. BenchmarkDotNet: the settings that matter

### 1.1 Let BDN choose the iteration counts; do not hand-tune them

BDN's own guidance: *"Usually, you shouldn't specify such characteristics like LaunchCount,
WarmupCount, IterationCount, or IterationTime because BenchmarkDotNet has a smart algorithm to
choose these values automatically based on received measurements."* The auto-selection is bounded
by `MinIterationCount` 15, `MaxIterationCount` 100, `MinWarmupIterationCount` 6,
`MaxWarmupIterationCount` 50, with a default `IterationTime` of 500 ms.
(https://benchmarkdotnet.org/articles/configs/jobs.html, fetched 2026-09-16; the bounds confirmed
in source at https://raw.githubusercontent.com/dotnet/BenchmarkDotNet/master/src/BenchmarkDotNet/Jobs/RunMode.cs,
fetched 2026-09-16.)

The predefined jobs, read from that same source file (2026-09-16), so nobody has to guess what
`--job short` costs:

| Job | LaunchCount | WarmupCount | IterationCount |
|---|---|---|---|
| Dry | 1 | - | 1 (RunStrategy ColdStart) |
| Short | 1 | 3 | 3 |
| Medium | 2 | 10 | 15 |
| Long | 3 | 15 | 100 |
| VeryLong | 4 | 30 | 500 |

**The rule for this repo**: `--job medium` (or longer) for a Phase 7 decision, never the BDN default job unqualified, `--job short` **never** for one.
`MatchingBenchmarks` already records why - at 20 characters on `(a|a)*b` under `--job short` the
error bar was 21% of the mean, which is not a number you can decide anything with
(`bench/FuzzyRegex.Benchmarks/MatchingBenchmarks.cs`, the `BacktrackingFailure` doc comment).
`--job medium` or `--job long` for a slice that lands a change; a long run is cheaper than a wrong
conclusion.

### 1.2 Run strategy, unroll, invocation

`RunStrategy.Throughput` is *"the default strategy which allows to get good precision level"*;
`ColdStart` *"should be used only for measuring cold start of the application or testing
purpose"*; `Monitoring` is *"a mode without overhead evaluating"*. `InvocationCount` *"must be a
multiple of UnrollFactor"*, and `UnrollFactor` is how many times the method is invoked per loop
iteration (https://benchmarkdotnet.org/articles/configs/jobs.html, fetched 2026-09-16).

Leave all four alone for this suite. The one case to revisit is a benchmark whose single
invocation runs for seconds (a catastrophic-backtracking workload): there, `RunStrategy.Monitoring`
with `InvocationCount=1, UnrollFactor=1` measures what you mean, because overhead evaluation is
noise next to a multi-second call. Decide it per benchmark, with the reason in the doc comment,
the way `MatchingBenchmarks` already writes its reasons down.

### 1.3 Outliers

Default is `OutlierMode.RemoveUpper`: *"all upper outliers (larger than Q3) will be removed"*.
Other modes are `DontRemove`, `RemoveLower`, `RemoveAll`
(https://benchmarkdotnet.org/articles/samples/IntroOutliers.html, fetched 2026-09-16).

Keep the default, but **read the outlier count in the output**. For a regex engine an upper
outlier can be a real tail (a GC pause caused by our own allocation, a pathological path taken
once), and removing it silently is how an allocation regression hides. If the report says many
outliers were removed, that is a finding, not a formality.

### 1.4 MemoryDiagnoser

Gen0/1/2 columns are *"number of GC collections per 1000 operations for that generation"*; it uses
`GC.GetAllocatedBytesForCurrentThread`; BDN states it is *"99.5% accurate about allocated memory
when using default settings or Job.ShortRun (or any longer job than it)"*; diagnosers run in a
separate pass and extend the run
(https://benchmarkdotnet.org/articles/configs/diagnosers.html, fetched 2026-09-16).

`[MemoryDiagnoser]` is already on `MatchingBenchmarks` and must be on every Phase 7 benchmark.
Allocated bytes/op is the more stable of the two headline numbers - it is a count, not a timing,
so it barely moves with machine noise. **Use it as the primary ratchet for allocation work and the
tie-breaker when a timing delta is inside the noise floor.**

### 1.5 Baseline, ratio, and statistical test

`[Benchmark(Baseline = true)]` marks the reference within a run. The Ratio column is *"the mean of
the ratio distribution, not the ratio of means"* - each paired measurement is divided first, then
averaged - and `RatioSD` is that distribution's spread, which is what to read when the baseline is
itself noisy (https://benchmarkdotnet.org/articles/features/baselines.html, fetched 2026-09-16).

`[StatisticalTestColumn("3%")]` (or an absolute threshold, `"500us"`) adds a Welch or
Mann-Whitney test against the baseline and prints `p-value(Slower)|p-value(Faster)` classified
Faster/Same/Slower (https://benchmarkdotnet.org/articles/samples/IntroStatisticalTesting.html,
fetched 2026-09-16). dotnet/performance exposes the same thing on the command line as
`--statisticalTest 5%` (https://github.com/dotnet/performance/blob/main/docs/benchmarkdotnet.md,
fetched 2026-09-16).

Baselines only compare **within one run**. Comparing an optimised build against yesterday's build
is section 3.

### 1.6 The command lines Phase 7 uses

`--filter`/`-f` glob-matches `namespace.typeName.methodName` and multiple filters are OR'd;
`--runtimes` takes e.g. `net472 net8.0` and also `nativeaot<version>` monikers; `--artifacts` sets
the output directory; `--exporters` takes GitHub, CSV, JSON, HTML, XML
(https://benchmarkdotnet.org/articles/guides/console-args.html, fetched 2026-09-16). The full JSON
exporter writes host environment, metadata, mean, stdev, percentiles, CI and outliers, with
options `fileNameSuffix`, `indentJson`, `excludeMeasurements`; the default artifacts path is
`.\BenchmarkDotNet.Artifacts\results`
(https://benchmarkdotnet.org/articles/configs/exporters.html, fetched 2026-09-16).

```powershell
# One slice's measurement, archived so it can be compared later.
dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- `
  --filter '*' --job medium --exporters json `
  --artifacts artifacts/bench/<date>-<slice>-<before|after>
```

`--runtimes net10.0 net11.0` is S54's two-runtime baseline (S54 scope, owner decision 2026-09-14) -
one `net10.0` build measured under both runtimes, no TFM change. That answers "did the newer JIT
already do this for free" before anyone hand-optimises for it.

**Native AOT is a separate run, not a runtime moniker on the same command.** The library ships AOT
(`IsAotCompatible=true` in `src/FuzzyRegex/FuzzyRegex.csproj`), and a technique that pays under the
JIT can be flat or negative under AOT (no tiering, no dynamic PGO, no guarded devirtualisation).
Any optimisation whose mechanism is JIT-dependent gets measured on both, via BDN's `nativeaot`
runtime moniker, with the result recorded per runtime.

### 1.7 What BDN does for you, and what it does not

Does: forces the Windows High-Performance power plan for the run and restores it afterwards
(`PowerPlanMode` controls it; an abnormally killed run can leave the machine on High Performance,
fix with `powercfg`) (https://benchmarkdotnet.org/articles/configs/powerplans.html, fetched
2026-09-16); runs each benchmark in its own process; measures and subtracts per-iteration overhead
with an empty-method baseline (https://benchmarkdotnet.org/articles/guides/how-it-works.html,
fetched 2026-09-16).

Does not: quiet the machine. BDN's own good-practices page says *"Never use the Debug build for
benchmarking. Never."*, never attach a debugger, *"turn off all of the applications except the
benchmark process and the standard OS processes"*, keep a laptop plugged in on maximum
performance, always consume computed results so nothing is dead-code eliminated, and that
*"results in different environments may vary significantly"*
(https://benchmarkdotnet.org/articles/guides/good-practices.html, fetched 2026-09-16).

**Not found**: no BDN page enumerates what it cannot control on Windows (thermal throttling, OS
scheduler jitter, Defender scans, Windows Update). Treat that as unstated, not as absent - which
is exactly why section 4 measures the noise floor empirically instead of arguing about it.

**VERIFIED on this machine, 2026-09-19 (S58)**, by reading the artifacts of a real `--keepFiles`
run rather than a BDN page. Both halves are archived beside the noise floor as
`2026-09-19-S58-bdn-generated-MediumRun.csproj.txt` and `-MediumRun.runtimeconfig.json`:

- **GC: forced, and not to the host's settings.** The generated project writes
  `<ServerGarbageCollection>false</ServerGarbageCollection>` and
  `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`, and the generated
  `runtimeconfig.json` carries `"System.GC.Server": false, "System.GC.Concurrent": true`. So every
  measurement in this suite is workstation concurrent GC. A server-GC number needs an explicit job
  (`.WithGcServer(true)`), and a server-GC production deployment is not what these rows measure.
- **Affinity: not pinned.** The benchmark process ran with mask `0xfffffff` - all 28 logical
  processors of this machine - sampled six times through a live run. BDN does raise the benchmark
  process to `High` priority, and does set the High Performance power plan (above).
- Also worth knowing, because it silently changes what is compiled: the generated project sets
  `ImportDirectoryBuildProps=false` and `ImportDirectoryBuildTargets=false`, so this repo's
  `Directory.Build.props` does **not** apply to the harness, and it sets `RunAnalyzers=false`,
  `DebugSymbols=false`, `UseSharedCompilation=false` and `AllowUnsafeBlocks=true`. The
  "copied settings from benchmarks project" block was empty for our project.

Reproduce: `pwsh -File tools/compare-benchmarks.ps1` style working directory (`bench/`, so BDN's
solution walk stops at `FuzzyRegex.Benchmarks.slnx`), then
`dotnet run -c Release --project FuzzyRegex.Benchmarks --no-build -- --job medium --filter
'*SpanOverloadBenchmarks.StringShort*' --keepFiles`, and read
`bench/FuzzyRegex.Benchmarks/bin/Release/net10.0/FuzzyRegex.Benchmarks-MediumRun-1/`.

---

## 2. Quieting a Windows 11 machine

The honest position: **the only trustworthy statement about this machine is a measurement taken on
this machine.** The general advice below came from web search, not from fetched authoritative
documentation, and is flagged accordingly.

Do, because BDN or the docs say so:
- Release build, no debugger, nothing else running (BDN good-practices, fetched 2026-09-16).
- Let BDN set the power plan; do not fight it (BDN power plans, fetched 2026-09-16).
- Do not run a benchmark while the slice driver is running tests in the repo. This is a live
  hazard here, not a hypothetical: S54's own review hunt lists *"a baseline taken with the driver
  still running tests"* as a defect to catch (`docs/plan/slices/S54-benchmark-baselines-and-edge-pins.md`).

**UNVERIFIED (search-derived, not fetched primary sources)**: Defender exclusions on the build and
artifacts directories; exiling `MsMpEng.exe` to specific cores; Windows timer resolution now being
per-process; Ultimate Performance inducing thermal throttling on compact machines under sustained
load. Every one of these is a hypothesis about *this* machine. The cheap way to settle them is
section 4: measure the noise floor with the setting on and with it off, and keep whichever is
quieter. That converts an argument into a number for the price of two runs.

---

## 3. Comparing two runs of ours (before vs after)

BDN's `[Baseline]` compares within a run. For before-vs-after across runs, dotnet/performance's
`ResultsComparer` consumes BDN full-JSON output:

```
dotnet run --base "<before-results-folder>" --diff "<after-results-folder>" --threshold 1% --top 10
```

Required: `--base`, `--diff`, `--threshold` (accepts `5%`, `10ms`, `100ns`, `1s`). Optional:
`--top N`, `--csv`, `-f/--filter`, and `--noise`, whose default is **0.3ns**, documented because
*"the difference for 1.0ns and 1.1ns is 10%, but it's just noise"*
(https://github.com/dotnet/performance/blob/main/src/tools/ResultsComparer/README.md, fetched
2026-09-16). dotnet/performance's workflow doc points cross-run comparison at that tool and
documents `--statisticalTest` with relative or absolute thresholds for in-run regression flagging
(https://github.com/dotnet/performance/blob/main/docs/benchmarkdotnet.md, fetched 2026-09-16).

**Which to use here.** This repo does not need to take a dependency on dotnet/performance: S54's
scope already commits `tools/compare-benchmarks.ps1`, *"that reports the ratio per benchmark
against the baseline and fails on a regression beyond a stated threshold"*. That script is the
right home, and ResultsComparer is the reference for what it must do:

1. consume the full JSON (both sides, same exporter), never the Markdown table;
2. take an explicit `--threshold` and an absolute noise floor below which a ratio is reported as
   "same" regardless of percentage;
3. compare per workload, never an average - the v1.0 gate is per workload by design, so
   *"an overall-mean win cannot hide a badly regressed case"* (spec section 11);
4. report allocated bytes/op alongside time, since allocation is half of the owner's goal.

**Threshold, and where it comes from.** Do not pick a number from a blog. Pick it from section 4:
the threshold is the measured noise floor of this machine, rounded up. Anything smaller is not a
result. As a starting hypothesis before that measurement exists, 3-5% relative with an absolute
floor is the range dotnet/performance's own examples use (`--threshold 1%`, `--noise 2ns`,
`--statisticalTest 5%`; same two URLs, fetched 2026-09-16) - but the measurement replaces it.

**Not found**: neither the ResultsComparer README nor dotnet/performance's workflow doc states a
"run the unchanged build twice" protocol. Section 4 is therefore this repo's own method, not a
cited practice, and should be written down as such.

---

## 4. Measuring the noise floor (do this first, before any optimisation)

The single most useful hour in Phase 7, and it needs no code change:

1. Build Release once. Do not touch the tree.
2. Run the full suite with `--job medium --exporters json --artifacts .../noise-A`.
3. Run it **again**, same binary, same machine, same session, into `.../noise-B`.
4. Compare A against B with the same script Phase 7 will use for before-vs-after.

The result is the distribution of "differences that are not differences". The largest per-workload
ratio in that comparison is the floor; a change that moves a workload by less than it has not been
shown to do anything. Record it in the baseline folder next to the machine description, with the
date, because it will drift as the machine changes.

Two refinements worth the extra runs: do it once with the machine as it normally is and once with
the quieting measures of section 2 applied, which settles them empirically; and do it a third time
after a reboot, which tells you whether the floor is a property of the machine or of its current
state.

The same discipline answers the "is this change real" question at the end: if before-vs-after
moves a workload by less than the floor, the honest report is *"no measurable change"*, and per
the `benchmark` skill, *"an optimisation with no measured win is just a bug you have not found
yet"*.

---

## 5. The Python side, with pyperf

`regex` **2026.9.10**, Python **3.14.6** and - since S58, 2026-09-19 - `pyperf` **2.10.0** are
installed on this machine. The install is per-user
(`%APPDATA%\Roaming\Python\Python314\site-packages`), which matters: pyperf's worker processes do
not see it, so a script has to put that directory on `PYTHONPATH` **and** name PYTHONPATH on
`--inherit-environ` or every run dies with `ModuleNotFoundError: No module named 'pyperf'` inside
the worker and `RuntimeError: python.exe failed with exit code 1` in the parent.
`tools/probes/s58-pyperf-floor.py` does both and documents why.

### 5.1 How pyperf runs

pyperf spawns multiple worker processes and calibrates the loop count per process so a raw value
takes at least `MIN_TIME` (100 ms default). Runner defaults: `-p/--processes` 20, `-n/--values` 3,
`-w/--warmups` 1 (non-JIT mode); `--rigorous` *"Spend longer running tests to get more accurate
results. Multiply the number of PROCESSES by 2"*; `-o/--output FILENAME` writes JSON;
`--append` adds to an existing file; `--loops` accepts exponent syntax (`--loops=2^8`); explicit
calibration via `--calibrate-loops`, `--recalibrate-loops`, `--calibrate-warmups`
(https://pyperf.readthedocs.io/en/latest/runner.html, fetched 2026-09-16).

`pyperf timeit` form, and the reporting commands:

```
python -m pyperf timeit [-s SETUP] [--name NAME] [-o out.json] [--rigorous] stmt
python -m pyperf stats out.json          # mean, stdev, median, MAD, percentiles, outliers
python -m pyperf check out.json          # warns on stdev > 10% of mean, min/max > 50% off, < 1ms
python -m pyperf compare_to ref.json new.json [--table] [--table-format md] [--min-speed N]
```

(https://pyperf.readthedocs.io/en/latest/cli.html, fetched 2026-09-16.)

`pyperf check` is the Python-side equivalent of reading BDN's error column: it warns when stdev
exceeds 10% of the mean, when min or max deviates more than 50% from the mean, when the shortest
raw value is under 1 ms, or when problematic kernel options are detected (same URL, fetched
2026-09-16). **Run it on every recorded baseline and record its verdict with the numbers.** A
baseline that fails `check` is not evidence.

### 5.2 `pyperf system tune` does not help on Windows

The documented actions of `pyperf system tune` are all Linux: set the CPU scaling governor to
`performance`, maximise `scaling_min_freq`, stop `irqbalance` and pin IRQ affinity, set the perf
event max sample rate to 1, check the power cable, disable Turbo Boost via MSR or `intel_pstate`,
check ASLR, `nohz_full` and CPU isolation. The page also has a macOS section covering Turbo Boost
only (https://pyperf.readthedocs.io/en/latest/system.html, fetched 2026-09-16).

Fetched twice, and the page contains **no sentence** stating which operating systems the command
supports. The only Windows-specific statement anywhere on it is about the Runner, not about
`tune`: *"On Windows, worker process are set to the highest priority:
`REALTIME_PRIORITY_CLASS`"* (same URL, fetched 2026-09-16).

So: the docs' content is Linux-only and contains no Windows tuning procedure, and pyperf's Windows
story is the `REALTIME_PRIORITY_CLASS` worker priority it already applies. That was a strong
inference rather than a quote; **S58 ran the probe on 2026-09-19 and it is now a measurement**:

```
> python -m pyperf system show
WARNING: no operation available for your platform
> python -m pyperf system tune
WARNING: no operation available for your platform
```

Archived verbatim as `bench/baselines/<machine-id>/2026-09-19-S58-pyperf-system-show.txt`. pyperf
has no system operations of any kind on Windows - not merely no tuning procedure - so the remedy
its own `check` warning recommends is unavailable here. The consequence, measured in the same
slice, is that `pyperf check` fails on every workload under 50 ms on this machine however many
samples it is given, and `--rigorous` clears only the ~150 ms ones. The Python floor (1.11x) stands
in for `check` as the admission test; the numbers and the reasoning are beside the .NET floor.

The consequence either way: **the Python side is quieted by the same section 2 measures as the
.NET side and by nothing else**, and its noise floor is measured the same way as section 4 - run
the same benchmark twice, compare with `pyperf compare_to`, and treat anything inside that spread
as no difference.

---

## 6. Making the cross-language comparison defensible

No fetched page from BDN, pyperf or dotnet/performance gives guidance on comparing across language
runtimes; BDN's good-practices page only warns against extrapolating across .NET runtimes, OSes and
architectures (https://benchmarkdotnet.org/articles/guides/good-practices.html, fetched
2026-09-16). The academic source that does address comparability argues for standardising the
controllable variables (CPU frequency and turbo, process isolation, affinity) rather than comparing
raw numbers as taken; for the median over the mean as outlier-resistant; for confidence intervals
accompanying every reported number rather than point estimates; and for publishing the full raw
dataset so others can re-derive the statistics (arXiv:2411.08494, *Achieving Consistent and
Comparable CPU Evaluation*, fetched 2026-09-16 - the cross-language framing is a paraphrase of the
paper, not a verbatim quote).

**UNVERIFIED**: the Kalibera/Jones rigorous-benchmarking method was surfaced in search as the
standard reference for repetition counts and CI-bounded speedups but could not be fetched. If
Phase 7 wants a citable statistical protocol beyond the above, fetch it first.

The rules that follow, for this port:

1. **Same subject, same operation, same answer.** A workload pairs a pattern, an input corpus and
   an operation, and the two sides must return the same thing - same match count, same spans. The
   pairing script should assert that before it records a timing. A faster number for a different
   answer is not a number. This repo already has the machinery to state what "the same answer"
   means: the oracle (`tools/run-oracle.ps1`) exists precisely to compare our answers with
   upstream's.
2. **Same machine, same session.** Already the skill's rule: *"A baseline carried over from
   different hardware means nothing."*
3. **Medians, with spread.** BDN reports median and error; pyperf's `stats` gives median and MAD.
   Compare median to median, and publish the spread beside each.
4. **Per workload, never averaged** - the gate is defined that way (spec section 11).
5. **Publish the raw JSON**: BDN full-JSON exports and pyperf's own JSON, committed under
   `bench/baselines/<machine-id>/` with the machine description, the `regex` version, the Python
   version, the .NET SDK and runtime versions, and the date. The skill already requires the first
   four; **add the .NET versions and the measured noise floor**, without which the numbers cannot
   be re-derived or re-checked.
6. **Say what the comparison cannot show.** Process startup, string representation (Python's
   codepoint-indexed `str` versus our UTF-16), and the GC are all different on the two sides;
   workloads Python cannot express are measured and excluded from the gate (spec section 11). The
   published table should say which rows are excluded and why.
7. **One traceable identity per number.** Every recorded figure carries the git SHA it was taken
   at, the job used, and the artifacts folder it came from. The S51 commit is the house standard
   for evidence: the measured before-and-after goes in the commit message.

---

## 7. The gaps this research leaves open

| Gap | Cost to close | Who |
|---|---|---|
| ~~pyperf not installed; its Windows behaviour inferred, not probed~~ | | **closed by S58**: `2026-09-19-S58-pyperf-system-show.txt` beside the floor - no system operations on Windows at all, and `check` fails on every light workload |
| ~~Noise floor of this machine unknown~~ | | **closed by S58**: 1.13 time, 1.0001 allocation |
| ~~Whether BDN pins affinity / forces a GC mode by default~~ | | **closed by S58**, section 1.7: no affinity pin, workstation concurrent GC forced |
| .NET 11 runtime installed or not (S54's two-runtime baseline) | `dotnet --list-runtimes` | S54 |
