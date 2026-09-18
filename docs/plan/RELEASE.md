# The 1.0 release checklist

Written and rehearsed by S66; followed exactly by S69, which is a repeat of this rehearsal with
the real version, the tag and the nuget.org push. Nothing in this file has been run against a
real publish target - `nuget.org push` and `gh release create` are S69's, not S66's.

## Versioning

`Directory.Build.props` sets `<VersionPrefix>1.0.0</VersionPrefix>`. No MinVer, no git-tag-derived
scheme: decided because there is exactly one release in flight at a time (1.0.0, then whatever
follows next) and no version tags exist yet in this repo, so MinVer's benefit - a distinct,
deterministic version for every commit between tags - has nothing to attach to. `dotnet pack`
takes `-p:VersionSuffix=rc.N` for a rehearsal build (this file's rehearsals use `rc.0`); the real
1.0.0 release passes no suffix. Bump `VersionPrefix` by hand for every release after 1.0.

## Gates that must be green before S69 tags anything

- **Ratchet**: `pwsh -File tools/check-ratchet.ps1 -Configuration Release` - GREEN, no ticket
  regressions.
- **Oracle, three seeds**: `pwsh -File tools/run-oracle.ps1` (three seeds by default since S34;
  never accept a single-seed run as evidence - `docs/VERIFICATION.md` rule 7a).
- **AOT gate**: `pwsh -File tools/run-aot-tests.ps1` (the whole suite, published natively) and
  `pwsh -File tools/run-aot-smoke.ps1` (the consumer sample via a project reference) on both
  win-x64 and linux-x64.
- **Benchmark gate**: Phase 7's baselines, `pwsh -File tools/compare-benchmarks.ps1` clean against
  the recorded baseline (S63).
- **Mutation exit gate**: Phase 6 closed (S56 survivors triaged, S57's coverage backstop in
  place).

S69 is blocked on all of the above plus S64-S68 and S71 (the roadmap's own gate list). This file
does not re-derive that list; it only says what to run once those gates are already green.

## Step 1: pack and validate

```powershell
pwsh -File tools/pack-and-validate.ps1                  # real release: no VersionSuffix
pwsh -File tools/pack-and-validate.ps1 -VersionSuffix rc.0   # rehearsal
```

Packs `src/FuzzyRegex` in Release with `ContinuousIntegrationBuild=true` (so the shipped PDB never
embeds a contributor's local path - see the SourceLink note below) and runs
`meziantou.validate-nuget-package` against the result. `EnablePackageValidation` (shape checks:
matching TFM assets, consistent dependencies) already runs as part of the `dotnet pack` step
itself, from the property in `src/FuzzyRegex/FuzzyRegex.csproj`; it has no
`PackageValidationBaselineVersion` yet because there is no earlier published version to diff
against - set it to `1.0.0` once a 1.1.0 or later release is being prepared.

This is also `ci.yml`'s `pack` job, run on every push. Three findings only resolve once the packed
commit is actually pushed to GitHub (SourceLink maps PDB paths to
`raw.githubusercontent.com/zejji/fuzzy-regex-cs/<sha>/...`, which needs that `<sha>` to exist on
GitHub): the project URL check, the repository-accessibility check, and every "source file not
accessible" finding. Rehearsing this script against an uncommitted or unpushed tree will always
fail on those three for that reason - it is not a false alarm, it is the SourceLink checks working
correctly on a commit that genuinely is not public yet. A contribution from a fork would embed the
wrong owner in this URL and legitimately fail the pack job's SourceLink checks; acceptable for a
single-maintainer project, revisit if outside contributions start.

`dotnet nuget verify` is not run: it requires a signed package (`NU3004` otherwise, checked
2026-09-18), and there is no code-signing certificate set up for this project. If the owner sets
one up before 1.0, add it back as a step here.

**A package icon does not exist.** `IconMustBeSet` is excluded from validation
(`--excluded-rules IconMustBeSet` in `tools/pack-and-validate.ps1`) because no artwork exists - a
branding decision for the owner, not a packaging gap. Add `<PackageIcon>` plus the file once one
does, and drop the exclusion.

**Package metadata, checked 2026-09-18** (the gaps S64 flagged): `Authors` is now
`Gerard Howell` (from `NOTICE`'s copyright line) and `PackageProjectUrl` is now the GitHub repo
URL, both in `src/FuzzyRegex/FuzzyRegex.csproj`. `PackageLicenseFile=LICENSE` stays as-is (S64's
call): `Apache-2.0 AND CNRI-Python` is valid SPDX and would work as `PackageLicenseExpression`,
but nuget.org renders a `PackageLicenseFile` identically for a reader and this way the license text
itself ships in the package rather than a link off it. The package id `FuzzyRegex` is still
available on nuget.org - checked 2026-09-18 (`meziantou.validate-nuget-package` raised no
`PackageIdAvailableOnNuGetOrg` finding), so nothing else has claimed the name.

## Step 2: rehearse the install and the AOT publish

```powershell
pwsh -File tools/run-release-rehearsal.ps1
```

Packs again on its own account (so it can run before Step 1 is green - see the SourceLink note
above), then: creates a fresh `dotnet new console` project outside the repo's normal project tree,
points a `nuget.config` at the pack output folder (a folder of `.nupkg` files is a valid feed on
its own - nothing is pushed to any real feed), `dotnet add package FuzzyRegex` from it, runs the
README's first Quick Start sample against the installed package, then `dotnet publish
-p:PublishAot=true` for that consumer and runs the trimmed native binary, checking its output
matches too.

This is deliberately not the same check as `samples/FuzzyRegex.AotSmoke` (the AOT gate above): that
project reaches the library through a project reference and roots nothing in the trimmer; this one
reaches it through a real package install, the way every other consumer will, and is the only place
that proves the **packaging** - the `.nupkg`'s dependency and asset metadata - does not break the
library's own AOT compatibility.

**Isolation, learned the hard way (2026-09-18).** The consumer project is created under
`.scratch/`, inside the repo tree, because the sandbox this slice ran in cannot write outside it.
Central package management (`Directory.Packages.props`) and `Directory.Build.props` apply to
*every* project under the repo root by MSBuild's own upward directory search, regardless of how
deep or throwaway that project is - the first rehearsal run of this script wrote a stray
`<PackageVersion Include="FuzzyRegex" Version="1.0.0-rc.0" />` straight into the real
`Directory.Packages.props`, because `dotnet add package` found no local override and climbed to
the root file. Fixed by writing an empty `<Project />` `Directory.Build.props` and
`Directory.Packages.props` into the consumer folder before `dotnet new`/`dotnet add package` run,
which stops MSBuild's search at that folder. If this script's target directory ever moves outside
the repo, the empty overrides become unnecessary but are harmless to leave.

Real evidence, win-x64, 2026-09-18 (`.scratch/rehearsal.log`, not committed):

```
dotnet run: 0: 2026-09-16, year: 2026, month: 09, day: 16
AOT publish: 0: 2026-09-16, year: 2026, month: 09, day: 16
binary: Consumer.exe  6.77 MB, win-x64, installed from a local feed, trimmed
```

## Step 3 (S69 only): tag, release, publish

Not rehearsed here - S66 stops before anything leaves the local machine.

- **Tag format**: `v1.0.0` (the conventional `v`-prefixed semver tag GitHub's release UI expects).
- **GitHub release notes**: generated from `docs/DIVERGENCES.md`'s `SHIPPED` rows (every deliberate
  difference from upstream that actually shipped) plus `docs/plan/upstream-reports/LEDGER.md`
  (bugs found and their resolution). Do not write release notes from memory - re-read both files at
  release time, the way `DIVERGENCES.md`'s own header requires.
- **nuget.org push**: `dotnet nuget push <path>.nupkg --api-key <key> --source
  https://api.nuget.org/v3/index.json`. The API key lives with the owner and is never committed,
  logged, or pasted into a session transcript; S69 asks for it interactively at the point of use
  and nowhere else.
- **Post-publish checks** (S69's own verification list): the nuget.org package page renders the
  README; the symbol package resolves (`snupkg`, already produced by every pack in this file);
  the demo link in `README.md` (`<!-- demo-link -->`, S71's placeholder) works; a consumer AOT
  sample builds against the *published* package, not a local feed - i.e. Step 2 above run once
  more with `nuget.org` as the only source.
