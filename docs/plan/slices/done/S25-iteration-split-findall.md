---
slice: S25
phase: 3
title: Iteration - Matches, Count, NextMatch, Split, and overlapped matching
delivers: [find-all, splitting, overlapped]
---

# S25 - Iteration: Matches, Count, NextMatch, Split, and overlapped matching

Needs S24 (the scan-advance and empty-match policy land there; this slice reuses them). The last
behaviour slice of the phase: everything that walks a subject producing more than one match.

## Scope

All line references are `upstream/src/_regex.c`.

- **The scanner**: `scanner_search_or_match` (`:20874`), `scanner_iternext` (`:20943`),
  `pattern_scanner` (`:21072`), `pattern_findall` (`:22360`), `pattern_finditer` (`:22490`).
  Public shape: `Matches` returning `MatchCollection` (lazy like .NET's, or eager - follow what
  S01's stubs and the ported tests already assert), `Match.NextMatch`, `Count`. The `overlapped`
  argument (next scan starts one past the previous *start*, not past its end) is a scanner
  parameter, not a separate engine - `overlapped` (4 tests) delivers here.
- **One `MatchState` per operation, not one per match.** `scanner_search_or_match` (`:20874`)
  keeps one state across the whole scan and upstream's `Scanner_Type` holds it, so this is what
  upstream does anyway - but there is now a second reason, and it is a performance cliff rather
  than a style point. Since the 2026-09-01 quadratic fix, building a `MatchState` costs one
  vectorised pass over the subject (`OneUnitPerCharacter`), and on a subject holding a surrogate
  pair the first character-count conversion costs a second pass to build `CharacterIndex`. Both
  amortise to nothing across a scan that shares one state, and both become per-match costs if
  `Matches`, `Count` or `Split` creates a state per match - which makes a find-all over a long
  subject quadratic again by a new route, with every correctness test still green.
  `Substitution.Subx` already does it correctly, one state then a loop: copy that shape.
  DECISIONS 2026-09-01.
- **The splitter**: `next_split_part` (`:21135`), `pattern_split` (`:22235`), `splititer`
  (`:22354`). Public: `Split(input, maxSplits)`, returning captured groups interleaved as
  upstream does, with `null` for an unmatched group's slot (the `string?[]` surface S01 stubbed).
  **Zero-width split is version-dependent**: V0 refuses a zero-width match as a split point
  (upstream warns/errors historically - pin current behaviour against the oracle), V1 splits on
  it; the `zero-width` test area exercises exactly this.
- **Upstream types deliberately not ported** (record in PORTMAP): `Scanner_Type` /
  `Splitter_Type` as public objects and their `copy`/`deepcopy` (`:20961-21014`,
  `:21264-21466`) - PORTMAP already records `Scanner` as not ported; the *iteration logic* is
  what this slice ports, surfaced as `MatchCollection`/`Split` instead.

## Verification

- **Un-skip** `needs:find-all` (37), `needs:splitting` (23) and `needs:overlapped` (4), reading
  each skip's prose first; the `FindAll`, `Splitting`, `Overlapped` and `ZeroWidth` STATUS areas
  are the scoreboard. Stragglers retag with prose.
- **Confirm the scan is linear in the subject.** A find-all that builds a `MatchState` per match
  passes every correctness test and only shows up as time, so time it: `Matches` over a long
  subject at two sizes, and check that doubling the subject doubles the work rather than
  quadrupling it. Cheapest decisive version is the one the repeat guards use - a subject big enough
  that a quadratic cannot finish inside a 20-second match timeout.
- **Oracle wave**: patterns with zero-width alternatives over subjects that produce adjacent and
  empty matches, compared as the full match *sequence* (spans and groups per match) for
  finditer/findall semantics; split outputs compared element for element including `null` slots
  and `maxSplits`; overlapped sequences compared likewise. Run under V0 and V1 both. Zero
  divergences; negative control.

## Done when

- [x] The three tags delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green comparing full sequences under both versions; counts quoted.
- [x] `docs/PORTMAP.md` updated, including the not-ported scanner/splitter objects.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: the empty-match advance diverging
      from S24's (they share it upstream - they must share it here), an overlapped scan
      restarting at `end` instead of `start + 1`, a split dropping the final trailing empty
      part), commit.

---

## Closing notes (2026-09-01)

