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
**S73 is closed** (2026-09-20, eight sittings). The demo is a product: the answer in the first
viewport at 1366x768 and 1440x900, every string through a copy linter that runs in the suite, a
light identity pinned to what Chrome paints, the C# snippet panel, two-way match linking, the
per-edit underlay, a skip link, and scripted reference layouts. Spec and closing notes:
`docs/plan/slices/done/S73-demo-as-a-product.md`; the measurements:
`docs/plan/slices/notes/S73-sittings.md`. Ratchet GREEN, 6,406 tests, **baseline updated to 6,298**.
259 tests in `demo/web`, all green; `tools/build-demo-web.ps1` green.

**Next slice: the queue's lowest number, S57** (`docs/plan/slices/`). Phase 9's remaining demo
polish is in the S72 notes, not a slice.

**Waiting on the owner, both from S73.** The two reference layouts in `docs/demo/` need the owner's
eye - the only Done-when box this slice could not close itself, and it is ticked with that said.
And the demo is unpublished until the owner pushes `phase9-demo` and sets Pages > Source = GitHub
Actions.

**Maintenance, small and greppable.** Four comments cite `_regex.c:20535-20537` for upstream's
deletion shift, which is the top of `match_fuzzy_changes`; the shift is at `:20555-20558`. They are
`src/FuzzyRegex/Match.cs:389`, `src/FuzzyRegex/Engine/MatchState.cs:32`,
`tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyMatchingTests.cs:75` and `tools/record-oracle.py:1366`.
S73 fixed the four in its own files. Also open: `tools/check-ratchet.ps1:94` writes the
upstream-commit line wrongly when there is no submodule, and MAIN has a stray
`.github/workflows/pages.yml`.

**Two gates are RED, neither from S73** (both need engine code): the oracle at 1 row of 6380 at seed
`20260919` (triage in `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`,
deliberately not pinned), and the AOT publish, `IL2065` at `PublicApiDocumentationTests.cs:62`.
Stryker is paused for S58's benchmarks.

**Left running:** four `python -m http.server` (8090, 8092, 8137, 8199) and Vite (PID 34120), all
from earlier sittings. The Roslyn compiler-server stall cleared on its own.
