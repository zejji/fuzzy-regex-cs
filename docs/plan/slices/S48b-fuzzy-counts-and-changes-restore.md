---
slice: S48b
phase: 6
title: The fuzzy counts and the change list are saved and restored together - ledger 11 mechanisms C and D, and ledger 9's remaining port-side count bug
delivers: []
---

# S48b - Counts and changes, saved and restored as one thing

Authored by S48 on 2026-09-14, out of its inventory of ledger entries 1-15. S45-S48 were the
inherited-bug group and they closed every entry except three, and those three are **one mechanism
seen three ways**: upstream saves and restores the fuzzy COUNTS as a block and unwinds the CHANGES
one item at a time, and nothing keeps the two in step across a construct that abandons a
sub-attempt without backtracking through it. Ledger entry 11 says so in its own words - "That is a
slice of its own" - and this is that slice.

The owner's rule of 2026-09-12 is what makes it mandatory rather than optional: the list of known
bugs in this port, ours or inherited, is empty before Phase 7 touches the engine.

## Scope

Three items, in this order, because the first is the one the other two are probably instances of.

- **Ledger 11 mechanism C** (inherited, both engines agree, so the oracle is blind to it):
  `POSIX` and `BESTMATCH` candidates leave the change list polluted or empty against the saved
  counts. Its worst measured case carries a **fourteen-entry** change list against counts of
  `(0,0,1)`; its twin has counts `(1,0,1)` against an **empty** list. Both rows, with their flag
  bits, are written out in the ledger entry.
- **Ledger 11 mechanism D** (inherited, ditto): a lookaround under `(?e)` restores a counts block
  whose changes were unwound item-wise, giving counts `(1,0,0)` against a list holding one
  DELETION. Found by S47's own wave property at seed 4242 on its first run.
- **Ledger 9's remaining port-side bug**, which is THIS PORT's and is open and red:
  `POSIX` plus `(?e)` loses an error count. Re-measured on the committed code, 2026-09-14:

  ```
  (?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}  over '+ aBA', fullmatch
    upstream, POSIX     (0, 5) counts (0, 1, 1)
    this port, POSIX    (0, 5) counts (1, 1, 1)     <- three errors for the same span
    this port, no POSIX (0, 5) counts (0, 1, 1)     <- and it is self-refuting
  ```

  The ledger's standing hypothesis, **not yet proven**, is that `RestoreBestMatch` puts back
  `FuzzyCounts` and `FuzzyChanges` but not `state.TotalErrors` or `state.TotalCost`, and this
  port's ranking reads both where upstream's keeps the last successful run. Upstream leaves
  `total_errors` stale too (`restore_best_match`, `:11565`), so the staleness is inherited and the
  cost field is not. Prove it before fixing it; the `/Od /Zi` MSVC build S47c installed and proved
  is available and is the instrument (see `docs/plan/OPERATIONS.md`).

