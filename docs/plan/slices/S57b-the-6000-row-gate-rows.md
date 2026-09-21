---
slice: S57b
phase: 6
title: The twenty rows of the 6000-row gate, judged
delivers: []
---

# S57b - The twenty rows of the 6000-row gate

Phase 6's exit gate is the differential oracle at `-Count 6000`, three seeds. S57 walked it and
found it RED - 3 rows at seed 7, 3 at seed 4242, 14 at the date seed 20260920, twenty distinct rows.
**This slice judges all twenty and turns the gate green. Nothing else.** S57 keeps the rest of the
phase close (bookkeeping, the Phase 7 handover) and commits after this one, because both of those
write that the phase is closed.

The full finding, the per-row door output and the evidence that none of it is S60's doing are in
`docs/plan/slices/notes/S57-sittings.md`, "Sitting 3 - 2026-09-20". Read that section, not this
file, for the rows themselves.

This slice's own working notes are in `docs/plan/slices/notes/S57b-sittings.md`.

## Scope

- **Every one of the twenty rows is judged**: this port right (an `ExpectedDivergences` entry with
  its mechanism, a permanent gap test carrying its provenance, and a `docs/DIVERGENCES.md` row if it
  is a deliberate difference a user can see), or this port wrong (an engine fix with a failing test
  first), or upstream's own defect (a `docs/plan/upstream-reports/LEDGER.md` entry). No row is
  parked, and no row is pinned on a reading of the table - each gets its mechanism.
- **Scripted in one pass, not row by row** (owner rule 2026-09-16, and the way S52's nineteenth
  sitting did what its first eighteen could not). `tools/probes/gate-divergence-doors.py` already
  asks every judged family's control of every diverging row in one run; start from its output over
  all three seeds, classify off the output, and only then write the pins. The amendment-16 ceremony
  (mechanism, blind review, verifier) runs ONCE over the batch, not once per row.
- **Date the rows with a scripted bisect.** The recorder changed after S52's close (S52c's
  metamorphic invariants, S53b), so a seed no longer draws the rows it drew then and "S52 closed
  green at 6000" is not a regression window. Bisect the ENGINE commits since S52 with a one-seed
  6000-row wave per step, driven by a script; a row that appeared with a known commit is judged
  against that commit's intent. `git worktree` + `git submodule update --init upstream` per step -
  the worktree needs the submodule or the recorder cannot run.
- **Two of the twenty are an upstream crash, not a divergence**: seed 20260920 rows 81232 and 87091,
  where upstream raises `IndexError: tuple index out of range` inside `get_firstset` on a reversed
  pattern this port handles. Those want a minimised reproduction and a ledger entry, and the ledger
  is drafted for the owner's approval, never filed (`docs/plan/upstream-reports/LEDGER.md`).
- **The gate re-run at the end**, `-Count 6000` at all three seeds plus the extra
  `fuzzy,interactions` wave at 99991 and 57057, and GREEN, with the numbers in the closing notes.

## Out of scope

- Phase 6's bookkeeping, the measured rate, the CHANGELOG entry, the Phase 7 handover. All S57's.
- Any optimisation, and any widening of a generator. A widened generator draws different rows and
  invalidates every control figure recorded against the current one.

## Done when

- [x] All twenty rows judged, each with its mechanism recorded and its probe committed under
      `tools/probes/`.
- [x] Every "port right" row has a permanent test whose expected value carries its provenance.
- [x] Every "port wrong" row has a failing test first, then the fix, and a DECISIONS line. None of
      the twenty is ours: row 72790 was the only candidate and sitting 4 killed it with a negative
      control.
- [x] Upstream's two `IndexError` rows have a minimised reproduction and a drafted ledger entry.
      Sitting 3 folded them into the existing ledger entry 6 rather than drafting a duplicate.
- [x] `pwsh -File tools/run-oracle.ps1 -Count 6000` GREEN at all three seeds. Re-run 2026-09-21
      after the last two pins: seeds 7, 4242 and 20260921, 0 of 126,080 rows each.
- [ ] `pwsh -File tools/run-oracle.ps1 -Generator fuzzy,interactions -Seeds 99991,57057` GREEN.
      First run, 2026-09-21: RED, four rows at 99991 and six at 57057, all unjudged.
- [ ] Ratchet GREEN, blind review, independent verifier, committed.
