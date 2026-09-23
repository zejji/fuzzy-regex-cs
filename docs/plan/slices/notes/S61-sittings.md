# S61 sittings

## Sitting 1 (2026-09-22, from 22:50, worktree `s61`, beside the S84 session in main)

The machine was busy the whole sitting, so **no time number below decides anything**. Allocated
bytes are unaffected by CPU contention and are the numbers this sitting acts on. Every run is
`--job short --inProcess`, one benchmark job at a time.

### Before (8dd746e)

ManyInputs, bytes per operation (one operation is 100,000 calls):

| Row | Allocated | Per call |
|---|---|---|
| FuzzyPhraseOneIsMatch | 92.39 MB | 969 B |
| FuzzyPhraseOneMatch | 92.39 MB | 969 B |
| FuzzyPhraseOneEnhanced | 95.61 MB | 1,003 B |
| FuzzyPhraseThreeAlternation | 92.5 MB | 970 B |
| FuzzyPhraseThreeNamedList | 92.5 MB | 970 B |
| FuzzyPhraseThreeSeparatePasses | 277.12 MB | 2,906 B |
| ValidateEmails | 179.97 MB | 1,887 B |
| ParseLogLines | 207.52 MB | 2,176 B |
| RedactDigits | 116.77 MB | 1,224 B |
| CaseFoldAccented | 86.98 MB | 912 B |

Walks and span overloads:

| Row | Allocated |
|---|---|
| WorkloadBenchmarks.MatchesToEnd (1 MB) | 34,266.28 KB |
| WorkloadBenchmarks.MatchesToEndDense (100 KB) | 3,460.98 KB |
| WorkloadBenchmarks.EnumerateMatchesToEndDense (100 KB) | 21,286.45 KB |
| WorkloadBenchmarks.EnumerateMatchesFirstTwo (1 MB) | 2.23 KB |
| SpanOverloadBenchmarks.StringShort / SpanShort | 912 B / 1,064 B |
| SpanOverloadBenchmarks.StringMegabyte / SpanMegabyte | 912 B / 2,099,410 B |

### Finding that shapes the design

Upstream caches three pieces of state storage on the pattern and reuses them on the next call:
`groups_storage`, `repeats_storage` and `stack_storage` (`upstream/src/_regex.c` :577-579, taken in
`state_init_2` at :18300, :18341, :18500 and handed back in `state_fini` at :18684-18711). This
port's `MatchState.Create` says that cache "has nothing to port on a garbage-collected heap". The
ManyInputs numbers say otherwise: nearly all of the 969 B per call is state that the previous call
built and threw away.

### Step A: one state per lazy walk, and NextMatch stops rescanning the subject

`EnumerateMatches` and `EnumerateSplits` hold one state for the walk and restart its clock per step
(the built-in `Regex` times each step: `tools/probes/bcl-lazy-walk-timeout.cs`). `Split` is now
`[.. EnumerateSplits]`. A `Match` carries `OneUnitPerCharacter` so `NextMatch` does not rescan.

| Row | Before | After |
|---|---|---|
| EnumerateMatchesToEndDense (100 KB) | 21,286.45 KB | 3,111.85 KB |
| EnumerateMatchesFirstTwo (1 MB) | 2.23 KB | 1.38 KB |
| MatchesToEnd (1 MB) | 34,266.28 KB | 33.46 MB (unchanged) |
| MatchesToEndDense (100 KB) | 3,460.98 KB | 3.38 MB (unchanged) |

A nullable `bool?` on `Match` first cost 8 bytes a match (35,939.6 KB); a plain `bool` fits the
padding. **Time gate open**: `--filter "*WorkloadBenchmarks.*MatchesToEnd*" "*sizing*"` on a quiet
machine decides whether the walk's time claim holds; tonight's times are not evidence.

### Step B: the predicates build no Match, and a failed match builds no groups

`IsMatch`, `IsMatchAtStart` and `IsFullMatch` run through a generic `Execute` that reads the
status off the state and never builds a `Match`. An unsuccessful `Match` holds no group data;
`Match.GroupAt` reports every group of it as absent.

