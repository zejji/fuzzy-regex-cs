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
