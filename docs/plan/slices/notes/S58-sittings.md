# S58 sittings

Per-sitting working notes. The slice file stays the spec; everything a later sitting needs to
resume is here.

## Sitting 1 - 2026-09-19

Started with a clean tree at `69c4dc6`. The orchestrator directed S58 ahead of the queue's
lowest-numbered file (S57) because S58's deliverable is a measurement and the machine was quiet;
S57 is unaffected by machine load and can follow.

### Checklist

- [x] New benchmarks added before the noise runs, so the floor covers them
- [x] `compare-benchmarks.ps1`: `-Job`, `-BaselinePath`, `-NoiseFloor`, `-AllocationNoiseFloor`
- [ ] Noise run A (in flight)
- [ ] Noise run B
- [ ] `noise-floor.md` committed with date, SHA, job, machine state
- [ ] BDN affinity / GC-mode question answered from a real run's artifacts
- [ ] pyperf installed, `system show` + `check` archived, Python floor measured
- [ ] EventPipe topN route proven
- [ ] dotTrace/Rider allocation route proven, or failure + fallback recorded
- [x] Optimise checklist written - BLOCKED from `.claude/`, parked in `phase7-research/`
- [x] `SYNC-DIVERGENCE.md` + `check-sync-divergence.ps1` + ratchet wiring
- [x] Tool tests over the floor and the divergence script (written; run after the noise runs)
- [~] `2026-09-19-span-threading-decision.md` drafted (both decisions in one file, as the slice
      asks) - **two number tables and two recommendations still to fill from run A**
- [ ] Lazy-walk decision document (same file, section 2)
- [ ] Ratchet, oracle at three seeds, AOT, tool tests
- [ ] Blind review, verifier, commit

### The machine: a hung compiler server (reported, not killed)

**`VBCSCompiler.exe` PID 16364**, 701 MB, is hung. Measured rather than inferred, 2026-09-19:

- `dotnet build -c Release bench/FuzzyRegex.Benchmarks/FuzzyRegex.Benchmarks.csproj -v:m` printed
  `All projects are up-to-date for restore.` and then produced nothing for over ten minutes.
- The identical build with `-p:UseSharedCompilation=false` finished in **10.79 seconds**.

That is the failure mode `~/.claude` memory records from 2026-09-19 and it is now reproduced a
second time, so the call is confirmed. Not killed: the owner's standing rule is to report the PID
and wait, and the orchestrator's note said a hung helper process would be cleared later.

**It does not affect benchmarking.** BenchmarkDotNet already passes
`/p:UseSharedCompilation=false` to its own generated restore and build - read off the run log, not
assumed - so every benchmark in this slice is unaffected. What it will block is the ratchet, the
test suite and the pre-commit inspections. Those need the daemon cleared, or every build in the
slice's verification step needs `-p:UseSharedCompilation=false`.

### What `--job medium` actually is

`MediumRun(IterationCount=15, LaunchCount=2, WarmupCount=10)`, read off the run log rather than
from documentation. Cost is dominated by per-benchmark process overhead, not by the workload: ten
sub-microsecond benchmarks took 6 m 55 s wall, about **41 s per benchmark**, so the ~49-benchmark
suite is about 36 minutes a run whatever the workloads do.

### Benchmarks added (before the noise runs, deliberately)

The floor has to cover the rows a later slice will compare against, so every new benchmark landed
before run A rather than after it.

- `WorkloadBenchmarks.FuzzyNoMatchLong` - the fuzzy no-match large-subject workload the 2026-09-18
  research sweep added to this slice, and the one S60 item 10 is judged against.
  `Corpus.LongNoMatch` is a megabyte of filler plus `a pin in a haystack.`; the absence of a near
  occurrence is **measured**, not assumed - upstream `regex 2026.9.10` answers `None` for both
  `(?:needle){e<=1}` and `(?:needle){e<=2}` over it, where the same patterns over `Corpus.Long`
  match at 1048577 and 1048576. Probe: `tools/probes/s58-nomatch-corpus.py`. First measurement
  (Dry job): **415.2 ms, 1.03 KB**.
