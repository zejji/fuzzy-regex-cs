# State

**S73 is in flight** (started 2026-09-19). Chunks 1 to 4 and 5a to 5e are committed and green. Spec:
`docs/plan/slices/S73-demo-as-a-product.md`; chunk plan, reviews and numbers:
`docs/plan/slices/notes/S73-sittings.md`. Chunk 5 delivered the sticky column header, focus kept
across a widening window, the per-edit underlay, one scripted five-width pass (which found and fixed
twenty-six tabs to the answer), and `checks.html` green whole with the reference screenshots
re-taken. 254 tests in `demo/web` (5 failing, see below), 6,406 in the .NET suite, ratchet GREEN at
6,298 distinct ids against a baseline of 6,294 - **the baseline has NOT been updated yet.**

**Chunk 5f is what is left, and it closes the slice.** In order:

1. **Four open review findings** - 5 to 8 in the sittings notes: two extraction bugs in
   `copy-sources.ts` (a `//` inside a string literal, and C# interpolations linted as prose), the
   missing guard rows for `src/lib` and `DemoEngine.cs` in `copy.test.ts`, and the provenance of the
   `2,2` deletion expectation in `demo-json-contract-expectations.py`. Reproduce each before fixing;
   the reviewer's own reproductions are in the notes.
2. **`page.test.ts:962`'s hash assertion cannot fail** (finding 4, fix not written): jsdom does not
   navigate a fragment on click. Pin `defaultPrevented` on a dispatched click instead, and prove it
   by removing `@click.prevent` and watching it fail.
3. **`tests/dev-server.test.ts`: five failures, not from this sitting's changes** - they reproduce on
   `aa5b016` with the tree stashed, and the file was green earlier in the same sitting before
   `.scratch/sync-serve.ps1` ran a `vite build`. Diagnose before anything else; it may be a stale
   artefact rather than a defect.
4. **The independent verifier** over chunks 4 and 5 - never run, and amendment 16 needs it.
5. Then: ratchet, `-UpdateBaseline`, tick "Done when" boxes 2, 3 and 6, `git mv` the slice to
   `docs/plan/slices/done/` with closing notes including the Review paragraph, append to
   DECISIONS.md, one commit.

**Two gates are RED, neither belongs to S73** (both need engine code): the oracle at 1 row of 6380
at seed `20260919` (triage in `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`,
deliberately not pinned), and the AOT publish, `IL2065` at `PublicApiDocumentationTests.cs:62`.

**Open items.** Owner to push `phase9-demo` and set Pages > Source = GitHub Actions; Stryker paused
for S58's benchmarks; `tools/check-ratchet.ps1:94` writes the upstream-commit line wrongly with no
submodule; MAIN has a stray `.github/workflows/pages.yml`; v3 polish after S73 is in the S72 notes.
Left running: five `python -m http.server` (8090, 8092, 8137, 8199, and 8213 serving `.scratch/serve`
for the browser probes), Vite (PID 34120), a hung `VBCSCompiler.exe`.
