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
