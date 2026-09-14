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

- [ ] Each of the three has a mechanism established by measurement, not by hypothesis, and is
      fixed test-first or parked with the evidence and the reason it cannot be fixed here.
- [ ] Ledger entries 9 and 11 rewritten to what is then true; `ExpectedDivergences` and
      `DIVERGENCES.md` updated for any new deliberate difference.
- [ ] Ratchet GREEN, blind review (hunt: a counts/changes restore that silently truncates a
      `END_FUZZY` merge; a POSIX fix that moves a `(?b)` answer), independent verifier, commit.
