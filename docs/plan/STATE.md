# State

**S73 is in flight** (started 2026-09-19), chunks 1 to 4 of 5 committed, green and closed. Spec:
`docs/plan/slices/S73-demo-as-a-product.md`; chunk plan, reviews and numbers:
`docs/plan/slices/notes/S73-sittings.md`. Chunk 1 landed the copy linter and the rewritten strings,
chunk 2 the shell, chunk 3 the C# snippet panel, chunk 4 two-way match linking and the header
GitHub link. 234 tests in `demo/web`, 6,402 in the .NET suite.

**Chunk 5 is next and closes the slice.** One pass, in this order: (1) the sticky column header,
which does not stick - `.table-scroll` is `overflow-x: auto`, so CSS computes its other axis to
`auto` and the `th` sticks to a box that never scrolls vertically; try `overflow-y: clip` and pin
the answer. (2) One scripted Playwright pass over 1920x1080, 1440x900, 1366x768, 1024x768 and
390x844 - boxes, screenshots, keyboard walk, contrast - in one session, the loop written as a probe
in `tools/probes/` first. (3) The independent verifier over chunks 4 and 5. (4) A blind pass over
chunk 1's fix delta, which no reviewer has seen. (5) `checks.html` re-run whole, and the mirror of
sitting 4's focus fix - the disclosure buttons are `v-if="!wide"`, so WIDENING drops focus to
`<body>` (measured, pre-existing, WCAG 3.2.2, needs a destination chosen). Still owed: the per-edit
underlay (`Match.FuzzyChanges` through `DemoEngine.cs`, `types.ts`, `highlight.ts`) and
`docs/demo/*.png`. Then `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`; S58 stays a
checkpoint.

**Two gates are RED, neither belongs to S73** (both need engine code): the oracle at 1 row of 6380
at seed `20260919` (triage in `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`,
deliberately not pinned), and the AOT publish, `IL2065` at `PublicApiDocumentationTests.cs:62`.

**Open items.** Owner to push `phase9-demo` and set Pages > Source = GitHub Actions; Stryker paused
for S58's benchmarks; `tools/check-ratchet.ps1:94` writes the upstream-commit line wrongly with no
submodule and should fail loudly (first maintenance job); MAIN has a stray
`.github/workflows/pages.yml`; v3 polish after S73 is in the S72 notes. Left running: four
`python -m http.server` (8090, 8092, 8137, 8199), Vite (PID 34120), a hung `VBCSCompiler.exe`.
