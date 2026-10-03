# Sizing the older known-defects rows (2026-10-03)

Plan item 6 (`docs/plan/2026-09-30-correctness-plan.md`) asks for D26, D29, D36, D38, D39 and D50
to be sized before they are scheduled; D67 is new and is sized here too. Research only: no engine
change was committed.

Reference points: main a8a89b64, the D11 build `maint/d11-partial-boundary` af012080 (Release build
in `.claude/worktrees/d11-build`), upstream `regex` 2026.9.10 (python `import regex`). Port answers
come from `tools/probes/port-probe.cs` (8dda4d9, extended to print every group), built Release.
"P(a,b)" is a partial match, "F(a,b)" a full one. Line numbers are on main unless marked D11.

## Summary

| Row | Reproduces on main? | On D11? | Cause | Goes with | Size | Ruling needed? |
|---|---|---|---|---|---|---|
| D26 | yes | fixed | no insertion is tried after an undetermined fuzzy zero-width test | D11 | S | no |
| D29 | yes (worse) | not checked | time: `FuzzyLiteralFilter.cs:792-794`; allocation: six merges | independent | M | no |
| D36 | yes | fixed | a run-out in a condition test's body ends the attempt as partial | D11 | S | no |
| D38 | `{i<=1}`: yes; `{e<=1}`: phantom MatchAtStart | `{i<=1}` fixed; `{e<=1}` phantoms remain | a blocked condition test is treated as able to fail when a later alternative of its body succeeds | D11 | M | no |
| D39 | yes | yes | a run-out inside a lookahead's body presumes the rest; D19's planned checks do not remove it | D19 | L | no |
| D50 | yes | yes | a call's copy compiles the group as parsed, before Optimise and PackCharacters | independent | M | no |
| D67 | yes | yes | a repeat's look at its tail returns the partial before the enclosing group closes | independent, after D11 | M | no |

## D26: fuzzy insertion after an undetermined zero-width test

Witnesses, partial, over 'a' (the non-partial witness 'a:' matches (0,2) with one insertion
everywhere):

| pattern | op | main | upstream | D11 |
|---|---|---|---|---|
| `(?:a$(?<=:)){i<=1}` | match | None | None | P(0,1) |
| `(?:a(?m:$)(?<=:)){i<=1}` | match | None | None | P(0,1) |
| `(?:a$(?<=:)){e<=1}` | match | None | None | P(0,1) |
| `(?r)(?:(?=:)(?m:^)a){i<=1}` | search | P(0,0) | P(0,0) | P(0,1) |

- **Cause.** A fuzzy zero-width test that passes undetermined at the edge offers no insertion.
  D11 chunk 6's repair (b734b23a) pushes an `UndeterminedFuzzyRetry` entry (D11 `Matcher.cs:12697`,
  handler `:16890`); its answers match the witnesses on every form. Confidence: high.
- **Goes with:** D11. Nothing is left to build. No test in the D11 suite uses these four patterns
  (searched for `$(?<=` and `(?m:$)(?<=`), so the merge should add them as regression tests.
- **Size:** S, for the tests and the register move at D11's merge. **Ruling:** none.

## D29: performance regression

The original measurement was a BenchmarkDotNet A/B (2026-09-29). This re-measure uses a small
harness built against each commit's `src` (Release; `git archive` per commit). It measures:

- allocation from `GC.GetAllocatedBytesForCurrentThread`, which gives the same result on every run;
- time as the minimum of 15-21 runs, run in an interleaved order on a machine at 1% CPU.

**FuzzyNoMatchLong** (`(?:needle){e<=1}` IsMatch over 1 MB of ASCII, no match):

| build | min ms (three rounds) |
|---|---|
| 2373e273 (baseline) | 0.19-0.20 |
| 7d39d99d (just before D14) | 0.19-0.20 |
| 1b767484 (D14) | 0.24-0.25 |
| a8a89b64 (main) | 0.22-0.27 |
| main with the change below | 0.20-0.21 |

