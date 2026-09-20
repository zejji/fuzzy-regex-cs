# State

**S60 is closed (2026-09-20, four sittings). S57 stays in flight. Nothing is in progress.**

**S60 - the prefilter family, closed on its required-string half** (the forward
`locate_required_string` arm, the per-pattern needle, the `(*SKIP)` constraint implemented rather
than asserted, scope items 5 and 15, 19 gap tests, three judged oracle rows). Spec, controls and
both review passes: `docs/plan/slices/done/S60-prefilter-family.md`; measurements:
`notes/S60-sittings.md`. Items 2, 3, 6, 8-14, 16 and 17 moved to
`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, item numbers kept (spec
amendment 30 plus a ROADMAP paragraph). **Phase 7 is nine slices.**

**Gates at the commit.** Ratchet GREEN, 6,459 tests, baseline updated to 6,351. Oracle GREEN at the
three default seeds (7, 4242, 20260920). Both AOT gates GREEN - the `IL2065` STATE reported RED does
not appear. The 20260919 red row was date-seeded and is no longer drawn (triage:
`docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`).

**Next slice: S57**, the lowest number in `docs/plan/slices/` (Phase 6 close-out, checkpointed).
Order and checklist in `notes/S57-sittings.md`: items 5 and 7-11 untouched, 911 uncovered lines
unclassified, **no blind review and no verifier over the S57 diff**. Its item 1 closed in S60.

**Benchmarks here** need `.claude/worktrees/stryker/.scratch/pause-stryker.ps1` AND `after-parsing.ps1`
there stopped - that watcher relaunched the queue mid-run at 08:54 today. Queue running since 10:47;
resume after a pause with `.scratch/run-queue.ps1` there.

**Waiting on the owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push
`phase9-demo`, Pages > Source = GitHub Actions). **Maintenance:** four comments cite
`_regex.c:20535-20537` for the deletion shift, which is at `:20555-20558` (`Match.cs:389`,
`Engine/MatchState.cs:32`, `Gaps/Engine/FuzzyMatchingTests.cs:75`, `tools/record-oracle.py:1366`);
`tools/check-ratchet.ps1:94` writes the upstream-commit line wrongly with no submodule; MAIN has a
stray `.github/workflows/pages.yml`. **Left running:** four `python -m http.server` (8090, 8092,
8137, 8199) and Vite (PID 34120).