### Step C: the pattern keeps one state between calls

`MatchStateCache` is upstream's `groups_storage`/`repeats_storage`/`stack_storage` cache
(`_regex.c` :577-579) in the shape of the built-in `Regex._runner`: one slot, taken with
`Interlocked.Exchange` and put back with `Volatile.Write`. `MatchState.Init` assigns every field a
call can dirty; `MatchStateCacheTests` compares a reused state with a new one field by field, over
15 dirtying patterns and a reflection scribble of every field. A state the cache built goes back
to it on `Dispose`, so Replace, Count, Split, the lazy walks and `NextMatch` rent from it with a
one-line change (a test's own `ArrayPool` bypasses the cache).

Controls, each run once and restored:

- Remove one assignment from `Init`: 13 of 16 lines fail `MatchStateCacheTests` (1 to 17 tests
  each). `BestMatchGroups = null`, `Sstack.Reset()` and `_characterIndex = null` survive because
  `Release` already clears them when the state comes back; the Init lines are belt and braces.
- `Rent` reads the slot with `Volatile.Read` instead of the exchange (two threads share a state):
  `ThreadSafetyStressTests.Every_family_answers_the_same_under_real_parallelism` fails.
- `Iteration.Scan` goes back to `MatchState.Create`: `A_warm_Count_allocates_nothing` fails at
  904 B and 832 B.

The last 96 B of a warm `IsMatch` was `ReadOnlySpan<char>.IndexOfAnyInRange('\uD800', '\uDBFF')`,
which allocated 96 B per call even after tier-up, in Debug and Release. The same search over
`MemoryMarshal.Cast<char, ushort>` allocates nothing. `AllocationTests` pins 0 B for a warm
`IsMatch` and `Count`.

ManyInputs after steps B and C (one operation is 100,000 calls):

| Row | Before | After B | After C | Per call now |
|---|---|---|---|---|
| FuzzyPhraseOneIsMatch | 92.39 MB | 78.58 MB | 0 | 0 B |
| FuzzyPhraseOneMatch | 92.39 MB | 90.15 MB | 11.57 MB | 121 B |
| FuzzyPhraseOneEnhanced | 95.61 MB | 93.36 MB | 14.79 MB | 155 B |
| FuzzyPhraseThreeAlternation | 92.5 MB | 78.58 MB | 4,656 B | 0 B |
| FuzzyPhraseThreeNamedList | 92.5 MB | 78.58 MB | 4,656 B | 0 B |
| FuzzyPhraseThreeSeparatePasses | 277.12 MB | 235.74 MB | 4,656 B | 0 B |
| ValidateEmails | 179.97 MB | 166.24 MB | 517 B | 0 B |
| ParseLogLines | 207.52 MB | 207.52 MB | 42.73 MB | 448 B |
| RedactDigits | 116.77 MB | 116.77 MB | 31.32 MB | 328 B |
| CaseFoldAccented | 86.98 MB | 73.24 MB | 1,552 B | 0 B |

What is left is output: the `Match` and its group copies, and Replace's result string.
Walks: `EnumerateMatchesFirstTwo` 1.38 KB to 504 B; `MatchesToEnd` (33.46 MB) and
`MatchesToEndDense` (3.38 MB) are unchanged, because their bytes are the `Match` objects.
**Time gate open**: `--filter "*ManyInputs*"` on a quiet machine decides the time side.

### Step D: span option (a), the memory overloads

`MatchState.Text` is a `ReadOnlyMemory<char>` (option (a) of
`docs/plan/2026-09-19-span-threading-decision.md`), and `IsMatch` and `Count` gained
`ReadOnlyMemory<char>` overloads that read the caller's buffer where it lies. The span overloads
still copy, and their remarks now say so. Everything returning a `Match` keeps its string, because
`Capture.Value` needs one.

`*SpanOverload*`, `--job short --inProcess`, over a megabyte:

| Row | Allocated |
|---|---|
| StringMegabyte | 0 B |
| SpanMegabyte | 2,098,060 B |
| MemoryMegabyte | 0 B |
| CountStringMegabyte | 3 B |
| CountSpanMegabyte | 2,098,135 B |
| CountMemoryMegabyte | 8 B |

Controls, each run once and restored: the memory `IsMatch` routed through
`IsMatch(input.ToString())`, and the memory `Count` through `input.ToString().AsMemory()`. Each
made `The_memory_overloads_read_a_megabyte_buffer_without_copying_it` fail at 2,097,176 B.

**Time gate open**: `CharAt` now reads through `Text.Span` on every path, so the deciding run is
the full suite at `--job medium` on a quiet machine, with `*ManyInputs*` and `*SpanOverload*` read
first.

### Oracle after step D (2026-09-23 01:30)

Green at seeds 7 and 4242; red at 20260923 on two rows. Both reproduce at 8dd746e, before any S61
change, so neither is S61's. Each still needs minimising and pinning:

- Row 5185 (partial-sliced): `(?:[[:digit:]]?(*SKIP)[^\d]|\s)([_])?\1\g<1>\b`, IgnoreCase, V0,
  over `BB__` sliced to [0, 2), partial. Upstream gives (0, 2). The port gives (1, 1) both here and
  at 8dd746e, cold and warm.
- Row 3752 (interactions): Split with flags 0x400a. The oracle timed out. With no timeout, `Split`
  ran for over 10 minutes on both builds (542 s and 229 s of CPU) and was still going. Upstream
  returns 2 parts.

### Blocker (01:50), cleared in sitting 2

The step D commit's pre-commit inspection (`jb inspectcode`, PIDs 45996, 23976 and 14720, started
01:04) stopped writing output at 01:05 and used no CPU. `dotnet build-server shutdown` did not free
it. The driver rolled the step D change set back to a34d895 and saved it as
`slice-rescue/S61-per-match-allocation-20260923-014852`.

