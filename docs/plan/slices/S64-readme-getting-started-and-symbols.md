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

- README complete as above, samples pinned by tests, snupkg produced, metadata gaps closed.
- Closing notes: what the README claims that Phase 7 might change (performance statements are
  forbidden here; say so), and the exact placeholder S71 must replace.
