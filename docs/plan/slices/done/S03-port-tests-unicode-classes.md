---
slice: S03
phase: 1
title: Port upstream tests - Unicode properties, classes and boundaries
delivers: []
---

# S03 - Port upstream tests: Unicode, classes and boundaries (lines 1008-1741)

Follow the `port-tests` skill.

## Scope

`upstream/regex/tests/test_regex.py` lines 1008 to 1741.

Notable methods in range: `test_properties` (1008, the largest single method in range - Unicode
general categories, scripts and blocks), `test_word_class`, `test_search_anchor`,
`test_search_reverse`, `test_atomic`, `test_possessive`, `test_zerowidth`,
`test_scoped_and_inline_flags`, `test_repeated_repeats`, `test_lookbehind`,
`test_unmatched_in_sub`, `test_overlapped`, `test_splititer`, `test_grapheme`,
`test_word_boundary`, `test_line_boundary`, `test_branch_reset`, `test_set`.

Suggested areas: `UnicodeProperties`, `Scripts`, `Blocks`, `Boundaries`, `Lookaround`, `Atomic`,
`Possessive`, `BranchReset`, `Sets`, `ZeroWidth`, `Grapheme`, `Reverse`, `Overlapped`.

## Watch for

- **`test_properties` is where non-BMP data starts appearing.** Every index in a test whose data
  contains an astral character must be recomputed for UTF-16, verified against Python, and
  commented. This is the highest-risk translation in the whole phase - do not batch it to a
  subagent without checking the output.
- **`test_grapheme`** exercises `\X`, which is grapheme-cluster matching, not codepoint matching.
  Keep its expectations in codepoint terms in the comment and UTF-16 terms in the assertion.
- **`test_search_reverse`** covers the `Reverse` option, an mrab feature with no `Regex`
  equivalent. Its API shape may need a note back to S01.

## Done when

Same criteria as S02: full coverage of the range, every non-ported method recorded in PORTMAP.md
with a reason, build clean, ratchet GREEN, slice moved with closing notes.

Additionally:

- [ ] Every test with non-BMP data carries a comment stating the codepoint span upstream expects
      and the UTF-16 span we assert.
- [ ] A matching gap test exists in `Fuzzy.Text.RegularExpressions.Tests.Gaps.Surrogates` for each distinct
      surrogate situation encountered, pinning the UTF-16 behaviour on purpose rather than as a
      side effect of translation.

---

## Closing notes (2026-08-29)

**What landed.** 22 new test files, 185 test methods expanding to 299 skipped test cases. All 19
upstream methods in lines 1008-1740 are ported; every assertion dropped from inside one is listed
in `docs/PORTMAP.md` with a reason. Suite 466 -> 765. Ratchet GREEN, baseline unchanged at 34 -
correct, since every new test is skipped.

**Ten capability tags now visible that were not on the board before:** `unicode-properties` (69
tests), `branch-reset` (21), `word-flag` (18), `line-boundaries` (15), `set-operations` (13),
`version-flags` (11), `possessive` (8), `grapheme` (5), `overlapped` (4), `atomic` (1). Nine are
new; `version-flags` was reserved in S02 and is activated here. Vocabulary is now 32 tags. Nine
new areas: `UnicodeProperties`, `Boundaries`, `BranchReset`, `Reverse`, `ZeroWidth`, `Overlapped`,
`Possessive`, `Atomic`, `Grapheme`.

**The slice's headline risk did not exist.** It warned that `test_properties` is where non-BMP
data starts appearing and that every index would need recomputing for UTF-16. Measured first:
lines 1008-1740 contain no code point above U+FFFF at all. Every index copies across unchanged,
and the `Gaps/Surrogates` done-criterion is vacuous rather than skipped - see DECISIONS.md for the
scan. Budget that time for S04/S05 instead. **Check the range before planning around a risk the
slice file asserts**; it cost twenty minutes to disprove and would have cost far more to work
around.

**Three defects in delegated output that no test could have caught**, all found before review:

1. **All 13 `(?V1)`/`(?rV1)` patterns in `test_search_reverse` and `test_zerowidth` were folded
   away** as "incidental" because the expected value matched the unflagged sibling's. The value is
   the same; the pattern text is not, and the parser has to accept it. This is the single most
   valuable catch of the slice: `version-flags` had no test anywhere, so nothing would ever have
   scheduled `(?V0)`/`(?V1)`. Same failure shape as S02's `right-to-left` mis-tag.
2. **Provenance numbering skipped `sys.version_info` else-branches** in three files, shifting every
   later number. S02 set the opposite convention and it was not in the brief. It is in DECISIONS
   now, and belongs in the next slice's brief.
3. **A class remark claimed `\u` escapes** where the file held literal UTF-8 - the authoring hazard
   STATE.md warned about, appearing as a *false comment* rather than corrupt data.

**Review found three more**, all reproduced before acting: a remark claiming the `test_properties`
`\X` copies were ported elsewhere when they are deliberately omitted; a `Replace`-calling test
tagged only `needs:character-classes`; a wrong upstream line citation. Chasing the third turned up
a fourth the reviewer missed - `test_set`'s last provenance was `#38-41` where the type-identity
assertion is #38, so it should be `#39-42`. **The reviewer's own per-method accounting said
`test_set: 42/42`, and it was wrong.** Verify a coverage count by re-deriving it, not by reading it.

**Delegation.** Three Sonnet agents took 18 methods; `test_properties` was kept in-session as the
slice instructed. Integration and the build stayed central, which is what caught 21 analyzer errors
(private fields must be `_camelCase`; every agent used PascalCase, and so did I) that no agent saw
in isolation - the same lesson as S02, so treat central build as non-negotiable rather than
learned-again. One agent left a scratch file in the repo root and the oracle runs left a
`__pycache__` inside the submodule, making it show dirty; both cleaned, and the skill now sets
`PYTHONDONTWRITEBYTECODE=1`.

**For S04/S05.** Reuse the 32-tag vocabulary before coining. Put the provenance-numbering rule and
the "never fold a version-flagged pattern" rule in the porting brief. Expect real non-BMP data.
