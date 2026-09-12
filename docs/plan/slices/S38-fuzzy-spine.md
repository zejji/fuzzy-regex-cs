---
slice: S38
phase: 5
title: Fuzzy spine - counts, constraints, FUZZY/END_FUZZY, one-character and zero-width items, insertions, do_simple_fuzzy_match
delivers: []
---

# S38 - The fuzzy spine

The first fuzzy engine slice. After it a pattern whose fuzzy section contains only single
characters, classes, properties, ranges, dots and zero-width assertions matches with substitutions,
insertions and deletions, reports its counts and changes, and the oracle has a `fuzzy` generator
for exactly that subset. Multi-character literals (`STRING*`), backreferences, the `*_REPEAT_ONE`
fuzzy loops and the `{...:test}` constraint are S39 and S40; `ENHANCEMATCH` and `BESTMATCH` are
S41 and S42. Delivers no tag by name: which of the nine `fuzzy-*` tags this subset already turns
green is measured at the end (below), not guessed now.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **State.** `RE_State`'s fuzzy members: `fuzzy_counts[RE_FUZZY_COUNT]`, `fuzzy_node`,
  `max_errors`, `total_errors` (already present), `fuzzy_changes` (`RE_FuzzyChangesList`, `:397`),
  and `fuzzy_guards` (`RE_FuzzyGuards`, `:346`, one body and one tail guard list per fuzzy node).
  Fill the two deliberate holes: the fuzzy-guard allocation in `state_init_2` (`:18310`, ported in
  `MatchState.Create`) and the fuzzy half of `reset_guards` (`:3392-3396`, `MatchState.ResetGuards`),
  and the "clear the counts, node and change list" lines `init_match` skips today
  (`MatchState.cs:539`). `push_fuzzy_counts`/`pop_fuzzy_counts` (`:2480`, `:2652`) onto the
  `ByteStack`. Find every reader of `fuzzy_guards` before deciding its shape: the S30 lesson is
  that a list upstream writes and never reads is not ported.
- **The constraint predicates** (`:9643-9750`): `total_errors`, `total_cost`,
  `any_error_permitted`, `this_error_permitted`, `insertion_permitted`,
  `fuzzy_within_constraints`, `check_fuzzy_partial`. The `values[RE_FUZZY_VAL_*]` indices are
  `Engine.FuzzyValue`, already ported. `record_fuzzy`/`unrecord_fuzzy` (`:9768`, `:9801`) and the
  change list's init/fini (`:9806`, `:9814`). `save_fuzzy_counts`/`restore_fuzzy_counts` and the
  `best_fuzzy_counts` copy in `Matcher.SaveBestMatch`/`RestoreBestMatch` that PORTMAP's
  `check_posix_match` row owes to Phase 5.
- **`FUZZY` and `END_FUZZY`**, forward (`:13132`, `:12448`) and in the backtrack switch (`:15752`,
  `:15488`). The backtrack `END_FUZZY` arm is where insertions after the section are tried; read
  its `insertions` loop before porting and quote it. `FUZZY_INSERT` in the backtrack switch
  (`:15769`) with `fuzzy_insert`/`retry_fuzzy_insert` (`:10346`, `:10372`); its forward callers
  are the string arms and are S39's, so this slice ports the mechanism and S39 the call sites.
- **One-character and zero-width items**: `fuzzy_match_item`, `next_fuzzy_match_item`,
  `retry_fuzzy_match_item` (`:10185`, `:10116`, `:10262`). Their forward callers are every
  `else if (node->status & RE_STATUS_FUZZY)` arm the port already has as a `Seam.For(Opcode.Fuzzy)`
  in the one-character case group, the `ANY*` cases, the boundaries and the anchors; their
  backtrack callers are the shared blocks at `:15210-15243` (one-character) and `:15330-15344`
  (zero-width) that PORTMAP recorded as "nothing to port until Phase 5". Note `step` is `1`, `-1`
  or `0`, and a zero-width item passes `0`.
- **`fuzzy_ext_match`** (`:9938`) is called from `next_fuzzy_match_item`. Port only its
  `if (!test_node) return TRUE;` shape here, with the switch left as a seam throwing
  `needs:fuzzy-matching`; S40 fills the switch.
