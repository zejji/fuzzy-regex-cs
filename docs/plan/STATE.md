# State

**Slice in flight: S71 (browser page v1), checkpoint after sitting 3.** Branch `phase9-demo`.
Notes: `docs/plan/slices/notes/S71-sittings.md`.

## Where it is

The front end is now `demo/web`: Vite 8, Vue 3.5, TypeScript 6.0.3 strict, Tailwind 4, Vitest 5, all
pinned and installed with `npm ci`. `npm run build` type-checks, runs **34 tests in 4 files**, then
writes the page into `demo/FuzzyRegex.Demo.Wasm/wwwroot` (gitignored), where the wasm publish gathers
it. `tools/build-demo-web.ps1` replaces `run-demo-js-tests.ps1`; `run-wasm-smoke.ps1` builds the front
end first and now checks the hashed bundle by parsing the published page. `pages.yml` installs Node
from `.nvmrc`. Design bar done: `App.vue` + `styles.css`, screenshots in `docs/demo/`.

Verified this sitting: **DEMO WEB BUILD GREEN**; **WASM SMOKE GREEN** (28 files, 7.64 MB, 56
integrity endpoints); **CHECKS GREEN 9/9 at the server root and 9/9 at `/fuzzy-regex-cs/`** in
Chrome 153.

## Next action, in order

1. **`pwsh -File tools/check-ratchet.ps1`.** It was NOT run this sitting - the allowance window ran
   out first - so this checkpoint's C# claim is unverified. The only C# change is
   `DemoCapsTests.cs`, repointed from `wwwroot/lib/caps.js` to `demo/web/src/lib/caps.ts` (the file
   exists and its three constants still match the regex). Expect 6365 passing, 0 failing, 0 skipped,
   baseline 6257. Then `-UpdateBaseline`.
2. **Blind review** over the whole sitting-3 diff (it has had none), then the independent verifier
   (amendment 16 limb d), which has been deferred since sitting 1 and must re-run the browser numbers
   with a publish and a server standing up.
3. Tick the slice file's "Done when" boxes, `git mv` it to `done/`, write its closing notes.

## Open items

- `.github/workflows/pages.yml` is committed here but the MAIN checkout also has a stray copy.
- The README's demo link 404s until the owner pushes and sets Settings > Pages > Source = GitHub
  Actions. **No pushing; the owner pushes.**
- `docs/STATUS.md:9`'s generator bug (unrelated to S71). Diagnosed 2026-09-18: in this worktree the
  `upstream` submodule is not checked out (`git submodule status upstream` prints
  `-7dd71c15c4fb5c94206bed1763abd4c2bd2f1b33`), and `PortTools.psm1:311` then writes *our* HEAD as
  the upstream commit. A run here produced "Parity against upstream commit `b393e63...`", which is
  the S71 commit. That dirty line was discarded, not committed. Fix: make the generator fail loudly
  when the submodule is absent rather than falling back to repo HEAD.
- `DemoEngine.cs`'s capture-list comment is wrong (unrelated to the front end).
- A `python -m http.server 8090` (PID 3440) and a Playwright browser are still running from this
  sitting; nothing was killed, per the owner's rule.