## Sitting 2 (2026-09-23)

The rescue stash was applied unchanged. The inspection that hung in sitting 1 ran through in
about 12 minutes. Ratchet GREEN at 6624 tests. Step D was committed as it was restored.

### Blind review of a34d895 and cd0c3d1

One Opus pass. Build clean, suite 6624/6624, tool tests 188/188. Two findings, both reproduced and
fixed:

1. `IsMatch(null!)` and `Count(null!)` became ambiguous (CS0121), because `null` converts to a
   `ReadOnlyMemory<char>` through `char[]`. Nothing has shipped, so no existing code broke, but
   the call used to compile and throw `ArgumentNullException`, as `Regex.IsMatch(null)` does.
   Fixed with `[OverloadResolutionPriority(-1)]` on the two memory overloads. Pinned by
   `MemoryOverloadTests.A_null_argument_still_binds_to_the_string_overload`, which failed to
   compile before the fix.
2. `compare-benchmarks.ps1` printed a failing allocation rise of 5,000,000 to 5,002,000 bytes as
   1.00x in the row and "1x" in the reason, and printed the default 1.0001x floor as 1.00x.
   Allocation ratios within 0.005 of 1 now print to four places. Pinned by the Pester test "shows
   a failing allocation rise that is too small for two decimal places", which was red before the
   fix.

Checked clean by the reviewer: memory and string answers agree on 30 patterns x 9 subjects x 4
memory kinds; the cached state holds no caller buffer after a call, cancelled calls included;
timeouts and cancellation on both new overloads; `ArgumentNullException` unchanged on the string
overloads; the gate's pass/fail on eight rise/drop/zero cases. The two fixes change public
surface and tooling, so they get their own pass (VERIFICATION rule 4).

