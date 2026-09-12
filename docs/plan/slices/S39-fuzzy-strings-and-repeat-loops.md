---
slice: S39
phase: 5
title: Fuzzy strings, backreferences and the repeat-one loops
delivers: []
---

# S39 - Fuzzy strings, backreferences and the repeat-one loops

The bulk of fuzzy matching by line count, and the slice that makes ordinary fuzzy patterns work:
any literal longer than one character compiles to `STRING`, so almost every test in
`FuzzyMatchingTests.cs` and `RegressionsFuzzyTests.cs` reaches an arm this slice ports. Depends on
S38's spine. Delivers no tag by name for the same reason S38 does not: the tag probe at the end
says which tags are green, and S40 is where the six non-`(?e)`/`(?b)` tags must be delivered at the
latest.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **Strings**: `fuzzy_match_string` / `retry_fuzzy_match_string` (`:10431`, `:10499`), called
  from `STRING` (`:14748`), `STRING_IGN` (`:15018`), `STRING_IGN_REV` (`:15075`), `STRING_REV`
  (`:15132`) and `REF_GROUP` (`:14039`), `REF_GROUP_IGN` (`:14297`), `REF_GROUP_IGN_REV`
  (`:14354`), `REF_GROUP_REV` (`:14410`). Each forward case also calls `fuzzy_insert` after a
  complete string (`:14765`, `:15035`, `:15092`, `:15149`) - the mechanism is S38's, the call sites
  are here.
- **Full case folding**: `fuzzy_match_string_fld` / `next_fuzzy_match_string_fld` /
  `retry_fuzzy_match_string_fld` (`:10635`, `:10580`, `:10721`) from `STRING_FLD` (`:14837`,
  `:14857`) and `STRING_FLD_REV` (`:14944`, `:14964`); `fuzzy_match_group_fld` /
  `next_fuzzy_match_group_fld` / `retry_fuzzy_match_group_fld` (`:10879`, `:10824`, `:10972`) from
  `REF_GROUP_FLD` (`:14130`) and `REF_GROUP_FLD_REV` (`:14231`); `folded_char_at`. The
  `folded_len` stores that S22 and S23 marked dead (S1854 disapplied at `STRING_FLD_REV` and
  `REF_GROUP_FLD_REV`) now have their reader: remove those disapplications.
  `fuzzy_ext_match_group_fld` (`:10033`) is called from `next_fuzzy_match_group_fld`; port its
  `if (!test_node) return TRUE;` shape and leave the switch for S40, as S38 did for
  `fuzzy_ext_match`.
- **The backtrack retry rows** for `STRING*` and `REF_GROUP*` (`:17269-17292`), which PORTMAP
  recorded three times as "nothing to port until Phase 5". `skip_pos` (`:16461`) comes with them
  if a string arm writes it.
- **The `*_REPEAT_ONE` fuzzy loops**: the retreat loop in `GREEDY_REPEAT_ONE`'s backtrack
  (`:15881`) and the advance loop in `LAZY_REPEAT_ONE`'s (`:16500`), today the two seams at
  `Matcher.cs:6119` and `:6253`. Read both loops whole before porting; S19's closing notes record
  the non-fuzzy halves and where the port's `CountOne` differs in shape.
- **Ledger entry 7** (`docs/plan/upstream-reports/LEDGER.md`): `İ` never reaching the full fold is
  on Phase 6's fix list, not this slice's. If a `STRING_FLD` fuzzy row hits it, classify it under
  the existing entry and move on.
- **Upstream issues to recognise, not reproduce**: 563 (`\m` with a fuzzy quantifier fails at
  position 0) and 564 (loosening `<=1` to `<=2` returns fewer matches), both fuzzy-string shapes
  the widened generator may hit. If a wave row is one of them, the S33 treatment applies: probe,
  research, verdict, entry, and a note in the ledger for Phase 6. The owner's rule stands: a
  bug identified with overwhelming evidence is fixed in this port sooner or later, never shipped.

## Verification

- Gap tests first: a `STRING` with each error type and the counts asserted; an `_IGN` and an `_FLD`
  string with an error inside a multi-character fold (`ß`/`ss`, `ﬆ`/`st`); a backreference with
  one error; `(?r)` versions of each; a fuzzy `a+`/`a+?` inside a section reaching each loop.
- **Widen the `fuzzy` generator**: multi-character literals, `(?i)` and `(?fi)` literals,
  backreferences, and `x+`/`x*?` bodies. GREEN at three seeds, 2000 rows.
- **Tag probe** at the end, as S38: un-skip every fully green tag, record per-tag counts.
- Negative controls: `fuzzy_insert` not called after a complete string; `string_pos` not restored
  on retry; the retreat loop stopping one short; an `_FLD` arm counting a fold of length 2 as two
  errors.

## Done when

- [ ] Every string, backreference and repeat-loop arm ported; no `Seam.For(Opcode.Fuzzy)` left in
      any `STRING*`, `REF_GROUP*` or `*_REPEAT_ONE` case; PORTMAP rows written and the three
      "nothing to port until Phase 5" notes rewritten.
- [ ] `fuzzy` generator widened and green at three seeds; tag probe recorded and every fully green
      tag un-skipped.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a fold-length miscount; an `_REV` arm
      stepping `string_pos` forward; the lazy loop's `fuzzy_insert` permitted where upstream
      forbids it), commit.
