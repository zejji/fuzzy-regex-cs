# State

**S56 is closed (2026-09-20, one sitting, branch `stryker-queue`). S57 stays in flight.**

**S56 - the engine's mutation survivors.** There are none: 4,673 in-window mutants across the 59
`engine-rand-*` chunks, **0 Survived**, 46 Timeout, 14 RuntimeError, all 60 judged with probes
(`tools/probes/s56-mutant-behaviour.py`) and all detections. Numbers and caveats:
`docs/plan/mutation/2026-09-20-engine.md`; sitting detail: `notes/S56-sittings.md`.
`Gaps/Engine/HangBoundTests.cs` now guards the assembly-wide 120 s `[Timeout]`.

**Two caveats S57 and Phase 7 must carry.** 533 in-window mutants never compiled, 185 of them in
`Matcher.cs::BasicMatch` and `DoEnhancedFuzzyMatch` (Stryker Safe Mode, on a `CS0165` it will not
attribute); and 1,048 lines of today's `Matcher.cs`, `PatternObject.cs`, `NodeCompiler.cs` and
`FuzzyRegex.cs` have never been mutated, because the engine moved under the three-day queue.
`tools/stryker-queue.json` ends with `engine-topup-01..09` (54 windows) covering those lines -
**queued, not run**, per the orchestrator's brief; regenerate with `tools/stryker-topup-windows.py`
if those files change. `NoCoverage` is absent everywhere because coverage analysis was off, not
because the lines are reached.

**S74 is closed** (2026-09-20). The demo's flags text box is a collapsible checkbox panel: a summary
row, ten checkboxes, two radio groups for the only two pairs the library refuses, and a per-flag help
sentence that is the enum's own `<summary>`. The flags string stays the state, so old shared links
and every `examples.json` row load unchanged. Spec, deviations, evidence and the three blind passes:
`docs/plan/slices/done/S74-flags-control.md`. Ratchet GREEN, 6,461 tests, **baseline updated to
6,353**; 297 web tests in 14 files; typecheck and `npm run build` green.

**S57 is CHECKPOINTED after three sittings (2026-09-20). Nothing is in progress.**

**Take S57b next, not S57** - the driver's lowest-numbered file is S57 and it is blocked, which its
own first paragraph says. Sitting 3 ran Phase 6's exit gate as the phase defines it,
`run-oracle.ps1 -Count 6000` at three seeds, and it is **RED: diverge 3 (seed 7), 3 (4242), 14
(20260920), twenty distinct rows**. Not S60's doing - the pre-S60 tree gives 3, 3, 15 on the same
command - and not datable to S52, because the recorder changed after it. The rows, their door
output and the recreate commands are in `notes/S57-sittings.md`, "Sitting 3 - 2026-09-20"; the slice
that judges them is `docs/plan/slices/S57b-the-6000-row-gate-rows.md` (spec amendment 32 and a
ROADMAP paragraph). **Phase 6 does not close until that wave is green.**

**What S57 still holds**, and only after S57b: items 10 and 11, the bookkeeping and the Phase 7
handover, both of which write that the phase is closed. The rest of its checklist is ticked. For
Phase 7 to regress against: 878 uncovered lines, 257 members, 0 wholly-unentered opcode arms.

**Five ledger entries are inherited and unfixed, and no slice schedules them** (the gate's own
question, answered NO): 17 in part, 18, 19, 20, 21. Three pieces of work - 19+20 are one bug
(`\m`/`\M` against a fuzzy section, upstream 563/564), 21 needs PCRE2's `hitend` model and is a
slice, 18 is a memory-cost bug that belongs in S61. Slices for them amend ROADMAP and the spec, so
it is the owner's call.

**Handed to Phase 7:** `tools/run-controls.py` needs a `suite` mode and a multi-generator wave
before S50's C, S50b's four, S52d's A and S53b's B can be registered.

**Benchmarks here** need `.claude/worktrees/stryker/.scratch/pause-stryker.ps1` AND `after-parsing.ps1`
there stopped. **Waiting on the owner, both from S73:** the `docs/demo/` reference layouts, and
publishing (push `phase9-demo`, Pages > Source = GitHub Actions). **Maintenance:** four comments cite
`_regex.c:20535-20537`, which is at `:20555-20558`; `tools/check-ratchet.ps1:94` writes the
upstream-commit line wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`.