**Second pass, over 3d958a2.** The attribute is clean. The reviewer read the bound overload out of
the compiled IL for null, string, `string?`, `char[]`, `Memory`, `ReadOnlyMemory`, `ArraySegment`
and method groups, under C# latest and C# 13, and every call bound as intended. `ArraySegment<char>`
used to be a CS0121 error and now binds to the span overload. Under C# 12 the attribute is
ignored, and `null` and `char[]` give CS0121: a compile error, never a wrong binding, and the
library targets net10.0, whose default is C# 14. One finding, reproduced: with
`-AllocationNoiseFloor 1.00001` a failing rise of 1.00002x still printed as 1.0000x, because four
fixed places only moved the limit. `Get-RatioDigits` now takes the fewest places, at least two,
at which the ratio no longer rounds to 1. Pinned by the Pester test "shows a failing allocation
rise however tight the floor is set", which was red before the fix. The reviewer's probe then
printed every case correctly (1.00002x, 0.996x, 1.005x, 1.20x, 0.20x, 1.0002x). This delta
changes tooling, so it gets one more pass.

**Third pass, over 5c91f57.** 336,555 random ratios printed correctly, and NaN, the infinities, 0
and negative ratios do not throw. Two findings, both reproduced, both from the 10-place cap: a
one-byte rise on a 30,000,000,000-byte baseline (slack 0) printed as 1.0000000000x, and a floor of
1.00000000001 printed as 1.0000000000x. Neither changed a verdict. The cap is now 15, which is
`Math.Round`'s own limit. Pinned by the Pester test "shows a one-byte rise on a very large
baseline", which was red before the fix.

**Fourth pass, over aa67e98: no defects found.** Tool tests 191/191. 200,000 random one-byte
rises on baselines up to 1e12 bytes all printed as something other than 1. The first ratio that
still prints as 1 is 1 + 2e-16, which needs a baseline of about 4.5e15 bytes per operation.
S61 review summary for a34d895 to aa67e98: four passes, five findings raised, five reproduced,
five fixed.

### AOT

`tools/run-aot-tests.ps1` first went RED. `MatchStateCacheTests.ScribbleField` (steps B and C)
called `Enum.GetValues(Type)`, and IL3050 makes that an error under Native AOT. It now calls
`Enum.GetValuesAsUnderlyingType` and converts back with `Enum.ToObject`. After the fix the AOT
suite has 6625 tests, 6622 succeeded and 0 failed. `tools/run-aot-smoke.ps1` is GREEN at
7,000,576 bytes, which is 27,648 bytes (+0.4%) over the 6,972,928 the slice names. The 3 of 6625
that did not succeed are skips (the write-once field audit cannot read BCL private fields under
AOT). Blind pass over 20bc7d5: no defects found.

## Sitting 4 (2026-09-23, from 05:24)

Sitting 3 left no notes of its own; its work is summarised here. It launched the time-gate runs
and wrote the wave-scale reset test, which it saved as `.scratch/s61-sitting3-wip.patch` without
running. This sitting re-applied the patch unchanged.

### Time gates: the runs sitting 3 launched

`--job medium`, one job at a time. Before is 8dd746e (the `.scratch/base` worktree), 02:59 to
03:37, under `artifacts/bench/2026-09-23-S61-gates-before`. After is cc0c896's code (the same as
3d88f14), 03:37 to 04:15, under `artifacts/bench/2026-09-23-S61-gates-after`. Filters
`*ManyInputsBenchmarks*`, `*WorkloadBenchmarks.*MatchesToEnd*` and `*SpanOverloadBenchmarks*`.
Sitting 3 was still working during the after run, so these times are a strong hint, **not the
verdict**. The orchestrator's quiet-machine run after 06:40 decides. Bytes are exact either way.