- `SpanOverloadBenchmarks` - the string and `ReadOnlySpan<char>` overloads of `IsMatch` and `Count`
  over short, 1 KB and 1 MB subjects, for the span-threading decision. The pattern matches at index
  0 so the copy is what is measured rather than a scan.
- `GroupCountStateBenchmarks` and `SubjectLengthStateBenchmarks` - the two `MatchState` cost
  sweeps, for the lazy-walk decision. Two classes rather than two `[Params]` on one, because
  BenchmarkDotNet would otherwise run their cross product: 24 combinations to learn 10 numbers.

The slice file names `FuzzyRegex.cs:338` and `:873` for the span overloads; they are at **465**
(`IsMatch`) and **1124** (`Count`) on this tree. The `ponytail:` notes are beside both.

### First result, and it reframes the lazy-walk decision

The `MatchState` sweeps, medium job, 2026-09-19. **These are the calibration run's numbers**; the
committed evidence is run A's, in
`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json`, and the
full tables with the attribution are in `docs/plan/2026-09-19-span-threading-decision.md`. The two
runs agree to about 1%, which is itself a first read on the floor.

| Groups | Allocated | | SubjectLength | Mean | Allocated |
|---:|---:|---|---:|---:|---:|
| 1 | 1.27 KB | | 64 | 273.4 ns | 1.27 KB |
| 2 | 1.52 KB | | 1,024 | 298.2 ns | 1.27 KB |
| 4 | 2.04 KB | | 16,384 | 700.4 ns | 1.27 KB |
| 8 | 3.07 KB | | 262,144 | 8,618.0 ns | 1.27 KB |
| 16 | 5.13 KB | | | | |
| 32 | 9.26 KB | | | | |

Read off those: a state costs a **fixed ~1.01 KB plus ~264 bytes per capture group**, and the
per-group figure is linear to three figures across the whole sweep (0.250, 0.260, 0.2575, 0.2575,
0.258 KB per group between successive points).

**The subject-length column is the finding.** Allocation is FLAT at 1.27 KB from 64 characters to
262,144, while time goes 273 ns to 8,618 ns - about 0.032 ns per character. So the per-step charge
that makes a lazy walk quadratic is **time, not allocated bytes**: it is the vectorised pass over
the subject, and the subject-sized buffers do not show up as GC allocation at all. One caveat to
carry into the write-up: `MemoryDiagnoser` measures managed GC allocation, so an `ArrayPool` rental
that is warm is invisible to it, and "flat" here means "not allocating per call", not "not using
memory". That has to be stated in the decision document rather than glossed, and checked against
what `MatchState.Create` actually rents.

This matters for the decision the owner is being asked to make: pooling the state on `Dispose`
removes the 1.27 KB and roughly 270 ns of fixed cost per step, and does **not** remove the O(n)
pass. Anything claiming a lazy walk stops being quadratic because the state is pooled is wrong.

**Followed through to the line, on run A's numbers.** The O(n) pass is `MatchState.cs:501`,
`text.AsSpan().IndexOfAnyInRange('\uD800', '\uDBFF')` in the constructor - one vectorised scan for a
high surrogate, per state, to learn a bool that is a pure function of the subject. 0.0321 ns per
character means **33.6 microseconds per step** on a 1 MB subject against ~275 ns of everything else,
so pooling removes about 1% of the 12,643 ms lazy walk. The fix is to hoist that flag (and
`GetCharacterIndex`) onto the walk, which needs no API change; pooling and the `ref struct`
enumerator are then arguments about the remaining 1%. That is the recommendation in the decision
document, and it is the opposite of what the deferral comment implies.

### Run B was discarded, and the monitoring changed because of it

A ran 08:22-08:56 with a clean contention signal. B ran straight after it, 08:56-09:35, and came
back with 19 of 49 rows under 0.85 and medians up to 2.39x A's. The full account, with the table
and the reasoning, is in `bench/baselines/<machine-id>/noise-floor.md`; the short version for a
later sitting:

