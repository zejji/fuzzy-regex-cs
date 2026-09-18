# S71 sittings

Per-sitting notes for `S71-vue-page-v1.md`. The slice file stays its spec plus the browser brief;
this is where each sitting records what it did.

## Sitting 1 (2026-09-18) - CHECKPOINT, not done

No browser tooling in this session: Playwright was granted "from the next session started in this
worktree, not this one", so the orchestrator's instruction was to take the page as far as it goes
without one, write down exactly what a browser must verify, and commit a green checkpoint. The
browser leg is specified in the slice file under "Browser brief for the next sitting".

### What landed

- **The page.** `index.html` (three inputs, the highlighted subject, the match list, the group
  table), `app.js` (the Vue application), and `lib/caps.js`, `lib/fragment.js`, `lib/highlight.js`,
  `lib/pool.js` - the parts that are testable under `node --test` with no browser.
- **Vue vendored**, `vue@3.5.43`, `dist/vue.esm-browser.prod.js`, 173,163 bytes, SHA-256
  `877f675a8c5f347073b4d5437439a042b984d81fc5da2770eb7e6d320d5017f3`, taken from the npm tarball and
  checked against the registry's `dist.integrity` (sha512-o5qZoksdnjIKvW1srZ3ab7pcDNYAerBjRe54D0LB
  LfRdCYFrSgBHVXokMas35czQc0//lmx4/tuY4ZNQ+Rf2Ng==, checked 2026-09-18). Version and hash sit in a
  comment above the import in `app.js` and in `demo/README.md`; the full build, not the runtime-only
  one, because the markup lives in `index.html` and is compiled in the browser.
- **Eight worked examples** in `examples.json`, each one's answer pinned by `DemoExamplesTests`
  against upstream `regex 2026.9.10` via `tools/probes/demo-examples-expectations.py`. The sidebar
  is that file; there is no second list to keep in step.
- **`checks.html`**, the browser-side verdict page, driving the real `index.html` in an iframe and
  printing CHECKS GREEN or CHECKS RED into `window.__checks`. Unrun this sitting - it is the
  deliverable of the browser brief.
- **`.github/workflows/pages.yml`**, the demo's own workflow, separate from `ci.yml` so a broken
  demo never blocks a library merge. It runs the Node tests, then `tools/run-wasm-smoke.ps1`, then
  asserts `.nojekyll` reached the publish root, then uploads and deploys.
- **`demo/README.md`**, hand-written: run-locally from a fresh clone on Windows, the one-off
  Settings - Pages - Source = GitHub Actions step, what the workflow does, how to tell it is live,
  and how to re-vendor Vue.
- **The README link**, replacing the `<!-- demo-link -->` placeholder, saying plainly it is v1.
  `docs/plan/RELEASE.md`'s post-publish check now names the URL rather than the removed marker.
- **`*.js binary` in `.gitattributes`**, below the existing `* text=auto eol=lf` line, because the
  last matching pattern wins.

### Two decisions worth the reader's time

**No `<base href>` at all**, which is a deliberate divergence from Microsoft's documented
Blazor-on-Pages recipe (the slice file's own Findings section had planned a hard-coded
`<base href="/fuzzy-regex-cs/">`). Every URL in the page is document-relative and the worker is
resolved against `import.meta.url`, so one artefact boots at the repository subpath on Pages, at the
root of a local server and at a fork's preview path. The hard-coded base is exactly the "works at
the subpath, breaks at the root" hunt the slice file names. The browser brief runs `checks.html`
both ways to settle it; if the subpath run is red, the decision is wrong and the fix belongs to this
slice.

**`pages.yml` also watches `src/**`**, which widens the slice's stated trigger list. The demo
compiles the library into the artefact, so a library change with no demo change still changes what
is deployed; a workflow watching only `demo/**` would leave the live page running an older engine
than `main`.

### Publish, verified after the last `wwwroot` change

`tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-s71` - **WASM SMOKE GREEN**: 31 files,
7,507,552 bytes on disk (7.16 MB), 2,161,333 gzip (2.06 MB), 62 integrity endpoints recomputed from
the files on disk and matched, nothing on disk the manifest does not name. Against S70's 22 files /
7,278,441 bytes: the page, the vendored Vue, the examples and the checks page, and their compressed
variants.

### Tests

- **34 `node --test` tests** across `demo/tests/` (`app.test.js`, `fragment.test.js`,
  `highlight.test.js`, `pool.test.js`), run by `tools/run-demo-js-tests.ps1`: DEMO JS TESTS GREEN.
  The page's state machine is tested with a fake worker and DOM shims, not a browser - what is under
  test is the state machine, and a browser would test Vue.
