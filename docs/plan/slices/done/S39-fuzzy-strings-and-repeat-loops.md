---
slice: S39
phase: 5
title: Fuzzy strings, backreferences and the repeat-one loops
delivers: [fuzzy-substitution, fuzzy-insertion, fuzzy-deletion]
---

# S39 - Fuzzy strings, backreferences and the repeat-one loops

The bulk of fuzzy matching by line count, and the slice that makes ordinary fuzzy patterns work:
any literal longer than one character compiles to `STRING`, so almost every test in
`FuzzyMatchingTests.cs` and `RegressionsFuzzyTests.cs` reaches an arm this slice ports. Depends on
S38's spine. Delivers no tag by name for the same reason S38 does not: the tag probe at the end
says which tags are green, and S40 is where the six non-`(?e)`/`(?b)` tags must be delivered at the
latest.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **Strings**: `fuzzy_match_string` / `retry_fuzzy_match_string` (`:10431`, `:10499`), called
  from `STRING` (`:14748`), `STRING_IGN` (`:15018`), `STRING_IGN_REV` (`:15075`), `STRING_REV`
  (`:15132`) and `REF_GROUP` (`:14039`), `REF_GROUP_IGN` (`:14297`), `REF_GROUP_IGN_REV`
  (`:14354`), `REF_GROUP_REV` (`:14410`). Each forward case also calls `fuzzy_insert` after a
  complete string (`:14765`, `:15035`, `:15092`, `:15149`) - the mechanism is S38's, the call sites
  are here.
- **Full case folding**: `fuzzy_match_string_fld` / `next_fuzzy_match_string_fld` /
  `retry_fuzzy_match_string_fld` (`:10635`, `:10580`, `:10721`) from `STRING_FLD` (`:14837`,
  `:14857`) and `STRING_FLD_REV` (`:14944`, `:14964`); `fuzzy_match_group_fld` /
  `next_fuzzy_match_group_fld` / `retry_fuzzy_match_group_fld` (`:10879`, `:10824`, `:10972`) from
  `REF_GROUP_FLD` (`:14130`) and `REF_GROUP_FLD_REV` (`:14231`); `folded_char_at`. The
  `folded_len` stores that S22 and S23 marked dead (S1854 disapplied at `STRING_FLD_REV` and
  `REF_GROUP_FLD_REV`) now have their reader: remove those disapplications.
  `fuzzy_ext_match_group_fld` (`:10033`) is called from `next_fuzzy_match_group_fld`; port its
  `if (!test_node) return TRUE;` shape and leave the switch for S40, as S38 did for
  `fuzzy_ext_match`.
- **The backtrack retry rows** for `STRING*` and `REF_GROUP*` (`:17269-17292`), which PORTMAP
  recorded three times as "nothing to port until Phase 5". `skip_pos` (`:16461`) comes with them
  if a string arm writes it.
- **The `*_REPEAT_ONE` fuzzy loops**: the retreat loop in `GREEDY_REPEAT_ONE`'s backtrack
  (`:15881`) and the advance loop in `LAZY_REPEAT_ONE`'s (`:16500`), today the two seams at
  `Matcher.cs:6119` and `:6253`. Read both loops whole before porting; S19's closing notes record
  the non-fuzzy halves and where the port's `CountOne` differs in shape.
- **Ledger entry 7** (`docs/plan/upstream-reports/LEDGER.md`): `İ` never reaching the full fold is
  on Phase 6's fix list, not this slice's. If a `STRING_FLD` fuzzy row hits it, classify it under
  the existing entry and move on.
- **Upstream issues to recognise, not reproduce**: 563 (`\m` with a fuzzy quantifier fails at
  position 0) and 564 (loosening `<=1` to `<=2` returns fewer matches), both fuzzy-string shapes
  the widened generator may hit. If a wave row is one of them, the S33 treatment applies: probe,
  research, verdict, entry, and a note in the ledger for Phase 6. The owner's rule stands: a
  bug identified with overwhelming evidence is fixed in this port sooner or later, never shipped.

