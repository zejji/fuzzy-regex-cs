---
slice: S53
phase: 6
title: The Native AOT dynamic gate - a published consumer that asserts real matches, in CI
delivers: []
---

# S53 - The AOT gate's dynamic half

`<IsAotCompatible>true</IsAotCompatible>` has enforced the static half since Phase 2 (amendment 12).
Static analysis cannot see a runtime-only failure, so the dynamic half is owed: a consumer published
with `PublishAot=true` that runs real matches.

## Scope

- **First, the whole suite natively (owner question 2026-09-16).** TUnit is source-generated and
  documents Native AOT as `<PublishAot>true</PublishAot>` plus `dotnet publish -c Release`
  (tunit.dev, engine modes; MSTest 4.4 shipped the same on 3 Sept 2026 for `net10.0`, so no
  .NET 11 is needed). Publish `tests/FuzzyRegex.Tests` with `-r win-x64` and `-r linux-x64` and run
  the produced executable: all 6,144 tests under the same trimming and reflection constraints a
  consumer's AOT app enforces, which is stronger evidence than any sample. Expected friction, and
  how it is handled: AwesomeAssertions is a FluentAssertions fork with reflection inside and is not
  marked AOT-compatible, so trim warnings will come from it. Handle them in the test project ONLY
  (replace the six `BeEquivalentTo` uses, or a suppression scoped to the test csproj with the
  warning code and reason recorded); `src/` keeps zero suppressions. If AwesomeAssertions cannot
  run natively at all, record the finding and the trade-off (TUnit's own assertions are AOT-clean)
  in STATE.md for the owner rather than switching libraries inside this slice. The oracle test
  project is NOT published (it parses JSON reflectively by design). Wire the native run into CI as
  the gate on both platforms.
- **Then `samples/FuzzyRegex.AotSmoke/`**, reduced to what only a consumer can prove: a project
  reference from a separate app, binary size and startup time for Phase 7's baseline, and a
  handful of feature cases as a smoke check. Original scope, kept for reference:
- **`samples/FuzzyRegex.AotSmoke/`**: a console app referencing `src/FuzzyRegex` by project, with
  `PublishAot=true`, exercising one case from every feature area: literals, classes, Unicode
  properties (which pull the transliterated tables), full case folding, named lists, lookaround,
  recursion, conditionals, verbs, partial, POSIX, fuzzy with counts and changes, `(?e)`, `(?b)`,
  substitution with templates and callbacks, `Split`, `Matches`, and a `MatchTimeout` that fires.
  Each case asserts the exact answer and the app exits non-zero on the first miss, printing which.
- **Publish and run in CI** on `windows-latest` and `ubuntu-latest` (the ILCompiler is
  platform-specific, so one platform is not evidence for the other), as a job in `ci.yml` that
  fails the build. Record binary size and startup time in the closing notes as Phase 7's baseline.
- **Trim warnings are errors** already (`TreatWarningsAsErrors`); confirm the published app has no
  `IL2xxx`/`IL3xxx` suppressions anywhere in `src/`, and that `Directory.Build.props` does not
  relax them for the sample.
- If the publish fails on a runtime-only path, that is the slice's finding: fix it in `src/`
  test-first, never by a `[DynamicDependency]` band-aid in the sample.

## Verification

- Both CI jobs green with the published binary's assertions; size and startup quoted.

## Done when

- [ ] Sample committed and asserting every feature area; CI jobs on two platforms.
- [ ] Zero suppressions; any runtime-only failure fixed in `src/`.
- [ ] Ratchet GREEN, blind review (hunt: an assertion that would pass on a wrong answer; a feature
      area whose table is only reached under a flag the sample never sets), commit.
