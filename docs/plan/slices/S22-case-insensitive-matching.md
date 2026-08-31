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

- [ ] Both tags delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green over the folding inventory; counts quoted.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a FLD comparison advancing both sides
      by one when the folding expanded, simple folding used where upstream full-folds (or vice
      versa) in a set range, a backreference IGN comparison folding the subject but not the
      captured text), commit.