**What landed.** `Engine/Iteration.cs`, the port of upstream's three iteration loops, and the five
public entry points that had been stubs since S01: `FuzzyRegex.Matches` (instance and static),
`FuzzyRegex.Count` (three overloads), `FuzzyRegex.Split` (instance and static), `MatchCollection`
itself, and `Match.NextMatch`. `needs:partial` is now the only `NotImplementedException` seam left
on the public surface.

**Tags delivered: all three, no stragglers.** 169 `[Skip]` attributes removed - 117
`needs:find-all`, 38 `needs:splitting`, 14 `needs:overlapped` - and every one of the tests behind
them passes. Suite 5715 total, 5161 passing, 554 skipped, 0 failing; overall parity **71.8%** (was
59.3%), 1412 of 1966 ported upstream tests. Eleven areas reached 100%, seven of them in this slice:
`FindAll`, `Splitting`, `Overlapped`, `Boundaries` (was 58.5%), `CharacterClasses` (was 25.0%),
`UnicodeProperties` (was 74.3%), `Grapheme` (was 40.0%), plus `Anchors` and `Groups` finishing off.
`Reverse` 86.5% (was 10.8%), `ZeroWidth` 64.3% (was 7.1%), `Regressions` 43.1% (was 27.8%). The
areas that were not the slice's own moved because so many of their tests only *reported* through
`Matches` or `Split`.

**Three ported tests were wrong, and upstream says so.** All three failed against a correct engine
and were fixed with the measurement quoted at the assertion (DECISIONS 2026-09-01):

1. `WordBoundaryTests` #6 dropped the trailing `""`. `pattern_split` appends the segment after the
   last match "even if empty" (`:22330`), and upstream's own assertion carries it
   (`test_regex.py:1566`). Measured: `regex.split(r"(?V1w)\b", "can't aujourd'hui l'objectif")` is
   `['', "can't", ' ', "aujourd'hui", ' ', "l'objectif", '']`.
2. and 3. `RegressionsKeepMarkerTests` #165 and #168 asserted on `m.Value` where upstream's
   `findall` yields **the one group's** text when the pattern has exactly one (`:22440`). That
   asserted the opposite of what the upstream assertion is *for*: `\K` shrinks the match without
   shrinking the group. Measured: `regex.findall(r'(\w\w\K\w\w)', 'abcdefgh')` is
   `['abcd', 'efgh']` where `[m[0] for m in finditer(...)]` is `['cd', 'gh']`.

**Two things the slice file expected that do not exist.**

- **There is no V0/V1 split difference in this release.** `state->version_0` is written by
  `state_init` (`:18482`) and read **nowhere** in the whole of `_regex.c` - the same shape S24
  found for `pattern_subx`. Measured over four V0/V1 pairs, every one identical, and pinned in
  `Gaps/Engine/IterationTests.cs` so the next slice does not go hunting. `state->visible_captures`
  is the same: written, never read, on both sides.
- **`findall` and `finditer` do not need two loops.** Upstream writes them separately and only
  `pattern_findall` carries the `slice_start <= text_pos <= slice_end` guard, so folding them is a
  real risk. It is measured, not argued: the two agree element for element on nine
  subject/pattern pairs, overlapped and not, including the reverse and zero-width cases where the
  guard is what ends the scan. One `Iteration.Scan` with a per-match callback.

**The post-match advance is one method, and that was the point.** `MatchState.AdvancePastMatch`
is called by the scanner, the splitter, `Substitution.Subx` and `Match.NextMatch`. Upstream spells
the rule in each of the four places, and the last two carry only the non-overlapped half because
they never set `overlapped`, so the shared method reduces to exactly the line each replaced. The
slice's own review hunt was for these four drifting apart; sharing them makes that unrepresentable
rather than merely unlikely.

**The overlapped step is one codepoint from the match's START.** Both halves are easy to get wrong
and both are pinned. Measured: `regex.finditer('..', three astral characters, overlapped=True)`
gives codepoint spans (0, 2) and (1, 3), so it is `NextPos`/`PrevPos` and not `± 1`; and it steps
from `match_pos`, so `'..'` over `'abcde'` gives ab, bc, cd, de. A step off either end of the slice
is caught by `MatchState.IsInSlice`, which is upstream's own loop condition, so there is no separate
bounds check.

**`maxSplits` is inverted against upstream at both ends**, as S25 was warned. Upstream's 0 is "no
limit" and its negative is "no splits at all"; this surface is -1 and 0. The `iteration` generator
draws a negative limit deliberately, so a one-ended translation is a RED rather than a latent bug -
Control D below is exactly that mistake.

