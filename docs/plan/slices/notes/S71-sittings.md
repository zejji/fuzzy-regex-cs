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
- Rejected: `docs/STATUS.md:9`'s "Parity against upstream commit ..." names a local commit - real,
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
4. **`docs/STATUS.md:9` says "Parity against upstream commit `<sha>`"**, which is this repository's
   HEAD, not an upstream commit. A generator bug, pre-existing, not part of this slice. Named by line
   rather than by SHA because the generator rewrites it on every commit.
5. **`DemoEngine.cs` has a comment calling capture lists "an mrab-regex feature the built-in engine
   does not have".** .NET has `Group.Captures`; the sentence is wrong as written.
6. **The independent verifier** (amendment 16, limb d) is deferred to the closing sitting, where it
   can re-run the browser evidence as well as the probe and the Demo tests. Blind pass 2 re-ran
   `tools/probes/demo-examples-expectations.py` and the `Gaps/Demo` tests and confirmed every quoted
   expectation.

## Sitting 2 (2026-09-18) - CHECKPOINT, not done

Two scope changes arrived from the owner during this sitting, the second of them large:

- **20:15, the Design bar** (in the slice file): the page must look professional, not merely work.
- **20:55, the toolchain**: the "vendored, no build step" rule is **withdrawn**. The front end moves
  to Vite + Vue 3 + TypeScript (strict) with Tailwind, everything pinned in `package.json` and
  installed with `npm ci`. Only the CDN ban survives. **21:05**: the layout must follow researched
  UI/UX guidance, with the sources recorded.

That supersedes most of the page sitting 1 built (the vendored `vendor/vue.esm-browser.prod.js`, the
hand-written ESM modules under `lib/`, and the styling the Design bar would have gone into). So this
sitting deliberately did **not** restyle the old page: an hour of CSS on a page being replaced within
the week is the wasted work the owner's 2026-09-16 rule names. What it did instead was finish the one
thing that was blocking the slice and is **not** affected by the rewrite - the browser leg, which
tests the worker, the pool and the engine - and then prepare the re-plan.

### The browser leg: GREEN at the root and GREEN at the subpath

Ran for the first time ever (sitting 1 had no browser). Chrome 153.0.0.0 driven by the Playwright MCP
server, serving `.scratch/wasm-s71-base/wwwroot` with `python -m http.server`.

| Run | URL | Verdict |
| --- | --- | --- |
| Root | `http://localhost:8090/checks.html?v=3` | **CHECKS GREEN**, 9 of 9 |
| Subpath | `http://localhost:8091/fuzzy-regex-cs/checks.html` | **CHECKS GREEN**, 9 of 9 |

**The subpath run settles the no-base-href decision in its favour.** Sitting 1 diverged from
Microsoft's Blazor-on-Pages recipe by shipping no `<base href>` at all and reasoned that every URL in
the page is document-relative. That is now tested rather than argued: the same artefact boots and
answers identically at the server root and at `/fuzzy-regex-cs/`, which is what GitHub Pages will
serve. The hard-coded base the Findings section originally planned would have worked at exactly one
of the two.

**Two real bugs, both invisible without a browser, both in `checks.html` rather than in the page.**

1. **The checks page hung for ever at check 2.** `openPage` assigns `frame.src = './index.html' +
   fragment`. When the previous src differed only in its FRAGMENT that is a same-document
   navigation: no reload, no `load` event, and the `await` never returns. Observed exactly: check 1
   PASS, then "running..." for 180 s with the iframe still showing the last example's inputs
   (`\bfuzzy\b`) under the new URL. Fixed by blanking the frame to `about:blank` and awaiting that
   load before assigning the real URL, so every `openPage` is a genuine document load.
2. **"the address bar follows the inputs" asserted with the wrong decoder.** `encode()` builds the
   fragment with `URLSearchParams`, which writes a space as `+`; the check read it back with
   `decodeURIComponent`, which leaves `+` alone, so a correct address bar failed the check. The page
   was right the whole time - check 2's round trip passes, because the page parses the fragment with
   `URLSearchParams` as well. Now asserted by parsing, which is also what the page does on load.

