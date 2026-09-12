---
slice: S36
phase: 4
title: Composed oracle wave, symbol accounting, and closing Phase 4
delivers: []
---

# S36 - Composed oracle wave, symbol accounting, and closing Phase 4

The last Phase 4 slice, shaped like S26. After it every non-fuzzy construct in upstream matches,
the oracle has swept them in combination, and Phase 5's author has a handover.

## Scope

- **First, judge the three `verbs` rows still unexpected at 2000 rows** (seeds 7 and 4242; S35 left
  them). All are reversed overlapped scans with `(*SKIP)` where upstream reports MORE matches than
  this port. Probed at the checkpoint (`tools/probes/upstream-reversed-overlapped-skip.py`, and the
  recorder run on each row in isolation):
  - **Row 1567 (seed 4242)** `(?r)(?:[^\d]{2,4}(*SKIP)A|😀)$` MULTILINE on the emoji subject: upstream
    `[(4,9),(7,8)]` both in the wave and in isolation, and upstream's stateless `search(endpos=8)` is
    `(7,8)`; **this port's own stateless search at the same end position also finds it** (UTF-16
    `(12,14)`) yet its overlapped scan reports only `(8,15)`. The scan is dropping a match the engine
    finds, most likely a slice bound a `(*SKIP)` moved in the first scan and the scanner carried into
    the next. **Port bug on the evidence so far**: fix test-first, then re-check row 1439.
  - **Row 1863 (seed 4242)** `(?r)([^a]{2,4}(*SKIP)[a\d])((?:[^\d]++(*SKIP)\s|\ ))` on `b0 0
 A`:
    upstream `[(0,6),(0,5)]` but its own stateless `search(endpos=5)` and `match(endpos=5)` are `None`,
    so the second match is upstream's stateful scanner carrying a moved slice - the case-F mechanism.
    **Upstream bug, port right** unless a probe says otherwise; classify in `ExpectedDivergences` and
    add to the ledger's entry 5.
  - **Row 1439 (seed 7)**: the wave recorded upstream at 3 matches, but the recorder run on that row
    alone gives **1**, identical to the port. Upstream's answer depends on what ran before it in the
    same process - a `(*SKIP)`-moved bound surviving across independent calls through the compiled
    pattern's cached scanner state, or similar. Confirm by recording the row after its wave
    predecessors, name the mechanism, and record it as a ledger entry: an oracle row whose ground
    truth depends on call order is a recorder hazard for every later wave, so decide whether the
    recorder must isolate each row (fresh process or `cache_pattern=False`) from here on.
- **Ledger entry 7 (`İ` never reaches the full fold) is decided: it goes on Phase 6's fix list** (owner
  rule: fixed before 1.0; it needs the folding tables changed, which is a slice of its own there).
  Record that in the ROADMAP's Phase 6 opening and in the ledger; nothing else about it here.
- **Widen the `interactions` generator** to compose the six new families with the S16-S25 ones:
  a lookaround round a group call inside a conditional inside a repeat, cut short under
  `partial=True`, under `(?p)`, with a `(*PRUNE)` in one alternative - the interactions no
  per-slice wave reaches. Bounded rows, seed recorded, zero unexplained divergences, every real
  one minimised into a permanent test.
- **Every Phase 4 negative control re-run** from its recorded four values at the recorded seed
  and at one fresh seed, exactly as S26 did for Phase 3; the three-run table in the closing
  notes. Raise the row count before touching a generator's weights when a control is thin.
- **Probe every remaining tag on the board** by removing its `[Skip]` attributes and running the
  suite - the 2026-09-11 rule (DECISIONS). Any test that passes is un-skipped here; any tag
  Phase 4 delivered that still has a skipped test is a defect or a mis-tag. Multi-line `[Skip(`
  attributes need a multi-line-aware removal.
- **PORTMAP accounting**: every `_regex.c` function is now ported, not-ported-with-reason, or
  deferred to Phase 5 (fuzzy) or Phase 7 (prefilters, `try_match_*`, `*_REPEAT_ONE` sub-arms).
  Re-run the S26 bucketing and quote the counts. Nothing may remain "Phase 4".
