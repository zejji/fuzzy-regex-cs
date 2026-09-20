# State

**Two slices are open: S60 is the one in hand, S57 stays in flight behind it.**

**S60 - prefilter family (Phase 7), checkpointed after sitting 3.** Read
`docs/plan/slices/notes/S60-sittings.md` first: sitting 3's route to close is written at its head
and only its first item is done. Landed in sitting 3: **the `(*SKIP)`-plus-partial oracle family is
judged** - rows 5014 (seed 20260920) and 5200 (seed 31337) are `partial-retry-carried-slice-
forward`'s rows 8 and 9, row 3633 (seed 31337) is `end-of-line-reads-a-skip-moved-slice`'s row 5
and that entry's first partial search. Port right on all three, by `(*PRUNE)`, the first anchor
upstream's own search tries, and on 3633 the `$` and `(?w)$` controls. Probe:
`tools/probes/upstream-skip-partial-anchor-grid.py`. Oracle GREEN at seeds 20260920 and 31337,
ratchet GREEN 6451/6451, baseline unchanged. Sitting 2's landings (item 1's case-sensitive forward
arm, item 5, item 15, 19 gap tests) are unchanged. Next sitting, in order:

1. **Blind review and the independent verifier over sitting 3's diff** - it has had NEITHER, because
   the sitting was cut at 92% of the allowance window. Judged rows, entry prose, new probe.
2. **A pinning test for row 3633's shape** in `Gaps/Engine/PartialMatchingTests.cs`.
3. **The benchmark**, on a quiet machine. The Stryker queue is mid-chunk in
   `.claude/worktrees/stryker`; `.scratch/pause-stryker.ps1` there pauses it and relaunching
   `.scratch/run-queue.ps1` resumes. `ReferenceBenchmarks.BacktrackingPort` now measures the
   prefilter refusing `(a|a)*b`, not the backtracker; say so when the number is re-run.
4. **Items 2, 3, 6, 8-14, 16, 17 move to a successor slice file** rather than to prose - sitting 3's
   note argues why they are not one sitting of porting (item 2 alone is ~30 `search_start_*`
   functions). That is a phase-plan change: spec amendment plus a ROADMAP row, same commit.
   **No `SearchValues` in `src/` yet**, so the slice file's item-4 box stays unticked.

**S57 - Phase 6 close-out, checkpointed, untouched since its own sitting 1.** Checklist and
next-sitting order in `notes/S57-sittings.md`: items 5, 7-11 untouched, 911 uncovered lines
unclassified, **no blind review and no verifier over the S57 diff**. Its item 1, the oracle report's
one red seed, is what this sitting judged, so that item is now closed. It closes Phase 6 behind
S56's survivor triage, which waits on the Stryker queue.

Standing: `git merge-base --is-ancestor <sha> HEAD` before quoting a SHA. `-Count` on
`run-oracle.ps1` is PER GENERATOR. `-Seeds` takes a comma-separated string; the third default seed
is today's date, so a date-seeded red row is a gate blocker only until midnight.
