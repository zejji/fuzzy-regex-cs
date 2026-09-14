# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S48b IS STILL A CHECKPOINT (2026-09-14, sitting 2). The slice file stays in `docs/plan/slices/`.**
Suite 5,963 (+4), ratchet GREEN, default wave GREEN at three seeds, **and the 6000-row three-seed
gate is back to 6 + 4 + 9 = 19**, which is S48's baseline exactly and the slice's Verification bar.

**All five of the gate's new rows are classified.** Sitting 1 named two families; measurement found
THREE, plus one row neither control reached. `tools/record-oracle.py` gained two second questions -
`posixFreeOutcome` and `atomicFreeOutcome`, in a new `_CONTROLS` table - and three
`ExpectedDivergences` entries were added, each keyed on judged questions AND this port's exact
answer: `posix-fuzzy-contradicts-its-own-flagless-answer` (3 rows),
`atomic-group-leaks-a-change-position` (1) and `reversed-lookahead-change-at-the-match-start` (1).
Four new gap tests, plus an over-classification guard that feeds every example row upstream's own
answer and a total failure and asserts neither is classified.

**The fifth row was NOT upstream's fault by default, and sitting 1's claim about it was wrong.**
Seed-7 row 73463: this port answered neither upstream's drawn answer nor its anchored one. Bisecting
against a worktree at `c8165b5` showed S48b's own truncation moved it. What justifies the new answer
is upstream contradicting ITSELF between directions - `A(?=[^A]{e<=1})A+\D` over `'AAA'`, forward 1,
reversed 0 - with the condition measured as a GENERAL REPEAT after the lookahead. Ledger 11 gains
mechanisms E and F, both NOT SHARED.

**WHAT IS LEFT, and it is the only reason this is still a checkpoint:**
1. **The independent verifier has NOT run** (spec amendment 16 limb (d)). Everything else is done.
2. **Blind review: one pass, 3 findings raised, 3 reproduced, 3 fixed** - all accuracy defects in
   text this sitting wrote (a false claim that `(?p)` scopes to its group; row 76101 described as a
   cost change when the COST is identical and the SPAN moves; a stale entry name in two files). The
   fixes are doc comments and a probe docstring only, so a second blind pass is not owed, and the
   ratchet was re-run GREEN after them.
3. Then tick the "Done when" boxes, `git mv` to `done/`, and the slice closes. Nothing else is open.

**Negative controls are in the closing notes, NOT `controls.json`, and that is a finding:**
`run-controls.py` records its waves BEFORE applying a mutation and caches them, so it cannot measure
a control that mutates the recorder. Applied by hand they fire exactly where their family is drawn:
POSIX no-op 4->6 at seed 7 and 3->4 at 20260914; atomic no-op 3->4 at 20260914 and 4->4 at seed 7.
The 76 stale `.scratch/control-waves` were deleted. **S42-2A is in the same position - owed.**

**Owed maintenance (unchanged):** `FOLD_TURKIC`'s share of the `case-folding` rotation; the two
broken control sites S32-B and S38-A; S35-A and S29-A/D are thin; PORTMAP's `_regex.c` line
references stale after the sync; `record-oracle.py --self-check` exits 1 on a pre-S46 message.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
**Housekeeping:** the `s48b-baseline` and `pre-s48b` worktrees can go once the slice closes.