- **Phase 5 handover** in the closing notes: the 18 `Seam.For(Opcode.Fuzzy)` sites in
  `Matcher.cs`; the fuzzy-guard hole in `state_init_2` and `reset_guards`;
  `save_captures`/`restore_groups`/`discard_groups` (`:17403-17500`, Phase 5 per PORTMAP);
  `do_best_fuzzy_match` (`:17614`) and the `RE_BestList` family; `do_enhanced_fuzzy_match`; the
  five `a{e<=1:\X}`-style rejections S15/S26 recorded; and upstream issues 470, 563, 564, 596
  (fuzzy and BESTMATCH bugs, Phase 6) so Phase 5 recognises them.
- **Roadmap and budget**: Phase 4's measured sessions-per-slice from `docs/plan/slice-log.jsonl`
  against the 7-slice estimate (10 once S33, S34 and S35 were added at the checkpoint); flag whether Phase 5's 5-8 still looks right. `CHANGELOG.md`
  gains Phase 4's entry.

## Verification

This slice's verification is the phase's: the composed wave, the control re-runs, the tag probe
and the symbol accounting, each with its output quoted.

## Done when

- [x] Composed wave ran with seed and counts quoted; zero unexplained divergences.
- [x] Every Phase 4 control re-run at its seed and a fresh one; table in closing notes.
- [x] Every remaining tag probed; no test skipped on a Phase 4 tag; `docs/STATUS.md` regenerated
      and overall parity quoted.
- [x] PORTMAP complete for `_regex.c` with nothing deferred to "Phase 4".
- [x] `CHANGELOG.md`, ROADMAP measured rate, STATE.md saying Phase 4 is complete, Phase 5
      handover written.
- [x] Ratchet GREEN, baseline updated, blind review over this slice's own changes, commit.

---

## Closing notes (2026-09-12)

### The three `verbs` rows, judged: all three are upstream's, and the port is right on all three

The slice file's provisional readings were wrong on two of the three, and both corrections came from
running a control rather than from reading more code.

**Row 1567 (seed 4242), `(?r)(?:[^\d]{2,4}(*SKIP)A|😀)$` MULTILINE. Provisionally "a port bug on the
evidence so far"; it is upstream's.** The evidence that looked damning was that this port's own
`search(0, 14)` finds the match its overlapped scan does not. But `search(0, 14)` truncates the
subject at 14, which is what makes `$` true there; inside the scan the subject runs to 15 and the
character at 14 is `A`, so `$` is false and the port is right to find nothing. Upstream's extra match
needs `$` to hold at a position with a letter after it, which it only does because the `(*SKIP)` moved
`slice_end` there. Three measurements settle it, all against `regex` 2026.7.19 recorded
prefilter-free:

- minimised to three ASCII characters, `regex.finditer(r'(?r)(?:.{2}(*SKIP)A|x)$', 'bxA', regex.M,
  overlapped=True)` is `[(0, 3), (1, 2)]` where this port gives `[(0, 3)]`;
- delete the verb, or make it `(*PRUNE)` - which moves no bound - and upstream gives `[(0, 3)]` too;
- ask upstream whether `$` can hold there at all with no verb in the pattern, and it says no:
  `regex.finditer(r'(?r)\U0001F600$', subject, regex.M, overlapped=True)` finds nothing.

This is the same defect S35 fixed on this side, seen through a scan instead of a single match, and it
is the strongest evidence yet that S35's reversal of S29 was right.

**Row 1863 (seed 4242). Upstream's, as the slice file guessed.** Its extra match `(0, 5)` carries
group 2 at `(4, 6)` - a capture ending outside the match it belongs to, in a pattern with no
lookaround - and upstream's own `search` and `match` over `(0, 5)` are both `None`. Removing the
verbs removes the extra match.

**Row 1439 (seed 7). NOT call-order dependence, which is what the slice file suspected.** The wave
recorded three matches and a run on the row alone gave one, and the explanation is mundane: the
`verbs` generator is recorded with upstream's required-string prefilter neutralised
(`tools/record-oracle.py:223`) and the isolated run was not. Recompiled prefilter-free the row gives
the same three matches every time, before and after a `gc.collect()`. **So the recorder needs no
per-row isolation and the decision the slice file asked for is "nothing to change"** - which is worth
recording as loudly as a change would have been, because "upstream's answer depends on call order"
would have been a standing hazard for every later wave. Its two extra matches carry the same
capture-outside-the-match tell as row 1863.

