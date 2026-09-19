# State

**S71 (browser demo v1) is DONE**, closed 2026-09-19 on branch `phase9-demo`. Notes:
`docs/plan/slices/notes/S71-sittings.md`; closing notes in `docs/plan/slices/done/S71-vue-page-v1.md`.
No slice is in flight. The queue's lowest-numbered file is **S57**.

## Where it is

The demo is a Vite 8 + Vue 3 + TypeScript (strict) + Tailwind project at `demo/web`, building into
the .NET web root so one wasm publish carries page, worker and runtime. Verified on the committed
tree: **ratchet GREEN** (6365 passing, 0 failing, 0 skipped, baseline 6257); **DEMO WEB BUILD GREEN**
(52 tests, 5 files); **WASM SMOKE GREEN** (28 files, 8,009,524 bytes, 56 integrity endpoints);
**CHECKS GREEN 9/9 at the server root and 9/9 at `/fuzzy-regex-cs/`** in Chrome 153. Three blind
review passes (12 findings, all reproduced, all fixed) and the independent verifier are recorded in
the notes.

## Next action

1. **The owner pushes `phase9-demo` and sets Settings > Pages > Source = GitHub Actions.** Nothing
   in the repository can do this, and until it happens the README's demo link 404s. This is S71's one
   open "Done when" box; S72 picks it up by watching the first `pages.yml` run and opening the live
   URL with the browser cache disabled.
2. Otherwise the driver takes **S57** (coverage backstop and phase close) from the queue.

## Open items

- **`docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out.** `tools/check-ratchet.ps1:94` runs `git -C <root>/upstream rev-parse HEAD`; with an
  empty submodule directory git answers with OUR HEAD, and sitting 2 committed `611be7e3...` (our own
  merge commit) as the upstream commit. Sitting 5 checked the submodule out in this worktree and the
  line now reads the true `7dd71c15...`, but the generator should fail loudly rather than fall back.
  First between-slice maintenance job.
- `.github/workflows/pages.yml` is committed here and the MAIN checkout also has a stray copy.
- `DemoEngine.cs`'s capture-list comment is wrong (unrelated to the front end).
- Left running, nothing killed per the owner's rule: `python -m http.server 8092` (PID 14996) over
  `.scratch/verify-serve`, a Vite dev server on port 5199, `python -m http.server 8090` from
  sitting 3, and PID 34120 - another session's Node process, which held
  `demo/web/node_modules/lightningcss-win32-x64-msvc` and made one `npm ci` fail with EPERM.
