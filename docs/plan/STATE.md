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

**This session (2026-09-18, ran out of road before any code):** S56 still blocked -
`dotnet-stryker.exe` (PID 50328) confirmed running in the `stryker` worktree, no reports yet, so
its engine survivor queue has not finished. Picked up **S56b** instead (independent of S56, runs
on `main` after S55 per its own file) and read its scope, the investigation doc and the existing
`matchTimeout` threading pattern in `FuzzyRegex.cs` (widest public ctor takes it as a required
param, cascades down via `: this(...)` to the internal ctor with `defaultVersion`, which calls
`Engine.PatternObject.Compile(_compiled)`). Plan for next sitting: add `maxCompiledNodes` the same
way (optional param, default `DefaultMaxCompiledNodes = 1_000_000`, threaded through every ctor
tier to `PatternObject.Compile`), store the budget on `PatternObject` and check
`NodeList.Count` at the top of `NodeCompiler.CreateNode` (`NodeCompiler.cs:205`) before `Add`,
throwing `FuzzyRegexParseException` (has a `(message, pattern, offset)` ctor already) naming the
count and budget. No code written yet - write the four tests first per the slice file's
`Gaps/Engine/` list, watch them fail, then implement. Tree was clean at session start and stays
clean; nothing to stash.
**Phase 8 (`docs` worktree, branch `phase8-docs`). S67 closed 2026-09-18.** Added `context7.json`
(repo root) and `docs/plan/FINDABILITY.md`, docs-only, `delivers: []`. Nothing submitted to
Context7 or DeepWiki - both need the owner's accounts and are listed as owner steps with proof
checks. **Blocker for the owner steps: `zejji/fuzzy-regex-cs` on GitHub is currently private or
not yet pushed** (`curl https://api.github.com/repos/zejji/fuzzy-regex-cs` returns 404
unauthenticated, 2026-09-18) - both registrations need it public first. Full account:
`docs/plan/slices/done/S67-registries-context7-and-deepwiki.md`.

**Next: S68**, `docs/plan/slices/S68-remarks-divergence-notes-after-phase-7.md`.

Ratchet: GREEN, 6343/6343 (6235 distinct ids), baseline 6235 (unchanged - S67 is docs-only,
`delivers: []`). `docs/STATUS.md` unchanged by this slice; note its parity-commit line will drift
on any run here because this worktree's `upstream` submodule checkout differs from what was last
committed to `docs/STATUS.md` (unrelated to S67 - not investigated, not fixed here).

**Not carried forward**: the S70/S71 checkpoint content that was in this file before S67 belonged
to `phase9-demo`, a different branch sharing this STATE.md through a prior merge. That work lives
on its own branch and reconciles at merge time (ROADMAP.md, 2026-09-18 entry); it is not part of
`phase8-docs`'s remaining queue (S68, S69).

**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop; the `zejji/fuzzy-regex-cs` repo
visibility blocker above.