- **The fix is not local, and the slice's real work is the judgement rather than the edit.** It is
  to save and restore the change list wherever the counts are saved and restored - eight
  `PushFuzzyCounts` sites and eleven `PopFuzzyCounts` sites in `Matcher.cs` - each needing a
  decision about whether the semantics are "restore" (truncate the list too) or "merge" (leave it
  alone, as `END_FUZZY`'s forward arm needs). Take them one at a time, with the wave between.
- Until it lands, `Match.FuzzyCounts` is tallied from the change list ONLY on a partial match and
  taken from the counter otherwise. That stopgap is S47's, it is deliberate, and this slice is
  what replaces it - say in the closing notes whether it stayed or went.

- **Do not widen the ledger 9 fix into the ranking rule** (orchestrator, 2026-09-14). S41/S42's
  cost ranking for `(?e)`/`(?b)` is an owner decision; if the bug is that the ranking reads stale
  totals after `RestoreBestMatch`, fix the staleness and leave the rule. Update the inherited-bugs
  row of `docs/DIVERGENCES.md` from PLANNED (S48b) to SHIPPED in the same commit.

## Verification

- `OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` is the instrument
  that can see C and D at all, because the oracle cannot: both engines agree on them. Widen it if
  it still cannot reach C, whose POSIX rows have no positions to count on either side (entry 9).
- A negative control per mechanism, run at a seed the slice has not used, with both numbers
  recorded. Expect them to be thin: S48's own S48-A fired **zero** at four seeds over 24,000
  `interactions` rows, which is a finding about the generator and not about the fix.
- Default wave GREEN at three seeds; the 6000-row three-seed gate no worse than the 19 rows S48
  left, and each of the three items checked against that list rather than assumed absent from it.

## Done when

- [x] Each of the three has a mechanism established by measurement, not by hypothesis, and is
      fixed test-first or parked with the evidence and the reason it cannot be fixed here.
      *(Sitting 1: all three fixed, all three measured. Ledger 9's was established by instrumenting
      the walk; C and D by before/after on the ledger's own rows plus upstream's own controls.)*
- [ ] Ledger entries 9 and 11 rewritten to what is then true; `ExpectedDivergences` and
      `DIVERGENCES.md` updated for any new deliberate difference.
      *(Sitting 1: entries 9 and 11 rewritten, entry 16 added, `DIVERGENCES.md` moved to SHIPPED.
      **`ExpectedDivergences` is NOT done** and is the whole of what makes this a checkpoint - see
      "What is LEFT" above.)*
- [ ] Ratchet GREEN, blind review (hunt: a counts/changes restore that silently truncates a
      `END_FUZZY` merge; a POSIX fix that moves a `(?b)` answer), independent verifier, commit.
      *(Sitting 1: ratchet GREEN, blind review done (2 raised, 2 reproduced, 2 fixed), independent
      verifier done (9 CONFIRMED, 1 DIFFERENT, corrected), checkpoint commit made. Re-run all of it
      on the sitting that closes the slice.)*

---

# Progress, sitting 1 (2026-09-14) - CHECKPOINT, the slice is NOT closed

## What landed

**All three scoped items are fixed, each with its mechanism established by measurement.**

- **Ledger 9's port-side count bug: mechanism proven to the line, fixed, closed.** Instrumenting
  `DoEnhancedFuzzyMatch` printed one line per run of the walk and the answer was one stale field:
  under POSIX, run 2 restores the RIGHT counts `(0, 1, 1)` and reads `TotalErrors == 3` from the
  candidate that lost, because the POSIX `FAILURE` arm's `RestoreBestMatch` copies `FuzzyCounts` and
  `FuzzyChanges` and not the running totals. `TotalErrors >= fewestErrors` is then `3 >= 3`, the walk
  is cut, and run 1's three errors stand. `SaveBestMatch`/`RestoreBestMatch` now carry
  `BestTotalErrors`/`BestTotalCost`. **The ranking RULE is untouched**, as the slice's own guard
  requires - `IsBetterFuzzyMatch` is not edited.
- **Ledger 11 mechanisms C and D: fixed by ONE edit, not nineteen.** `PushFuzzyCounts` pushes the
  change list's LENGTH beside the counts block; `PopFuzzyCounts` truncates back to it. The nineteen
  sites reduce to one judgement each: eight are a **restore** and truncate
  (`ATOMIC`/`END_ATOMIC`, `CONDITIONAL`/`END_CONDITIONAL`, `LOOKAROUND`/`END_LOOKAROUND`) and three
  are a **merge** (`PopFuzzyCountsMerging`: both `END_FUZZY` arms and the `FUZZY` backtrack arm).
  The buffer the pop writes into is a useful smell - a scratch span is always a merge - but it is
  **not** a rule, and the blind review found the counter-example: the `FUZZY` backtrack arm merges
  into `state.FuzzyCounts`.
- **The two are not independent.** Mechanism C came in two shapes - list too LONG (the worst case,
  and D) and too SHORT (the twin) - and only the first is the save/restore bug. The twin was fixed by
  ledger 9's stale-totals fix, because it carries `(?b)` as well as `(?p)`. Truncation cannot fix a
  list that is too short, and the truncation deliberately never GROWS the list.

**Suite 5,959 (+4), all green. Default oracle wave GREEN at three seeds** (7, 4242, 20260914).

**The S47 stopgap STAYED, with its justification changed.** `Match.FuzzyCounts` is still tallied from
the change list only on a partial - that is S47's mechanism-B fix and unrelated to C and D.
`SplitFuzzyChanges`'s `Math.Min` bound also stays, but it is now upstream's own line (`:20522`) and
nothing more: with C and D fixed the two views agree on every path, so it is a no-op rather than the
thing holding an arbitrary answer down. Both notes are corrected in `Match.cs` and the ledger.

## What was measured

Re-run on the committed tree: `pwsh -File tools/probes/port-fuzzy-counts-and-changes.ps1` and
`python tools/probes/upstream-fuzzy-counts-and-changes.py`.

| row | before | after | upstream |
|---|---|---|---|
| C worst, match 2 | `(4,6)` counts `(0,0,1)` changes `sub[4]` | `del[4]` - agrees | codepoints `(3,4)` counts `(0,0,1)`, changes `sub[3]` (still contradicts) |
| C twin, match 5 | `(0,4)` counts `(1,0,1)` changes `[]` | seven self-consistent matches | six matches; the seventh is entry 16 |
| D | `(6,8)` counts `(1,0,0)` changes `del[7]` | `sub[8]` - agrees | codepoints `(4,6)` counts `(1,0,0)` changes `del[5]` (still contradicts, anchored too) |
| ledger 9 | `(0,5)` counts `(1,1,1)` | `(0,5)` counts `(0,1,1)` changes `([],[4],[0])` | `(0,5)` counts `(0,1,1)`, same positions |

**Two new upstream findings, both with upstream contradicting itself.**

- **Ledger entry 16, new**: a `POSIX` overlapped scan of a `BESTMATCH` fuzzy pattern drops its
  LONGEST match. Three other doors and upstream's own `fullmatch` at the identical flags all answer
  `(0, 9)`; only `POSIX`+`BESTMATCH` starts at `(0, 8)`. Drawn independently a second time by the
  gate at seed 20260914 (row 76101).
- **Entry 11 gains an ATOMIC GROUP door**, and S47's anchored `_leak_free_fuzzy` question cannot see
  it, because the leak is within ONE attempt. Upstream's own control settles it: replace `(?>` with
  `(?:` and upstream moves its deletion from codepoint 2 to 3 - UTF-16 4, this port's answer.

**The 6000-row three-seed gate is 9 + 4 + 11 = 24, against S48's 6 + 4 + 9 = 19.** The five new rows
were identified by REPLAY, not by arithmetic: saved waves consumed by a worktree at HEAD
(`.claude/worktrees/s48b-baseline`, `-SkipRecord`). Seed 20260914 whole gave baseline 9 against 11;
seed 7 narrowed to `interactions,conditionals,partial` - which carry all nine of its divergences -
gave baseline 6 against 9. Both baselines reproduce S48's recorded figures exactly.

The five, and which family each is:

| seed | row | family |
|---|---|---|
| 7 | 73463 | change positions; fuzzy section in a LOOKAHEAD with `(*PRUNE)` |
| 7 | 73895 | `(?b)(?e)(?r)(?p)` - entry 16; the port spends 0 errors where upstream spends 1 |
| 7 | 76983 | `(?e)(?r)(?p)` - ledger 9's mechanism; port `(0,0,1)` against upstream `(1,0,1)`, same span |
| 20260914 | 74033 | change positions; ATOMIC group |
| 20260914 | 76101 | `(?b)(?e)(?r)`+POSIX - entry 16, independently drawn |

**On every one of the five this port's answer is the one upstream's own control gives.** None is a
regression; all five are upstream defects this port stopped sharing.

## What is LEFT, and why this is a checkpoint

**The five new rows are judged but not CLASSIFIED**, so the gate is 24 rather than <= 19 and the
slice's Verification bar is not met. Classifying them needs work the recorder does not yet support:

1. **A new recorded control per family.** Neither existing discriminator reaches these.
   `leakFreeFuzzy` re-asks upstream anchored, which removes an EARLIER attempt's leak and cannot see
   an atomic group's; `bestmatchFreeOutcome` removes `(?b)`, and row 76983 has no `(?b)` - it is
   `(?e)`+POSIX. What is needed is a **POSIX-free** outcome for the better-fit family and an
   **atomic-free** (or more generally backtracking-cut-free) control for the change-position family.
2. **Two `ExpectedDivergences` entries** keyed on those controls plus their recorded example rows,
   in the shape S47b established.
3. **Re-run the three-seed 6000-row gate** to confirm the entries absorb exactly these five and
   nothing else, and that the total returns to 19.

`docs/DIVERGENCES.md`'s inherited-bugs row is already moved to SHIPPED (S48b) as the slice asks,
because the three scoped fixes ARE shipped; the two NEW upstream findings are ledgered (entries 11
and 16) and are what remains to be pinned in the oracle.

## Negative controls

Both are in `tools/controls.json` and re-run with
`python tools/run-controls.py --ids S48b-A,S48b-B`. Numbers below were taken **after the last code
change**, against the tree being committed.

**They fire in an unusual direction, and that is the finding.** Each fault makes this port reproduce
upstream's bug again, so restoring it makes the port AGREE with upstream more often: the divergence
count goes DOWN, not up. That is a detection, but it is the wrong instrument, because the oracle is
blind to mechanisms C and D by construction - both engines share them, so a shared bug reports as
agreement. **The instrument that actually sees these is the suite**, and it is recorded beside each
control below.

> **Control S48b-A, `counts-restored-without-truncating-the-change-list`**: in
> `src/FuzzyRegex/Engine/MatchState.cs`, `TruncateFuzzyChanges`, change
> `        if (FuzzyChanges.Count > changeCount)`
> to
> `        if (FuzzyChanges.Count < changeCount)`
> so the truncation never fires. Wave: `interactions`, 6000 rows.
> Seed 31337: **8 diverge unmutated, 6 mutated**. Seed 555: **3 unmutated, 3 mutated - IT DOES NOT
> FIRE AT THIS SEED.**
> Suite: **5,957 of 5,959 pass, 2 fail** -
> `FuzzyCountsAndChangesTests.A_posix_overlapped_scan_reports_changes_of_the_kinds_it_counted` and
> `.A_lookaround_under_enhancematch_reports_changes_of_the_kinds_it_counted`.

> **Control S48b-B, `posix-restore-leaves-the-running-totals-stale`**: in
> `src/FuzzyRegex/Engine/Matcher.cs`, `RestoreBestMatch`, delete
> `        state.TotalErrors = state.BestTotalErrors;`
> `        state.TotalCost = state.BestTotalCost;`
> and the blank line after them. Wave: `interactions`, 6000 rows.
> Seed 31337: **8 unmutated, 6 mutated**. Seed 555: **3 unmutated, 2 mutated** - thin.
> Suite: **5,957 of 5,959 pass, 2 fail** -
> `.A_posix_enhancematch_fullmatch_spends_no_more_errors_than_the_same_span_needs` and
> `.A_bestmatch_posix_overlapped_scan_reports_a_change_for_every_error_it_counted`.

**The two controls partition the four new tests 2 + 2, and the partition is itself evidence**: the
twin (`A_bestmatch_posix_overlapped_scan...`) falls to the STALE-TOTALS control and not to the
truncation one, which is the independent confirmation that C's twin was ledger 9's bug rather than
the save/restore desynchronisation the slice was scoped around.

**A first draft of S48b-A would not compile** and so reported `NO REPORT` at both seeds: it deleted
the `TruncateFuzzyChanges(changeCount);` call, which left `changeCount` assigned and unread
(IDE0059, an error here). Reshaped into the one-character comparison flip above. A control that does
not build is not a weak control, it is no control, and the harness says so rather than scoring zero.

## Review and verification

**Blind review: one pass, 2 findings raised, 2 reproduced, 2 fixed.** Both were accuracy defects in
text this slice wrote, and both were real:

1. `MatchState.PopFuzzyCounts`'s remarks said `PopFuzzyCountsMerging` is used at "the two sites" and
   named only `END_FUZZY`. There are **three** - grepping `PopFuzzyCountsMerging` in
   `src/FuzzyRegex/Engine/Matcher.cs` gives lines 5230, 8047 and 8505 - and the third is the `FUZZY`
   backtrack arm.
2. **The rule of thumb this slice wrote down is false, and the counter-example is that same third
   site.** "Which is which is readable off the buffer the pop writes into: `state.FuzzyCounts` is a
   restore, a scratch span is a merge" was stated in `PORTMAP.md`, `DECISIONS.md` and these notes.
   `Matcher.cs:8505` **merges into `state.FuzzyCounts`**. The buffer is a useful smell, not a rule;
   corrected in all four places to say so, and `DECISIONS` records the first draft and its killing
   rather than quietly replacing it.

**No second blind pass was needed, and the reason is what changed rather than how the first pass
went**: both fixes are XML doc comment and markdown text, in the very passages the reviewer read and
flagged. No public API, no tooling and no production logic was touched after the review - the `src/`
diff between the review and the commit is doc comments only.

**Independent verifier (spec amendment 16 limb (d)): a FRESH Opus agent, briefed with nothing but
the commit-ready tree, re-ran every number these notes and the ledger quote. Nine CONFIRMED, one
DIFFERENT, and the DIFFERENT is corrected here rather than kept.**

- CONFIRMED: suite 5,959/0; ratchet GREEN; default wave GREEN at three seeds; both probes and every
  value quoted from them, port and upstream, including the seven-against-six C-twin scan and all
  three upstream `fullmatch` answers of `(0, 9)`; both negative controls, applied by hand, failing
  exactly the two named tests each, with the tree restored to 5,959/0; both controls' wave numbers
  and the unmutated 8 / 3 baseline; the 8 push / 11 pop / 3 merging counts; and `DIVERGENCES.md`'s
  row reading SHIPPED.
- **DIFFERENT: `python tools/run-controls.py --check` does NOT resolve every site - it reports two
  FAILs**, `S32-B` and `S38-A`, both "the 'before' text does not appear after the anchor". This
  slice's own two sites resolve. **The claim was wrong because it was read off a `tail` of the
  output** - precisely the lesson S48 recorded on 2026-09-14, repeated here within a day.
  **Proven NOT to be this slice's doing** by running the same command in a worktree at HEAD, where
  both FAIL identically; that mattered, because `S32-B`'s anchor is `RestoreBestMatch`, the function
  this slice edited. These are the "two broken control sites" STATE.md already carries as owed
  maintenance.

**One incidental observation from the review, recorded as UNVERIFIED**: the reviewer's own
22,400-row sweep of atomic, lookaround, conditional and nested-fuzzy bodies completed in minutes on
the patched engine and had produced no row after about twenty minutes on a build of HEAD, which
would mean this change removes a hang rather than introducing one. **This slice did not reproduce or
minimise that**, and it is written down as a lead, not a result. The sweep's headline figure - zero
counts-versus-changes contradictions across 22,400 rows, including the POSIX rows the wave invariant
cannot reach - is corroboration of the fix and was likewise not independently re-run.
