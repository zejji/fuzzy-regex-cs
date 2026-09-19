# State

**S73 is in flight** (started 2026-09-19), chunks 1 and 2 of 5 committed and green. Phase 9 reopened
for it: the owner's first look at the live demo judged it as a product and found seven faults. Spec:
`docs/plan/slices/S73-demo-as-a-product.md`. Chunk plan, review record and numbers:
`docs/plan/slices/notes/S73-sittings.md`.

Chunk 1 landed the copy linter and the rewrite of every user-facing string (1,185 words to 921).
Chunk 2 landed the shell: a header/two-pane/footer frame gated on width AND height, the examples and
help behind an ARIA tab set, two one-column disclosures, the ink-and-white palette with its 22
measured contrast pairs, and `prefers-color-scheme: dark` removed. `checks.html` ran eight of nine;
the red one was its own stale literal, not the page, and it now matches the cap instead. Re-run the
whole page next sitting to see nine.

## Next action

**Chunk 2's seven review findings, before chunk 3.** The blind pass reported after the allowance
ran out, so nothing is fixed. Every finding has a reproduction in the sittings notes ("Review (chunk
2)"); confirm each yourself, then fix. The three that matter most:

- `.shell` compiles to `min-height: 100vh` under the gate's `height: 100dvh`, so the shell is still
  `vh`-sized. Use `min-h-dvh`.
- Four `layout.test.ts` tests assert over the stylesheet *source*, which is why they missed it and
  why harmless CSS edits break them. Assert over the built CSS (`npm run build`, then
  `wwwroot/assets/index-*.css`).
- Three dangling or wrong `aria-controls`, and a `tabpanel` with no focusable content and no
  `tabindex="0"`.

**Then chunk 3**, from the spec's remaining deliverables. It inherits two things:

- **The per-edit underlay is still not buildable.** `Match.FuzzyChanges` has to be threaded through
  `DemoEngine.cs`, `types.ts` and `highlight.ts` first; chunk 2 delivered the edit hues on chips.
- **`docs/demo/*.png` are the old page** and want re-taking from the published build.

**The independent verifier still owes chunk 2**, and **chunk 5's blind pass must cover chunk 1's fix
delta**, which no reviewer has seen.

After S73: `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`.

## Open items

- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
  S71's one open box; nothing in the repository can do it, nothing is live.
- **v3 polish after S73.** Kept: a formatter for `demo/web`, and a saved-case list beside the
  samples. Dropped, reasons in the S72 notes: the explanation pane, the step debugger, the code
  generator.
- **`docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out** (`tools/check-ratchet.ps1:94`); it should fail loudly. First maintenance job.
- `.github/workflows/pages.yml`: the MAIN checkout has a stray copy of it.
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8092 (PID 14996),
  8090, 8137, and 8181 (serving `.scratch/demo-publish/wwwroot`).