**`Match.NextMatch` carries the slice, not just the match end.** `pos` moves `slice_start`, so a
state rebuilt from the match end alone would let a `\B` - or, from Phase 4, a lookbehind - at the
resumption point read a subject that starts there. Measured:
`regex.compile(r'\Bb').finditer('abab', 1)` gives (1, 2) and (3, 4), so what is before `pos` is
still readable, while `regex.compile('^b').finditer('abab', 1)` gives nothing, so a slice really is
narrower than the subject and cannot be guessed. It carries `overlapped` for the same reason, and a
gap test walks six pattern/subject pairs asserting the `NextMatch` walk equals the `Matches`
sequence.

**Two marked corner cuts, both `ponytail:` with a named ceiling.** `MatchCollection` is eager where
the built-in one is lazy: `IReadOnlyList` promises a `Count` no lazy scan can answer without running
to the end, and the state owns rented buffers a half-enumerated iterator would never return. And
`Match.NextMatch` builds one state per call, which is the cliff DECISIONS 2026-09-01 describes,
confined to the one entry point that cannot avoid it - measured at 209 seconds for 640,000 matches
where `Matches` takes 0.14.

**The linearity check is a ratio, and it had to be.** A wall-clock ceiling failed the ratchet on its
first run: the same three scans cost 2.1s alone and 7.5s alongside the suite's other 5,700 tests.
So the test is `[NotInParallel]` and asserts the ratio between two subject sizes, which does not
depend on machine speed. Measured in Debug inside the full suite: 468ms at 480,000 code units and
970ms at 960,000, a ratio of **2.07**, against **10.8** for a scan that builds a state per match
(Release, 2,670ms then 28,718ms, and 208,922ms at 1,920,000). Threshold 5. Note for a later slice:
the engine's own `MatchTimeout` **cannot** enforce this, because a state carries its own start time
and a per-match-state scan resets the budget on every match.

**Oracle: the wave now compares sequences.** Three new operations - `finditer`,
`finditer-overlapped` and `split` - whose recorded outcome is the whole list, because a scan that
finds the right matches in the wrong order or stops one match early agrees on every individual
match. Plus a twelfth generator, `iteration`, now in `run-oracle.ps1`'s default list. Final runs,
after the last code change:

- `iteration` alone, 600 rows: `agree 600  unsupported 0  diverge 0` at seed 7 and at seed 4242.
- All twelve generators, 1500 rows each: `agree 18000  unsupported 0  diverge 0` at seed 606.

### Negative controls

Six, all fired, all re-run at the end against the code and the generator in this commit and at a
second seed. Every one is `pwsh -File tools/run-oracle.ps1 -Generator iteration -Count 600 -Seed <seed>`;
`.scratch/run-controls.py` applied each mutation, ran both seeds and reverted, so all twelve figures
come from one clean pass whose two baseline runs were `600 agree, 0 diverge`.

> **Control A, `overlapped-from-end`**: in `Engine/MatchState.cs`, `AdvancePastMatch`, change
> `            TextPos = Reverse ? PrevPos(MatchPos) : NextPos(MatchPos);`
> to
> `            TextPos = Reverse ? PrevPos(TextPos) : NextPos(TextPos);`
> Wave: `iteration`, 600 rows, seed 7. Result: 546 agree, **54 diverge**. Seed 4242: **72 diverge**.

> **Control B, `split-tail-dropped`**: in `Engine/Iteration.cs`, `Split`, wrap the two tail lines
> `        // Get segment following last match (even if empty).`
> `        list.Add(state.Reverse ? input[..lastPos] : input[lastPos..]);`
> so that the `list.Add` sits inside
> `        if ((state.Reverse ? lastPos : input.Length - lastPos) > 0)` with braces.
> Wave: `iteration`, 600 rows, seed 7. Result: 497 agree, **103 diverge**. Seed 4242: **98 diverge**.

> **Control C, `always-advance`**: in `Engine/MatchState.cs`, `AdvancePastMatch`, change
> `            MustAdvance = TextPos == MatchPos;`
> to
> `            MustAdvance = true;`
> Wave: `iteration`, 600 rows, seed 7. Result: 508 agree, **92 diverge**. Seed 4242: **100 diverge**.

> **Control D, `split-limit-one-end`**: in `Engine/Iteration.cs`, `Split`, change
> `        int maxSplit = maxSplits < 0 ? int.MaxValue : maxSplits;`
> to
> `        int maxSplit = maxSplits <= 0 ? int.MaxValue : maxSplits;`
> Wave: `iteration`, 600 rows, seed 7. Result: 579 agree, **21 diverge**. Seed 4242: **26 diverge**.