All three are one family, classified as `overlapped-skip-extra-match-reversed` and pinned by
`BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_stops_where_upstreams_own_extra_matches_refute_themselves`.
`verbs` at 2000 rows is now green at seeds 7, 4242 and 20260912: agree 1998/1995/1998, expected
2/5/2, diverge 0. Ledger entry 5 gained both symptoms.

### The composed wave, and the two things it found

`interactions` gained two pieces and three row-level options, so that Phase 4's families compose with
S16-S25's: `called-group` (a lookaround round a group call, inside a group-existence conditional,
inside a repeat - four families in one piece), `verb-alt` (a `(*PRUNE)` or `(*SKIP)` in one branch of
an alternation), a lookaround-as-condition arm on `cond`, POSIX at the row level half as `(?p)` and
half as the flag, and `partial=True` with the subject cut short. Re-measured at 600 rows, seed 1: 119
rows answer, 94 are POSIX, 71 ask for a partial, 133 hold a conditional (48 on a lookaround), 123 a
lookaround, 81 a group call, 67 a verb.

**Green at 2000 rows on seeds 7, 4242 and 20260912** - agree 1999/2000/2000, one classified row,
zero unexplained. It found two things in its first three seeds, which is the argument for the
widening:

1. **The recorder could not write one row down.** `(?P<g1>A*)(?<=(?&g1))` over `'A'`: upstream
   records g1's second capture as `(2, 1)` - a start past the end of a one-character subject, with an
   end before its own start - and `_to_index_length` raised `IndexError` indexing a two-entry
   codepoint table with 2. One upstream bug therefore failed a whole 2000-row wave. `_utf16_index`
   now **extends** the index rather than clamping it, so the impossible span stays impossible on this
   side and is reported rather than quietly made plausible; the recorder's `--self-check` gained two
   cases for it, watched failing against a clamping version.
2. **That row is issue 614's forward mirror.** The defect is the match direction not reaching a
   called group, so `(?r)` plus a lookahead is only one way to arrange it - a lookbehind in an
   ordinary forward pattern is the other. regex 2026.9.10 answers `(0, 1)`, which is this port's
   answer and always was. The `reverse-group-call-direction` entry is therefore renamed
   **`group-call-direction`** and its precondition widened from "reversed" to "reversed or contains a
   lookbehind", with a second permanent test,
   `GroupCallTests.A_group_called_from_a_lookbehind_records_its_capture_inside_the_subject_here`.

### The composed wave is NOT green at 6000 rows, and that is the next slice's work

Stated first because it is the one thing this slice found and did not finish. At 6000 rows on five
seeds the widened generator is red at every seed - 13 rows in all - and **every one of them belongs
to a family somebody has already judged**, which is why the wave is bounded at 2000 above rather than
claimed green at 6000.

| family | rows | already judged as |
|---|---:|---|
| A group call inside a lookaround that runs the other way from the pattern: upstream loses matches this port finds | 9 | S30's KNOWN DIVERGENCE, pinned by `GroupCallTests.A_group_called_from_a_lookbehind_with_anything_after_it_matches_here_and_not_upstream`, and confirmed on 2026-09-12 as NOT fixed by issue 614 |
| `(*SKIP)` with `partial=True`, where upstream's partial starts earlier than this port's | 3 | `search_start`'s own partial arms, which the slow path has none of - the `search-start-partial` family, in a shape that entry's predicate does not reach |
| A bounded lazy repeat losing its partial, `(?r)BB([^\d]??)` over `' B.\r.'` | 1 | `bounded-lazy-repeat-partial`, which is keyed on individually judged rows, so this is a row to judge and add |

None is a new defect and none is this port's. What each needs is the ordinary treatment - probe the
row, confirm which side is right, write the entry or the row, pin the answer - and that is a slice's
worth of work, not a paragraph. **Two attempts to shortcut it were made and both are recorded because
they failed**, which is more useful than the fact that they were tried:

