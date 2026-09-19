# State

**S73 is in flight** (started 2026-09-19), chunk 1 of 5 committed and green. Phase 9 reopened for
it: the owner's first look at the live demo judged it as a product and found seven faults. Spec:
`docs/plan/slices/S73-demo-as-a-product.md`. Chunk plan, review record and numbers:
`docs/plan/slices/notes/S73-sittings.md`.

Chunk 1 landed the copy linter (`demo/web/tests/copy-rules.ts`, `copy-sources.ts`, `copy.test.ts`)
and the rewrite of every user-facing string: 1,185 words down to 921.

## Next action

**Chunk 2: the shell.** The `100dvh` three-region layout, the tab set for examples and help, the
visual tokens, and the `prefers-color-scheme: dark` block removed. Deliverables (i), (iii), (iv).
Every string it writes is already under the linter, so write to the rules.

**Chunk 5's blind pass must cover chunk 1's fix delta**, which no reviewer has seen.

After S73, the queue's lowest-numbered file is
`docs/plan/slices/S57-coverage-backstop-and-phase-close.md`.

## Open items

- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
  S71's one open box; nothing in the repository can do it, the branch is unmerged, nothing is live.
- **v3 polish after S73.** Kept: a formatter for `demo/web` (the `csharpier`/husky chain does not
  cover the front end), and a saved-case list beside the samples. **Dropped, reasons in the S72
  notes:** the token-by-token explanation pane, the step debugger, the code generator.
- **`docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out** (`tools/check-ratchet.ps1:94`); it should fail loudly. First maintenance job.
- `.github/workflows/pages.yml`: the MAIN checkout has a stray copy of it.
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8092 (PID 14996),
  8090 and 8137. PID 34120 is gone, so `tools/run-wasm-smoke.ps1` is available to chunk 5.
