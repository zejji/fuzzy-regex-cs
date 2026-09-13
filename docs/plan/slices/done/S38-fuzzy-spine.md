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

- [x] Every scope item ported, PORTMAP's fuzzy bucket rows written for each symbol, the two
      `MatchState` holes closed (or the reader-less part recorded as deliberately not ported, with
      the grep).
- [x] `fuzzy` generator green at three seeds; tag probe recorded; any fully green tag un-skipped.
- [x] `Match.FuzzyCounts`/`FuzzyChanges` delivered; the S36 note "the `fuzzy_counts`/`fuzzy_changes`
      half is Phase 5's" in PORTMAP's `pattern_new_match` row updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a change position recorded in
      codepoints; `max_errors` compared with `<` where upstream has `<=`; a `step` of `0` treated
      as forward), commit.

## Closing notes (2026-09-13)

**What landed.** Plain fuzzy matching works for a section of one-character and zero-width items.
`MatchState` gained `FuzzyCounts`, `FuzzyNode`, `FuzzyChanges`, `PushFuzzyCounts`/`PopFuzzyCounts`,
`RecordFuzzy`/`UnrecordFuzzy` and the fuzzy half of `InitMatch`; `ByteStack` gained
`PushInt8`/`PopInt8`. `Matcher` gained `FuzzyData`, the six constraint predicates,
`CheckFuzzyPartial`, `FuzzyExtMatch` (its two null arms only), `NextFuzzyMatchItem`,
`FuzzyChangePos`, `FuzzyMatchItem`, `RetryFuzzyMatchItem` and `DoSimpleFuzzyMatch`; the forward
`FUZZY` and `END_FUZZY` arms and their backtrack partners; and both of upstream's shared backtrack
blocks. Seven of the 27 `Seam.For(Opcode.Fuzzy)` sites are filled - the ANY family, the one-character
forward and reverse groups, and the zero-width group. `Match.FuzzyCounts` and `Match.FuzzyChanges`
stop throwing. Suite 5791, passing 5608, ratchet GREEN, baseline 5500 distinct ids.

**Scope changed in two places, both deliberate and both recorded in PORTMAP.**

1. **`fuzzy_insert`/`retry_fuzzy_insert` and the `FUZZY_INSERT` backtrack case move to S39.** All
   five of their call sites (`:14765`, `:15035`, `:15092`, `:15149`, `:15769`) are `STRING*` arms
   that S39 delivers, so nothing in this slice could reach them, and a private method with no caller
   fails this repo's own inspection gate (S4487). Everything they need from S38 - `insertion_permitted`,
   `record_fuzzy`, `fuzzy_ext_match`, `PushInt8` - is here. The slice file's scope line said "this
   slice ports the mechanism and S39 the call sites"; that is the one part of it not done.
2. **`state->fuzzy_guards` is never to be ported, not deferred.** The slice asked for the
   reader-grep before deciding the shape, and the answer is the S30 answer: `grep -n fuzzy_guards
   upstream/src/_regex.c` on 2026-09-13 gives twelve lines - a declaration, a null, two resets in
   `reset_guards`, an allocation, a `memset`, and the frees - and no read anywhere. The two holes in
   `MatchState.Create` and `ResetGuards` that said "waits for Phase 5" now say "never", with the grep.
   `RE_FuzzyData.limit` is the same shape (written at `:10203` and `:10206`, read nowhere) and is
   left out of `Engine.FuzzyData`.

**The subset is narrower than it reads, and the next slice should know why.** `(?:foo){e<=1}` is
NOT in S38: `Sequence.pack_characters` (`upstream/regex/_regex_core.py:3526`) packs a run of two or
more `Character` items into one `STRING` node, so a fuzzy `foo` is a fuzzy *string* and S39's. Every
pattern in the gap tests and in the oracle generator therefore avoids two adjacent literals. The
first draft of both used word-like literals throughout and every single test failed at the STRING
seam - that is why the probe and the tests were rewritten against classes, properties and dots.