- **`do_simple_fuzzy_match`** (`:18027`) replaces the `fuzzy-matching` entry-point seam in
  `Matcher.cs:6592`. The `BESTMATCH` and `ENHANCEMATCH` seams stay.
- **The Match object.** `pattern_new_match`'s fuzzy half (`:20738`) and `match_fuzzy_changes`
  (`:20504`): `Match.FuzzyCounts` and `Match.FuzzyChanges` (`Match.cs:225`, `:231`; record structs
  in `FuzzyCounts.cs`) stop throwing. Upstream reports change positions as codepoint indices; this
  port's public indices are UTF-16 code units (AGENTS.md), so convert as `Match.Index` does, and
  pin one astral case.
- **The oracle.** `tools/record-oracle.py`: a `fuzzy` generator over the S16-S20 one-character
  constructs (literal, `.`, class, property, range, negated forms, `\b`/`^`/`$`) wrapped in
  `{e<=N}`, `{s<=N}`, `{i<=N}`, `{d<=N}`, mixed and cost forms (`{1i+2d+1s<=4}`, `{e<=2,i<=1}`),
  and the `(?e)`/`(?b)` flags EXCLUDED (S41/S42 add them); subjects mutated from an exact match by
  0-3 edits; through `search`, `match`, `fullmatch`, `(?r)`, `partial=True`, and `finditer`. The
  recorded outcome gains `fuzzyCounts` and `fuzzyChanges`, appended to `Describe()` only when
  non-zero so every older row renders unchanged (the S31 `partial` precedent). `OracleComparer`
  reads `Match.FuzzyCounts`/`FuzzyChanges` and compares both. Upstream issues 607 and 608 were
  crashes in exactly these shapes (`upstream/changelog.txt:7-8`); state which upstream version the
  recorder ran against and that it did not crash.
- **Not in scope**, and each keeps throwing its seam: `STRING*`, `STRING_FLD*`, `REF_GROUP*`
  (S39); the retreat/advance loops in `GREEDY_REPEAT_ONE`/`LAZY_REPEAT_ONE` backtracking
  (`:15881`, `:16500`, S39); `fuzzy_ext_match`'s switch (S40).

## Verification

- Gap tests first, in `Gaps/Engine/FuzzyMatchingTests.cs` (new): a substitution, an insertion, a
  deletion, each with counts and change positions asserted against a quoted upstream run; a cost
  equation; `(?r)`; `partial=True` reaching `check_fuzzy_partial`; a fuzzy zero-width item; a
  nested fuzzy section (the `push_fuzzy_counts` path).
- **Tag probe at the end**, S36's method: remove every `needs:fuzzy-*` skip, run the suite, and
  restore the skips on every test still red. Any tag whose tests are ALL green is delivered by this
  slice and its `delivers:` line updated; record the per-tag pass counts for S39 and S40. Expect
  `fuzzy-insertion`, `fuzzy-deletion` and `fuzzy-substitution` (3 tests each) to be candidates and
  the rest not.
- Oracle `fuzzy` generator GREEN at three seeds, 2000 rows; add `fuzzy` to the default list. Zero
  divergences is the bar; any divergence is judged the S33 way (probe, research, blind review) and
  never waved through as "fuzzy is approximate".
- Negative controls: `any_error_permitted` ignoring cost; `this_error_permitted` reading the wrong
  `MAX_BASE` slot; `END_FUZZY` skipping `fuzzy_within_constraints`; an `unrecord_fuzzy` dropped on
  backtrack (changes list grows); counts not restored on `FUZZY` backtrack.

## Done when

- [ ] Every scope item ported, PORTMAP's fuzzy bucket rows written for each symbol, the two
      `MatchState` holes closed (or the reader-less part recorded as deliberately not ported, with
      the grep).
- [ ] `fuzzy` generator green at three seeds; tag probe recorded; any fully green tag un-skipped.
- [ ] `Match.FuzzyCounts`/`FuzzyChanges` delivered; the S36 note "the `fuzzy_counts`/`fuzzy_changes`
      half is Phase 5's" in PORTMAP's `pattern_new_match` row updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a change position recorded in
      codepoints; `max_errors` compared with `<` where upstream has `<=`; a `step` of `0` treated
      as forward), commit.
