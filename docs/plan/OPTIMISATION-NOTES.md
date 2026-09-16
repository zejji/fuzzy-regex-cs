# Known optimisations not yet implemented

Owner request 2026-09-16: keep the list so Phase 7 does not rediscover it. **This file is the
index, the source is the record.** Every deferral in the engine carries a comment at the line, and
the table below is regenerated from them, so a Phase 7 slice starts by running the command and
reading the comments, not by re-deriving what the port skipped.

```
grep -rn -i 'ponytail:\|Phase 7' src/FuzzyRegex --include=*.cs
```

Rule for slices from here on: a new deferral gets a `ponytail:` or `Phase 7` comment at the line
naming the ceiling and the lift, and one row here. A Phase 7 slice that implements one deletes the
comment and the row in the same commit. Correctness constraints on the optimiser are in
ROADMAP.md ("Phase 7 ports upstream's start optimisations without importing their answers") and
are not repeated here.

## Engine (upstream's own optimisations, deliberately deferred)

| Where | What is deferred | Notes |
|---|---|---|
| `Engine/Matcher.cs:197`, `:4786`, `:7149-7583` | `locate_required_string` and the required-string search (`string_search`, `_fld`, `_ign`, `_rev` variants) | Six "unreachable until Phase 7" arms already ported and waiting. DECISIONS 2026-08-31. **Constraint**: the pinned answers in `Gaps/Engine/BacktrackingVerbTests.cs`, `PartialMatchingTests.cs`, `ReverseMatchingTests.cs` are permanent; the prefilter must honour the slice a verb moved. |
| `Engine/Matcher.cs:388`, `:4718`, `:4772`, `:10046`, `:10157` | `search_start` / `do_search_start` prefilter family | Same constraint. `:10048` marks the underflow site the slice must handle. |
| `Engine/Matcher.cs:5404` | `try_match`'s test-node fast arm | Unreachable until the locator exists. |
| `Engine/Matcher.cs:8894` | The fast path S19 measured and reverted | Reinstate behind a benchmark. |
| `Engine/Matcher.cs:8715` | `string_search_rev` | Part of the locator family. |
| `Engine/Matcher.cs:2765-2787`, `:3005` | Partial-match fast paths | Deferring is semantically neutral; see the class remarks. |
| `Engine/Matcher.cs:2065`, `:9126` | Capture storage reused across runs and across POSIX best-match saves (`SaveBestMatch`) | "A Phase 7 question rather than a correctness one." |
| `Engine/MatchState.cs:641`, `Engine/Node.cs:56` | `search_positions` and node search offsets | Only the prefilters read/write them; not ported. |
| `Engine/Optimiser.cs:508` | The match loop consulting the optimiser's markings | Markings are made so the graph has upstream's shape. |
| `Engine/PatternObject.cs:176` | Required-string case flags naming an opcode | Consumed only by the locator. |
| `Unicode/Encodings.cs:166` | `same_char_ign_turkic` inside `string_search_fld` | Locator family. |
| `Unicode/Encodings.cs:177` | The `*_has_property_ign` encoding-table slot | Its only other caller is `search_start`; prefilter family. |

## API-level and allocation (this port's own ceilings)

| Where | What is deferred | Notes |
|---|---|---|
| `FuzzyRegex.cs:338`, `:873` | `ReadOnlySpan<char>` overloads copy the span to a string | The engine indexes a string; lifting means threading a span through `MatchState` and every `try_match_*`. Biggest allocation win available; decide early in Phase 7 because it touches everything. |
| `Engine/Iteration.cs:216`, `:336` | One `MatchState` per step, for `Match.NextMatch` and for `EnumerateMatches`/`EnumerateSplits` | A walk re-creates state per match, so it costs one vectorised pass over the subject per match where `Matches` costs one in total (DECISIONS 2026-09-01). **It is not simply an oversight to delete**: a state owns rented buffers, so one held across a `yield return` is one an abandoned iterator never returns, and the lift has to solve that - a pooled state released on `Dispose`, or a struct enumerator. A second, visible consequence is that a lazy walk's `timeout` bounds each step rather than the walk (DIVERGENCES, API shape). |
| `Engine/Iteration.cs:487` | `EnumerateSplits` repeats `Split`'s loop instead of sharing it | The two have different state models, so today they cannot share. `OracleWaveTests.The_lazy_walks_answer_exactly_what_the_eager_ones_do` is what stops them drifting; if the per-step state goes, `Split` becomes `[.. EnumerateSplits(...)]` and the duplication with it. |
| `FuzzyRegex.cs` static conveniences (six sites) | No pattern cache: `new FuzzyRegex(pattern, options)` per call | Upstream caches (that is what `purge`/`cache_all` control); .NET caches the 15 most recent static patterns behind `Regex.CacheSize` (Microsoft Learn, best-practices page, read 2026-09-16). Planned as the first Phase 7 slice: a bounded MRU keyed on the raw flags (not on `Options`, see S53b item 2), `CacheSize` property, AOT-safe, and a concurrent test under S52b's contract. |
| `Engine/Substitution.cs:443`, `:618` | Template parsing: one subscript level; integer parsing limited to signed ASCII decimal | Correctness ceilings, not speed; listed so they are not lost. |
| `Unicode/CharacterNames.cs:34` | Character-name table size and lookup speed | "Phase 7 is where size and speed get measured." |
| `Engine/Matcher.cs:8166` | A branch no test pins; deleting it changed nothing measurable (full suite, three seeds, S42's review, four hand-built patterns) and it is kept only against a possible hang on an untested re-entry | Candidate for deletion once Phase 7 can build the re-entry case; write that test first. |

## Measurement notes for Phase 7

- S54 baselines the workloads and the traps. Any optimisation lands behind a BenchmarkDotNet delta on
  the same machine, per the `benchmark` skill.
- Native AOT: the classic .NET regex `Compiled` route (IL emit) is unavailable; the constraint and the
  fast-path alternative are in ROADMAP.md under Phase 7.
- Every optimisation must keep the oracle GREEN at three seeds and `ExpectedDivergences` strict; an
  optimisation that changes an answer has ported an upstream bug (ROADMAP, owner rule 2026-09-12).
