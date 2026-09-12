---
slice: S42
phase: 5
title: BESTMATCH - do_best_fuzzy_match and the best list
delivers: [fuzzy-bestmatch]
---

# S42 - `BESTMATCH`

The mode that searches the whole slice for the match with the fewest errors instead of taking the
first that fits, then re-examines the equal-best candidates for the earliest, lowest-cost fit.
Depends on S41 (shares the capture trio). Eighteen tests, and the mode with the most open upstream
issues.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **`do_best_fuzzy_match`** (`:17584-17861`) replaces the `fuzzy-bestmatch` seam at
  `Matcher.cs:6582`. Two passes. The first walks `start_pos` across the slice, running
  `basic_match` with `max_errors` set to one below the fewest so far, collecting every equal-best
  `(match_pos, text_pos)` pair into the best list and its changes into the best changes list,
  stopping at a perfect match. The second, when `fewest_errors > 0`, revisits each entry at up to
  `min(fewest_errors, RE_MAX_ERRORS)` offsets from its `match_pos` with `max_errors` climbing from 1,
  keeping the earliest lowest-cost result; if nothing improves, it re-runs entry 0 inside a slice
  widened by `fewest_errors` on each side and copies the recorded changes back. Port the control
  flow as written, `goto`s included in the port's usual labelled form.
- **The best list** (`RE_BestList` / `RE_BestEntry`, `:692-701`; `init_best_list`,
  `fini_best_list`, `clear_best_list`, `add_to_best_list`, `:17532-17583`) and the best changes list
  (`RE_BestChangesList`, `:403`; `init/fini_best_changes_list`, `clear_best_fuzzy_changes`,
  `add_best_fuzzy_changes`, `:9831`, `:9859`). A `List<T>` is the obvious shape; keep upstream's
  four function names as methods so the rows in PORTMAP's row 428 map.
- **Ranking by cost, not by error count (owner decision 2026-09-12, DECISIONS).** Every comparison
  in `do_best_fuzzy_match` uses `state->total_errors` (`:17647`, `:17664`, `:17743`, `:17746`); the
  weighted `total_cost` (`:9649`) is never consulted. That is upstream issue 470 (open since May
  2022, no maintainer reply): `(?b)(voices){1i+1d+2s<=2}` on `voixes voicees` returns `voixes`
  (one substitution, cost 2) over `voicees` (one insertion, cost 1) because both are one error and
  the earlier wins - verified on regex 2026.9.10. Evidence this is a regression rather than a
  design: releases 2014.12.24 to 2015.09.28 ranked by cost (`state->max_cost = state->total_cost -
  1`), and the 2015.11.5 Hg issue 165 hang fix replaced `max_cost` with `max_errors` throughout;
  TRE/agrep, the only other engine with per-type costs, defines best as lowest cost with leftmost
  tie-break; upstream's one weighted `(?b)` test (`test_regex.py:2713`) gives `(34, 39)` under both
  rules (brute-forced 2026-09-12, `tools/probes/upstream-bestmatch-cost-ranking.py`). **Procedure:**
  port upstream's count ranking first, line for line, and get the 18 tests green; then switch the
  ranking to cost, ties by fewer errors, then earliest (upstream's own tie rule), through one helper
  shared with S41. The pruning bound inside `basic_match` follows: the pre-rework source is the
  reference (`pip download regex==2015.09.28 --no-binary :all:`, the `max_cost` bound in its
  constraint predicates). With unit costs the rules agree, so no ported test changes; the
  divergence is pinned by a gap test on the issue 470 example and an `ExpectedDivergences` entry
  whose predicate requires `(?b)` and a non-unit cost. Ledger entry for 470 with this evidence;
  nothing filed. Issue 427 was fixed upstream (`upstream/changelog.txt:410`) and its test is in
  the ported suite.
- **Benchmark both rankings** once, with BenchmarkDotNet or a stopwatch loop over the `fuzzy`
  generator's `(?b)` rows: the cost rule must not cost more than the count rule in runs of
  `BasicMatch`. Quote the numbers.
- **`RE_MAX_ERRORS` is 10** (`:203`); it clamps `error_limit` and the second pass's offsets. Pin a
  case where it binds (a best match with more than ten errors).

## Verification

- Un-skip `needs:fuzzy-bestmatch` (18 tests: `FuzzyBestMatchTests.cs` and flagged siblings).
- Gap tests: a subject where the first fuzzy match is not the best; equal-best candidates where
  the earliest wins; `(?b)` with `(?r)`; `(?b)` in a scan; `(?b)` with `partial=True`; the
  widened-slice fallback path (`best_groups == NULL`) reached and its changes copied.
- **Add `(?b)` to the `fuzzy` generator** on every body shape, and `(?b)(?e)` together. GREEN at
  three seeds, 2000 rows.
- Negative controls: the best list not cleared on a strictly better match; the bound not lowered
  after a match; the ranking reverted to error count (must turn the pinned 470 test red and the
  wave's non-unit-cost rows green, proving both can see the rule).

## Done when

- [ ] Tag delivered; counts in closing notes; every fuzzy tag now delivered, zero `needs:fuzzy-*`
      skips anywhere.
- [ ] `fuzzy` generator with `(?b)` green at three seeds.
- [ ] PORTMAP rows 417 and 428 rewritten as ported; the cost ranking in the "where we diverge"
      table; ledger entry for 470 written, nothing filed; benchmark numbers in the closing notes.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: `start_pos = state->match_pos` versus
      `text_pos` after a match; `max_offset` computed from the wrong end under `(?r)`), commit.
