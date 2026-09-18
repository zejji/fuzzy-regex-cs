---
slice: S64
phase: 8
title: README as the complete getting-started and nupkg readme, plus symbol packages
delivers: []
---

# S64 - The README a first user actually reads

`README.md` still says the library is "not yet usable". It is also the `PackageReadmeFile`, so it
is the nuget.org page and the GitHub landing page at once (research, 2026-09-16). This slice makes
it the complete getting-started, written from what is SHIPPED in `docs/DIVERGENCES.md` and the
public surface in `PublicAPI.Shipped.txt`, never from memory of upstream.

Runs in the `docs` worktree. `src/` is read-only for this slice except the two csproj lines below;
`DIVERGENCES.md` and `PORTMAP.md` are read-only.

## Scope

- **README.md**: what it is (a port of mrab-regex to .NET, fuzzy matching included) in two
  sentences; install (`dotnet add package`, with the version placeholder the release fills);
  a first match in ten lines; fuzzy matching in ten lines with the *expected output shown*;
  the three headline differences from `System.Text.RegularExpressions` and from Python `regex`
  with a link to `docs/COMPARISON.md` for the rest; the "try it in your browser" link placeholder
  that S71 fills (`<!-- demo-link -->`); thread-safety and timeout summary from the spec;
  licensing (`NOTICE` already covers attribution: do not restate, link); where the docs are.
  Every code sample compiles and runs: put each in `tests/FuzzyRegex.Tests/Docs/ReadmeSamples.cs`
  as a test that asserts the shown output, so the README cannot drift silently.
- **Symbols**: `IncludeSymbols=true` and `SymbolPackageFormat=snupkg` in `src/FuzzyRegex/FuzzyRegex.csproj`
  (Source Link is on by default in the .NET 10 SDK; verify with `dotnet pack` that a `.snupkg` appears).
- **Package metadata check**: `PackageId`, `Authors`, `Description`, `PackageTags`,
  `PackageProjectUrl`, `RepositoryUrl`, `PackageLicenseExpression`, `PackageReadmeFile` all present
  and consistent with the README; list what was missing in the closing notes.

## Verification

- `dotnet pack src/FuzzyRegex -c Release` produces `.nupkg` and `.snupkg`; open the nupkg readme
  (unzip) and confirm it is this README and renders (relative links resolve to GitHub URLs, since
  nuget.org does not rewrite them).
- The README sample tests pass and fail when a shown output is edited (prove once, revert).
- Ratchet green.

## Done when

- [x] README complete as above, samples pinned by tests, snupkg produced, metadata gaps closed
      (gaps listed below rather than closed - the two csproj lines this slice may touch are the
      symbol-package ones, not metadata).
- [x] Closing notes: what the README claims that Phase 7 might change, and the exact placeholder
      S71 must replace.

## Closing notes