- **Recording `interactions` prefilter-free**, on the good a-priori argument that the widening put
  `(*SKIP)` and `partial` into it, which are the two shapes the other two prefilter-free generators
  exist for. Measured over 6000 rows at five seeds it removed one diverging row and introduced
  another and left all three `(*SKIP)`-plus-partial rows unchanged, because those are `search_start`
  - not reachable from Python - rather than `locate_required_string`, which is. Reverted, with the
  measurement written into `PREFILTER_FREE_GENERATORS`.
- **Retargeting the five thin controls at `interactions`** (below). They appeared to spring to life
  and had not: the unmutated wave is not green at 6000 rows, so every number was at or below
  baseline. Reverted.

### Negative controls: all 24 Phase 4 controls re-run at their recorded seed and at seed 99991

`python tools/run-controls.py --slices S27,S28,S29,S30,S31,S32,S33,S35 --seeds 2`. The fresh seed
99991 is new to this repository and is now each Phase 4 control's **second** seed, because
`run-controls.py` gained `--seeds N` (take the first N) - a control accumulates seeds, and by a phase
close re-running all of them costs hours, since a mutated engine is not merely wrong but often slow:
S28-C's 2400-row wave goes from 44 seconds to ten minutes under its own mutation.

| id | what it breaks | recorded seed → diverge | 99991 → diverge |
|---|---|---:|---:|
| S27-A | lookaround backtrack drops `text_pos` | 7 → 12 | 13 |
| S27-B | lookaround never saves its captures | 7 → 7 | 7 |
| S27-C | lookbehind body compiled forwards | 7 → 25 | 22 |
| S28-A | a condition that holds takes the no-branch | 7 → 29 | 31 |
| S28-B | a negative condition that holds takes the yes-branch | 7 → 17 | 25 |
| S28-C | `push_repeats` saves the wrong repeat count | 7 → 9 (2400 rows) | 11 |
| S28-D | a condition that fails takes the wrong branch | 7 → 46 | 57 |
| S28-E | the repeat guard lists are not restored | 7 → **0** (2400 rows) | **0** |
| S29-A | `SKIP` widens the slice instead of moving it | 7 → **0** | 1 |
| S29-B | `SKIP` moves the wrong end under `(?r)` | 7 → 48 | 31 |
| S29-C | a verb prunes to the bottom of the bstack | 7 → 225 | 221 |
| S29-D | the scanner carries `findall`'s guard | 7 → 2 | **0** |
| S30-A | pop-groups restores nothing | 7 → 22 | 25 |
| S30-B | group call does not clear the repeat guards | 7 → 10 | 10 |
| S31-A | the `do_match` fallback is skipped | 31 → 16 | 10 |
| S31-B | `text_pos` not restored between the two runs | 31 → 25 | 29 |
| S31-C | every match is reported as partial | 31 → 77 | 69 |
| S31-D | `try_match` no longer reports a partial | 31 → 3 | 2 |
| S31-E | a lazy repeat that cannot extend loses its partial | 7 → 1 | 1 |
| S32-A | `check_posix_match` prefers the SHORTER match | 31 → 440 | 442 |
| S32-B | `restore_best_match` drops the captures | 31 → 338 | 375 |
| S33-A | the reversed string test bounds itself by `text_start` | 31 → 1 (8000 rows) | 1 |
| S33-B | `try_match` never consults a string test node | 31 → 2 | 7 |
| S35-A | `$` reads `slice_end` | 7 → **0** | **0** |

**Twenty of the twenty-four reproduce healthily. Four do not, and all four were already recorded as
weak by the slice that wrote them - which is the re-run doing its job rather than a surprise.**

- **S28-E is dead**, and raising it from 600 to 2400 rows did not help: 0 of 2400 at both seeds. The
  `conditionals` generator does not build a conditional inside a repeat whose guard lists have to
  survive, so the mutation is invisible to it. The count is left at 2400 as the evidence that the
  count is not the problem.