- The damage was a contiguous **episode** - first class clean, then everything up to
  `WorkloadBenchmarks.ClassScan` hurt, then the last seventeen rows clean inside 1.03x. Not
  thermal (that worsens through a run), not a slower machine.
- **`.scratch/load-guard.log` reported `avg=1% peak=7%` throughout it.** A five-minute mean over
  20 cores cannot see one core taken for ninety seconds, so "the monitor said quiet" was worth
  nothing here. That is the lesson, and it is bigger than this slice: **every future performance
  claim in this repo needs per-process sampling beside it, not a load average.**
- **Cause confirmed by the orchestrator at 09:55**: a review fix pass in the demo worktree ran npm
  builds, vitest, Pester, a TUnit Debug run and csharpier/ReSharper inspections from about 08:47 to
  09:50. That brackets B and misses A. A separate worktree is not a separate CPU.
- **The detector worked; the watchman did not.** BDN's min/median flagged 19 of 49 rows and the
  compare script refused to be read as a gate, all without knowing the worktree existed. Only the
  load guard's five-minute average was fooled.
- **Second competitor, and it is us**: the sampler caught `python(21184) 39.1s/15s` - 2.6 cores -
  from `headroom.cli proxy --port 8787`, the context proxy every session here routes through. A
  session that writes documents while its own run is in flight is measuring its own typing. Run C
  was being polluted by this sitting's note-writing until the sampler showed it.
- Run C (09:44) was stopped, not finished: its first benchmark class ran inside the episode's tail,
  so it could not have been A's partner whatever happened later. No C baseline exists.
- Nothing was killed: the two background jobs stopped were this session's own.
- New: `tools/probes/sample-machine-load.ps1` runs beside a noise run and logs per-process CPU
  deltas every 15 seconds, and `tools/probes/compare-two-baselines.ps1` prints each run's own
  contention signal beside the ratio, from committed baselines alone.
- B's baseline is kept as `2026-09-19-S58-noise-B-discarded.json`. Run C (09:44-) replaces it.

### Blocked: the session cannot write under `.claude/`

Scope item 5 names `.claude/skills/optimise/SKILL.md`. Two `Write` calls to that path were refused
for permission, so the finished body is in `docs/plan/phase7-research/optimise-skill-pending.md`
with the one-command move at the top, and ROADMAP's Phase 7 paragraph points at it. Not worked
around with a shell copy: the refusal is a permission boundary, and a slice does not get to decide
it was meant leniently. The owner moves the file, or grants the write and a later sitting does.

### Ratchet wiring

`check-sync-divergence.ps1` runs as a ratchet PRE-FLIGHT, beside the control-mutation check and
before the test run, so it applies under `-SkipTestRun` too and reports in a second rather than
after the suite. Pairing is by FILE, not by line: line numbers move under CSharpier and under the
next edit either side of a marker, and a check that goes red on a reformat is a check people learn
to bypass.

### Hazard: no edit to `src/` or `bench/` while a noise run is in flight

BenchmarkDotNet generates and builds a project per benchmark class **during** the run, not once at
the start, so a source edit halfway through a 34-minute run would have the later classes measuring a
different library than the earlier ones. Everything touching `src/` or `bench/` waits until run B
has printed its baseline. Docs and `tools/` are safe.

### Deviation from the slice file: where archived evidence goes

The slice says to archive under `artifacts/bench/...` and `artifacts/prof/S58/`. **`artifacts/` is
gitignored** (`.gitignore:9`), so anything left there is invisible to the independent verifier and
to every later slice - the "scratch-only probe" failure the skill warns about. So: raw
BenchmarkDotNet output stays in `artifacts/` (it is large and regenerable), and the derived
evidence is committed - the A and B baseline JSONs and `noise-floor.md` under
`bench/baselines/<machine-id>/`, the profiler texts under `docs/plan/phase7-research/profiles/`.
The comparison is then re-runnable from committed files alone.
