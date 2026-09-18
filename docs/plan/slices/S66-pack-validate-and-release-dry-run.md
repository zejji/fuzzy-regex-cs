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

- `RELEASE.md` exists and a rehearsal followed it end to end without a manual fix; the pack job is
  green in CI; the package metadata and validation are clean.
