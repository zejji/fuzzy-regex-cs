# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase 8 (`docs` worktree, branch `phase8-docs`). S66 closed 2026-09-18.** `docs/plan/RELEASE.md`
is the full 1.0 release checklist; S69 follows it exactly. Versioning is a hand-bumped
`VersionPrefix` (`1.0.0`), no MinVer. `src/FuzzyRegex/FuzzyRegex.csproj` gained the metadata S64
flagged missing (`Authors`, `PackageProjectUrl`) plus SourceLink and `EnablePackageValidation`.
Two rehearsal scripts: `tools/pack-and-validate.ps1` (CI's `pack` job, every push - fails locally
on three SourceLink findings that only resolve once the packed commit is pushed to GitHub, by
design) and `tools/run-release-rehearsal.ps1` (install from a local feed into a fresh consumer,
run the README sample, publish trimmed Native AOT - rehearsed green, see DECISIONS.md 2026-09-18
for the log). `dotnet nuget verify` is skipped (no signing cert; `NU3004` confirmed). Caught and
fixed mid-slice: `dotnet add package` run inside a repo-tree scratch folder can leak into the real
root `Directory.Packages.props` via MSBuild's central-package-management search - the rehearsal
script now writes empty `<Project />` overrides into the scratch consumer first. Full account:
`docs/plan/slices/done/S66-pack-validate-and-release-dry-run.md`.

**Next: S67**, `docs/plan/slices/S67-registries-context7-and-deepwiki.md`.

Ratchet: GREEN, 6307/6307 (6199 distinct ids), baseline 6199 (unchanged - S66 is packaging/infra,
`delivers: []`). `docs/STATUS.md` unchanged.

**Not carried from the pre-fork STATE.md**: this branch forked from `main` at the S55 Stryker
checkpoint. That content belongs to the mutation-testing worktree working Phase 6, not to Phase 8;
it will reconcile at merge time, which is expected under the parallel-worktree plan (ROADMAP.md,
2026-09-18 entry).
**S70 IS A CHECKPOINT, NOT DONE (sitting 2, 2026-09-18, branch `phase9-demo`).** Slice file stays
in `docs/plan/slices/`. Full detail: `docs/plan/slices/notes/S70-sittings.md`.

**Green at this commit.** Ratchet GREEN, 6305 passing, 0 failing, 0 skipped (baseline 6197 ids).
`dotnet build FuzzyRegex.slnx -c Release`: 0 warnings. `tools/run-wasm-smoke.ps1`: WASM SMOKE
GREEN, 22 files, 6.94 MB on disk, 1.98 MB gzip, 44 integrity endpoints. The port itself is the
largest file at 3.62 MB - half the bundle, twice the runtime.

**The one thing left is the browser leg.** `harness.html` has never been run in a browser: the
Playwright MCP tool was not permission-granted this session, so the round trip, the `terminate()`
proof and the responsiveness counter are unexecuted and the warm-time baseline is unmeasured. Run
`python -m http.server 8080` from `demo/FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot`,
open `harness.html`, read `window.__harness`. **Owner: this needs a Playwright permission grant.**

**Do not re-measure the compile blow-up.** `Run` has no clock over `FuzzyRegex` construction, so a
pattern like `(((a{100}){100}){100}){100}` - inside `MaxPatternLength` - never returns. It is
recorded in `DemoEngine.MatchTimeout`'s remarks and sliced as S56b on `main`. Probing it took the
machine to 0 GB free on 2026-09-18. Every subagent brief now carries
`DOTNET_GCHeapHardLimit=0x40000000`, a wall-clock timeout and a subject cap. **This is the argument
for S71 treating `worker.terminate()` as its primary control, not an error path.**

**Two blind passes ran** (16 findings, 9 fixed; then 6 over the fix delta, 4 fixed). The
independent verifier has NOT run - it belongs to the closing commit.

**Also left:** the smoke script's new missing-artefact branch is reasoned, not seen to fire; the
zero-trim-warning claim needs restating from a clean publish against the committed code.

**Carried from main:** benchmark baseline still needs retaking on a quiet machine
(`pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline`, ~32 min). `.claude/skills/benchmark/
SKILL.md` documents a stale run command. `_regex.c:22091`'s comment on `text_length` is wrong.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop. The orchestrator merges `phase9-demo`.