## Verification

- Gap tests first: a `STRING` with each error type and the counts asserted; an `_IGN` and an `_FLD`
  string with an error inside a multi-character fold (`ß`/`ss`, `ﬆ`/`st`); a backreference with
  one error; `(?r)` versions of each; a fuzzy `a+`/`a+?` inside a section reaching each loop.
- **Widen the `fuzzy` generator**: multi-character literals, `(?i)` and `(?fi)` literals,
  backreferences, and `x+`/`x*?` bodies. GREEN at three seeds, 2000 rows.
- **Tag probe** at the end, as S38: un-skip every fully green tag, record per-tag counts.
- Negative controls: `fuzzy_insert` not called after a complete string; `string_pos` not restored
  on retry; the retreat loop stopping one short; an `_FLD` arm counting a fold of length 2 as two
  errors.

## Done when

- [x] Every string, backreference and repeat-loop arm ported; no `Seam.For(Opcode.Fuzzy)` left in
      any `STRING*`, `REF_GROUP*` or `*_REPEAT_ONE` case; PORTMAP rows written and the three
      "nothing to port until Phase 5" notes rewritten.
- [x] `fuzzy` generator widened and green at three seeds; tag probe recorded and every fully green
      tag un-skipped.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a fold-length miscount; an `_REV` arm
      stepping `string_pos` forward; the lazy loop's `fuzzy_insert` permitted where upstream
      forbids it), commit.

## Closing notes (2026-09-13)

**What landed.** Ordinary fuzzy patterns work. `Matcher` gained `FuzzyInsert`/`RetryFuzzyInsert`,
`FuzzyMatchString`/`RetryFuzzyMatchString`, `NextFuzzyMatchStringFld`/`FuzzyMatchStringFld`/
`RetryFuzzyMatchStringFld`, `NextFuzzyMatchGroupFld`/`FuzzyMatchGroupFld`/`RetryFuzzyMatchGroupFld`,
`FuzzyExtMatchGroupFld` (its null arm only), `AdvanceItem` and `PermitInsertionInFold`; `FuzzyData`
gained `NewStringPos`, `NewFoldedPos`, `FoldedLen`, `NewGfoldedPos` and `StringPosIsText`;
`next_fuzzy_match_item` got back the `is_string` parameter S38 dropped. **All twenty remaining
`Seam.For(Opcode.Fuzzy)` sites are gone** - the twelve forward `STRING*`/`REF_GROUP*` arms, the four
backtrack case groups (`:15766`, `:17269-17290`, `:17291-17306`, `:17361-17376`) and the two
`*_REPEAT_ONE` branches, which are not ported at all. Suite 5806, passing 5632, ratchet GREEN,
baseline 5524 distinct ids, parity **91.1%** and 174 skipped, all fuzzy.

**`StringPosIsText` is the one thing in here that upstream does not have to say.** `string_pos` is an
index into the node's `values` for a `STRING*` and a position in the subject for a `REF_GROUP*`;
upstream's `data->new_string_pos += step` serves both because C counts codepoints either way, and
this port has to move one by `+ step` and the other by `Matcher.Step`. The S21 comment at
`REF_GROUP`'s own walk predicted exactly this ("Phase 5's fuzzy retry moves `stringPos` on its own,
where the lockstep argument no longer holds") and it was right.

