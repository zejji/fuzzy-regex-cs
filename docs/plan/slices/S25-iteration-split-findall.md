---
slice: S25
phase: 3
title: Iteration - Matches, Count, NextMatch, Split, and overlapped matching
delivers: [find-all, splitting, overlapped]
---

# S25 - Iteration: Matches, Count, NextMatch, Split, and overlapped matching

Needs S24 (the scan-advance and empty-match policy land there; this slice reuses them). The last
behaviour slice of the phase: everything that walks a subject producing more than one match.

## Scope

All line references are `upstream/src/_regex.c`.

- **The scanner**: `scanner_search_or_match` (`:20874`), `scanner_iternext` (`:20943`),
  `pattern_scanner` (`:21072`), `pattern_findall` (`:22360`), `pattern_finditer` (`:22490`).
  Public shape: `Matches` returning `MatchCollection` (lazy like .NET's, or eager - follow what
  S01's stubs and the ported tests already assert), `Match.NextMatch`, `Count`. The `overlapped`
  argument (next scan starts one past the previous *start*, not past its end) is a scanner
  parameter, not a separate engine - `overlapped` (4 tests) delivers here.
- **The splitter**: `next_split_part` (`:21135`), `pattern_split` (`:22235`), `splititer`
  (`:22354`). Public: `Split(input, maxSplits)`, returning captured groups interleaved as
  upstream does, with `null` for an unmatched group's slot (the `string?[]` surface S01 stubbed).
  **Zero-width split is version-dependent**: V0 refuses a zero-width match as a split point
  (upstream warns/errors historically - pin current behaviour against the oracle), V1 splits on
  it; the `zero-width` test area exercises exactly this.
- **Upstream types deliberately not ported** (record in PORTMAP): `Scanner_Type` /
  `Splitter_Type` as public objects and their `copy`/`deepcopy` (`:20961-21014`,
  `:21264-21466`) - PORTMAP already records `Scanner` as not ported; the *iteration logic* is
  what this slice ports, surfaced as `MatchCollection`/`Split` instead.

## Verification

- **Un-skip** `needs:find-all` (37), `needs:splitting` (23) and `needs:overlapped` (4), reading
  each skip's prose first; the `FindAll`, `Splitting`, `Overlapped` and `ZeroWidth` STATUS areas
  are the scoreboard. Stragglers retag with prose.
- **Oracle wave**: patterns with zero-width alternatives over subjects that produce adjacent and
  empty matches, compared as the full match *sequence* (spans and groups per match) for
  finditer/findall semantics; split outputs compared element for element including `null` slots
  and `maxSplits`; overlapped sequences compared likewise. Run under V0 and V1 both. Zero
  divergences; negative control.

## Done when

- [ ] The three tags delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green comparing full sequences under both versions; counts quoted.
- [ ] `docs/PORTMAP.md` updated, including the not-ported scanner/splitter objects.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: the empty-match advance diverging
      from S24's (they share it upstream - they must share it here), an overlapped scan
      restarting at `end` instead of `start + 1`, a split dropping the final trailing empty
      part), commit.