| Row | Before | After | Time ratio | Bytes before | Bytes after |
|---|---|---|---|---|---|
| ManyInputs.CaseFoldAccented | 324.7 ms | 283.1 ms | 0.87 | 91,200,000 | 0 |
| ManyInputs.FuzzyPhraseOneEnhanced | 2,523.7 ms | 2,337.1 ms | 0.93 | 100,246,712 | 15,500,496 |
| ManyInputs.FuzzyPhraseOneIsMatch | 2,425.6 ms | 2,376.5 ms | 0.98 | 96,872,864 | 0 |
| ManyInputs.FuzzyPhraseOneMatch | 2,433.3 ms | 2,267.3 ms | 0.93 | 96,872,864 | 12,126,648 |
| ManyInputs.FuzzyPhraseThreeAlternation | 6,950.4 ms | 6,719.7 ms | 0.97 | 96,988,752 | 0 |
| ManyInputs.FuzzyPhraseThreeNamedList | 7,119.0 ms | 6,501.9 ms | 0.91 | 96,989,480 | 0 |
| ManyInputs.FuzzyPhraseThreeSeparatePasses | 8,027.3 ms | 6,917.8 ms | 0.86 | 290,579,624 | 0 |
| ManyInputs.ParseLogLines | 84.4 ms | 65.1 ms | 0.77 | 217,600,000 | 44,800,000 |
| ManyInputs.RedactDigits | 78.0 ms | 66.0 ms | 0.85 | 122,438,152 | 32,838,152 |
| ManyInputs.ValidateEmails | 86.1 ms | 58.5 ms | 0.68 | 188,710,960 | 0 |
| Workload.EnumerateMatchesToEndDense | 87.2 ms | 2.8 ms | 0.03 | 21,796,464 | 3,017,968 |
| Workload.MatchesToEnd | 58.6 ms | 59.2 ms | 1.01 | 35,083,854 | 35,082,956 |
| Workload.MatchesToEndDense | 6.12 ms | 6.13 ms | 1.00 | 3,543,497 | 3,542,602 |
| SpanOverload.StringShort | 177 ns | 82 ns | 0.46 | 912 | 0 |
| SpanOverload.StringKilobyte | 171 ns | 125 ns | 0.73 | 912 | 0 |
| SpanOverload.StringMegabyte | 34.9 us | 36.8 us | 1.06 | 912 | 0 |
| SpanOverload.CountStringMegabyte | 1,142.6 us | 1,110.3 us | 0.97 | 768 | 0 |
| SpanOverload.SpanShort | 182 ns | 97 ns | 0.53 | 1,064 | 152 |
| SpanOverload.SpanKilobyte | 231 ns | 212 ns | 0.92 | 3,096 | 2,184 |
| SpanOverload.SpanMegabyte | 411.9 us | 526.1 us | **1.28** | 2,098,463 | 2,097,483 |
| SpanOverload.CountSpanMegabyte | 1,387.0 us | 1,641.0 us | **1.18** | 2,098,298 | 2,097,480 |

`MemoryMegabyte` (30.1 us, 0 B) and `CountMemoryMegabyte` (1,087.7 us, 0 B) are new in step D and
have no before row. Compare with `python .scratch/cmp_gates.py`, which reads both folders'
`*-report-full-compressed.json`.

The two rows over S58's 1.13x time floor both copy a megabyte span into a string, and that copy
did not change: their bytes are the same before and after. `StringMegabyte`, which does the same
engine work over the same text without the copy, is 1.06x. So the rise is most likely in the
copy's garbage collection and not in `CharAt`, but a busy machine cannot tell the two apart. The
after run's standard deviation on `SpanMegabyte` is 54 us, 10% of its mean.

### How the orchestrator decides each open time gate

Run both builds, 8dd746e and the merged S61, back to back on the quiet machine, `--job medium`,
and read the time ratio after / before against S58's noise floor of 1.13x.

- **Step A, one state per lazy walk** (da66f01). Filter `*WorkloadBenchmarks.*MatchesToEnd*`.
  Keeps if `EnumerateMatchesToEndDense` is below 1.0x and `MatchesToEnd` and `MatchesToEndDense`
  are at or below 1.13x.
- **Steps B and C, predicates build no Match and the pattern keeps one state** (a125fd0). Filter
  `*ManyInputsBenchmarks*`. Keeps if all ten rows are at or below 1.13x.
