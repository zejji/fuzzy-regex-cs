# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase 8 (`docs` worktree, branch `phase8-docs`). S65 closed 2026-09-18.** `docs/COMPARISON.md`
checked row by row against every SHIPPED row in `docs/DIVERGENCES.md` (34 rows, all named); its
runnable examples are pinned (`tests/FuzzyRegex.Tests/Docs/ComparisonSamples.cs`, 27 tests). Two
new convention tests keep it that way: `Conventions/ComparisonCoversDivergencesTests.cs` and
`Conventions/PublicApiDocumentationTests.cs` (every public type/member carries an XML
`<summary>`/`<inheritdoc/>`, read from the generated `FuzzyRegex.xml` against the real reflected
surface). Found and fixed two real bugs along the way (DECISIONS.md, 2026-09-18): a naive
`Split('|')` desynchronised by a literal `|` inside a code span was silently dropping a row from
the scan, and a whole-file substring check let a COMPARISON cross-reference stand in for a
deleted section. Both proved by negative control, both fixed, both re-proved. COMPARISON.md's
`##`/`###` headings are frozen for S72 (full list in the slice's closing notes,
`docs/plan/slices/done/S65-comparison-completeness-and-convention-tests.md`).

**Next: S66**, `docs/plan/slices/S66-pack-validate-and-release-dry-run.md` - pack/validate/dry-run.
S64's closing notes flagged two package-metadata gaps still open for S66 or the owner to settle:
no `Authors`, no `PackageProjectUrl`; `PackageLicenseFile` used instead of
`PackageLicenseExpression` (a deliberate choice, not a gap - valid SPDX would be
`Apache-2.0 AND CNRI-Python`).

Ratchet: GREEN, 6307/6307 (6199 distinct ids), baseline 6199 (unchanged - S65 added tests, not
ported behaviour). `docs/STATUS.md` regenerated.

**Not carried from the pre-fork STATE.md**: this branch forked from `main` at the S55 Stryker
checkpoint. That content belongs to the mutation-testing worktree working Phase 6, not to Phase 8;
it will reconcile at merge time, which is expected under the parallel-worktree plan (ROADMAP.md,
2026-09-18 entry).
