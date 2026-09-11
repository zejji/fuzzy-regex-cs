---
slice: S31
phase: 4
title: Partial matching - the fallback in do_match and Match.PartialMatch
delivers: [partial]
---

# S31 - Partial matching

The largest single win left on the board (82 tests) and, per the S26 handover, the cheapest per
test: every one-character and string opcode already has its partial arm, `MatchState.PartialSide`
is already set from the `partial` argument (`MatchState.cs:379`), and the whole seam is the guard
in `FuzzyRegex.Run` (`FuzzyRegex.cs:318`) that throws on `partial: true`. Needs nothing from
S27-S30, but is placed after them so the partial arms of the new opcodes are written with their
opcodes rather than retrofitted.

## Scope

All line references are `upstream/src/_regex.c` unless marked.

- **`do_match`'s fallback** (`:18121-18162`): try a normal match first with `partial_side` forced
  to none; on failure restore `text_pos` and `partial_side` and run again. The second run is the
  one that may return `RE_ERROR_PARTIAL`.
- **`RE_ERROR_PARTIAL` as a result**, not an error: `pattern_search_or_match` sets
  `match->partial = status == RE_ERROR_PARTIAL` (`:20774`). Ours is `Match.PartialMatch`
  (`Match.cs:208`, stubbed by S01, never yet true). Check what a partial match reports for its
  span and groups - upstream stores results for both `SUCCESS` and `PARTIAL` (`:18164`).
- **The partial arms already in the matcher** (`CountOne` reports `isPartial`; each
  `CHARACTER*`/`STRING*`/`RANGE`/`SET*` arm returns `RE_ERROR_PARTIAL` at the text end) were
  ported without a test that could reach them. Every one is now reachable: expect the wave to
  find at least one that was ported wrong, and minimise it.
- **The three public overloads** already take `partial` (`Match`, `MatchAtStart`, `FullMatch`,
  `FuzzyRegex.cs:500-528`). The scanner does not: upstream's `finditer`/`findall` have no
  `partial` argument, so `Matches` stays as it is. Confirm against `_main.py` and record.
- **Upstream issue 367**: `partial=True` returns a true positive when two lookaheads are jointly
  unsatisfiable. Port faithfully; gap test pins current behaviour for Phase 6.
- **`partial_string_match_ign` (`:11683`)** stays unported: its only callers are the
  `*_REPEAT_ONE` string arms, which are Phase 7 optimisations.

## Verification

- **Un-skip** `needs:partial` (82 tests, `PartialMatchTests.cs`, `RegressionsPartialTests.cs`).
- **Oracle generator `partial`**: every S16-S25 construct with the subject cut short at every
  prefix length from 0 to full, under `partial=True`, through `match`, `search` and `fullmatch`,
  forward and `(?r)` (where the cut is at the *start*); the comparison includes `m.partial`, the
  span and every group. Also cases where a full match exists *and* a longer partial would - the
  fallback must prefer the full match. Zero divergences. Negative controls: the fallback skipped
  (always partial); `text_pos` not restored between the two runs; `PartialMatch` reported for a
  full match.
- **Add `partial` to the default oracle list.**

## Done when

- [ ] Tag delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green; controls recorded in full. Any partial arm found wrong is a permanent
      test.
- [ ] PORTMAP: `do_match` row updated; `Match.partial` row moved from S01's stub to delivered.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a partial arm returning partial on
      `slice_end` rather than `text_end`; `(?r)` partial at the wrong end; group spans on a
      partial match), commit.