- **Cause.** D14 (1b767484) moved the ASCII check into `ScanMemory.IsAscii`
  (`FuzzyLiteralFilter.cs:792-794`). There it uses `IndexOfAnyExceptInRange` over ushort, which is
  about three times slower than the `Ascii.IsValid` it replaced. A microbenchmark over 1 MB of ASCII
  measures 20-22 us for `Ascii.IsValid` and 63-65 us for `IndexOfAnyExceptInRange`, the same 40 us
  gap as the table.
- **Proof.** A throwaway patch that calls `Ascii.IsValid` first, and searches for the first
  non-ASCII character only when that check fails, brings main back to the baseline and allocates
  nothing extra. At 8 MB the gap is lost in cache noise. Confidence: high.

**UncachedFuzzyIsMatch** (construct `(?:needle){e<=1}`, then IsMatch over a short sentence):

| step | bytes per call | change |
|---|---|---|
| 2373e273 (baseline) | 11,240 | |
| 58e57e5e fi-sharp-s (ledger 49) | 13,080 | +1,832 |
| 1b767484 D14 | 13,249 | +167 |
| fa42f231 F-A (D1, D2) | 13,657 | +408 |
| eb5124ad D17 | 13,728 | +72 |
| ce904e6c D3 | 14,058 | +330 |
| 0491572d D40 family | 14,168 | +110 |
| a8a89b64 (main) | 14,184 | +16 over later merges |

- **Size of the regression.** Main allocates 1.26x the baseline (the register's 1.18x was measured
  at e67ef400). Construct-and-match time went from 10.0 to 11.7 us at the minimum (about +17%).
  Most of that came at 58e57e5e.
- **Likely cause, not proved.** The fuzzy literal filter's build allocates more since 58e57e5e:
  per-character arrays and LINQ lists in the piece builder (`FuzzyLiteralFilter.cs:323`,
  `:521-524`). The other steps are F-A's memo, D3's guard keys, D17's repeat-state record and the
  D40 copies being set up for a pattern with no calls and no ß. The rule that a feature-free path
  pays nothing says each of these should cost this pattern nothing. Confidence: high on the bisect,
  medium on the attribution inside 58e57e5e.
- **Goes with:** independent.
- **Size:** M. The time fix is S: one fast path, a WorkCounter-neutral change, and a re-run of the
  benchmark gate. The allocation needs a profile and lazy allocation in up to six places.
- **Ruling:** none. The 10% gate already applies.

## D36: a condition whose test reads to the edge

| pattern, match 'ac' partial | main | upstream | D11 |
|---|---|---|---|
| `(x)?(?(?=.*z)a\|a)b(?(1)y)` | P(0,2) | P(0,2) | None |
| `(x)?(?(?=.*z)a\|a)b` | P(0,2) | P(0,2) | None |

- **Cause on main.** `.*z` runs out at the edge inside the condition test's body. The run-out
  returns Partial for the whole attempt (character arm, `Matcher.cs:11074-11077`; upstream
  `_regex.c:12127-12129`), although both branches then fail at 'c'.
- **Fix.** D11 chunk 8 (run-outs in bodies) makes the run-out local to the body. It is already
  pinned on D11 by `PartialUnknownCaptureTests.cs:168-169`. Confidence: high.
- **Goes with:** D11. **Size:** S, the register move at merge. **Ruling:** none.

## D38: partial search misses a start; phantoms in the `{e<=1}` form

| row (partial) | main | upstream | D11 |
|---|---|---|---|
| `(?:(?(?=a\b\|a)a\|b)){i<=1}a` search 'ab' | P(1,2) | P(1,2) | **P(0,2)** (right) |
| same, match 'ab' | P(0,2) | None | P(0,2) |
| `(?:(?(?=a\b\|a)a\|b)){e<=1}a` search 'ab' | P(2,2) (right) | P(2,2) | P(1,2) phantom |
| same, match 'ab' at 0 | P(0,2) phantom | None | P(0,2) phantom |
| same, match 'ab' at pos 1 | - | - | P(1,2) phantom |

