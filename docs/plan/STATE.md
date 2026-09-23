# Current state

**S61 in flight, checkpoint** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`), on branch `slice/s61`. Steps A to D and the allocation
gate are committed and blind-reviewed through 3d88f14. Ratchet GREEN at 6625 tests; AOT GREEN.
Sitting 3 (2026-09-23, 02:57) stopped at the allowance hook's order.

## Next, in this order

1. **Re-apply sitting 3's unrun work:** `git apply .scratch/s61-sitting3-wip.patch`. It holds
   `OracleWaveTests.A_pattern_that_has_answered_before_answers_every_row_as_a_fresh_one_does` (the
   "reset proven over a wave" item: each row asked fresh and again after the same pattern walked
   other text), a corrected comment in the lazy-walk test, and a `sync-divergence:` marker plus
   ledger row for `MatchStateCache`/`MatchState.Init` (`check-sync-divergence.ps1` was GREEN
   with it). The new test has NOT been run: run it through `tools/run-oracle.ps1` at three seeds,
   then prove it can fail (drop one line from `MatchState.Init`).
2. **Read the time gates.** Launched detached at 02:59 on a quiet machine (CPU 1%), `--job medium`:
   before (8dd746e, the `.scratch/base` worktree) under `artifacts/bench/2026-09-23-S61-gates-before`,
   done; after (HEAD) under `artifacts/bench/2026-09-23-S61-gates-after`, then the rest of the suite
   into the same folder. Filters `*ManyInputsBenchmarks*`, `*WorkloadBenchmarks.*MatchesToEnd*`,
   `*SpanOverloadBenchmarks*`. Progress in `.scratch/bench-gates.log`. Compare with
   `tools/compare-benchmarks.ps1 -UseExisting`; record time and bytes per step in the notes and
   fill the time cell of the new SYNC-DIVERGENCE row.
3. **Two oracle rows red at seed 20260923 are S87's**, planned on main in 0c2679c (row 3752 is a
   hang upstream shares; row 5185 an already-judged family). S61 only records the hand-off.
4. Closing notes, blind review of the sitting-3 delta, update the benchmark baseline, commit.

## Still pending from before S61

**S60b is a checkpoint, not a landing.** Its next sitting starts with the benchmark triage, then
re-runs the four negative controls, then its blind review (see its notes).
