# Releasing FuzzyRegex to nuget.org

How to publish a release, reused for every version. Pushing a tag such as `v1.0.0` starts
`.github/workflows/release.yml`, which re-runs the CI gates, packs, waits for your approval, pushes
to nuget.org and creates the GitHub release. The rehearsal record and the gate list for 1.0 are in
[`docs/plan/RELEASE.md`](plan/RELEASE.md).

nuget.org never lets a version number be used twice and never deletes a package. Every step below
is arranged so that nothing reaches nuget.org until you approve it.

## One-time setup (manual)

Do these once, before the first release. None of them can be automated, because each needs the
owner's account.

1. **nuget.org Trusted Publishing policy.** Sign in to nuget.org, open your user name menu, then
   **Trusted Publishing**, and add a policy: repository owner `zejji`, repository `fuzzy-regex-cs`,
   workflow file `release.yml` (the file name only), environment `nuget`. Give it the scope that
   allows pushing new packages, and include `FuzzyRegex` if it asks for packages. This lets the
   workflow swap a GitHub sign-in token for a nuget.org API key that lasts one hour, so no
   long-lived key is stored anywhere.
2. **GitHub environment.** Repository **Settings > Environments > New environment**, named `nuget`.
   Add yourself (`zejji`) as a required reviewer. Leave **Prevent self-review** off: you push the
   tag and you approve, and with it on you could not approve your own run. Under deployment
   branches and tags choose **Selected** and add a tag rule `v*.*.*`.
3. **Environment secret.** In the `nuget` environment add a secret `NUGET_USER` holding your
   nuget.org profile name (not your email address).
4. **The workflow on main.** `release.yml` must be merged to main before you tag: a tag runs the
   workflow as it is in the tagged commit.

## Before you tag (manual checklist)

- The gates in [`docs/plan/RELEASE.md`](plan/RELEASE.md#gates-that-must-be-green-before-s69-tags-anything)
  are green, with the evidence recorded in section 7 of
  [`docs/plan/2026-10-16-plan-to-1.0.md`](plan/2026-10-16-plan-to-1.0.md).
- CI is green on main for the commit you will tag.
- `VersionPrefix` in `Directory.Build.props` is the version you are releasing.
- `CHANGELOG.md` has the entry for this version.
- The status line at the top of `README.md` names this version and links its release notes. The
  package carries the README as it is in the tagged commit, so a stale status line stays on that
  version's nuget.org page for good (1.1.0 shipped saying "Status: 1.0.0").
- The release notes are at `docs/plan/release-notes-<version>.md` (for example
  `release-notes-1.0.0.md`) and have been reviewed. The GitHub release body is everything after the
  first line that is exactly `---`. The path is set once, in `RELEASE_NOTES_PATH_FORMAT` at the top
  of `release.yml`.

## The release

| Step | Who | What happens |
| --- | --- | --- |
| 1. Tag | MANUAL | `git tag v1.0.0 <commit on main>` then `git push origin v1.0.0`. |
| 2. Gates | AUTOMATED | All of `ci.yml` runs again on the tagged commit. |
| 3. Checks | AUTOMATED | The tag must equal `v` + `VersionPrefix` (`tools/check-release-tag.ps1`), the commit must be on main, the notes file must exist. |
| 4. Rehearsal | AUTOMATED | `tools/run-release-rehearsal.ps1`: installs a `-rc.0` pack of the same commit into a new console app and publishes it as Native AOT on Linux. |
| 5. Pack | AUTOMATED | `tools/pack-and-validate.ps1` packs the real version and validates it. The `.nupkg` and `.snupkg` are kept as the run's `packages` artifact. |
| 6. Approve | MANUAL | The run stops at **Push to nuget.org** and waits for you. |
| 7. Push | AUTOMATED | Signs in to nuget.org through Trusted Publishing and pushes the `.nupkg`; the `.snupkg` beside it goes up with it. |
| 8. GitHub release | AUTOMATED | Creates the release for the tag, with the notes. |
| 9. Check | MANUAL | See "After the release". |

**What the approval looks like.** GitHub notifies you that a deployment needs review. On the run's
page click **Review deployments**, tick `nuget`, optionally add a comment, then **Approve and
deploy**. **Reject** fails the run and nothing is published. Before approving you can download the
`packages` artifact from the run page and inspect exactly what will be pushed.

nuget.org then validates and indexes the package, which usually takes under 15 minutes. It shows as
unlisted until that finishes, and you get an email when it is done.

## When something goes wrong

- **A step before the approval fails.** Nothing has been published. Fix the problem on main, then
  move the tag: `git push --delete origin v1.0.0`, re-tag the fixed commit and push again. Moving a
  tag is safe only because nothing left GitHub.
- **The push gets 403 Forbidden on the very first release.** The Trusted Publishing policy does not
  yet allow a new package called `FuzzyRegex`. Fix the policy's scope and package list, then use
  **Re-run failed jobs**. If it still fails, push once by hand: create a classic API key on
  nuget.org scoped to "Push new packages and package versions" with glob `FuzzyRegex` and a short
  expiry, run `dotnet nuget push FuzzyRegex.<version>.nupkg --api-key <key> --source
  https://api.nuget.org/v3/index.json` on the files from the `packages` artifact, then delete the
  key. Re-run the failed jobs afterwards: the push step skips the version that is already there and
  the GitHub release is created.
- **The push worked but the GitHub release failed.** Re-run the failed job. Nothing is pushed twice.
- **A re-run cannot find the `packages` artifact.** Use **Re-run all jobs** instead. The pack is
  rebuilt from the same commit and the push skips any version already on nuget.org.
- **A bad version reached nuget.org.** You cannot delete it or publish the same number again.
  Unlist it (it stays restorable by exact version for anyone already using it) and, if it is
  harmful, mark it deprecated with a message. Then fix, bump `VersionPrefix` (for example to
  `1.0.1`) and release that.

## After the release (manual)

- The nuget.org page renders the README and lists the symbols package.
- The demo link in the README works from nuget.org as well as from GitHub.
- A new console app restores `FuzzyRegex` from nuget.org only, runs the README's first sample and
  publishes as Native AOT (Step 2 of `docs/plan/RELEASE.md`, with nuget.org as the only source).
- Open the next `[Unreleased]` section in `CHANGELOG.md`.
- Set `PackageValidationBaselineVersion` in `src/FuzzyRegex/FuzzyRegex.csproj` to the version you
  just published, so every later pack checks the public API against it. It is `1.1.0` now.
- Bump `VersionPrefix` when work on the next version starts.

## Sources (read 2026-10-20)

- Trusted Publishing: https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing
- Publishing, API key scopes, indexing time: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package
- Symbol packages: https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg.
  `dotnet nuget push` pushes the `.snupkg` that sits beside the `.nupkg` when the source supports
  symbols: NuGet.Client `PushRunner.cs` and `PackageUpdateResource.cs` at commit 4c4510d2.
- No deletion, unlisting, deprecation: https://learn.microsoft.com/en-us/nuget/nuget-org/policies/deleting-packages
- Environments and approvals: https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments
  and https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/review-deployments
- 403 on a new package id until the policy covers it: https://github.com/IAFahim/grid-influence/issues/31
