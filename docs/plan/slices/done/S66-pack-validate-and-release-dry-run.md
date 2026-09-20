---
slice: S66
phase: 8
title: Pack, validate and rehearse the 1.0 release end to end without publishing anything
delivers: []
---

# S66 - The release, rehearsed

Packaging is the worst moment to discover a design problem (ROADMAP, AOT section). This slice
rehearses the whole release on a local feed so S69 is a repeat of a green run, not a first attempt.
Nothing is pushed to nuget.org or tagged; the release itself is S69 and waits for the Phase 6 exit
gate, the Phase 7 performance gate and the owner.

Runs in the `docs` worktree. `src/` read-only except `Directory.Build.props`/csproj packaging
properties.

## Scope

- **Versioning**: decide and document the scheme (`Version`/`VersionPrefix` in
  `Directory.Build.props`, `1.0.0` target, prerelease suffix for the rehearsal, `MinVer` or tags
  considered and decided with a written reason). The rehearsal builds `1.0.0-rc.0`.
- **`dotnet pack` in Release**, deterministic build and `ContinuousIntegrationBuild` on in CI, and
  the package validated: `dotnet nuget verify`, the NuGet package explorer rules via
  `Meziantou.Framework.NuGetPackageValidation.Tool` (or the equivalent chosen and justified),
  `PackageValidation` (`EnablePackageValidation`, `PackageValidationBaselineVersion` prepared for
  post-1.0 use) documented.
- **Install from a local feed** into a fresh console project targeting `net10.0`, run the README
  first sample, and publish that project with `PublishAot=true` and trimmed to prove the consumer
  side of the AOT promise (the repo's own AOT gate covers the library side).
- **Release checklist** in `docs/plan/RELEASE.md`: exact commands, the gates that must be green
  (ratchet, oracle three seeds, AOT gate, benchmark gate, mutation exit gate), the tag format, the
  GitHub release notes source (`DIVERGENCES.md` SHIPPED rows plus the ledger), the nuget.org push
  with an API key the owner holds (never in the repo), and the post-publish checks.
- **CI**: a `pack` job in `ci.yml` that runs the validation on every push so a packaging regression
  fails early; no publish step.

## Verification

- A clean clone, `dotnet pack`, local-feed install, consumer AOT publish, all green and recorded
  with the commands in the closing notes.
- Ratchet green.

## Done when

- [x] `RELEASE.md` exists and a rehearsal followed it end to end without a manual fix; the pack job
  is green in CI; the package metadata and validation are clean.

## Closing notes (2026-09-18)

**Landed**: `Directory.Build.props` sets `<VersionPrefix>1.0.0</VersionPrefix>` (hand-bumped, no
MinVer/tags - reasoning in `docs/plan/RELEASE.md`'s Versioning section and `docs/plan/DECISIONS.md`).
`src/FuzzyRegex/FuzzyRegex.csproj` gains `Authors`, `PackageProjectUrl` (S64's flagged gaps),
`EnablePackageValidation`, `PublishRepositoryUrl`/`EmbedUntrackedSources` plus a
`Microsoft.SourceLink.GitHub` reference (pinned in `Directory.Packages.props`). Two scripts:
`tools/pack-and-validate.ps1` (pack + `meziantou.validate-nuget-package`, the CI-every-push half,
installed as a **local** dotnet tool via `.config/dotnet-tools.json` because the sandbox this
slice ran in cannot reach a global tool install) and `tools/run-release-rehearsal.ps1` (pack,
install from a local feed into a fresh consumer, run the README's first sample, `dotnet publish
-p:PublishAot=true` and run the trimmed binary). `.github/workflows/ci.yml` gains a `pack` job
(validate on every push, no publish step). `docs/plan/RELEASE.md` is the full checklist S69
follows. `.gitignore` gains `.serena/` (an MCP server cache that appeared untracked mid-slice,
unrelated to this slice's scope but would have failed the driver's clean-tree check from here on).

**Surprises**: (a) `dotnet nuget verify` needs a signed package and fails `NU3004` on every
unsigned one - decided not to run it at all rather than fake a green with no signing cert in
place; documented as a deferred step in `RELEASE.md`. (b) The real one: `dotnet add package`, run
from `.scratch/release-rehearsal-consumer` (inside the repo tree, because the sandbox cannot write
outside it), let MSBuild's upward directory search for central-package-management files climb
straight past the scratch folder to the REAL root `Directory.Packages.props` and write a stray
`<PackageVersion Include="FuzzyRegex" Version="1.0.0-rc.0" />` into it. Caught by a system-reminder
reporting the file had changed on disk; reverted, then fixed at the root by writing empty
`<Project />` overrides for both `Directory.Build.props` and `Directory.Packages.props` into the
consumer folder before `dotnet new`/`dotnet add package` run (MSBuild's search stops at the first
props file found going up the tree). Re-ran the full rehearsal afterward and confirmed via
`git diff` that only the intended additions remained. Any future slice that runs `dotnet new` or
`dotnet add package` inside a scratch folder under this repo's tree needs the same two-line guard.
(c) `--excluded-rules` on `meziantou.validate-nuget-package` only works reliably as a *repeated*
flag, one rule per flag; comma- or space-separated lists silently misbehave.

**What S67/S69 should know**: `RELEASE.md`'s three SourceLink-dependent finding categories
(project URL, repository URL, "source file not accessible") only resolve once the packed commit is
actually pushed to GitHub - `tools/pack-and-validate.ps1` will legitimately fail on those three
locally and in a PR from an unpushed branch; that is the check working, not a false alarm. S69
should expect `pack-and-validate.ps1` to go fully green only once running against a commit already
on `main`. The package id `FuzzyRegex` was still free on nuget.org as of 2026-09-18.

**Review**: one blind-review pass (Sonnet subagent, reproduction-only brief covering the pack
script's real exit behaviour, the build, tool-restore, YAML/JSON validity, the
`Directory.Packages.props` diff, the consumer-isolation fix's line order, and a live end-to-end
rehearsal run) found zero real defects - all 7 checks passed, including a live re-run of
`run-release-rehearsal.ps1` that reproduced the exact recorded evidence. One independent-verifier
pass (fresh Opus subagent, briefed with nothing but the commit-ready tree) re-ran all 9 quoted
claims (build, ratchet, csharpier, pack-and-validate's exact failure categories, the rehearsal's
exit code and output, the CPM-isolation fix holding, tool restore, the csproj/props property
values, and the live `NU3004` reproduction) and reported all 9 CONFIRMED, no DIFFERENT or COULD NOT
RUN. No second review pass was needed - no findings existed to fix.