**Scope changed in one place: the two `*_REPEAT_ONE` fuzzy loops are not ported, because nothing can
reach them.** This is the S30/S38 treatment - `fuzzy_guards` and `RE_FuzzyData.limit` - applied to
control flow rather than to state, and the evidence is two independent arguments plus a measurement.
`sequence_matches_one` (`:24056`) returns `FALSE` when the repeat's body node carries
`RE_STATUS_FUZZY`, and every op `node_matches_one_character` (`:3433`) would accept is emitted by a
parser class that sets `FUZZY_OP` inside a section (`Any`, `Character`, `Property`, `Range`,
`SetBase`, `SetUnion`, `ZeroWidthBase`), so no `REPEAT_ONE` is ever built inside one; and outside
one, the node after the repeat is `FUZZY` or `FUZZY_EXT`, which `Fuzzy._compile` emits with
`REVERSE_OP` and never `FUZZY_OP`, and `can_test_past` (`:23697`) walks past neither. Measured over
all 1,534 compile-parity patterns: **639 `REPEAT_ONE` nodes, none with a fuzzy test node.** The
greedy branch would also have been a copy of the default arm this port already has (`:15881` against
`:16271`; `status != FAILURE` after the `< 0` return is `status == SUCCESS`). Pinned by
`RepeatTests.No_repeat_one_node_in_the_corpus_has_a_fuzzy_test_node`, which is the guard on the
argument rather than a restatement of it: it goes red if a sync changes any of the three facts.
`skip_pos` (`:16461`) stays unported with the string arms that are its only writers.

**How that was found is worth carrying forward.** The slice file asked for "a fuzzy `a+`/`a+?`
inside a section reaching each loop", and the first two gap tests written for it - `(?:a+x){e<=1}`
and `(?:a+?x){e<=1}` - **passed before a line of S39 was written**, which is the signal the skill
names. A probe over twelve candidate patterns then hit the `STRING` seam every time and the
`*_REPEAT_ONE` seams never, and dumping the compiled nodes is what turned "these tests are weak"
into "this branch is dead". Both tests are kept, renamed to say what they actually pin
(`RepeatTests.A_fuzzy_repeat_answers_without_a_repeat_one_node`).

