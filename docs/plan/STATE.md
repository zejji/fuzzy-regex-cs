# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S55 closed on `main` 2026-09-18.** Mutation testing tooling built (`tools/run-stryker.ps1`,
`stryker-config.json`, `tools/stryker-queue.json`), calibrated, and its three in-scope chunks -
API layer (`*.cs`), `Parsing/*.cs`, `Engine/Substitution.cs` - all ran to completion with **zero
survivors** (237/237, 2156+29 Timeout+4 RuntimeError/2189, 261/261). The 4 RuntimeError mutants
are recursion-bound-removing mutations in `ParseFunctions.cs`, correctly detected by crashing
the host rather than a scored result. Full numbers and mechanism analysis:
`docs/plan/mutation/2026-09-17-api-parser.md`. Blind review (0 findings) then an independent
verifier pass (6 real corrections, all fixed) ran before commit; full account:
`docs/plan/slices/done/S55-mutation-testing-tooling-and-calibration.md` and
`docs/plan/slices/notes/S55-sittings.md`.

**Next: S56**, `docs/plan/slices/S56-mutation-testing-engine-survivors.md`. Blocked on the engine
mutation queue: 59 `engine-rand-*` chunks (~7,300 mutants over 12 files) running detached in the
`stryker` worktree (branch `stryker-queue`), started 2026-09-18. Measured rate from S55's
`parsing-remaining` (543 mutants, 4h05m, 2 runners = 2.2/min) puts that queue at roughly 55
machine-hours at 2 runners - not something to wait on inline; check for finished reports before
starting S56's sitting. That worktree's settings (`stryker-config.json`, `run-stryker.ps1`,
`stryker-queue.json`, `TestThreadsLimit.cs`) are approved but stay on `stryker-queue`, not
copied to `main` - they land at merge time.

Ratchet: GREEN, 6343/6343 (6235 distinct ids), baseline 6235 (unchanged - S55 is
tooling/measurement, `delivers: []`). `docs/STATUS.md` regenerated (upstream submodule moved
independently to `7dd71c1`; no test-count change).

**Parallel streams, untouched this session:** Phase 8 (`docs` worktree, branch `phase8-docs`) is
at S67 (`docs/plan/slices/S67-registries-context7-and-deepwiki.md`), S66 already merged to
`main`. Phase 9 (`demo` worktree, branch `phase9-demo`) has S70 parked as a checkpoint, not done
- see `docs/plan/slices/notes/S70-sittings.md`; needs a Playwright permission grant for the
browser leg. Neither is this slice's concern.

**Open for the owner (carried, unverified this session):** `slice-log.jsonl` marks S26
`failed`; `origin/main` needs a push; benchmark baseline needs retaking on a quiet machine.
