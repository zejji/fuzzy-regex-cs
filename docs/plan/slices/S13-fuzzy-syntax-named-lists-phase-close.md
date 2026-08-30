---
slice: S13
phase: 2
title: Fuzzy constraints, named lists, grapheme, and closing the parser
delivers: [parse-errors]
---

# S13 - Fuzzy constraints, named lists, grapheme, and closing the parser

The last Phase 2 slice. After it, every construct upstream's parser accepts is parsed and
compiled here, and every one of the S06 corpus rows passes.

## Scope

`src/FuzzyRegex/Parsing/`, mirroring `upstream/regex/_regex_core.py`.

- **Fuzzy constraints**: `parse_fuzzy`, `parse_fuzzy_item`, `parse_cost_constraint`,
  `parse_cost_limit`, `parse_constraint`, `parse_fuzzy_compare`, `parse_cost_equation`,
  `parse_cost_term`, `parse_fuzzy_test`, `is_actually_fuzzy`, `apply_constraint`
  (`:548-560`, `:590-604`, `:655-846`); `Fuzzy` (`:2786-2919`) including
  `_constraints_to_string` and the `FUZZY`/`FUZZY_EXT` compile paths; the `fuzzy` argument
  threaded through every node's `_compile(reverse, fuzzy)`, which earlier slices carried but
  never set.
- **Named lists**: `StringSet` (`:4069-4110`), `make_string_set` (`:441`), `parse_string_set`
  (`:1427`), `Info.named_lists_used`, and the pipeline's `named_lists`, `named_list_indexes`,
  `args_needed`, `complain_unused_args` and `missing named list` handling
  (`_main.py:482-492`, `:606-621`). Upstream's `ValueError` for an unused keyword argument maps
  to `ArgumentException`; its `error` for a missing list stays `FuzzyRegexParseException`.
  `FuzzyRegex.NamedLists` returns the sets upstream returns as `frozenset`s.
- **Grapheme**: `Grapheme`, `GraphemeBoundary` (`:2919-2938`) and `\X` in `parse_escape`.
- `PrecompiledCode` (`:3303`) is used only by `Scanner`, which is not ported (PORTMAP); record
  it as not ported rather than porting dead code.

## Closing the phase

- **Un-skip `needs:parse-errors`** in full (72 tests across 7 files), reading each skip's prose
  first: a few name a missing position (`multiple-repeat detection does not report a
  position`, `an empty \p{} property name does not report a position`), which the faithful
  port now reports. Template rows S12 could not un-skip stay skipped for Phase 3 with their
  prose updated to say why.
- **Every `_regex_core.py` symbol accounted for.** List every `class` and `def` in the file
  (`grep -n "^class \|^def \|^    def "`) and check each appears in `docs/PORTMAP.md` as ported
  or as deliberately not ported with a reason (`Scanner`, `_shrink_cache`, `PrecompiledCode`,
  the `dump` methods if not ported, bytes-only branches). S05 did this for the test methods;
  do it for the parser.
- **All corpus rows pass**: 1534 compiles, 46 errors, 62 templates, zero skipped. If any row
  still skips, the phase is not complete; if a row needs a construct the corpus never exercised,
  that is a gap for the Phase 3 oracle to cover and is written into the closing notes.
- **Phase 3 handover notes**, for whoever authors the Phase 3 slices: which opcodes the compiler
  emits (all of them, by the end), the seam's shape, where the `re_compile` port
  (`_regex.c:25863-26121`, which turns the code list into the node graph) should start, and the
  span convention warning from DECISIONS 2026-08-30 (`(start, end)` versus `(Index, Length)`).
- Roadmap and budget: revise the Phase 3 session estimate against Phase 2's measured rate, as
  the roadmap asks, and record what `docs/plan/slice-log.jsonl` now holds.

## Done when

- [ ] Corpus: every row passes, none skipped, none failing.
- [ ] `parse-errors` delivered; `docs/STATUS.md` shows the tag gone from the waiting table.
- [ ] PORTMAP complete for `_regex_core.py` and `_main.py`; "Deliberately not ported" and
      "Where we diverge" tables current.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a cost equation parsed with integer
      division where upstream uses `/`, `UNLIMITED` as a fuzzy limit, `frozenset` versus list
      semantics in a named list with duplicate entries), commit.
- [ ] STATE.md says Phase 2 is complete and the driver stops at the boundary.
