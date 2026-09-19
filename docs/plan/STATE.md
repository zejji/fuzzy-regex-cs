# State

**S73 is in flight** (started 2026-09-19), chunks 1, 2 and 3 of 5 committed, green and closed.
Spec: `docs/plan/slices/S73-demo-as-a-product.md`. Chunk plan, review record and numbers:
`docs/plan/slices/notes/S73-sittings.md`.

Chunk 1 landed the copy linter and the rewritten strings, chunk 2 the shell, chunk 3 the C# snippet
panel (eight snippets compiled and run, panel driven in Chrome 153). 229 tests in `demo/web`, 6,402
in the .NET suite.

**Chunk 4 is next: two-way match linking and the header GitHub link**, to the spec's sections. From
chunk 2 it inherits the per-edit underlay (needs `Match.FuzzyChanges` threaded through
`DemoEngine.cs`, `types.ts`, `highlight.ts`) and `docs/demo/*.png`, still the old page.

**Chunk 5 owes four things:** the independent verifier over chunk 4's numbers; a blind pass covering
chunk 1's fix delta, which no reviewer has seen; `checks.html` re-run whole; and the mirror of
sitting 4's focus fix - the disclosure buttons are `v-if="!wide"`, so WIDENING drops focus to
`<body>` (measured). Pre-existing, same WCAG 3.2.2 case, needs a focus destination chosen. After
S73: `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`. S58 remains a checkpoint.

**Two gates are RED, neither belongs to S73** (both need engine code): the oracle at 1 row of 6380
at seed `20260919` (triage in `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`,
deliberately not pinned), and the AOT publish, `IL2065` at `PublicApiDocumentationTests.cs:62`.

**Open items.** The owner still has to push `phase9-demo` and set Pages > Source = GitHub Actions;
Stryker is paused for S58's benchmarks; `tools/check-ratchet.ps1:94` writes the upstream-commit line
wrongly when the submodule is absent and should fail loudly (first maintenance job); MAIN has a
stray `.github/workflows/pages.yml`; v3 polish after S73 is in the S72 notes. Left running per the
owner's rule: `python -m http.server` on 8090, 8092 and 8137, a Vite dev server (PID 34120) and a
hung `VBCSCompiler.exe`.
