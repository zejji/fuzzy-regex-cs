# State

**S75 DONE (2026-09-21, branch `phase9-demo`, demo worktree).** Four sittings; what each one
measured is in `docs/plan/slices/notes/S75-sittings.md`, and the closing notes are on the slice file
in `docs/plan/slices/done/`.

**What landed:** edit markers in their own row under the subject, with neighbouring errors of one
kind drawn as one mark; a legend, a per-run note and an alignment view; the line that names a fuzzy
budget with no bound; help notes on all six input headings; the `(?:colour){e<=2:[a-z]}` worked
example; snippet identifiers pinned by compiling the snippet the page writes; and the copy linter
extended over `README.md`, six documents and the public XML doc comments.

**Green at the close:** ratchet GREEN, 6,487 tests against baseline 6,379; 427 web tests;
`vue-tsc` clean; 123 Pester tests; 40 documentation examples; demo web build GREEN; WASM smoke
GREEN over 58 published endpoints.

**Take S57b next on the main line, not S57** - S57 is blocked by its own first paragraph. Phase 6's
exit gate is RED: `run-oracle.ps1 -Count 6000` diverges 3 (seed 7), 3 (4242) and 14 (20260920) over
twenty distinct rows, and the pre-S60 tree gives 3, 3 and 15 on the same command. The rows and the
commands that recreate them are in `notes/S57-sittings.md`, "Sitting 3". Phase 6 does not close
until that wave is green. S57 itself holds only items 10 and 11, bookkeeping for the Phase 7
handover.

**Five ledger entries are inherited unfixed and no slice schedules them:** 17 in part, 18, 19, 20,
21. That is three pieces of work; the slices that take them amend ROADMAP and the spec, and it is
the owner's call.

**Waiting on the owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push
`phase9-demo`, then Pages > Source = GitHub Actions).

**One environment note:** a stale vite dev server (PID 27600, port 5179) holds
`lightningcss.win32-x64-msvc.node`, so `npm ci` fails with EPERM in `demo/web` and deletes
`node_modules` on the way. `npm --prefix demo/web install --no-audit --no-fund` repairs it, then
build with `tools/build-demo-web.ps1 -SkipInstall` and smoke with `run-wasm-smoke.ps1 -SkipWebBuild`.