- **S29-A (0/1) and S29-D (2/0)** reproduce exactly what S29's own notes say - "not a control at all
  as recorded" and "blind at two of four seeds".
- **S35-A is 0 at both**, which is also what S35 recorded: it fires at two of five seeds, and seed 7
  is one of the three it misses. The scarce ingredient S35 named - a trailing `$` after the verb
  alternation - is still scarce.
- **S31-D (3/2) and S31-E (1/1)** are at S31's own recorded numbers, which its notes call thin and
  hand to the next slice.

**The obvious fix was tried and measured and does not work yet.** The widened `interactions`
generator composes exactly the shapes these four are missing - a conditional inside a repeat, a
`(*SKIP)` alternation with a trailing `$` - and pointing S28-E, S29-A, S29-D, S31-D and S31-E at it
at 6000 rows made all five fire at all three seeds. Then the unmutated baseline was measured, as it
should have been first: `interactions` at 6000 rows is red at every seed, 2 to 4 rows per seed, so
every one of those figures is at or below baseline and none of them is a control firing. All five are
back on the generators their slices recorded. **The composed generator cannot serve as a control
substrate until it is green at the row count where it reaches these cells**, and making it so is the
same work the table above hands on.

### Symbol accounting

**567 function definitions in `upstream/src/_regex.c`, cross-check 567, 286 named in PORTMAP outside
the accounting section and 281 not; none unaccounted for.** S26's figures were 274 and 293 against
the same commit, so twelve symbols crossed from unnamed to named, which is Phase 4's rows being
written. S26's script lived in its session scratch and is gone - the same unreproducible-evidence
problem the slice skill records for negative controls - so it was rebuilt from the section's own
description of what it did, and the rebuild reproduces 567 exactly.

**Nothing in PORTMAP defers anything to "Phase 4" any more, and all three rows that still said so
were wrong rather than pending.** `same_span_of_group`'s only caller is `do_enhanced_fuzzy_match`
(`:17952`), not the branch-reset and `GROUP_EXISTS` paths its row claimed, so it is Phase 5's;
`partial_string_match_ign`'s only caller is the `STRING_IGN` arm of `*_REPEAT_ONE`'s backtrack
(`:16155`), so it is Phase 7's and not partial matching's; and `save_capture`'s row said branch reset
was untested when the ported suite has 21 passing tests for it. A fourth claim was withdrawn:
**`SKIP` was labelled "not yet faithful" by S29 and is not.** The opcode is a line-for-line port of
`:14544-14555`; what S29 saw was `locate_required_string` moving upstream's first attempt, and the
2026-09-12 research settled that upstream's answers there are the wrong ones.

### The tag probe, and where Phase 4 leaves the board

Every remaining `[Skip(...)]` attribute was removed - 92 of them, across ten files, by a
balanced-paren script because most are wrapped across several lines - and the suite run: **185
skipped became 183 failed and 2 passed.** The two are `RegressionsFuzzyTests
.A_pattern_with_a_fuzzy_recursive_group_reference_compiles` and
`.A_pattern_starting_with_a_literal_nul_before_a_fuzzy_recursive_reference_compiles`, and both assert
only that a pattern COMPILES, which the parser has done since S13 - so neither was ever waiting on
the matcher. Both are permanently un-skipped. The other 183 are the nine `fuzzy-*` tags and nothing
else: **no test is skipped on a tag Phase 4 delivered.** Ratchet GREEN, 5,770 tests, 5,587 passing,
overall parity **90.7%**, 28 feature areas at 100%.

### Phase 5 handover

- **The seams are the map.** `Seam.For(Opcode.Fuzzy)` appears **27 times** in `Matcher.cs` - the
  slice file said 18, which was already stale - plus four `default`-arm seams which, now that every
  other opcode has a case, can only be reached by a fuzzy one, and three entry-point seams
  (`fuzzy-matching`, `fuzzy-bestmatch`,
  `fuzzy-enhancematch`, `Matcher.cs:6582-6592`). **There are no other seams left anywhere in
  `src/`**: every `Seam.For` call in the port is fuzzy or a fuzzy opcode's default arm, which is the
  cleanest statement of what Phase 4 closed.
