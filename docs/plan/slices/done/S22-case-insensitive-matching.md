---
slice: S22
phase: 3
title: Case-insensitive and full-casefold matching - every IGN and FLD variant
delivers: [ignore-case, case-folding]
---

# S22 - Case-insensitive and full-casefold matching

Needs S17-S21. One cross-cutting dimension in one pass: every `_IGN` (simple folding) and `_FLD`
(full case folding, where one character can fold to up to three) opcode variant, using the
folding tables S09/S10 landed for the parser. `ignore-case` (144) and `case-folding` (69) are the
two biggest remaining tags after quantifiers.

## Scope

All line references are `upstream/src/_regex.c`.

- **Folding, per encoding**: `unicode_simple_case_fold` (`:1997`), `unicode_full_case_fold`
  (`:2009`), `unicode_all_cases` (`:1989`), `unicode_possible_turkic` (`:1984`),
  `unicode_all_turkic_i` (`:2021`); the ASCII counterparts (`:946-1002`). These read
  `UnicodeCasing.g.cs` - the same tables the parser folds with, so parser and engine cannot
  disagree by construction.
- **Comparison helpers**: `same_char_ign` (`:2849`), `same_char_ign_turkic` (`:2871`),
  `in_range_ign` (`:2821`), `matches_CHARACTER_IGN` (`:2918`), `matches_PROPERTY_IGN` (`:2937`),
  `matches_RANGE_IGN` (`:3009`), `matches_member_ign` (`:3085`), the `in_set_*_ign` four
  (`:3177`, `:3218`, `:3257`, `:3295`), `matches_SET_IGN` (`:3334`).
- **Main-switch cases**: `CHARACTER_IGN` (`:12146`), `PROPERTY_IGN` (`:13827`), `RANGE_IGN`
  (`:13937`), `SET_*_IGN` (`:14471-14474`), `STRING_IGN` (`:14989`), `STRING_FLD` (`:14776`),
  `REF_GROUP_IGN` (`:14262`), `REF_GROUP_FLD` (`:14060`).
- **Backtrack cases**: the IGN rows of the one-character block (`:15210-15243`), `STRING_IGN` /
  `STRING_FLD` in both REPEAT_ONE sub-switches (`:16143`, `:16045`, `:16852`, `:16741`), the
  backreference rows (`:17269-17276` IGN, `:17291` FLD).
- **FLD is the hard half**: a folded comparison consumes different lengths on each side
  (`STRING_FLD` walks the pattern's folded characters against the subject's full case folding,
  where the ligature and sharp-s families expand). `partial_string_match_ign` (`:11683`) only as
  far as non-partial matching reaches it. `match_many_CHARACTER_IGN` (`:3861`) and the other
  `_IGN` bulk steppers for the REPEAT_ONE paths.
- `Sequence._fix_full_casefold` (parser, S10) decided which literals became FLD chunks; this
  slice is where those opcodes first execute, so a divergence here may implicate either side -
  the corpus already pins the parser's half, so suspect the engine first.

## Verification

- **Un-skip** `needs:ignore-case` (144) and `needs:case-folding` (69), reading each skip's prose
  first; stragglers retag with prose.
- **Oracle wave**: reuse S10's folding-sensitive inventory as *subjects and patterns both* - the
  104 expand-on-folding characters, Turkic dotted/dotless i, Cherokee (case-folds upward),
  sharp-s and the ligatures - under `(?i)` with and without `(?f)`, as literals, in sets, in
  ranges, as backreferences, inside repeats. Zero divergences; negative control.
- The Turkic special case (`same_char_ign_turkic`) only triggers under the locale encoding
  upstream - verify against the oracle what a `str` pattern can reach and pin exactly that,
  the S10 `(?L)` precedent.

## Done when

- [x] Both tags delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green over the folding inventory; counts quoted.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a FLD comparison advancing both sides
      by one when the folding expanded, simple folding used where upstream full-folds (or vice
      versa) in a set range, a backreference IGN comparison folding the subject but not the
      captured text), commit.

## Closing notes (2026-08-31)

**What landed.** Every forward `_IGN` and `_FLD` opcode. `Matcher` gained `SameCharIgn`,
`InRangeIgn`, `MatchesCharacterIgn`, `MatchesRangeIgn`, `MatchesPropertyIgn`, `MatchesMemberIgn`,
`MatchesSetIgn` and the four `InSet*Ign`; `Encodings` gained `PropGcLu`/`_Ll`/`_Lt`, which upstream
defines in `_regex.c` rather than in the header the transliterator reads. The dispatch switch gained
`CHARACTER_IGN`, `PROPERTY_IGN`, `RANGE_IGN` and the four `SET_*_IGN` as arms of the existing
one-character case group, and `STRING_IGN`, `STRING_FLD`, `REF_GROUP_IGN` and `REF_GROUP_FLD` as
cases of their own. `foldedPos` and `gfoldedPos` join `stringPos` as `basic_match` locals.
`CountOne`, `MatchOne` and `MatchesOne` gained the same opcodes, and `Seam.Tag` lost its
`ignore-case` arm.

