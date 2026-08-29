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
- [ ] A matching gap test exists in `FuzzyRegex.Tests.Gaps.Surrogates` for each distinct
      surrogate situation encountered, pinning the UTF-16 behaviour on purpose rather than as a
      side effect of translation.