- **Two holes in the state, both deliberate.** `MatchState.Create` does not allocate the fuzzy
  guards (`state_init_2`, `:18327`) and `ResetGuards` does not reset them (`reset_guards`,
  `:3392-3396`). The group-call half of both is settled as never to be ported - upstream's
  `group_call_guard_list` is written in five places and read in none - and that comment has been
  corrected, because it still said "until Phase 4".
- **What to port, in upstream's own order.** `save_captures` / `restore_groups` / `discard_groups`
  (`:17403`, `:17468`, `:17500`); `do_best_fuzzy_match` (`:17584`) with the `RE_BestList` /
  `RE_BestEntry` pair and its four functions (`init_best_list`, `fini_best_list`, `clear_best_list`,
  `add_to_best_list`); `do_enhanced_fuzzy_match` (`:17862`), which is where `same_span_of_group` is
  used; `do_simple_fuzzy_match` (`:18027`). PORTMAP's fuzzy bucket is 33 functions.
- **Five inputs Phase 5 must keep rejecting.** `{e<=1:\X}`, `{e<=1:\b}`, `{e<=1:\A}`, `{e<=1:\Z}`,
  `{e<=1:\L<a>}` - upstream's parser accepts them, emits exactly this port's bytecode, and its C
  engine then answers `RuntimeError: invalid RE code`. S15 rejects them with
  `NotSupportedException("invalid RE code")`, pinned by `Gaps/Engine/NodeGraphTests.cs`. A
  backreference inside a fuzzy test, `(a)(?:abc){e<=1:\1}`, is the same shape.
- **Four upstream bugs to recognise rather than reproduce**: issues 470, 563, 564 and 596, all fuzzy
  or `BESTMATCH`, all on Phase 6's sweep list.
- **There is no fuzzy oracle generator at all.** Every phase so far found its worst bugs through one,
  so write it early in Phase 5 rather than at the end.

### Review

**One blind pass over this slice's whole diff. Findings raised: 5. Reproduced: 5. Fixed: 5.** An
unusually high survival rate for this repository's usual one-in-five, and the reason is that four of
the five were arithmetic rather than judgement - the kind of finding that either reproduces or does
not.

**Finding 1 is the one that mattered, and it falsified a claim this slice had written into its own
code.** `CarriesACaptureOutsideItself` - the capture tell of the new entry - excluded the four
lookaround forms and nothing else, and the entry's comment said flatly that this port "cannot
produce" a capture outside a match. It can: `\K` moves the reported match start, so a group captured
before it lies outside the span. Reproduced directly against the built engine rather than taken on
trust:

```
new FuzzyRegex(@"(?r)ab\K(cd)(*SKIP)").Matches("abcdabcd", overlapped: true)
  match (4,6)  g1 (6,8)
  match (0,2)  g1 (2,4)
```

So a port defect that ended such a scan one match early would have been classified rather than
reported - which is the exact failure mode the whole `ExpectedDivergences` doctrine exists to avoid.
`\K` joins the exclusion list, the overstated comment is replaced by the measurement, and the
entry's "hole, said out loud" paragraph now points at the method instead of asserting safety. The
reviewer also established that no such row appeared in 10,000 rows over five seeds, so nothing was
being hidden in practice - but the guard is cheap and the claim was wrong.

**Findings 2 to 5 were four numbers stated wrongly**, each reproduced with one command: CHANGELOG
said 90.6% and 26 areas at 100% where the regenerated board says 90.7% and 28 (the slice's own two
un-skips moved it); STATE.md repeated the 26; ROADMAP said 18 fuzzy seams where `grep -o` counts 27,
contradicting this slice's own handover note; and three files said the recorder indexed a
"three-entry" codepoint table where the subject `'A'` gives a two-entry one. All four are fixed.

**No second pass was run, and that is a judgement rather than an omission.** The delta after the
findings is one clause in a predicate the reviewer had just read line by line and prescribed the fix
for, plus four number corrections it supplied itself, plus CSharpier reformatting. The new clause can
only make the wave redder - it removes rows from a classification - so its failure direction is the
safe one. No public API changed, no tooling changed, and the ratchet and the default oracle were
re-run after it.