**One check was measuring something other than the demo.** "the page keeps painting while a runaway
pattern runs" counted `requestAnimationFrame` callbacks and wanted more than 3 in 500 ms; it got 1.
The first hypothesis - "this browser does not drive rAF" - was tested, and it is **wrong**. Two 500 ms
windows on each of two *idle* pages, same browser, same server, `visibilityState === 'visible'`:

| Idle page | frames | 16 ms timer ticks |
|---|---|---|
| A page with nothing on it (`.scratch/wasm-s71-base/wwwroot/raf-probe.html`) | 31, 31 | 31, 30 |
| The demo page, which boots two .NET WebAssembly workers | 1, 0 | 28, 30 |

So rAF works fine here in general and does not work **on this page**, and the frame count was
measuring that interaction rather than whether the demo stayed responsive. Cause not established -
the observation is what is recorded, and it is enough to disqualify the metric. (Written down because
the first draft of these notes asserted the general version, and the blind review caught it.)

The check now counts 16 ms timer ticks on both threads - a blocked main thread cannot service a timer
either - and reads the iframe's **status pills** mid-runaway to prove the page drew its "matching..."
state while the worker was wedged. Pills, not the page's whole text: the intro paragraph contains the
words "compiled to WebAssembly and matching in your browser", so a body-text search for "matching"
passes on a page doing nothing at all. That, too, is a review finding. The rAF count is still
reported, never asserted on. Result: **32 ticks on the checks page and 32 inside the demo** in the
500 ms window, status pills `["matching..."]`. That is a stronger claim than the original, and one
that holds in a headless browser as well as a headed one.

### The measurements the ROADMAP wanted

There is no published figure for a .NET WebAssembly respawn, so these are the port's own. Three
observations of each, across the two green runs plus the intermediate one:

| Measurement | Root run | Subpath run | Earlier run | Notes |
| --- | --- | --- | --- | --- |
| `stopToNextAnswerOnScreenMs` | 440.7 | 438.7 | - | includes the 250 ms debounce |
| `respawnWithoutSpareMs` | 249.5 | 242.7 | 271.5 | spare disabled, raw pool |
| `respawnWithSpareMs` | 193.8 | 119.3 | 144.6 | raw pool |

So a respawn costs roughly **250 ms without the spare and roughly 150 ms with it** - the spare saves
about 100 ms, and Stop to a fresh answer on screen is a hair over 400 ms of which 250 ms is the
page's own debounce. Honest caveat: the with-spare figure ranges 119-194 ms over three runs on a
machine that is also running the Stryker queue, so treat it as "about 150 ms", not as a benchmark.

### Screenshots (the page as it stands, before the redesign)

`.scratch/s71-v1-before-1280.png` and `.scratch/s71-v1-before-390.png`. They are the "before" for the
Design bar: the page is correct, tidy and plainly a test harness - system colours, one flat column of
labels and boxes, no hierarchy between the inputs and the answers. Keeping them is the cheapest way
for the next sitting to show what changed.

### BLOCKER: `npm` is not in this session's Bash allowlist

The re-plan cannot start until this is granted. `npm --version` is refused before it runs:

> This command requires approval

`node` is allowed (v24.16.0 is installed) and only `npm` is missing, so this is a one-line
allowlist change rather than a missing tool. **Nothing was worked around.** Widening my own sandbox
is the owner's call, not mine, and a Vite project written blind - no install, no `vue-tsc`, no test
run - would be exactly the unverified code the standard forbids.

What was established instead, so the next sitting starts from facts rather than guesses:

- **The network is fine.** `.scratch/probe-npm-registry.py`: DNS `registry.npmjs.org` ->
  104.16.10.34, `GET /vite/latest` -> HTTP 200. The sandbox is not the obstacle; the allowlist is.