- **The `{i<=1}` miss is fixed on D11.** Its red test (`OpenDefectTests.cs:60`) moves at the merge.
- **The `{e<=1}` rows are phantoms under the port's own rules.** On D11, none of 168
  continuations of 'ab' (up to 3 characters of 'abx ', at starts 0 and 1) matches. On main, none of
  780 continuations (up to 4 characters of 'abx :') of the simpler `(?:(?(?=a\b)a|b)){e<=1}a`
  matches.
- **Mechanism.** The condition's test first succeeds through an insertion with `\b` undetermined at
  the edge. When that read is false, the test's next alternative, the deletion that ledger 42
  allows, still succeeds, so the test holds for every continuation. Main raises its phantom through
  `HitEnd` (`NoteBoundaryAtTruncationPoint`, `Matcher.cs:2245-2259`, read at `:16437-16443`). D11
  explores the branch it would take if the test failed.
- **An exact witness of the same fault**, with no fuzzy matching:

  | pattern, match 'ab' partial | main | upstream | D11 design model | D11 port |
  |---|---|---|---|---|
  | `(?(?=ab$\|a)ac\|abc)` | None | None | None | **P(0,2)** |
  | `(?(?=ab\b\|a)ac\|abc)` | P(0,2) | P(0,2) | None | P(0,2) |

  The D11 design model (`d11-direction-model.py`, driven through
  `tools/probes/d19/runout-rest.py row`) answers None for both. So the first row is a phantom the
  D11 build adds against main and against its own design model. For contrast,
  `(?(?=ab$)ac|abc)` over 'ab' is P(0,2) on D11 and in the model, which is right (witness 'abc').
  Main misses it with None.
- **Where to look.** D11's condition-test arm (D11 `Matcher.cs:15590-15650`, M2's blocked-success
  handling). Confidence: high that it is D11's; medium on the exact site.
- **Goes with:** D11, before it merges, because the build is worse than main on this row. It is
  not D18's C4. C4 is the design model's own accepted limit, and the model answers None here.
- **Size:** M. The site needs finding, the fix needs a red test, and the D11 grid must be re-run on
  condition rows. **Ruling:** none. The truth is fixed by the partial rule, and the `{e<=1}` answer
  by ledger 42.

## D39: a repeated lookahead whose body can only end at the edge

| pattern, match partial | main | upstream | D11 |
|---|---|---|---|
| `(?:(?=(?:a\|ab)$)a){2,}b` over 'a' | P(0,1) | P(0,1) | P(0,1) |
| `(?:x(?R)\|(?=(?:a\|ab)$)a)b` over 'xa' | P(0,2) | P(0,2) | P(0,2) |