**Tag probe (S36's method): no tag goes green, so `delivers:` stays empty.** All 80 `needs:fuzzy-*`
skip attributes were commented out and the suite run: 139 tests un-skipped, 3 passed, 136 failed.
Every one of the 136 is a seam and not a wrong answer - 80 at the fuzzy `STRING`/`REF_GROUP`/
`*_REPEAT_ONE` seams (S39), 29 at `BESTMATCH` (S42), 25 at `ENHANCEMATCH` (S41) and 2 at
`fuzzy_ext_match`'s switch (S40). The slice file expected `fuzzy-insertion`, `fuzzy-deletion` and
`fuzzy-substitution` to be candidates; they are not, because upstream's own tests spell their
patterns with multi-character literals. **S40 is where the seven plain tags must land, and S39 is
what unblocks 80 of them.** The skips were restored with `git checkout -- tests/`.

**Two defects found and fixed, both by instruments rather than by reading.**

1. **The change list was reported in full where upstream reports only the first `Total` entries.**
   Found by the widened oracle wave: 1, 1 and 2 rows across the three seeds. `match_fuzzy_changes`
   loops `for (i = 0; i < count; i++)` over the sum of the three **counts** (`:20522`), not over the
   length of the list `pattern_new_match` copied, and a nested fuzzy section leaves the two out of
   step. Fixed in `Match.SplitFuzzyChanges`; pinned by
   `Only_as_many_changes_are_reported_as_the_counts_say_even_when_more_were_recorded`.
2. **`start_match`'s fuzzy-counts clear (`:11790-11792`) was unported.** Found by the blind review.
   Ordinarily invisible, because the `FUZZY` backtrack arm has already restored the counts by the
   time `FAILURE` jumps back - but `(*PRUNE)`/`(*SKIP)` drops the fuzzy frames instead of unwinding
   them, and then a search restart carries the abandoned attempt's errors into the next one. Pinned
   by `A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one`.

**One real hazard fixed that no test could have caught.** The first draft put
`Span<long> ... = stackalloc long[FuzzyValue.Count]` inside the `END_FUZZY` cases, which sit inside
`BasicMatch`'s advance and backtrack loops. Stackalloc memory is not released until the method
returns, so that is unbounded stack growth per backtrack step. CA2014 does **not** see a stackalloc
nested inside a switch case - it fired only on the control mutation, which put one directly in a
case - so this was found by running the controls and not by the analyzer. The three buffers are now
declared once beside `folded`/`gfolded`, which is the convention that was already there.

**One upstream crash found, and it is new.** `regex.search(r'(?p)(?:[ab][bc]){e<=1}', 'ax')` segfaults
`regex` 2026.7.19 - the process dies, there is nothing to catch. Both halves are needed: the same
pattern without `(?p)` and the same pattern without the fuzzy section both answer normally.
**Ledger entry 9**, with `tools/probes/upstream-posix-fuzzy-crash.py` and its two controls. Two
consequences: the `fuzzy` generator must draw no `(?p)` until upstream fixes it, because a recorder
row that kills the interpreter takes the whole wave with it; and this port's own POSIX fuzzy counts
are not yet trustworthy, since `best_fuzzy_counts` is unported until S42.

**Oracle.** New `fuzzy` generator in `tools/record-oracle.py` over the S16-S20 one-character
constructs and the six zero-width assertions, wrapped in eighteen constraint shapes, with subjects
built to match exactly and then mutated by 0-3 random edits, through `search`/`match`/`fullmatch`/
`finditer`/`findall`/`sub`/`subf`/`split`, 20% reverse, 25% partial and 20% nested. `(?e)` and `(?b)`
are excluded - S41 and S42. The recorded outcome gained `fuzzyCounts` and `fuzzyChanges`, appended
only when non-zero so every older row renders unchanged (the S31 `partial` precedent); the consumer
reads them into `OracleFuzzy`. **GREEN at 2000 rows on three seeds (7, 4242, 20260913), 6000 rows,
zero divergences**, recorded against regex 2026.7.19 (the sanctioned PyPI fallback), which did not
crash - the shapes that crash it are the `(?p)` ones the generator does not draw. `fuzzy` is now in
`tools/run-oracle.ps1`'s default generator list.

**Negative controls.** Six, in `tools/controls.json` as `S38-A` ... `S38-F`, run with
`python tools/run-controls.py --slices S38`. Each mutates `src/FuzzyRegex/Engine/Matcher.cs`, wave
`fuzzy`, 600 rows, at seed 7 and at seed 31 (a seed this slice used nowhere else). **Every figure
below was re-run against the exact code and generator being committed**, after the last fix:

| id | what it breaks | seed 7 | seed 31 |
|---|---|---:|---:|
| A `any-error-never-permitted` | `AnyErrorPermitted`'s `return` becomes `TotalCost(...) < 0 && TotalErrors(...) < state.MaxErrors`, so no error is ever permitted | 282 | 265 |
| B `this-error-wrong-max-slot` | `values[FuzzyValue.MaxBase + fuzzyType]` becomes `values[FuzzyValue.MaxBase]`, always the SUB slot | 35 | 42 |
| C `end-fuzzy-skips-constraints` | the forward `END_FUZZY`'s `if (!FuzzyWithinConstraints(...)) { goto backtrack; }` becomes `_ = FuzzyWithinConstraints(...);` | 14 | 10 |
| D `retry-keeps-the-unrecorded-change` | `state.UnrecordFuzzy();` is deleted from the top of `RetryFuzzyMatchItem` | 31 | 26 |
| E `nested-section-inherits-the-outer-counts` | `Array.Clear(state.FuzzyCounts);` in the forward `FUZZY` arm is commented out | **0** | **0** |
| F `change-position-steps-the-wrong-way` | `FuzzyChangePos`'s `Step(state, data.NewTextPos, -data.Step)` becomes `Step(state, data.NewTextPos, data.Step)` | 122 | 105 |

**Three of those figures are findings rather than ticks, and all three are recorded here because the
numbers alone would mislead.**

* **A was rewritten twice before it measured anything.** Its first form relaxed the cost gate by one
  (`<= MAX_COST + 1`) and found nothing at either seed; its second tightened it (`< MAX_COST`) and
  also found nothing. The reason is structural: `any_error_permitted`'s cost test is strictly weaker
  than `this_error_permitted`'s (`cost + this_cost <= MAX_COST`, and every per-error cost is at least
  1), and its `error_count < state->max_errors` test is dead in the simple path, where
  `do_simple_fuzzy_match` sets `max_errors` to `PY_SSIZE_T_MAX`. **Both of `any_error_permitted`'s
  terms are unreachable as discriminators in S38's subset**; only the fact that it is consulted at
  all is observable, which is what the committed form measures. Expect the `max_errors` term to come
  alive in S41/S42, where the entry points set a real budget.