- **Current versions, read from the registry today** (`.scratch/probe-npm-versions.py`), for pinning:
  `vite 8.3.0` (needs node `^20.19.0 || >=22.12.0`), `vue 3.5.43`, `@vitejs/plugin-vue 6.0.9`,
  `typescript 7.0.2`, `vue-tsc 3.3.11`, `vitest 5.0.1` (node `^22.12.0 || ^24.0.0 || >=26.0.0`),
  `tailwindcss 4.3.3`, `@tailwindcss/vite 4.3.3`, `@types/node 26.6.1`, `jsdom 30.1.0`.
  Installed Node is **v24.16.0**, which satisfies every one of those ranges - so `.nvmrc` should say
  24 and `engines.node` should be `>=22.12.0`.
- **One compatibility question to settle before pinning, not after:** `typescript 7.0.2` is current
  and `vue-tsc 3.3.11` has historically pinned a TypeScript range. Check `vue-tsc`'s peer range
  first; if it has not caught up to TS 7, pin TypeScript to the newest 5.x/6.x it accepts and say so
  in the commit. Do not assume this either way - it is one `npm view vue-tsc peerDependencies` away.

### The UI/UX guidance this design will follow (owner's 21:05 requirement)

Searched 2026-09-18. Three sources, and what each one changes about the layout:

1. **NN/g, "Website Forms Usability: Top 10 Recommendations" and "Placeholders in Form Fields Are
   Harmful"** (<https://www.nngroup.com/articles/web-form-design/>,
   <https://www.nngroup.com/articles/form-design-placeholders/>). Single column; labels above their
   field, never inside it; hints persistent rather than vanishing. **Changes:** the Flags box
   currently uses a placeholder (`e.g. IgnoreCase, BestMatch`) as half its explanation - that becomes
   a persistent hint under a real label. The three inputs stay one column and stop competing with the
   examples sidebar for the eye.
