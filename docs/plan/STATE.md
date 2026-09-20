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

**Gates at the commit.** Ratchet GREEN, 6,460 tests, baseline 6,352. Oracle not re-run (no engine
code changed). **Next slice: S57**, the lowest number in `docs/plan/slices/` (Phase 6 close-out, checkpointed).
Order and checklist in `notes/S57-sittings.md`: items 5 and 7-11 untouched, 911 uncovered lines
unclassified, **no blind review and no verifier over the S57 diff**. Its item 1 closed in S60.

**Carried from S73/S60:** the owner owes the `docs/demo/` reference layouts and the Pages publish
(push `phase9-demo`, Source = GitHub Actions); four comments cite `_regex.c:20535-20537` for the
deletion shift, which is at `:20555-20558`; `tools/check-ratchet.ps1:94` writes the upstream-commit
line wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`; a benchmark here
must stop `.scratch/pause-stryker.ps1` AND `after-parsing.ps1`. Left running: four
`python -m http.server` (8090, 8092, 8137, 8199) and Vite (PID 34120).
