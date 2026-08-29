---
slice: S04
phase: 1
title: Port upstream tests - test_various, captures, named lists, fuzzy
delivers: []
---

# S04 - Port upstream tests: the feature body (lines 1741-3084)

Follow the `port-tests` skill.

## Scope

`upstream/regex/tests/test_regex.py` lines 1741 to 3084.

Notable methods in range: `test_various` (1741, about 735 lines and by far the densest method in
the file - a grab bag of patterns and expected spans), `test_replacement`, `test_common_prefix`,
`test_captures`, `test_guards`, `test_turkic`, `test_named_lists`, `test_fuzzy` (2612 - the
headline feature), `test_recursive`, `test_format`, `test_fullmatch`, `test_issue_18468`,
`test_partial`.

Suggested areas: `Various`, `Replacement`, `Captures`, `NamedLists`, `Fuzzy`, `Recursion`,
`Format`, `FullMatch`, `Partial`, `Turkic`.

## Watch for

- **`test_various` is a table, not a narrative.** It is mostly (pattern, subject, expected span)
  triples. Port it as `[Arguments(...)]` rows on a handful of tests rather than 700 individual
  methods, keeping the upstream assertion index range in the `Upstream` property. Judgement
  call: split where the *operation* differs, use arguments where only the data differs.
- **`test_fuzzy` is the reason this project exists.** Port it meticulously and do not batch it to
  a subagent. Give fuzzy tests fine-grained capability tags (`fuzzy-substitution`,
  `fuzzy-insertion`, `fuzzy-deletion`, `fuzzy-budget`, `fuzzy-bestmatch`, `fuzzy-enhancematch`,
  `fuzzy-charset`) so Phase 5 can slice them sensibly.
- **`test_copy`** (2876) is Python's copy protocol - not ported; record it in PORTMAP.md.

## Done when

Same criteria as S02, plus:

- [ ] Fuzzy tests are tagged finely enough that `docs/STATUS.md` shows a usable breakdown of
      fuzzy work rather than one large `needs:fuzzy` bucket.