2. **NN/g, "Response Time Limits"** (<https://www.nngroup.com/articles/response-times-3-important-limits/>):
   0.1 s feels instant, 1 s keeps the flow of thought, 10 s is the limit of attention. **Changes:**
   this is the justification for the 250 ms debounce and for showing "matching..." rather than
   nothing - and it says the busy state must appear within ~1 s of the keystroke, which the measured
   440 ms Stop-to-answer comfortably meets. A wait animation, not a bare word, for anything past 1 s.
3. **W3C, "What's New in WCAG 2.2"** (<https://w3.org/WAI/standards-guidelines/wcag/new-in-22/>):
   **2.5.8 Target Size (Minimum), Level AA, 24x24 CSS px**; 2.4.13 Focus Appearance (AAA) wants a
   2 px perimeter at 3:1 contrast against the unfocused state; 2.4.11 Focus Not Obscured (AA).
   **Changes:** the eight example buttons and the Stop button get a real minimum hit area, and the
   focus ring becomes a 2 px solid outline with its own contrast rather than the browser default.
   Body text and the highlight colours still have to clear 4.5:1 (1.4.3, AA), which is worth
   computing rather than eyeballing - the match highlight is a background behind body-sized text.

Two design consequences worth writing down before anyone opens an editor, because both are about
this page specifically rather than about forms in general:

- **Adjacent matches must stay visually separate.** `\w` over `abab` is four matches, and one flat
  highlight colour renders them as a single block - the page would be lying about the answer. The
  redesign alternates two tints and gives every `<mark>` its own edge, so "four matches" looks like
  four. Keep it a border rather than a hue difference, so it survives colour blindness.
- **`checks.html` counts `<mark>` elements** (`marks === demo.view.shown`) and greps the page for
  "did not participate". Whatever the new components look like, one displayed match must still be
  exactly one `<mark>`, or the display-cap check silently changes meaning.

### Tests

- **34 `node --test` tests GREEN** (`tools/run-demo-js-tests.ps1`), unchanged by this sitting's edits.
- **Ratchet GREEN** - see the commit's STATE.md line for the numbers.
- No C# changed this sitting; the only code edit is `checks.html`, which no test reads.

### Review

**Blind pass over this sitting's diff: 5 findings raised, 5 reproduced, 5 fixed.** No second pass was
needed - the fixes touched `checks.html`'s one check plus prose, all of it inside what the reviewer
had already read, and the fixed page was re-run in the browser rather than reasoned about.

1. **`renderedWhileRunning.includes('matching')` was vacuous.** `index.html`'s intro paragraph reads
   "compiled to WebAssembly and matching in your browser", so a body-text search for "matching"
   passes on a page doing nothing. Reproduced by reading the paragraph. Fixed: the check now reads
   `.status .pill` and asserts a pill starting "matching", and the detail string prints the pills, so
   a future regression is visible in the output as well as in the boolean.
2. **The 500 ms in the detail string was really ~750 ms.** Both intervals started before the inputs
   were set, so the window included the 250 ms debounce and the `until()` poll. Reproduced by
   arithmetic and then by measurement: the old code reported 47 ticks, the fixed code reports 32, and
   47/0.75 s = 63/s against 32/0.5 s = 64/s - the same rate over an honestly-measured window. Fixed
   by starting the counters after the wait.
3. **The intervals could leak.** `await until(...)` can throw on its 30 s timeout between the
   intervals being created and cleared, leaving both running for the life of the checks page and of
   the frame it drives. Fixed with `try`/`finally`.
4. **The rAF claim was too broad** - see the table above. The reviewer was right; the measurement I
   had was real but the generalisation drawn from it was not. Corrected in four places
   (`checks.html`, these notes, `DECISIONS.md`, the slice file).
5. **`STATE.md` quoted a stale SHA** for the `docs/STATUS.md` open item. Fixed, and both copies of
   that item now name `docs/STATUS.md:9` rather than a SHA the generator rewrites every commit.

After the fixes, `checks.html` was re-run in both layouts: **CHECKS GREEN 9/9 at
`http://localhost:8090/checks.html?v=4` and 9/9 at
`http://localhost:8091/fuzzy-regex-cs/checks.html?v=4`**, runaway check reporting
`32 timer ticks on the checks page and 32 inside the demo during the 500 ms window, status pills
["matching..."]`.

The independent verifier (amendment 16, limb d) stays deferred to the closing sitting, as in
sitting 1: the numbers this sitting quotes are browser measurements, and re-running them needs the
publish, two servers and the Playwright session that only the closing sitting will have standing up
anyway.

---

## Sitting 3 (2026-09-18, evening) - the Vite/Vue/TypeScript re-plan, built

Resumed from checkpoint `2546c51` with the npm blocker lifted (owner grant `e842ca9`: npm, npx and
node allowlisted). This sitting did the rewrite sitting 2 planned, end to end: the front end is now a
real project, the hand-written modules are typed, the page is designed, and both layouts are green in
a browser.

### What landed

- **`demo/web/`**: Vite 8.3.0, Vue 3.5.43, TypeScript 6.0.3 strict, Tailwind 4.3.3, Vitest 5.0.1,
  `vue-tsc` 3.3.11, all pinned exactly and installed with `npm ci`. `npm run build` is
  `typecheck && test && clean && vite build`, so a type error or a failing test cannot produce a
  bundle.
- **The build writes into the .NET web root** (`demo/FuzzyRegex.Demo.Wasm/wwwroot`, `emptyOutDir:
  false`), so the wasm publish gathers page, worker and runtime into one static-asset manifest.
  `npm run clean` deletes exactly `index.html` and `assets/` first - listed, never globbed, because
  that directory also holds hand-written files. Both are gitignored now.
- **The four modules and their tests are ports, not rewrites**: `caps.ts`, `fragment.ts`,
  `highlight.ts`, `pool.ts`, plus `demo.ts` (was `app.js`'s setup function) and `types.ts`, which is
  the TypeScript side of `DemoEngine`'s JSON. The 34 `node --test` tests became **34 Vitest tests in
  4 files**, assertions unchanged, with jsdom giving the real `document`/`location`/`history` the old
  hand shims faked.
- **`App.vue` and `styles.css`** are the design bar: one column, labels above their field, persistent
  hints, one accent reserved for interactive things, two alternating match tints each with its own
  edge, 44 px controls, a 2 px `:focus-visible` outline, and a dark scheme.
- **Tooling**: `tools/run-demo-js-tests.ps1` is replaced by `tools/build-demo-web.ps1` (Node floor
  22.12, `npm ci`, `npm run build`, then a check that the two artefacts landed).
  `tools/run-wasm-smoke.ps1` gained `-SkipWebBuild`, runs the front-end build before publishing, and
  no longer names `app.js`/`lib/`/`vendor/`: the bundle's name carries a content hash, so it now
  parses the published `index.html` and asserts every `assets/...` it references exists. `pages.yml`
  installs Node from `.nvmrc` with npm caching and runs the build as its own step.
- **Deleted**: `wwwroot/app.js`, `wwwroot/lib/`, `wwwroot/vendor/vue.esm-browser.prod.js`, the
  tracked `wwwroot/index.html`, `demo/tests/`, and the README's "Re-vendoring Vue" section. Vue now
  comes from `node_modules` and is bundled.

### Two decisions worth keeping

1. **`typescript` is pinned to 6.0.3, not the current 7.0.2.** Sitting 2 read `vue-tsc@3.3.11`'s peer
   range (`typescript >=5.0.0`) and concluded 7 was fine. It is not: `vue-tsc --build` under 7.0.2
   fails immediately with
   `Error [ERR_PACKAGE_PATH_NOT_EXPORTED]: Package subpath './lib/tsc' is not defined by "exports" in
   .../typescript/package.json`. A declared peer range is not evidence about a package layout change;
   running it is. 6.0.3 is the newest 6.x on the registry and type-checks clean.
2. **The worker URL is `document.baseURI`, not `import.meta.url`.** Before bundling the page's module
   sat beside `worker.js`; a bundled module sits under `assets/`, so `import.meta.url` would resolve
   to `assets/worker.js` and 404. `document.baseURI` says what is meant - the worker is a sibling of
   the PAGE - and keeps the no-base-href property that makes one artefact work at a root and at a
   subpath.

### checks.html had to change with it

Check 6 imported `./lib/pool.js` and `./app.js` to build its own pools. There is no stable module URL
any more (one hashed bundle per build), so the page publishes what the harness needs:
`window.__demoInternals = { createPool, spawnEngineWorker }`, and check 6 takes them from the frame's
window. That is also the truer measurement - the pools are now built in the window whose workers the
demo actually spawns.

### Runs, all on the committed tree

| What | Command | Result |
| --- | --- | --- |
| Front end | `pwsh -File tools/build-demo-web.ps1` | `npm ci`, type-check clean, **34 tests / 4 files passed**, bundle written. DEMO WEB BUILD GREEN |
| Publish + artefacts | `pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-publish` | **WASM SMOKE GREEN**: 28 files, 8,007,899 bytes (7.64 MB), gzip 2.16 MB, 56 integrity endpoints recomputed |
| Browser, root | `http://localhost:8090/checks.html` | **CHECKS GREEN, 9 of 9** |
| Browser, subpath | `http://localhost:8090/fuzzy-regex-cs/checks.html` | **CHECKS GREEN, 9 of 9** |

Both browser runs used one `python -m http.server 8090` over `.scratch/serve`, which holds the
publish twice: at the root and under `fuzzy-regex-cs/`. Chrome 153 via the Playwright MCP server.

Numbers from the root run: display cap `200 drawn of 300 found, 200 <mark> elements`; runaway
`32 timer ticks on the checks page and 32 inside the demo during the 500 ms window, status pills
["matching..."]`; respawn **71.2 ms with the spare, 175.9 ms without**; stop-to-next-answer-on-screen
414.3 ms. Subpath run: 107.1 ms with the spare, 185.6 ms without.

**One earlier finding does not reproduce.** Sitting 2 measured 1 and 0 animation frames on the demo
page and recorded that it "does not get rAF in this browser". This sitting's root run reported **30
frames** in the same 500 ms window on the new page. So that was a property of the old page or of that
session, not a standing fact about the browser; the reported-only treatment of the frame count stays
right either way, and the assertion has never depended on it.

### Screenshots

`docs/demo/page-1280.png` and `docs/demo/page-390.png`, taken with Playwright at those two widths
against the subpath URL. They are the reference layouts and are linked from `demo/README.md`.
