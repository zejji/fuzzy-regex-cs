# State

**S72 is DONE and Phase 9 is closed** (2026-09-19). The demo is v2: eighteen editable samples, one
per feature, help generated from `docs/COMPARISON.md`, and the regex101 interactions the owner asked
for. Spec and closing notes: `docs/plan/slices/done/S72-demo-v2-features-and-help.md`; the sittings,
every measurement and both review passes: `docs/plan/slices/notes/S72-sittings.md`.

**Estimate against actual.** The ROADMAP estimated three slices and got three; what it did not
estimate is sittings - **11**, S70 four, S71 five, S72 two - and the overrun is all browser work,
which nothing in the ported suite stands in for.

## Next action

`docs/plan/slices/S57-coverage-backstop-and-phase-close.md`, the queue's lowest-numbered file.

## Open items

- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
  S71's one open box; nothing in the repository can do it, the branch is unmerged, nothing is live.
- **v3 polish after v2.** Kept: a formatter for `demo/web` (the `csharpier`/husky chain does not cover
  the front end), and a saved-case list beside the samples. **Dropped, reasons in the S72 notes:**
  the token-by-token explanation pane, the step debugger, the code generator.
- **`docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out** (`tools/check-ratchet.ps1:94`); it should fail loudly rather than fall back to our
  own HEAD. First between-slice maintenance job.
- `.github/workflows/pages.yml`: the MAIN checkout has a stray copy of it.
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8092 (PID 14996),
  8090 and 8137, and **PID 34120**, another session's Vite dev server on port 5199, holding
  `demo/web/node_modules` so `npm ci` fails there with EPERM. Because of it,
  **`tools/run-wasm-smoke.ps1` could not run at the close of S72** (it starts with `npm ci`).