- **What D19's checks do.** D19's research driver (`runout-rest.py row`, on D11's model) still
  answers P(0,1) for the first row with every check on (mask, rest, rest in bodies). The mask
  prunes two paths, the `a$` ones (C3). The `ab$` path survives: 'b' is consumed inside iteration
  1's lookahead body, and the rest-in-bodies check stops at the end of that body. After the
  lookahead, the outer path continues at an earlier position. Iteration 2's lookahead then needs
  'a' where 'b' was consumed, and no check sees that. The follow automaton treats lookarounds as
  passing.
- **The second row.** The model answers None even at its baseline. Whether it models `(?R)` here is
  unverified, so the D11 port's P(0,2) there is unresolved.
- **Goes with:** D19, as a named limit of its follow automaton (lookahead bodies read ahead of the
  outer path). Neither D18a nor D19 as designed removes it. Confidence: high for the first row,
  low for the second.
- **Size:** L. It needs the follow automaton to carry cells consumed in a lookahead body into the
  outer path, which the research lists as over-approximated. Add both rows to D19's grid as known
  phantoms when it is built. **Ruling:** none; the partial rule decides the truth.

## D50: a call's copy is compiled from the unoptimised group

| pattern, search 'ß' | main | upstream | D11 | written out | main with the change below |
|---|---|---|---|---|---|
| `(?fi)(?(DEFINE)(?P<g1>SS))(?<=(?&g1))` | None | None | None | (1,1) | (1,1) |
| `(?fi)(?(DEFINE)(?P<g1>SS))(?:(?&g1)){i<=1}` | None | None | None | (0,1) | (0,1) |
| `(?fi)(?(DEFINE)(?P<g1>SS))(?&g1)` | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |

The forward exact call is right because it needs no copy.

- **Cause.** `FixGroups` records the group as parsed. Upstream's `Group.optimise` returns a new
  `Group` (`_regex_core.py:3069-3076`), so `defined_groups` keeps the old one, and
  `pack_characters` runs on the new one. A call with other features copies the old group
  (`_regex_core.py:4449`, compiled at `_main.py:637`; port `ParseFunctions.cs:3276-3281`, compiled at
  `PatternCompiler.cs:366`). That copy's `SS` stays two single characters, so it never meets the
  full fold of ß.
- **Proof.** A throwaway patch at `PatternCompiler.cs:366` compiles
  `group.Optimise(info, rev).PackCharacters(info)`. With it, both rows answer as written out.
  Confidence: high.
- **Questions for the builder:**
  - The copy must be optimised in its own direction (`rev`), not the pattern's.
  - `PackCharacters` mutates a `Group` in place, and one parsed group can be copied twice.
  - Check whether any other `Optimise` step changes answers, not just speed.
- **Goes with:** independent.
- **Size:** M. The fix is small; the day goes on a grid (calls x full fold x lookbehind, fuzzy and
  reversed), a pinned divergence (upstream is wrong), a ledger entry and a draft report.
- **Ruling:** none. F34's rule already decides it: a call answers as the group written out there.

## D67: a partial raised while peeking past an optional repeat leaves the enclosing group open

| row, partial | main | upstream | D11 | main with the change below |
|---|---|---|---|---|
| `(?<o>b(?<i>a)?)b` search 'ba' | P(0,2) o unset, i=(1,2) | same | same | P(0,2) o=(0,2), i=(1,2) |
| `(?<o>b(?<i>a)*)b` search 'ba' | same | - | - | P(0,2) o=(0,2), i=(1,2) |

The complete row, 'bab', gives o=(0,2), i=(1,2) everywhere.

- **Cause.** `EndGreedyRepeat` looks at its tail with `TryMatch` (`Matcher.cs:10641-10645`;
  upstream `_regex.c:12591-12594`). The tail starts with o's end and then `b` at the edge, so the
  peek answers Partial. The arm returns that at once, before the main loop reaches o's end.
- **Proof.** A throwaway patch that treats the tail peek's Partial as "the tail may match" and goes
  on gives o=(0,2) for both forms. Confidence: high.
- **Other sites.** The same shape is at all 12 `TryMatch` peeks (`Matcher.cs:9475, 10278, 10608,
  10641, 10809, 10833, 11305, 11327, 11771, 11793, 14629, 14789`) and at upstream's eight body and
  tail pairs. D11 has the same code (D11 `Matcher.cs:12071-12074`).
- **Goes with:** independent. Build it after D11 merges, as a small branch from that merge (as
  D64 plans), because D11 changes what a run-out does on the second pass.
- **Size:** M:
  - each peek site in both directions, and the lazy and one-character repeats;
  - a grid of repeats inside groups under partial matching;
  - a pinned divergence from upstream, with a ledger entry and a draft report.

  The body peek already treats a Partial at its minimum as Failure (`:10613`). The builder should
  check that this special case still holds once the tail peek proceeds.
- **Ruling:** none. D62's rule ("a partial reports what every completion of its path would
  report") already gives o=(0,2).

## Evidence files

Probe rows and outputs were in `.scratch/probes/` of the sizing worktree, and the D29 harness was
in the session scratchpad. Both are deleted after this note. To reproduce, re-run any witness above
with `tools/probes/port-probe.cs` and `python -c "import regex"`.