**Counts.** All 63 `needs:ignore-case` and `needs:case-folding` skip attributes removed; 12 tests
were stragglers and were retagged with prose - 11 `needs:find-all` (`FuzzyRegex.Matches`, S25) and 1
`needs:right-to-left` (S23). Passing went 4416 to 4702, overall parity **42.2% to 53.0%**.
`CaseFolding` 1.4% to 97.1%, `Various` 71.6% to 95.4%, `UnicodeProperties` 70% to 74.3%,
`Regressions` 20.7% to 23.9%, `CharacterClasses` 19.2% to 23.1%. `find-all` (181) is now the
biggest remaining tag by a wide margin.

**Nothing to port in three places the scope named.** `same_char_ign_turkic` and `all_turkic_i` are
reachable only from `string_search_fld`/`_rev`, the Phase 7 deferral - `grep -n
'same_char_ign_turkic\|all_turkic_i' upstream/src/_regex.c` gives the table entries and exactly two
call sites, both there. The `STRING_IGN`/`STRING_FLD` arms of the two `*_REPEAT_ONE` sub-switches
were already excluded by S19's default-arm-only decision. `partial_string_match_ign` has no other
caller. All three are recorded in PORTMAP.

**The surprise: upstream does not agree with itself about a cased property under the ASCII
encoding.** Three code paths reach three predicates and only one does the cased-category collapse -
the dispatch switch's `matches_PROPERTY_IGN`, `count_one`'s `match_many_PROPERTY_IGN` (which calls
the encoding table's `has_property_ign`) and `search_start`'s screen. Measured against regex
2026.7.19, `regex.match` against `'KsKK'`:

| pattern | `(?ai)` | `(?ui)` |
|---|---|---|
| `\p{Ll}` | (0, 1) | (0, 1) |
| `\p{Ll}{2}` | (0, 2) | (0, 2) |
| `\p{Ll}{4}` | (0, 4) | (0, 4) |
| `\p{Ll}+` | (0, 2) | (0, 4) |
| `\p{Ll}*` | (0, 0) | (0, 4) |
| `(\p{Ll})+` | (0, 4) | (0, 4) |

A greedy `*` consuming nothing where `+` consumes two is not a rule any predicate states. Same fault
from the search side: `regex.search(r'(?ai)\p{Ll}', 'A')` is `None` where `regex.match` of the same
pair spans `(0, 1)`, and `regex.search(r'(?ai)x?\p{Ll}', 'A')` - which defeats the screen - spans
`(0, 1)` again. **This cost most of the slice**: an early probe used `regex.search` only, which made
`matches_PROPERTY_IGN`'s ASCII arm look wrong, and the predicate was rewritten to
`has_property_ign` before a 1500-row wave of `match`/`fullmatch` rows showed the opposite. The port
now collapses in every path, which is the literal transliteration; the oracle's `case-folding`
generator keeps the ASCII flag off property rows, and both halves are pinned in
`Gaps/Engine/CaseInsensitiveMatchingTests.cs` as known differences rather than parity.
**Lesson for the next slice: probe upstream with `regex.match`, not `regex.search` - the search
prefilter this port defers can make upstream's own answer misleading.**

**Two more known differences, both found by the blind review, both Phase 7 deferrals showing
through.** `regex.match('(?fi)fi', 'fı')` spans `(0, 2)` where this port finds nothing, because a
whole pattern that is one `STRING_FLD` goes through `locate_required_string` and
`string_search_fld`, which compares with `same_char_ign_turkic`; splitting the string
(`(?fi)(f)i`) makes upstream answer `None` and agree with us again. And
`regex.match('(?fi).*ẖẛ', 'ẖṡ')` is `None` where this port spans `(0, 2)`, because upstream's
`GREEDY_REPEAT_ONE` retreat fast path clamps by a folded length it recomputes from already-folded
values and gives up early; `(?fi)x?ẖẛ` and the bare `(?fi)ẖẛ` both span `(0, 2)`. Both are pinned.

**Oracle.** New `case-folding` generator, in the default list, so it is nine generators now. Green
at `agree 13500 unsupported 0 diverge 0` at 1500 rows each, at seeds 22, 555 and 777, and
`--verify-determinism` passes. **The generator was widened four times during the controls**, each
time because a control did not fire hard enough: one-case ranges (`FOLD_ONE_CASE_RANGES`), a
`folded-literal` shape, a `folded-backref` shape with subject-aware group selection, and the
expanding alphabet twice in the rotation. Each widening's before/after numbers are in the comment
beside it.

**Negative controls.** All figures from
`pwsh -File tools/run-oracle.ps1 -Generator case-folding -Count 600 -Seed <n>`, run against the
generator as committed.

> Control A, `same-char-ign-off-by-one`: in `Matcher.cs`, `SameCharIgn`, change
> `        for (int i = 1; i < count; i++)` to `        for (int i = 2; i < count; i++)`.
> Wave: `case-folding`, 600 rows, seed 7. Result: 585 agree, **15 diverge**. Seed 4242: **16**.

> Control B, `in-range-ign-skips-the-character`: in `Matcher.cs`, `InRangeIgn`, change
> `        for (int i = 0; i < count; i++)` to `        for (int i = 1; i < count; i++)`.
> Wave: `case-folding`, 600 rows, seed 7. Result: 593 agree, **7 diverge**. Seed 4242: **8**.
> Before `FOLD_ONE_CASE_RANGES` was added it was 3 at both seeds.

> Control C, `string-fld-advances-both-sides`: in `Matcher.cs`, the `Opcode.StringFld` case,
> replace
> ```
>                                 if (foldedPos >= foldedLen)
>                                 {
>                                     state.TextPos = state.NextPos(state.TextPos);
>                                 }
> ```
> with `                                state.TextPos = state.NextPos(state.TextPos);`.
> Wave: `case-folding`, 600 rows, seed 7. Result: 595 agree, **5 diverge**. Seed 4242: **5**.
> Before the `folded-literal` shape was added it was 0 at seed 7 and 1 at seed 4242.

> Control D, `ref-group-fld-does-not-fold-the-capture`: in `Matcher.cs`, the `Opcode.RefGroupFld`
> case, change
> `                        if (foldedPos < foldedLen && SameCharIgn(state.Encoding, gfolded[gfoldedPos], folded[foldedPos]))`
> to
> `                        if (foldedPos < foldedLen && SameCharIgn(state.Encoding, state.CharAt(stringPos), folded[foldedPos]))`.
> Wave: `case-folding`, 600 rows. Result: seed 7 **0 diverge**, seed 4242 **4**, seed 99 **1**.
> **This control does not fire reliably and the generator is why**, after three attempts to widen
> it (doubled subjects, a `folded-backref` shape, subject-aware group selection): only 1 of 19
> FULLCASE backreference rows that match at all has a captured character that expands on folding,
> because so many things have to line up at once. What does catch it deterministically is the gap
> tests: with the control applied, `dotnet test tests/FuzzyRegex.Tests` fails
> `Full_case_folding_matches_where_upstream_does((?fi)(ss)\1, ßß, 0, 2)`,
> `((?fi)(ffi)\1, ﬃﬃ, 0, 2)` and
> `A_backreference_under_full_case_folding_matches_a_different_length_form`. **A later slice that
> touches `REF_GROUP_FLD` should not trust the wave for it.**

> Control E, `property-ign-drops-the-collapse`: in `Matcher.cs`, `MatchesPropertyIgn`, change
> `            return value is UnicodeTables.PropLu or UnicodeTables.PropLl or UnicodeTables.PropLt;`
> to `            return value is UnicodeTables.PropLu;`.
> Wave: `case-folding`, 600 rows, seed 7. Result: 594 agree, **6 diverge**. Seed 4242: **10**.

**Analyzer findings.** One, S4144 ("implementation is not identical to"), on two gap-test methods
whose bodies were the same three lines. Fixed on the merits rather than disapplied: the shared body
became one private helper and the test methods became expression bodies over it, which is smaller
code and what the rule was asking for. Nothing was suppressed.

**Review.** One blind pass over the whole diff (Opus, reproduction-only brief). **Findings raised:
0 against the diff.** The reviewer ran ~275,000 generated pairs of its own and reported three
divergence families, all traced by it to documented deferrals rather than to this slice; two of
them were new information and are the "two more known differences" above. **Findings reproduced: 2**
(both re-run in this session against regex 2026.7.19 before being believed, output quoted in the gap
test's doc comment). **Findings fixed: 0** - nothing in the diff was wrong; the two were turned into
gap tests. A **second blind pass was needed and was run** (Sonnet, scoped to the delta only): the
first pass never saw the new `Two_phase_7_deferrals_show_through_on_full_case_folding` method or the
`anchored` parameter added to `ShouldMatchUpstream`. It re-ran all six quoted upstream answers,
confirmed the other three callers still search rather than anchor, and reported "delta clean". The
ratchet and the oracle were re-run green after the additions.

**For the next slice.** S25 (`find-all`, 181 tests) is now much the biggest win, ahead of
`substitution` (104). Eleven of the tests S22 retagged are waiting on it specifically.
