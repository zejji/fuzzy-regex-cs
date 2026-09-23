# Current state

**S61 in flight** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`), on branch `slice/s61`. Landed so far: step A (one state
per lazy walk), steps B and C (a warm pattern reuses one match state; a warm `IsMatch` allocates
nothing), the allocation gate in `compare-benchmarks.ps1`, and step D (span option (a): `IsMatch`
and `Count` take a `ReadOnlyMemory<char>` and read it in place). Ratchet GREEN at 6625 tests.
Every commit through aa67e98 has been blind-reviewed. The last pass found no defects; the review
fixes are in the S61 notes.

Ledger entry 18 (bytes per repetition of a capture group) was handed to S86 in writing.

## Next, in this order

1. **Two oracle rows red at seed 20260923**, both reproduced at 8dd746e, before any S61 change, so
   neither is S61's. They still need minimising and pinning: row 5185 (partial-sliced, a `(*SKIP)`
   pattern, upstream (0, 2), port (1, 1)) and row 3752 (interactions, `Split` with flags 0x400a
   runs for more than 10 minutes where upstream returns 2 parts). Details are in the S61 notes.
3. **Time gates, on a quiet machine only**: `*WorkloadBenchmarks.*MatchesToEnd*`, `*ManyInputs*`
   and `*SpanOverload*`, then the full suite at `--job medium`. Allocation has already decided each
   step; time decides whether each one stays.
4. AOT tests and smoke test, with the binary size checked against 6,972,928 bytes. Then the closing
   notes.

## Still pending from before S61

**S60b is a checkpoint, not a landing.** Its next sitting starts with the benchmark triage after
22:00, then re-runs the four negative controls, then its blind review (see its notes).

S84 (a full-folded backreference that ends half-way through a folding) is queued.
