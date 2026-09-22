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