- **Ratchet GREEN** over the commit-ready tree: 6357 passing, 0 failing, 0 skipped, 6249 distinct
  ids, baseline 6249. The baseline moved from 6235 because `DemoExamplesTests` and `DemoCapsTests`
  are new and one example's title changed (below).
- `DemoCapsTests` reads `wwwroot/lib/caps.js` as text and checks its numbers against `DemoEngine`'s,
  so the page's caps and the engine's cannot drift apart silently.

### Blind review, both passes

**Pass 1** over the whole diff: 6 findings, 3 reproduced, 3 fixed, 3 rejected on reproduction.

- **Reproduced and fixed: `.github/workflows/pages.yml` did not exist.** It had been written into
  the main checkout rather than this worktree. Written into the worktree; see "Open items" for the
  stray copy.
- **Reproduced and fixed: `demo/README.md` claimed a `dotnet run` dev server.** `Properties/` holds
  only `AssemblyInfo.cs` - there is no launch profile. The claim was removed rather than asserted
  unchecked.
- **Reproduced and fixed: a stale answer readable as a new one.** `running` is false for the whole
  250 ms debounce, so any driver waiting on it reads the previous case's answer. Added a `pending`
  ref and a `busy` computed, used by the UI and by `checks.html`, whose `settled()` also had to
  `await sleep(0)` because `until()` tests its predicate synchronously.
- Rejected: `docs/STATUS.md`'s "Parity against upstream commit aee2430" names a local commit - real,
  but a pre-existing generator issue, not this diff (recorded as an open item). Rejected: the README
  link 404s - expected until the owner enables Pages. Rejected: a style rewrite.

**Pass 2** over the delta pass 1 never saw (the workflow, the README, the `pending`/`busy` change
and the tests around them): 4 findings, 4 reproduced, 4 fixed.

- **Stop's message was overwritten by the pool's bare reason.** Killing the worker settles the
  promise `ask` is awaiting, and that continuation lands a microtask after `stop()` returns. Fixed
  by retiring the question (`++question`) *before* killing its worker.
- **The subject-cap refusal was overwritten the same way**, by the abort of the question it
  replaced. Fixed by numbering the question before the cap check can return.
- **Stop during the debounce killed a worker for a question never asked**, and said a match had been
  killed. Now it cancels the timer, spends nothing, and says so.
- **A worker killed mid-boot latched a permanent "engine failed to load".** A terminated client
  reports `aborted: true`, and `pool.ready` now follows replacements until a worker survives to give
  a verdict, with `current.ready === pending` as the dispose-time stop so the loop cannot spin. The
  fix is in `pool.js`, where the wrong claim is made, not in `app.js`, which only displays it.

### Non-vacuity

Every fix above has a test, and each test was run with its fix reverted. Eight checks, A to H; each
showed exactly the named test failing and nothing else. Two were rewritten because the first version
passed without the fix: "no worker killed for a question never asked" used a non-answering fake, so
a question *was* in flight; and the mid-boot-kill test took the `!wasRunning` path and killed
nothing, so it was rewritten with `{answers: false, bootMs: 600}` and two keystrokes, which makes
the debounce's `pool.stop` kill a still-booting worker.

### Open items for the next sitting

1. **A stray `.github/workflows/pages.yml` in the main checkout**
   (`C:\Users\<user>\source\repos\fuzzy-regex-cs\.github\workflows\pages.yml`). Written there
   by mistake; both `ls` and `rm` on that path were refused by this session's working-directory
   restriction, so it is still there. It must be deleted before `main` is touched, or the merge
   brings in a second copy.
2. **The browser leg**, specified in the slice file. It carries the slice's only outstanding
   measurements: `stopToNextAnswerOnScreenMs`, `respawnWithoutSpareMs` and `respawnWithSpareMs`.
3. **The live URL**. The README link 404s until the owner pushes and sets Settings - Pages -
   Source = GitHub Actions. Nothing was pushed this sitting.
4. **`docs/STATUS.md` says "Parity against upstream commit aee2430"**, which is this repository's
   HEAD, not an upstream commit. A generator bug, pre-existing, not part of this slice.
5. **`DemoEngine.cs` has a comment calling capture lists "an mrab-regex feature the built-in engine
   does not have".** .NET has `Group.Captures`; the sentence is wrong as written.
6. **The independent verifier** (amendment 16, limb d) is deferred to the closing sitting, where it
   can re-run the browser evidence as well as the probe and the Demo tests. Blind pass 2 re-ran
   `tools/probes/demo-examples-expectations.py` and the `Gaps/Demo` tests and confirmed every quoted
   expectation.
