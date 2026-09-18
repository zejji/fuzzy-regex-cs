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
