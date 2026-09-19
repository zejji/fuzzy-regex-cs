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
| `Engine/Matcher.cs:388`, `:4718`, `:4772`, `:10048`, `:10157` | `search_start` / `do_search_start` prefilter family | Same constraint. `:10048` ("We've narrowed the slice. The required string position might now be outside it") is the underflow site the slice must handle. |
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
| `FuzzyRegex.cs:461`, `:1117` | `ReadOnlySpan<char>` overloads copy the span to a string | The engine indexes a string; lifting means threading a span through `MatchState` and every `try_match_*`. Biggest allocation win available; decide early in Phase 7 because it touches everything. **S58 measured it and corrected both halves of that sentence** (`docs/plan/2026-09-19-span-threading-decision.md`): the copy is 2.0002 bytes per character and 9.6x the call on `IsMatch` at index 0, but only **+6.5%** on `Count` over the same megabyte where the scan is real; and it does not touch every opcode - `Text[` appears at five sites, all in `MatchState`, and the engine reads through one accessor, `CharAt`. The ceiling is `IsMatch` and `Count` alone, because `Capture.Value` needs a string. Owner decision pending; S58 recommends documenting the copy and adding `ReadOnlyMemory<char>` overloads rather than turning on `unsafe`. |
| `Engine/Iteration.cs:216`, `:336` | One `MatchState` per step, for `Match.NextMatch` and for `EnumerateMatches`/`EnumerateSplits` | A walk re-creates state per match, so it costs one vectorised pass over the subject per match where `Matches` costs one in total (DECISIONS 2026-09-01). **S54 measured it and it is the largest number in this file**: a full lazy walk of `\w+` costs **12,643 ms over 1 MB against 111 ms** for the eager `Matches`, and 117 ms over 100 KB against 4.51 ms - 113x, and quadratic, since 10.2x the subject cost 108x the time. Reproduce with `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- sizing`. **It is not simply an oversight to delete**: a state owns rented buffers, so one held across a `yield return` is one an abandoned iterator never returns, and the lift has to solve that - a pooled state released on `Dispose`, or a struct enumerator. A second, visible consequence is that a lazy walk's `timeout` bounds each step rather than the walk (DIVERGENCES, API shape). **S58 attributed the cost and it is not the state's allocation** (`docs/plan/2026-09-19-span-threading-decision.md`): a state costs 1,032 bytes plus 264 per capture group and about 275 ns fixed, FLAT across subject length, plus **0.0321 ns per character** - which is `MatchState.cs:501`'s one vectorised `IndexOfAnyInRange` scan for a high surrogate, 33.6 microseconds per step on a 1 MB subject and over 100x everything else a state does. So pooling or a `ref struct` enumerator removes about 1% of the 12,643 ms and the walk stays quadratic; **the fix is to hoist `OneUnitPerCharacter` (and `GetCharacterIndex`) out of the per-step state**, which needs no API change at all. |
| `Engine/Iteration.cs:487` | `EnumerateSplits` repeats `Split`'s loop instead of sharing it | The two have different state models, so today they cannot share. `OracleWaveTests.The_lazy_walks_answer_exactly_what_the_eager_ones_do` is what stops them drifting; if the per-step state goes, `Split` becomes `[.. EnumerateSplits(...)]` and the duplication with it. |
| `FuzzyRegex.cs` static conveniences (twelve `new FuzzyRegex(...)` sites, counted 2026-09-16; five of them take a caller-supplied named-lists dictionary) | No pattern cache: `new FuzzyRegex(pattern, options)` per call | Upstream caches (that is what `purge`/`cache_all` control); .NET caches the 15 most recent static patterns behind `Regex.CacheSize` (Microsoft Learn best-practices page, and `[Regex]::CacheSize` printed 15 on .NET 10.0.10, both 2026-09-16). `Regex` has no `cache_all` equivalent; `CacheSize = 0` is the `purge`. Planned as the first Phase 7 slice: a bounded MRU keyed on the raw flags (not on `Options`, see S53b item 2), `CacheSize` property, AOT-safe, and a concurrent test under S52b's contract. |
| `Engine/Substitution.cs:443`, `:618` | Template parsing: one subscript level; integer parsing limited to signed ASCII decimal | Correctness ceilings, not speed; listed so they are not lost. |
| `Unicode/CharacterNames.cs:34` | Character-name table size and lookup speed | "Phase 7 is where size and speed get measured." |
| `Engine/NodeCompiler.cs:1430` (`BuildRepeat`'s minimum-count loop) | Counted repeats are unrolled, one copy of the body per repetition, so compile memory grows with the PRODUCT of nested counts at about 250 bytes a node | Ceiling: `(a{1000}){1000}` keeps 1,005,009 nodes and 238 MB; `((a{150}){150}){150}` reached `OutOfMemoryException` in 9 s under a 1 GB cap before S56b, and the `{1000}` cubed form passed 15 GB and crashed a 32 GB machine twice (`docs/plan/2026-09-18-repeat-unrolling-investigation.md`). **S56b bounded it rather than fixing it**: `maxCompiledNodes` refuses the pattern at construction, default a million nodes. The lift is a counting loop instead of copies, and it is **not** a transparent rewrite - every guard and per-iteration effect currently sees distinct nodes, so a slice doing it owes a soundness argument (captures per repetition, fuzzy sections, the group-call guard) plus an oracle wave, not just a benchmark. Post-1.0, not Phase 7 by default. |
| `Engine/Matcher.cs:8166` | A branch no test pins; deleting it changed nothing measurable (full suite, three seeds, S42's review, four hand-built patterns) and it is kept only against a possible hang on an untested re-entry | Candidate for deletion once Phase 7 can build the re-entry case; write that test first. |

## Measurement notes for Phase 7

- **The baselines exist** (S54), in `bench/baselines/<machine-id>/net10.0.json`. A slice measures
  against them with one command, which runs the suite and prints the per-benchmark ratio:

  ```
  pwsh -File tools/compare-benchmarks.ps1
  ```

  It is RED on any benchmark more than 1.25x slower than the baseline - the v1.0 gate's own
  per-workload tolerance - and on a baselined benchmark missing from the run. **Run it rather than
  `dotnet run` by hand**: BenchmarkDotNet finds the benchmark project by searching down from the
  working directory's nearest solution file, and from the repository root that search also finds the
  copy inside every git worktree, so a hand-run dies having executed nothing. DECISIONS 2026-09-16
  has the mechanism; `bench/FuzzyRegex.Benchmarks.slnx` is the fix.
- The traps an optimiser is tempted to special-case are pinned in
  `tests/FuzzyRegex.Tests/Gaps/Engine/OptimiserTrapsTests.cs`, and **those tests are permanent** -
  a Phase 7 slice that turns one red has changed an answer.
- **`(a+)+b` is not the catastrophic shape in this port; `(a|a)*b` is.** Measured S54: `(a+)+b` is
  flat at 0.2-0.7 ms from n=18 to n=24, `(a|a)*b` goes 161 ms to 10.6 s over the same range. Both
  are pinned, so a change that moves a shape between the two classes is visible.
- Native AOT: the classic .NET regex `Compiled` route (IL emit) is unavailable; the constraint and the
  fast-path alternative are in ROADMAP.md under Phase 7.
- Every optimisation must keep the oracle GREEN at three seeds and `ExpectedDivergences` strict; an
  optimisation that changes an answer has ported an upstream bug (ROADMAP, owner rule 2026-09-12).

## Measured by S58's allocation attribution (2026-09-19)

Re-run any figure here with
`dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- attribution`.

| Where | What is deferred | Notes |
|---|---|---|
| `FuzzyRegex.cs:444` (`IsMatch`), `:568` (`Run`'s `visibleCaptures: true`) | A predicate call that does not build the captures it will never be asked for | **`IsMatch` allocates byte-for-byte what `Match` does** - 1,392 B at one group, 9,576 B at 32, identical at every group count measured. `IsMatch` is `Run(...).Success` and `Run` passes `visibleCaptures: true` unconditionally, so 224 of the 264 B per group a match costs are paid to produce a `bool`. Upstream's `state_init_2` takes the same flag for the same reason. **Constraint**: `visibleCaptures` also drives repeated-capture retention, so a slice taking this must prove the oracle green at three seeds - `IsMatch` rows included - rather than assume the flag is only about the returned object. |

**The paired source comment is owed.** The rule above is a `ponytail:` comment at the line plus a
row here; S58 is a measurement slice forbidden from touching `src/`, and its whole verification
claim is that `git diff -- src` is empty, so it recorded the row only. The next slice that is
allowed into `src/FuzzyRegex/FuzzyRegex.cs` adds the comment at `:444` and `:568`. S60 is the first
such slice.

## From the Rust fuzzy-regex library (reviewed 2026-09-18)

`docs/plan/2026-09-18-fuzzy-regex-rs-techniques.md` has the full table. Two items adopted, both
into S60: a rarity gate on the prefilter's skip character (their highest-ROI roadmap item, borrowed
from resharp) and an Aho-Corasick or `SearchValues<string>` path for large `\L<name>` lists behind
a threshold ("Results are identical to the alternation; only the speed differs"). Everything else
there is either already in S59/S60, already deferred here (bounded-repeat unrolling), or rests on
a non-backtracking automaton or a different fuzzy algebra and must not be copied.

## Research sweep of 2026-09-18 (three sources, one note)

`docs/plan/2026-09-18-optimisation-research.md` is the record: regex-automata/RE2/Navarro (§1), .NET
Regex and PCRE2 (§2), literature 2018-2026 and hardware (§3), with every quote and URL. Ten items
went into S60 (8-17), one workload into S58, one verification item into S62. Two bets went to the
owner and, on 2026-09-19, into the plan as experiment slices (spec amendment 28), each free to end
in "reverted, recorded": **`docs/plan/slices/S62b-auto-atomicity-experiment.md`**,
auto-atomicity/auto-possessification (a compile time rewrite), and
**`docs/plan/slices/S62c-memoisation-spike.md`**, selective memoisation of failed positions (Davis
et al. 2021, extended to lookaround and atomic groups by Fujinami and Hasuo 2024), which is the one
published technique that makes a backtracker linear without changing its answers and the fix for
the `(a|a)*b` class; it needs the fuzzy counters in its key and is off for backreferences and verbs.
