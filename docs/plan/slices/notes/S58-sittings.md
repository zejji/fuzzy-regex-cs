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
- [x] Noise run A (sound, committed)
- [x] Noise run B (discarded - worktree episode), C (stopped), D (discarded - our own proxy)
- [x] Noise run E, and the floor set from A against E: time 1.13, allocation 1.0001
- [x] `noise-floor.md` committed with date, SHA, job, machine state
- [ ] BDN affinity / GC-mode question answered from a real run's artifacts
- [ ] pyperf installed, `system show` + `check` archived, Python floor measured
- [ ] EventPipe topN route proven
- [ ] dotTrace/Rider allocation route proven, or failure + fallback recorded
- [x] Optimise checklist written - BLOCKED from `.claude/`, parked in `phase7-research/`
- [x] `SYNC-DIVERGENCE.md` + `check-sync-divergence.ps1` + ratchet wiring
- [x] Tool tests over the floor and the divergence script - run at last, 114/0 after a fixture fix and one added test
- [~] `2026-09-19-span-threading-decision.md` drafted (both decisions in one file, as the slice
      asks) - **two number tables and two recommendations still to fill from run A**
- [ ] Lazy-walk decision document (same file, section 2)
- [x] Ratchet GREEN (6399 passing), tool tests 114/0
- [~] Oracle: 2 of 3 seeds green; the third is a pre-existing engine divergence, written up
- [~] AOT: RED on a trim-analysis error in an S65 convention test, written up
- [~] Blind review (two passes, nine findings, all fixed) and verifier done; committed as a
      checkpoint - the slice's own unstarted scope is what keeps it open

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

### Sitting 2 - 2026-09-19, 11:21

Inherited a clean tree at `e30a4e8` with one untracked file, and a running benchmark nobody had
written down.

**Run D was in flight and this sitting did not know.** Sitting 1 launched it detached at 11:16:18
and was killed at 11:17 before recording it; `STATE.md` said "take the one missing noise run", which
reads as an instruction to start one. Orientation - reading the state files, sampling the machine
for two minutes to check it was quiet, enumerating processes to identify two busy `dotnet`
processes - ran 11:19-11:21 through the Headroom proxy, which D's own sampler logged at
**54.5s and 64.1s per 15-second window, 3.6 and 4.3 cores**. The orchestrator's message at 11:21
named the run; by then the damage was done. D's first eleven rows are 1.10-2.06x A's with
min/median down to 0.54, and its last 38 are inside 1.07x - the same episode shape as B, at the
front of the run instead of the middle.

D is discarded and committed as evidence (`-noise-D-discarded.json` + `-discarded-load.log`), and
`noise-floor.md` gains a sixth rule from it: **write a detached run into `STATE.md` with its PID,
log path and expected finish before launching it**, because the session that knows is the session
that gets killed.

#### Run E, and the floor

E ran 11:56:31-12:29:55 (33 m 24 s, 49 benchmarks) with the session blocked on `Wait-Process` the
whole time and nothing else started anywhere. First attempt of four to pass its own gate: **0 of 49
rows below 0.85**, two multimodal rows, and a sampler log whose busiest non-benchmark sample is
`msedge 4.5s/15s` - 0.3 of a core, once.

| | Floor | Set by |
|---|---:|---|
| Time | **1.13** | `ReverseFailedScan` 0.88916, inverted = 1.1247 |
| Allocation | **1.0001** | `SplitLong`, 307 B in 11.06 MB |

The time floor comes from the *improvement* side, which is worth understanding rather than
memorising: the widest slowdown was 1.0768, but `ReverseFailedScan` ran 11% FASTER in E than in A
with no code in between, and a band that ignores that direction would report the same artefact as a
win. noise-floor.md records that dropping that one row gives 1.08, and that the row is the suspect
rather than the machine - it was one of A's three shakiest rows on the contention signal (0.9148,
behind `MatchesToEndDense` 0.9008 and `MatchesFirstTwo` 0.9102; 0.99 in E) and is one of A's four
multimodal rows.

Allocation is the half worth having: 38 of 45 allocating rows matched **to the byte**, so almost any
allocation change is *measurable*, which is exactly what the span and lazy-walk decisions turn on.
Measurable is not gated, though: `compare-benchmarks.ps1` fails a run on `-Threshold` (1.25 on both
axes) and a floor only decides whether a ratio prints `same`, so a row allocating 1.20x its baseline
is still GREEN. That is pre-existing behaviour, not something this slice's floors changed; it is now
pinned as a test in `CompareBenchmarks.Tests.ps1` and handed to S63 as scope item 7, because picking
a gate number is the gate slice's job and not a measurement slice's.

Self-test green: `compare-benchmarks.ps1 -UseExisting` over A and E reads `same` on all 49 rows.

#### Three tool tests that had never run, and were failing