> **Control E, `overlapped-code-unit-step`**: in `Engine/MatchState.cs`, `AdvancePastMatch`, change
> `            TextPos = Reverse ? PrevPos(MatchPos) : NextPos(MatchPos);`
> to
> `            TextPos = Reverse ? MatchPos - 1 : MatchPos + 1;`
> Wave: `iteration`, 600 rows, seed 7. Result: 567 agree, **33 diverge**. Seed 4242: **42 diverge**.

> **Control F, `split-null-as-empty`**: in `Engine/Iteration.cs`, `GetGroup`, in the
> `group.Current < 0` arm, change
> `            return null;`
> to
> `            return "";`
> Wave: `iteration`, 600 rows, seed 7. Result: 539 agree, **61 diverge**. Seed 4242: **47 diverge**.

Two things about running these that cost a turn each, so that the next slice does not pay them
again. **Format the mutated file before the wave**: `IDE0055` is an error here, so an unformatted
mutation fails the *build* and the harness reports RED with no verdict line at all, which looks
exactly like a control that fired. `.scratch/run-controls.py` runs
`dotnet csharpier format src/FuzzyRegex` after every write and after every revert. And **do not
pipe a long oracle run into `grep`**: it buffers, so a run that is still going and a run that hung
are indistinguishable. Redirect to a file and poll it.

**A seventh control is worth recording even though it has no number.** Removing the empty-match rule
outright - `MustAdvance = false` in the non-overlapped arm rather than `true` - does not produce a
divergence count, it produces a **scan that never terminates**: the first zero-width match repeats
at its own position for ever. That is what `must_advance` is load-bearing for, it is detected
immediately, and it is why Control C uses the terminating mutation instead. Do not spend a turn
re-running it expecting a figure.

### Review

One blind pass over the whole diff, dispatched with the `VERIFICATION.md` brief and the six failure
modes above as the hunt list, then read inside the same turn. **Findings raised: 0**, so none to
reproduce and none to fix. It ran the suite, the ratchet and the oracle, and went further than
asked: four differential probe sets of its own (finditer with `pos`/`endpos`, which the wave never
passes; the same over astral subjects; a 9,000-row `NextMatch`-walk comparison against upstream's
`finditer`; a 4,000-row split comparison including branch reset), a 59,616-case boundary sweep over
extreme `beginning`/`length` values, and five mutations of its own against the harness to check the
comparison was as strong as it looked. All clean. Its verdict: "No defects found."

**No second pass was needed**, because there were no fixes: nothing changed after the reviewer
looked except this file, `STATE.md`, `DECISIONS.md` and one line-ending normalisation. Two
operational notes, both about the reviewer rather than the code:

- **It left `Engine/Iteration.cs` with CRLF line endings**, which fails `IDE0055` and therefore the
  build, since `TreatWarningsAsErrors` is on. `dotnet csharpier format src/FuzzyRegex` fixes it, but
  a slice that trusted the reviewer's own green ratchet would have committed a tree that does not
  compile. **Re-run the build yourself after any review pass that edited files**, however clean the
  report.
- **`baseline: 5054` in its report is not a corrupted baseline.** `passingCount` counts distinct test
  ids and the ratchet's "N passing tests recorded" counts results, so 5054 and 5161 describe the
  same run. Half a turn went on chasing that.

### What S26 should know

- **The three new oracle operations are the template for any future sequence-valued answer.** Row
  shape in `tools/record-oracle.py` (`ITER_OPERATIONS`, `LIMIT_OPERATIONS`, `_describe_match`);
  outcome types in `OracleWave.cs` (`MatchesOutcome`, `SplitOutcome`). The limit field is shared
  with substitution and translated in exactly one place, `OracleComparer.OurLimit`.
- **`FuzzyRegex.Limits` is now shared** by `Match`, `Matches` and `Count`. It holds the rule the S16
  blind review had to fix once - resolve `beginning` before adding `length` - so there is one copy
  of it to get wrong rather than three.
- **Phase 3 has no behaviour tag left.** `docs/STATUS.md`'s remaining wins are all Phase 4 and 5:
  `fuzzy-matching` 98, `partial` 82, `lookaround` 61, `recursion` 60, `backtracking-verbs` 34,
  `inline-flags` 29, `fuzzy-counts` 28.
