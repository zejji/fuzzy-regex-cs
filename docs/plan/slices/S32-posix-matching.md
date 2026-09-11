---
slice: S32
phase: 4
title: POSIX leftmost-longest matching
delivers: [posix-matching]
---

# S32 - POSIX leftmost-longest matching

Eight tests and a small mechanism, placed last because it changes what `SUCCESS` means: under
`RE_FLAG_POSIX` the matcher does not stop at the first match but records it and keeps
backtracking. Everything Phase 4 added to the backtrack paths therefore gets exercised once more,
under a flag that forces exhaustive backtracking.

## Scope

All line references are `upstream/src/_regex.c`.

- **The hook in the `SUCCESS` arm** (`:15178-15186`): if POSIX, `check_posix_match` then
  `goto backtrack`. **The exit in the `FAILURE` backtrack arm** (`:15685-15688`): if
  `found_match`, `restore_best_match` and succeed.
- **The helpers**: `check_posix_match` (`:11602`), `save_best_match` (`:11493` - saves the
  captures too, read it to the end), `restore_best_match` (`:11565`), `same_values` (`:11438`)
  and `equivalent_nodes` (`:11453`). State fields `best_match_pos`, `best_text_pos` (`:493`),
  `found_match` (`:534`), reset at `:3427`.
- **Not this slice**: `init_best_list`/`add_to_best_list` (`:17532`, `:17552`) and the rest of
  the `RE_BestList` family are `do_best_fuzzy_match`'s (`:17614`) and stay Phase 5, whatever
  PORTMAP's POSIX row currently says - correct the row.
- **Interaction with `MatchTimeout`**: POSIX exhausts the backtracking, so a pattern that was
  fast at first-match can be slow here. Nothing to build; note it in the option's XML doc.

## Verification

- **Un-skip** `needs:posix-matching` (8 tests, `RegressionsPosixMatchingTests.cs`).
- **Oracle generator `posix`**: alternations whose first alternative is a prefix of a later one
  (`a|ab|abc`), nested and repeated, with captures whose spans differ between the first match and
  the longest, under `(?p)` and the `Posix` option, through search, match, findall and sub, and
  under `(?r)`. Zero divergences. Negative controls: `check_posix_match` preferring shorter;
  `restore_best_match` not restoring captures.
- **Add `posix` to the default oracle list.**

## Done when

- [ ] Tag delivered or stragglers retagged.
- [ ] Oracle wave green; controls recorded in full.
- [ ] PORTMAP: the five helpers and the two arms; the best-list row corrected to Phase 5.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: captures from the first match
      surviving into the longest; `found_match` not reset between search positions), commit.
