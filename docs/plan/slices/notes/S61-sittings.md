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