`tools/run-tool-tests.ps1` came back 110/3. All three failures were in
`CheckSyncDivergence.Tests.ps1`, written last sitting and never executed. The script was innocent:
the fixture's `$LedgerHeader` here-string ends at the newline BEFORE `'@`, so every row a test
appended was glued onto the table's separator line. The script then read `---` as the first cell,
found no backticked path, and saw a ledger with **no rows at all** - which flipped the three
paired-case tests red and made two others (`marker with no ledger row`, `paths in the prose above
the table`) pass for the wrong reason. One blank line fixes it; 113/0 after, and 114/0 once this sitting added a test.

The lesson is the one the slice skill already states and this is a clean instance of: a test that
has never been watched fail is not yet a test.

#### Verification: two gates red, neither S58's

- **Oracle: RED at 1 of 3 seeds**, one row of 6380. Seeds 7 and 4242 are `diverge 0`. The third
  default seed is `Get-Date -Format 'yyyyMMdd'` (`run-oracle.ps1:256`), so it had never been run
  before today. `git diff dfa8767 -- src` is EMPTY - `dfa8767` (S56b) is the last commit that
  touched `src/` at all, 28 commits back from this slice's first - so it cannot be S58's. Match,
  spans and edit totals all agree; only the
  attribution of one substitution and one insertion to adjacent positions differs. Written up with
  its reproduction in `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`, because
  the oracle's own evidence lives in gitignored `TestResults/`. **Not pinned**: pinning before
  deciding which engine is right cements whichever answer happened to be there.
- **AOT: RED**, and also not S58's - this slice changed no `.cs` file at all. `ilc` fails on
  `Trim analysis error IL2065` in `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs:62`,
  a `type.GetMembers(...)` over types that are not statically known. That convention test arrived
  yesterday in S65 (`3b09b76`, 2026-09-18) and the AOT gate has not been run since. The fix is a
  real decision between annotating, suppressing with a reason, or excluding the convention test
  from the native publish, and it is a `.cs` change - so it is not taken mid-slice.

**Rejected: a filtered re-run of just the eleven damaged rows.** It would have cost 12 minutes
instead of 35, and the owner's cheapest-route rule points at it. Two things killed it.
`compare-benchmarks.ps1:201-203` refuses `-UpdateBaseline` under a filter ("would record a partial
suite"), so it would have meant weakening a guard to get a result - banned outright. And the
cheap currency here is allowance, not wall time: a full run is four blocking waits and near-zero
tokens, so the 23 minutes bought nothing worth having.

### Deviation from the slice file: where archived evidence goes

The slice says to archive under `artifacts/bench/...` and `artifacts/prof/S58/`. **`artifacts/` is
gitignored** (`.gitignore:9`), so anything left there is invisible to the independent verifier and
to every later slice - the "scratch-only probe" failure the skill warns about. So: raw
BenchmarkDotNet output stays in `artifacts/` (it is large and regenerable), and the derived
evidence is committed - the A and B baseline JSONs and `noise-floor.md` under
`bench/baselines/<machine-id>/`, the profiler texts under `docs/plan/phase7-research/profiles/`.
The comparison is then re-runnable from committed files alone.

### Review: two blind passes, nine findings, all nine reproduced

**Pass 1, over the committed floor (`14d4c53`).** Four findings raised, four reproduced, four fixed.

1. *The floors gate nothing.* Substantive, and the only one that was about behaviour rather than
   prose. See below - it changed what the slice claims and added a test.
2. `ReverseFailedScan` was written up as run A's **worst** contention signal. It is the third worst
   (0.9148, behind `MatchesToEndDense` 0.9008 and `MatchesFirstTwo` 0.9102). Corrected in
   `noise-floor.md` and here.
3. `noise-floor.md` said run E's sampler log "names nothing above 0.3 of a core". False as written:
   `dotnet` itself reaches 14.5 s in a 15 s window. The qualifier "non-benchmark" was in these
   notes and missing there. Corrected.
4. `1/0.8892 = 1.1247` does not hold at the precision quoted; the ratio is 0.88916 and 1/0.8892
   rounds to 1.1246. Every copy of the number now reads 0.88916.

**Pass 2, over the delta pass 1 never saw** (the new test, the S63 scope item, the corrected prose).
Five findings, five reproduced, five fixed - and the first of them corrected the fix for pass 1's
finding 1, which is exactly what a second pass is for.

1. *The fix for finding 1 was itself wrong.* It said a floor "only decides whether a ratio prints
   `same`". A floor also **excuses**: `$regressed = $ratio -gt $Threshold -and -not $withinFloor`
   (`compare-benchmarks.ps1:343`, and `:331` for allocation), so a floor **above** `-Threshold`
   does change the verdict. Reproduced on run A's artifacts with one row scaled to 1.20x: at
   `-Threshold 1.05` the run is RED with the shipped floors and GREEN with `-NoiseFloor 1.25
   -AllocationNoiseFloor 1.25` - both, because the doctored row moved on both axes. The
   true statement, now in all four places, is narrower: a floor can only ever excuse, never fail,
   so a floor **below** `-Threshold` excuses nothing and moves no verdict.
2. Three files still said "tool tests 113/0" after this sitting's own test made it 114.
3. Run E was written up as 33 m 10 s; 11:56:31 to 12:29:55 is 33 m 24 s.
4. "byte-identical to two commits before this slice" - `dfa8767` is 28 commits back, and is simply
   the last commit that touched `src/`. Corrected here and in the oracle write-up.
5. The new test would still pass if the allocation gate were deleted outright, so it does not by
   itself prove `-Threshold` governs allocation. Left as it is: the pre-existing test "floors
   allocation on its own number, not the timing one" fails under that mutation, so the pair covers
   it, and the new test's comment no longer claims more than the test shows.

#### The finding worth carrying: the floors move no verdict

The measurement says this machine resolves allocation to 2.8e-5. The gate does not use that.
`-Threshold` (1.25) is what fails a run and it governs both axes; a floor only forgives a ratio
that is already over `-Threshold`. Both of this slice's floors are below 1.25, so a benchmark
allocating **1.20x** its baseline is GREEN. That is pre-existing behaviour, not something these
floors introduced, and tightening it is a gate decision - so it is now **S63 scope item 7** rather
than a measurement slice's improvisation, and it is pinned as a test so nobody has to rediscover
it. Probe: `tools/probes/gate-scale-one-row.py`.

#### Two gate re-runs on the commit-ready tree, for the record

- `tools/run-aot-tests.ps1` still RED, and identically:
  `PublicApiDocumentationTests.cs(62): Trim analysis error IL2065 ... System.Type.GetMembers(BindingFlags)`.
- `tools/run-aot-smoke.ps1` GREEN. Binary **6,977,536 bytes**, against the 6,972,928 that S63's
  scope quotes as the baseline - 4,608 bytes larger, and no `.cs` changed in this slice, so the
  growth is somewhere in S64-S66. A number for S63 to reconcile, not a finding here.
- `tools/check-sync-divergence.ps1` GREEN, 0 marked files.

### Verifier: 43 claims re-derived, 5 DIFFERENT, 3 COULD NOT RUN

A fresh Opus session, briefed on nothing but the commit-ready tree, re-derived every number in
`noise-floor.md`, these notes, the oracle write-up and S63's new scope item. It renders no opinion;
it reports CONFIRMED, DIFFERENT or COULD NOT RUN. All five DIFFERENT are fixed, and all five were
in the evidence rather than in the measurement - the floor, the spans, the row attributions, the
run timings and the allocation determinism all re-derived exactly.

1. Run B's tail was written up as `0.93-0.99` on the contention signal and "a clean contention
   signal". It is **0.83-0.99**: `MatchesFirstTwo` reads 0.83, below the 0.85 line, with a ratio of
   1.01. The exception is now stated instead of rounded away.
2. Rule 5 said A and B "agreed to 1.02-1.08x on every row the episode missed". The seventeen rows
   the episode missed span **0.92-1.03**, which the table two sections above already said. Rule 5
   now agrees with the table.
3. The sampler's overhead was quoted as "below 0.7% of one core at 15-second spacing". 0.2 s in
   15 s is **1.3%**; 0.7% is the script's own figure for its default 30-second interval.
4. The gate reproduction said RED at `-AllocationNoiseFloor 1.0001` and GREEN at 1.25. Widening the
   allocation floor alone leaves the run RED on the **time** axis, because the doctored row moved
   1.20x on both. Both floors have to be widened, and the text now says so. Re-run to confirm.
5. The partial-baseline guard is at `compare-benchmarks.ps1:201-203`, not `:199` (a closing brace).

It also raised one tension rather than an error: **rule 3 as written would have discarded run E.**
"A run whose log names any process other than `dotnet`, `csc`, `MSBuild` and the sampler is a run
to discard" - and E's log names `msedge`, `msedgewebview2`, `python` and `VBCSCompiler`. The rule
was describing the wrong quantity. It now judges CPU taken rather than names present, and says what
the evidence supports and no more: 0.3 of a core demonstrably harmless (E), 2.6 demonstrably fatal
(C), nothing in between proven either way, so re-take rather than argue.

COULD NOT RUN, and why: the pre-fix 110/3 and intermediate 113/0 tool-test counts (the broken
fixture is no longer on disk); the oracle at seeds 7 and 4242 (forbidden as expensive - **since
run in this sitting: seed 7 is GREEN, `no row diverged from upstream`**); and the AOT IL2065
failure (needs a native publish - **since re-run here, identical**).
