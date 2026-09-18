# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase 8 (`docs` worktree, branch `phase8-docs`). S64 closed 2026-09-18.** README.md is now the
complete getting-started page (status, install, three worked samples pinned by
`tests/FuzzyRegex.Tests/Docs/ReadmeSamples.cs`, differences from Python `regex` and from
`System.Text.RegularExpressions`, thread safety, licensing, "where the docs are"). Every
repo-relative link in it is now an absolute `github.com/zejji/fuzzy-regex-cs/blob/main/...` URL,
because nuget.org does not rewrite relative links itself (DECISIONS.md, 2026-09-18). Symbol
packages ship (`IncludeSymbols`/`SymbolPackageFormat=snupkg` on `FuzzyRegex.csproj`).

**Next: S65**, `docs/plan/slices/S65-comparison-completeness-and-convention-tests.md` -
`COMPARISON.md` checked row by row against `DIVERGENCES.md`'s SHIPPED rows and the two convention
tests. Two things S65 (or S66) should pick up from S64's closing notes:
- `README.md`'s `<!-- demo-link -->` placeholder (in "A browser demo is planned before 1.0.") is
  S71's to replace, not S65's.
- Package metadata gaps found but left open (S64's csproj edit was scoped to the two symbol-package
  lines only): no `Authors`, no `PackageProjectUrl`; `PackageLicenseFile` used instead of
  `PackageLicenseExpression` (valid SPDX would be `Apache-2.0 AND CNRI-Python`, not changed since
  it's a choice, not a gap). S66 (pack/validate/dry-run) or the owner should settle these.

Ratchet: GREEN, 6272/6272, baseline 6164 (updated this session). `docs/STATUS.md` regenerated.

**Not carried from the pre-fork STATE.md**: this branch forked from `main` at the S55 Stryker
checkpoint. That content belongs to the mutation-testing worktree working Phase 6, not to Phase 8;
it will reconcile at merge time, which is expected under the parallel-worktree plan (ROADMAP.md,
2026-09-18 entry).