* **E does not fire, and the fault it models is real.** Removing the inner section's count clear
  changes an answer only when the outer section has already spent an error *before* the inner one is
  entered, and the generator does not reach that: its sections are one to four atoms and its subjects
  three to five characters. It was nearly missed - the first version of the nesting drew the inner
  section's start from index 0, and moving it to index 1 wherever there is room did not help either.
  **Proved observable by hand:** `python tools/probes/fuzzy-nested-rows.py` writes 24 rows whose first
  atom must be substituted, and `pwsh -File tools/run-oracle.ps1 -Rows .scratch/fuzzy-nested-rows.jsonl`
  gives 24 agree on the honest engine and **3 agree, 21 diverge** with the mutation applied. The same
  shapes are pinned as
  `A_nested_section_counts_its_own_errors_from_zero_and_not_from_the_outer_ones`. **S43 should widen
  the generator here rather than trust E's zero.**
* **The nesting that the generator does have earned its keep anyway**: it is what turned up defect 1
  above. The first 6000-row wave, before nesting, was green.

**Review.** Two blind passes, both dispatched inside the working turn.

*First pass*, over the whole diff, briefed with the failure modes the change could plausibly have
(codepoint-vs-UTF-16 change positions, `<` for `<=`, a step of 0 treated as directional, an unpaired
`push_fuzzy_counts`, `FUZZY_VAL_*` slot arithmetic, the `END_FUZZY` insertion loop, the deletion
shift, an allocation inside the matcher's loops). **Two findings raised, both reproduced, one fixed.**

* *Raised and fixed:* the unported `start_match` fuzzy-counts clear, with a runnable reproduction.
  Reproduced independently here before touching code - upstream answers
  `(1, 3) (0, 0, 1) ([0], [], [])` and this port answered counts `(2, 0, 1)` - test written first,
  watched fail, then fixed. Its wider claim (47 of 2548 rows on a hand-built fuzzy-plus-verbs wave)
  was not re-run here; the single row was enough to fix the right thing.
* *Raised and NOT acted on:* a partial-search divergence on `(?=(?:[ab][bc]){e<=1})[ab][bc][wx]`.
  Reproduced, and it is the **already-classified `search-start-partial` family**: upstream's
  `search_start` prefilter picks the non-fuzzy test node after the lookaround and never tries the
  position where only the fuzzy one could match, then answers its own zero-width partial at
  `slice_end`. The port agrees with upstream's own `match` and `fullmatch`. The ROADMAP's Phase 7
  rule covers it - port the prefilter without importing its answers - and the committed generator
  cannot produce the shape, because it emits no lookarounds and no groups, which is why the 6000-row
  wave is green. **Handover: the first slice to compose fuzzy with lookaround (S43) will reach this,
  and will need an `ExpectedDivergences` entry or a narrower generator.** Nothing changed for it here;
  acting on an unreachable row would have been speculation.

*Second pass*, over the `start_match` fix alone, because it is engine code the first reviewer never
saw. **No defects found.** It verified the placement against `_regex.c:11790-11792` line for line,
confirmed the label has the same single inbound jump as upstream's, neutralised the fix to show the
new test fails without it, checked the POSIX path never reaches it, and ran `fuzzy,verbs` at three
seeds (4000 rows, 0 diverge) plus its own 936-row hand-built wave over verbs, reverse, nesting,
overlapped scans and the `Matches`/`Split`/`Replace` state reuse. It also surfaced two out-of-scope
observations, both chased here: the POSIX fuzzy counts gap (real, `best_fuzzy_counts` is S42's, named
above) and the upstream crash (real, reproduced, ledger entry 9).

*Not re-reviewed:* the two tests added after the second pass
(`A_POSIX_search_of_a_fuzzy_pattern_answers_where_upstream_crashes` and the ledger entry). Both are
test and documentation only, and both of their values were measured directly against upstream in this
session rather than reasoned about.

**Analyzer findings, decided on the merits.** Four fired and all four were fixed rather than
disapplied, because each named a real problem: S4487 on `FuzzyData.NewStringPos` (written, never read
in S38 - the field and the `isString` parameter were dropped and go to S39 with their first reader);
S3218 on `FuzzyData.Step` shadowing `Matcher.Step` (the struct moved to namespace scope, which is
where upstream declares `RE_FuzzyData` anyway); S3358 on a nested ternary (an `if`/`else`, closer to
upstream's own shape); S125 on a test comment that read like code (reworded). Nothing was suppressed
and nothing was added to `.editorconfig`.

**For S39.** The fuzzy `STRING*` and `REF_GROUP*` arms and the `*_REPEAT_ONE` retreat and advance
loops are what is left of the 27 seams. `fuzzy_insert`/`retry_fuzzy_insert` land with them. When
adding fuzzy strings to the generator, note that `pack_characters` is the reason S38's patterns look
the way they do - a fuzzy `STRING` row is simply two adjacent literals, so the generator needs only
to stop avoiding them.