**Tag probe (S36's method).** All 80 `needs:fuzzy-*` skip attributes were commented out and the
suite run: 139 tests un-skipped, **76 passed and 63 failed, against S38's 3 and 136**. Every one of
the 63 is a seam and not a wrong answer - 29 `BESTMATCH` (S42), 25 `ENHANCEMATCH` (S41), 9
`fuzzy_ext_match`'s switch (S40). Per tag: `fuzzy-substitution` 3/3, `fuzzy-insertion` 3/3,
`fuzzy-deletion` 3/3, `fuzzy-matching` 24/31 methods, `fuzzy-budget` 9/11, `fuzzy-changes` 1/8,
`fuzzy-counts` 0/8, `fuzzy-bestmatch` 0/14, `fuzzy-enhancematch` 0/5. **The three fully green tags
are un-skipped and are this slice's `delivers:`** - exactly the three S38 expected and could not
deliver, because upstream spells them with multi-character literals. The rest were restored.
`git checkout -- tests/` restores them and also throws away anything else uncommitted under
`tests/`; it ate this slice's own new `RepeatTests` cases once.

**Oracle.** The `fuzzy` generator is widened four ways, one per arm family: multi-character literals
(S38's "no two literals adjacent" rule is gone, which is all it took to build `STRING` nodes),
`(?i)` and `(?fi)` sections, a backreference to a group outside the section, and repeat bodies.
**GREEN at 2000 rows on three seeds (7, 4242, 20260913), 6000 rows, zero divergences**, and the full
default wave is GREEN too - 12,600 rows a seed, 37,800 in all, 16 expected divergences and no new
ones. Four things the widening had to learn, all measured:

1. **`{e}` is only safe over one-character atoms.** An unbounded budget next to a string, a fold, a
   repeat or a backreference makes upstream raise `MemoryError`, which kills the whole wave rather
   than producing a comparable row - `regex.sub(r'(abx)(?:\W[a-f](?:[ab]+\1){e}){s<=2}', '<>',
   'abx.dabx')` and `regex.split(r'(?fi)(?:straße\d\A){e}', 'sTrasßE0')`, both on
   2026.7.19. Same family as upstream issues 551 and 554, already on Phase 6's list.
   `FUZZY_BOUNDED_CONSTRAINTS` is what a section with a multi-character item draws from.
2. **At most one expanding fold per section, and that cap is a finding.** Two of them in one literal
   run is a COMPILE-time divergence this port already owns: `Sequence._fix_full_casefold`
   (`_regex_core.py:3637`) finds its chunks in the folded text and slices the unfolded run with
   those offsets, so `(?fi)ßaß` stops matching `'ssass'` upstream and matches here. That is
   S35's decided divergence (DECISIONS 2026-09-12) and **S39's wave is the first instrument ever to
   reach it** - 22 divergences over three seeds, every one this family, and
   `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` has no entry for it. Drawing the shape would
   fill the wave with rows about a compiler difference both sides' own tests already pin and would
   hide a real defect in the matcher arms this slice ports, so the generator stays off it. **Phase 6
   should decide whether the manifest gets an entry**; the reason it is not one now is that no
   predicate over the two answers is narrow enough - upstream compiles a genuinely different pattern,
   so any difference is possible.
3. **A bare `\1` next to a literal digit parses as `\10`.** Nine rows of a 2000-row wave were
   `invalid group reference` errors on both sides, agreeing about nothing. The generator emits
   `(?:\1)`.
4. **Two probabilities were raised because a control said so** - see below.

**Negative controls.** Six, in `tools/controls.json` as `S39-A` ... `S39-F`, run with
`python tools/run-controls.py --slices S39`. Each mutates `src/FuzzyRegex/Engine/Matcher.cs`, wave
`fuzzy`, 600 rows, at seed 7 and at seed 20260913. **Every figure below was re-run against the exact
code and generator being committed**, after the blind review's fix:

| id | what it breaks | seed 7 | seed 20260913 |
|---|---|---:|---:|
| A `fuzzy-insert-never-pushes` | `FuzzyInsert`'s `if (state.TextPos == limit \|\| ...)` becomes `if (state.TextPos != limit \|\| ...)`, so it never pushes a frame | 3 | 3 |
| B `string-pos-not-restored-on-retry` | `stringPos = (int)poppedStringPos;` in `RetryFuzzyMatchString` becomes `_ = poppedStringPos;` | 43 | 34 |
| C `fld-fuzzy-step-never-advances-text-pos` | `STRING_FLD`'s post-fuzzy `if (foldedPos >= foldedLen && foldedLen > 0)` becomes `foldedPos > foldedLen` | 5 | 5 |
| D `fld-error-cannot-land-at-the-end-of-a-folding` | `NextFuzzyMatchStringFld`'s INS bound `newPos <= data.FoldedLen` becomes `newPos < data.FoldedLen` | 2 | 1 |
| E `group-fld-deletion-moves-the-subject-side` | `NextFuzzyMatchGroupFld`'s DEL arm writes `data.NewFoldedPos` where upstream writes `data.NewGfoldedPos` | **0** | 1 |
| F `string-error-recorded-where-it-ended-not-where-it-began` | `FuzzyMatchString`'s `RecordFuzzy(..., state.TextPos)` becomes `RecordFuzzy(..., data.NewTextPos)` | 50 | 53 |

**Three of those figures are findings rather than ticks.**

* **The first run of all six measured nothing, and the reason is a trap the skill names.**
  `tools/run-controls.py` caches each wave in `.scratch/control-waves/<generator>-<count>-<seed>.jsonl`,
  and `fuzzy-600-7.jsonl` was still **S38's** wave - a generator with no strings, no backreferences
  and no folds in it. Every control read 0 at seed 7 and non-zero at the seed that had no cached
  file. **Delete the cached wave for any generator you have widened**; the cache key does not know
  the generator changed.
* **D and E are too thin on the generator to be evidence, so they have a hand-built wave of their
  own.** `python tools/probes/fuzzy-group-fld-rows.py` writes 168 rows - 14 folded-backreference
  shapes and 28 folded-string shapes, four operations each - and
  `pwsh -File tools/run-oracle.ps1 -Rows .scratch/fuzzy-group-fld-rows.jsonl` gives **168 agree on
  the honest engine**, **128 agree / 40 diverge with E applied** and **120 agree / 48 diverge with D
  applied**. That is S38's control-E method, and it is what makes E's zero readable: the fault is
  real and the generator reaches it once in six hundred, not never.
* **Two generator probabilities were raised while holding the controls, and one of the two moves did
  not help.** `FUZZY_CASE_MODE_WEIGHTS` went from (6, 2, 2) to (5, 2, 3) and
  `FUZZY_BACKREF_PROBABILITY` from 0.18 to 0.25 to give `REF_GROUP_FLD` more traffic, and E went from
  2/1 to 0/1 - which is what a rule reached by chance looks like, and is why the probe file exists.
  The insertion bias in `_fuzzy_mutate` (one insertion in three lands on the END of the subject,
  because `fuzzy_insert` runs only after a string has matched in full) did work: A went from 3/1 to
  3/3.

**Review.** One blind pass over the whole diff, dispatched inside the working turn, briefed with the
failure modes the change could plausibly have (a fold length charged as two errors, a `_REV` arm
stepping the wrong way, `fuzzy_insert` permitted where upstream forbids it, a push/pop order
mismatch between a `fuzzy_*` and its `retry_fuzzy_*`, `record_fuzzy` given the wrong position, the
`permit_insertion` folding rule applied at a site upstream does not apply it to, `data.Step` used
where the call's own `step` is meant, an `int` narrowing, a subject position moved by a bare
`+ step`, and the unreachability argument being wrong). **One finding raised, reproduced and fixed;
no second pass, because the fix is two comments and no code.**

* *Raised and fixed:* `Matcher.cs:6434` and `:6555`, the two `REF_GROUP_FLD*` arms, still carried
  S22/S23's comment "so nothing reaches this arm yet". S39's `RetryFuzzyMatchGroupFld` is what
  reaches it; the reviewer instrumented both arms and counted 1,382 and 342 entries over a
  39,936-row wave. The sibling `STRING_FLD` comments had been reworded and these two had not.
* The reviewer verified the rest against `_regex.c` line by line - all four push/pop frame families
  in upstream's order and matching their own `/* bstack: */` comments, `record_fuzzy` given
  `text_pos` at all six string sites and `new_text_pos - step` only where `fuzzy_match_item` does,
  `PermitInsertionInFold` against `:10659`/`:10761`/`:10905` and `:11019`'s different spelling
  transcribed literally, every sign at all twelve forward arms, `fuzzy_insert` at upstream's four
  sites only, no subject position moved by a bare `+ step`, every `(int)` cast over a value that was
  an `int`, the new tests non-vacuous, and the `*_REPEAT_ONE` argument independently re-derived. It
  built two further waves of its own - 39,936 boundary and backtrack rows, 66,240 `pos`/`endpos`,
  astral and atomic/lookaround-wrapped rows - both `diverge 0`.

**Analyzer findings.** One fired and was fixed rather than disapplied: S1481 on the now-unused
`Node test` local in `GREEDY_REPEAT_ONE`'s backtrack arm, which existed only for the fuzzy branch
that is not ported; the line became part of the NOT PORTED comment. Two S1854 disapplications were
**removed**, at `REF_GROUP_FLD_REV` and `STRING_FLD_REV`: S22 and S23 kept a dead `folded_len`/
`gfolded_len` store against the day its reader arrived, and S39 is that day.

**For S40.** `fuzzy_ext_match`'s switch and `fuzzy_ext_match_group_fld`'s switch are what is left,
with `folded_char_at` (`:10017`), whose only callers they are. Nine tests fail at those two seams
with every `needs:fuzzy-*` skip removed. The four plain tags S40 must land are `fuzzy-matching`,
`fuzzy-counts`, `fuzzy-budget` and `fuzzy-changes`; `fuzzy-changes` at 1/8 and `fuzzy-counts` at 0/8
are almost entirely `(?e)`/`(?b)` rows, so in practice they follow S41 and S42 rather than S40.