**What landed.** `README.md`'s status line now says pre-1.0/feature-complete/API-frozen instead of
"not yet usable" (accurate against `docs/STATUS.md`'s 100% parity and the S53b freeze), and points
at `docs/plan/ROADMAP.md` for what remains rather than enumerating it, after a blind-review finding
that an enumerated list went stale immediately (see Review). A "Where the docs are" section links
`docs/COMPARISON.md`, `docs/DIVERGENCES.md`, the design spec and `OPERATIONS.md`. The three-bullet
"coming from `System.Text.RegularExpressions`" list gained a pointer to `COMPARISON.md` "for the
rest". `src/FuzzyRegex/FuzzyRegex.csproj` gained `IncludeSymbols`/`SymbolPackageFormat=snupkg` -
the only two lines this slice touched there, per scope - and `dotnet pack` now produces both a
`.nupkg` and a `.snupkg` (verified: the snupkg's `lib/net10.0/FuzzyRegex.pdb` is present).
`tests/FuzzyRegex.Tests/Docs/ReadmeSamples.cs` pins the three Quick Start samples' values directly
against the library (not by re-parsing the markdown - see below).

**No performance claims are in the README**; none were added, so there is nothing for Phase 7 to
invalidate.

**The exact placeholder S71 must replace**: `<!-- demo-link -->` on the line
`A browser demo is planned before 1.0. <!-- demo-link -->` in `README.md`. The sentence is written
to read sensibly on nuget.org too, where HTML comments render as nothing - the invisible comment
leaves a complete sentence rather than a dangling label (a blind-review finding; see Review).

**Package metadata gaps, left open rather than fixed** (the slice's csproj edits are scoped to the
two symbol-package lines above): `Authors` and `PackageProjectUrl` are not set anywhere in
`src/FuzzyRegex/FuzzyRegex.csproj` or `Directory.Build.props`. `PackageLicenseFile=LICENSE` is used
instead of `PackageLicenseExpression`, although `Apache-2.0 AND CNRI-Python` is valid SPDX syntax
and would work as an expression - not changed here since it is a licensing-metadata choice, not a
gap, and outside this slice's two permitted csproj lines. S66 (pack, validate, dry-run release
checklist) or the owner should decide `Authors`/`PackageProjectUrl` before 1.0 actually ships.

**A researched surprise: nuget.org does not rewrite a packaged README's relative links to the
source repository.** That rewriting is a third-party NuGet SDK extension
(`devlooped/readme`), not a built-in feature - checked Microsoft Learn's own readme-rendering page
(relative *image* paths are documented as simply not rendering) and a websearch that surfaced only
the third-party package as the mechanism, 2026-09-18, recorded in DECISIONS.md. Every repo-relative
link in `README.md` (to `docs/*`, `src/FuzzyRegex/PublicAPI.Unshipped.txt`) is now an absolute
`https://github.com/zejji/fuzzy-regex-cs/blob/main/...` URL instead, verified by unzipping the
packed nupkg (`.scratch/pack/FuzzyRegex.1.0.0.nupkg`) and confirming its `README.md` is
byte-identical to the repo's.

**A second, TUnit0055 surprise.** TUnit's analyzer forbids `Console.SetOut` (it can break TUnit's
own log correlation) and publishes no documented API to read a test's own captured console output
back for an assertion (checked tunit.dev's Test Context and Logging pages, and
thomhurst/TUnit#2144, 2026-09-18). `ReadmeSamples.cs` therefore asserts the *values* the README's
`Console.WriteLine` calls would print, computed by calling the library directly, rather than
redirecting `Console.Out`. `tools/check-doc-examples.ps1` remains the gate that catches the
sample's own source drifting from the README's markdown (it is still run in CI, `ci.yml`); the new
TUnit test is a faster, in-suite pin of the same three answers, not a replacement for it - true
markdown-to-execution fidelity inside a TUnit test would need either a subprocess build or Roslyn
scripting, and the latter is not Native-AOT-safe, which the whole suite is published as.

### Review

**Blind pass over the whole diff (`README.md`, `FuzzyRegex.csproj`, `ReadmeSamples.cs`): four
findings, all reproduced, three fixed.**

1. The status line's "what's left before 1.0" list omitted Phase 6 coverage, Phase 8's own
   remaining slices and the Phase 9 demo, contradicting the demo placeholder two lines below it.
   **Fixed**: the line now names all four areas and points at ROADMAP.md instead of trying to stay
   exhaustive.
2. The bare `**Try it in your browser:** <!-- demo-link -->` line renders as a dangling label on
   nuget.org, where HTML comments are invisible. **Fixed**: reworded to a complete sentence with
   the comment trailing it.
3. `ReadmeSamples.cs`'s original doc comment claimed an edited README example would fail a test;
   reproduced (change the fuzzy budget in the README's own code and the test is unaffected, since
   it never reads the file). **Fixed**: the comment now says what the test actually guarantees and
   names `check-doc-examples.ps1` as the markdown-fidelity gate.
4. `check-doc-examples.ps1` (via `ci.yml`) and `ReadmeSamples.cs` now both check the same three
   samples, by different mechanisms. **Not a bug** - deliberate defense in depth, recorded above
   rather than changed.

All four fixes were prose/doc-comment only, touching files the reviewer already saw in full; no
second blind pass was run. No probe, second-engine run or oracle claim is made in this slice, so
the independent-verifier step (amendment 16 limb (d)) does not apply - there is nothing of that
kind to reproduce.