- **Step D, span option (a)** (cd0c3d1, with 3d958a2's overload attribute). Filters
  `*SpanOverloadBenchmarks*` and `*ManyInputsBenchmarks*`, and the rows above from
  `*WorkloadBenchmarks.*MatchesToEnd*`. The owner's rule (DECISIONS 2026-09-22) is "flat within
  S58's noise floor: land it; slower beyond the floor: do not land it; bring the owner the
  numbers". Keeps if every row that exists in both runs is at or below 1.13x. If any row is above
  1.13x, including `SpanMegabyte` or `CountSpanMegabyte`, step D is not landed: the orchestrator
  brings the owner that table and the explanation above, and the owner decides.

No step reverts cleanly on its own, because later commits build on each one. So a failed gate is
recorded here with its numbers, and the next S61 sitting takes the step back out, rather than the
orchestrator reverting a commit by hand.

### The reset proven over a wave

`OracleWaveTests.A_pattern_that_has_answered_before_answers_every_row_as_a_fresh_one_does` asks
each row twice: on a fresh pattern, and on one that first ran `Matches` over the previous row's
subject joined to this row's. Sitting 3's version caught `Exception` and discarded it, which
ERP022 refuses; the walk's exception is now counted in the failure message instead.

`tools/run-oracle.ps1`, default generators, 300 rows each, Release:

- Seed 7: 31 of 31 tests pass, diverge 0 of 6680.
- Seed 4242: 31 of 31 pass, diverge 0.
- Seed 20260923: the new test passes. Two others fail, on rows that are not S61's.
  `The_wave_agrees_with_upstream` diverges on 2 rows, 3752 and 5185, which sitting 1 showed at
  8dd746e too. `The_lazy_walks_answer_exactly_what_the_eager_ones_do` fails on 3752 alone: eager
  `Split` times out on its whole-scan clock where the lazy walk times each step. Both rows are
  S87's (planned on main in 0c2679c).
- Seed 99 (used only by the controls below) diverges on row 4957 (partial):
  `(?r)\s+(?:[[:alpha:]]+?(*SKIP)\p{L}|\W)` over `A` + four U+1D518 + CR, flags 0x410a, V0,
  partial search. Upstream gives (0, 0); the port gives (0, 9). The same wave replayed on
  8dd746e (`.scratch/base`, `run-oracle.ps1 -SkipRecord`) gives the same divergence, so it is not
  S61's. It looks like the family of row 5185 (partial, `(*SKIP)`) and has not been judged.

Controls, each on the committed code with one line removed from `MatchState.Init`, run with
`pwsh -File tools/run-oracle.ps1 -Seeds "7,99"` (300 rows per generator) and then restored:

- `ReqPos = -1;` removed: the new test fails on 47 of 6594 rows at seed 7 and 41 of 6592 at seed
  99. The wave test also fails (150 and 145 rows), because a new state's default of 0 is wrong
  too, but the new test's failure is its own: it compares two runs that both lack the reset.
- `Array.Clear(FuzzyCounts);` removed: no failure at either seed.
- `ClearGroups();` removed: no failure at either seed.

The last two cannot fail any test that asks a question, because `MatchState.InitMatch` (which
upstream calls before every attempt) resets the groups and the fuzzy counts again. They are the
belt-and-braces lines sitting 1 found; `MatchStateCacheTests` still checks them field by field.

### Blind review of cc0c896 to 865f612

One Opus pass: **no defects found.** Build clean; oracle at seed 7 31 of 31 tests, diverge 0. The
reviewer ran its own controls with `-SkipRecord` on the seed 7 wave: without `ReqPos = -1` the new
test fails on 47 of 6594 rows, and without `MustAdvance = false` on 466 of 6594. Without
`MaxErrors = 0` it still passes, because every best-match and enhance-match path sets `MaxErrors`
first (`Matcher.cs:10390`, `:10527`, `:10878`, `:11276`). It checked every mutable field of
`MatchState`, `GroupData`, `RepeatData` and `GuardList` against `Init`, confirmed the dirtying walk
rents from the same instance's cache (`Iteration.cs:149`), and reproduced all 21 rows of the gate
table from the JSON. Findings raised 0, reproduced 0, fixed 0; no second pass needed.

### What is left for S61

1. **Time gates** (above), decided by the orchestrator's quiet-machine run.
2. **`EnumerateMatches(ReadOnlySpan<char>)` returning a `ValueMatchEnumerator`**, signed off on
   2026-09-22 and gated on a measured gain. Not started. It adds public API, so it needs
   `tools/update-public-api.ps1`, the documents the slice file lists, and its own blind pass.
3. Update the benchmark baseline (`tools/compare-benchmarks.ps1 -UpdateBaseline`) from the
   quiet-machine run, then the closing notes and the move to `done/`.
4. Hand rows 3752, 5185 and 4957 to S87 (4957 is new here).
5. Ledger entry 18 is S86's, handed over in writing on 2026-09-23 (50d0aec).

## Sitting 5 (2026-09-23, from about 21:30)

Worked to the owner's decisions of 2026-09-23 (DECISIONS, f8c7056), relayed by the orchestrator.

### Time gates A to C

The orchestrator's quiet run, 06:46 to 07:16 on 2026-09-23, reported steps A, B and C **KEEP**.
Step D was flat or faster on every row except `SpanOverload.SpanMegabyte`, 1.36x (354 us to
481 us), and `CountSpanMegabyte`, 1.09x. The owner chose option (c): keep step D and make the span
overloads copy into a pooled buffer.

### Step D, option (c): 1fa48a9

The `ReadOnlySpan<char>` overloads of `IsMatch` and `Count` now rent a `char[]` from
`ArrayPool<char>.Shared`, copy the span into it, run the memory path over it and return it in a
`finally`. Internal overloads take the pool, so `PoolDisciplineTests` can pass its tracking pool
(now `TrackingPool<T>`) and prove the return on a match, a miss, an empty subject, a timeout and a
cancelled token.

Bytes, from `--filter "*SpanOverload*" --job short --inProcess --memory` on a busy machine (so the
times are not evidence):

| Row | Before | After |
|---|---|---|
| SpanMegabyte | 2,098,060 B | 1 B |
| CountSpanMegabyte | 2,098,135 B | 8 B |
| SpanKilobyte | 2,184 B | 0 B |
| SpanShort | 152 B | 0 B |

`AllocationTests` pins a warm megabyte `IsMatch` plus `Count` at 0 B (4,194,352 B before).
Control: in `IsMatch(ReadOnlySpan<char>, ArrayPool<char>, ...)`, replacing `pool.Return(copy);`
with `_ = copy;` fails the three `IsMatch` cases of the new pool tests (3 failed, 11 passed).

**The time gate for this is still open.** The orchestrator re-reads `*SpanOverload*` against
8dd746e on a quiet machine; if `SpanMegabyte` is still above 1.13x, only the span-overload part of
step D comes out.

### Item 2: the span walk, 05019f0

`FuzzyRegex.EnumerateMatches(ReadOnlySpan<char>, TimeSpan?, CancellationToken)` returns a
`ValueMatchEnumerator` (a `ref struct`) of `ValueMatch` values, each holding `Index` and `Length`.
It is the shape `Regex.EnumerateMatches(ReadOnlySpan<char>)` has. The loop is
`Iteration.Enumerate`'s, one turn per `MoveNext`, over the pattern's cached state and a pooled copy
of the span. Both go back when `MoveNext` returns `false` or throws, or on `Dispose`.

The owner's sign-off said this shape walks a span "with no copy of the subject at all". That is
not achievable while `MatchState.Text` is a `ReadOnlyMemory<char>`: the span still has to be
copied, once per walk, into the pooled buffer. What the walk does achieve is no `Match` objects
and no allocation of its own. The search can still allocate, the same amount as the span `Count`
(review probes, below).

A copy of the struct shares the state. `MatchState.Lease` counts hand-backs, and the enumerator
acts only while the count is the one it saw, so disposing a copy a second time does nothing, and
neither does stepping it after the state has been lent to another call. `MatchStateCacheTests`
leaves `Lease` out of its field comparison, since a rented state's count is higher by design.

Bytes, `--filter "*WorkloadBenchmarks.EnumerateMatches*ToEndDense*" --job short --inProcess
--memory`, busy machine: `EnumerateMatchesToEndDense` 3,017,968 B, `EnumerateMatchesSpanToEndDense`
16 B. The allocation half of the gate is met; the time half waits on the orchestrator's quiet run.
`AllocationTests.A_warm_span_walk_over_many_matches_allocates_nothing` pins a warm 20,000-match
walk at 0 B.

Controls, each run against the affected test classes and then restored:

- In `ValueMatchEnumerator.Dispose`, `_pool.Return(_copy!);` replaced with `_ = _pool;`: six tests
  fail (the five new pool tests that reach `Dispose` and the allocation test).
- In the same method, `if (_state is null || _state.Lease != _lease)` reduced to
  `if (_state is null)`: the copied-enumerator test fails ("Expected found to be 3 ... but found
  0").

Also fixed: the string `EnumerateMatches` remarks said each step builds its own engine state,
which step C ended.

Ratchet GREEN at 6768/6768; oracle GREEN at three seeds; `run-aot-tests.ps1` 6765 passed, 3
skipped; `run-aot-smoke.ps1` GREEN.

### Blind review of 05019f0 (with the wording fixes after 1fa48a9's review)

One Opus pass. Findings raised 1, reproduced 1, fixed 1: `docs/GUIDE.md` said a warm span walk
allocates nothing, which is false for `(?b)`, `(?e)` and `(?p)` patterns (reproduced with the
reviewer's probe, `.scratch/review-vme`: walk and span `Count` both 5,392 B, 7,152 B and 1,464 B).
The GUIDE, the DECISIONS line and these notes now name the three modes. The reviewer found the
code clean: 896 pattern and subject pairs (56 patterns, including reverse, empty matches,
surrogates, fuzzy, `(?b)`, `(?e)`, `(?p)`, `\G` and lookbehind) matched `Matches` exactly, whole
and sliced; no stale state or subject after a break, timeout, cancel, or a junk-filled pool buffer;
the lease guard held; 4000 parallel walks and calls gave no wrong answers; overload binding is
unchanged for strings and null. Full suite 6768/6768.

Second pass, over the GUIDE fix only. Findings raised 2, reproduced 2, fixed 2, with
`.scratch/review-vme2` (warm walk, `GC.GetAllocatedBytesForCurrentThread`):

- Non-fuzzy patterns allocate over text holding a character outside the BMP, growing with length:
  `\w+`, `cat`, `x` and `(\w)\1` over `"cat 😀 "` x 200 give 416 B, and 0 B over ASCII. Fuzzy
  patterns give 0 B there. Not investigated; it is the engine's, since `Count` pays the same.
- `(?b)` and `(?e)` allocate only on a fuzzy pattern (`(?b)\w+` and `(?e)cat` 0 B); `(?p)`
  allocates on plain ones too (`(?p)\w+` 3,360 B over 560 characters).

The GUIDE now says the walk allocates nothing of its own and names those three cases.

Third pass, over that rewording: **no defects found.** Its probe (`.scratch/review-vme3`, 29
patterns over four texts up to 1.2M characters) found the walk, `Count(string)` and
`Count(span)` allocating identical bytes in every row, each named case allocating, and the
qualifiers holding (`(?b)cat` 0 B, a fuzzy pattern over emoji text 0 B).

Open observation, not a defect of this slice: a non-fuzzy search over text outside the BMP
allocates in proportion to the text, and a fuzzy one does not. Worth a look in a later
optimisation slice.

### Worktree hooks

The first commit here failed with `.husky/_/husky.sh: No such file or directory`: this worktree
had never run the hook install. `dotnet husky install` fixed it.
