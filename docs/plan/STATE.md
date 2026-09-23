# Current state

**S61 in flight, checkpoint** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`), on branch `slice/s61`. Steps A to D, the allocation gate
and the wave-scale reset test are committed and blind-reviewed. Ratchet GREEN at 6625 tests; AOT
GREEN. Sitting 4 (2026-09-23, 05:24) stopped for the orchestrator's 06:40 merge.

## Next, in this order

1. **Time gates.** The orchestrator measures them on a quiet machine after merging. The notes'
   "How the orchestrator decides each open time gate" names the filters and the keep/revert rule
   per step. Record the verdicts in the notes. A failed gate means the next sitting takes that
   step back out.
2. **`EnumerateMatches(ReadOnlySpan<char>)` returning a `ValueMatchEnumerator`**, signed off
   2026-09-22, gated on a measured gain. Not started. New public API: `update-public-api.ps1`, the
   documents the slice file lists, its own blind pass.
3. Update the benchmark baseline from the quiet-machine run, closing notes, move the slice to
   `done/`.

## Hand-offs

- Oracle rows 3752 and 5185 (seed 20260923) and 4957 (seed 99, new) are S87's. All three
  reproduce at 8dd746e, before S61.
- Ledger entry 18 is S86's (50d0aec).

## Still pending from before S61

**S60b is a checkpoint, not a landing.** Its next sitting starts with the benchmark triage, then
re-runs the four negative controls, then its blind review (see its notes).
