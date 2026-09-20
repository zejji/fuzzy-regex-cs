---
slice: S71
phase: 9
title: Demo v1 - the Vue page the ROADMAP specified, live on GitHub Pages
delivers: []
---

# S71 - v1, and a link that works

> **Design bar and toolchain (owner, 2026-09-18 20:15 and 20:55; part of done).** The page must look
> professional, as in a product page a stranger would trust, not a test harness with inputs.
> Concretely: one restrained palette (neutrals plus a single accent, with match highlights that stay
> distinguishable from each other and from the accent); a type scale and a spacing scale applied
> consistently; monospace for pattern, flags and subject; clear hierarchy between the inputs, the
> highlighted subject and the group table; designed empty, loading, error (parse error, cap refused,
> worker killed) and no-match states; visible focus states and WCAG AA contrast; usable at 390 px
> and 1280 px wide; honours `prefers-color-scheme` or picks one scheme and does it well. Layout
> follows researched UI/UX practice (owner, 21:05: spacing must not be dodgy): one spacing scale applied
> everywhere, consistent alignment, labels and inputs sized and placed per current form-design
> guidance, a clear primary action, and the sources followed named in the closing notes.
>
> **Toolchain.** The owner withdrew the "no build step" rule (spec amendment 27): the front end is
> a Vite + Vue 3 + TypeScript project in strict mode, with strong typing everywhere possible (typed
> worker messages shared by page and worker, typed examples, no `any`), and a CSS framework with a
> build step (Tailwind) is allowed and encouraged for consistency. `npm run build` type-checks with
> `vue-tsc` and runs the unit tests before `vite build`. Everything pinned in package-lock.json and
> installed with `npm ci`; the Pages workflow builds it with a pinned Node. **No CDN, ever.** Record
> the framework choice and the reason in the closing notes, with screenshots at both widths.
S70 proved the engine answers from a worker. This slice is the deliberately small page around it
(ROADMAP, 2026-08-31) plus the deployment that makes it a link the README can carry. Nothing widens
the scope: accounts, persistence, analytics and anything server-side stay out; the feature tour is
v2 (S72).

## Scope

- **Vue 3 + TypeScript built with Vite** (superseded 2026-09-18, was: vendored ESM build, no build
  step). No CDN: a CDN makes the demo's availability someone else's uptime. The bundler objection
  is withdrawn with the rule; what survives it is that every dependency's exact version is pinned
  and committed, now in `package.json` + `package-lock.json` and installed with `npm ci` rather
  than in a SHA-256 comment beside a vendored file.
- **Three inputs** - pattern, flags, subject - and a result pane showing the subject with every
  match's span highlighted, plus a group table of index, length and captures per group. Named groups
  show their name; a group that did not participate shows as such rather than as an empty string,
  because those two are different answers and the demo exists to show real answers.
- **A sidebar of about eight worked examples**, loaded from a single JSON array in
  `demo/wwwroot/examples.json`. That array **is** the guided tour; clicking one fills the three
  inputs. Keep it to eight: the per-feature set is S72's, and a sidebar trying to be both is neither.
- **The three inputs encoded in the URL fragment**, so a case can be pasted into a bug report.
  Fragment, not query string: it never reaches a server, which keeps a pasted subject out of
  GitHub's logs. Read on load, rewrite on change with `history.replaceState` so back still leaves.
- **Caps, enforced in the page as well as in `Run`**: a stated maximum subject length (refuse with a
  message, never silently truncate - a truncated subject gives wrong answers that look right) and a
  stated maximum number of *displayed* matches, with the true total shown beside the cap. Rendering
  is the main thread's job and the worker cannot protect it, so this cap is the page's own freeze
  guard, not a cosmetic one.
- **A warm spare worker**: one serves requests while a second sits booted and idle. On `terminate()`
  the spare is promoted at once and a new spare starts in the background, so the respawn is not
  felt. Respawn latency has no published figure and none is measured yet (ROADMAP); the closing
  notes record the first one, taken with the spare disabled so the number means something.
- **A Pages workflow, `.github/workflows/pages.yml`**, separate from `ci.yml` so a demo failure
  never blocks a library merge, and triggered on pushes to `main` that touch `demo/**` plus
  `workflow_dispatch`. It publishes the demo, writes `.nojekyll` into the publish root, sets the
  `<base href>` to the repository subpath, then `actions/upload-pages-artifact@v3` and
  `actions/deploy-pages@v4` with `permissions: pages: write` and `id-token: write` and the
  `environment: github-pages` block. It runs `tools/run-wasm-smoke.ps1` first, so a broken artefact
  set is caught before it is deployed rather than after.
- **`*.js binary` in `.gitattributes`**, appended *below* the existing `* text=auto eol=lf` line
  because the last matching pattern wins (see the Findings for what it does and does not protect),
  and **the README gets the link**, one line under the status paragraph, saying plainly it is v1.

## Verification

- `tools/run-wasm-smoke.ps1` green locally before the workflow is written.
- The page served locally: `python -m http.server 8080` from the published `wwwroot`, then every
  claim exercised by hand and in the session's Playwright tooling - each of the eight examples
  loading and matching, a subject over the cap refused, a match count over the display cap showing
  the true total, a runaway pattern killed with the page still animating, the spare taking over, and
  a fragment round trip (copy the URL, open it in a new tab, get the same three inputs).
- `gh workflow run pages.yml` then `gh run watch`, and the deployed URL opened for real, once with
  the browser cache disabled (the only way the integrity path is actually exercised). A green
  workflow is not the verification; the live page answering correctly is.

## Done when

- [x] Page delivers all seven v1 items above. (Delivered in sitting 1 as a vendored no-build page;
      the vendoring half of this box is superseded by the toolchain amendment and is re-delivered by
      the two boxes below, not re-ticked here.)
- [x] The front end is a Vite + Vue 3 + TypeScript project, strict, no `any`, Tailwind, pinned and
      installed with `npm ci`; `npm run build` type-checks and runs the unit tests before building.
      (Sitting 3, after the owner lifted the npm blocker. 52 tests in 5 files at the close.)
- [x] The Design bar is met, with the researched UI/UX sources named and screenshots at 390 and
      1280 px. (Sitting 3: `App.vue` + `styles.css`, sources in the sitting-2 notes, screenshots in
      `docs/demo/`. Sitting 5's verifier re-took both and confirmed they still depict the page.)
- [x] The browser leg: `checks.html` CHECKS GREEN at the server root **and** under a
      `/fuzzy-regex-cs/` subpath, with `stopToNextAnswerOnScreenMs`, `respawnWithoutSpareMs` and
      `respawnWithSpareMs` recorded. Done in sitting 2; the subpath run settles the no-base-href
      decision in its favour.
- [ ] `pages.yml` green and the live URL answering; README link landed. (Workflow written and README
      link landed in sitting 1; green run and live URL need the owner's push and the one-off Pages
      setting, so this box stays open.)
- [x] Ratchet GREEN, blind review, commit. (Sitting 5: 6365 passing, three blind passes, the
      independent verifier.) **Hunt:** an integrity failure after line-ending
      conversion - a Windows runner checking out with `core.autocrlf`, or any step that rewrites a
      published file between publish and upload, both of which produce a page that boots on the
      developer's machine and 404s or fails SRI on Pages; a base href that works at the repository
      subpath and breaks at the root, or the reverse, because it was hard-coded in two places; the
      worker URL resolved relative to the page rather than the base, which is the same bug wearing a
      different hat; a match-count cap that caps the array but still builds one DOM node per match
      before slicing; a fragment long enough to be silently dropped by the browser.

## Browser brief - RUN AND GREEN in sitting 2 (kept as the re-run recipe)

**Outcome, 2026-09-18, Chrome 153.0.0.0 under the Playwright MCP server: CHECKS GREEN at the root
and CHECKS GREEN at the subpath, 9 of 9 each.** Measurements and the two `checks.html` bugs the run
exposed are in `docs/plan/slices/notes/S71-sittings.md`, sitting 2. The steps below stay because
they are how anyone re-runs it; two corrections to them, learned by running them:

- Serve on **8090** as written, but load `checks.html` **with a cache-busting query**
  (`?v=3`). Plain `python -m http.server` plus an edited checks page gives the browser's cached copy
  and the run silently tests the old file.
- The check formerly called "the page keeps painting while a runaway pattern runs" is now **"the
  page keeps running..."** and counts timer ticks rather than animation frames: this browser drives
  `requestAnimationFrame` normally on a bare page (31 frames per idle 500 ms) but not on the demo
  page, which boots two WebAssembly workers (1, then 0), so the old threshold measured that and not
  the demo. Expect `32 timer ticks on the checks page and 32 inside the demo` and status pills
  `["matching..."]`.

Per-sitting narrative is in `docs/plan/slices/notes/S71-sittings.md`. This section was written by
sitting 1, which had no browser tooling, as the spec for the one thing it could not do.

**Serve it yourself; do not reuse what is already running.** A `python -m http.server` on port 8080
(PID 37516) belongs to the owner and serves S70's publish - leave it alone. Publish to a fresh
directory and serve that on another port:

```powershell
pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-s71-browser
cd .scratch/wasm-s71-browser/wwwroot
python -m http.server 8090
```

**Run 1, served at the root** - `http://localhost:8090/checks.html`. Wait for the element with id
`verdict` to read `CHECKS GREEN`, then read `window.__checks`:

- `ok === true`, and `checks` is six entries, every `ok` true. Their names, in order: every worked
  example answers what upstream answers; a pasted link opens the case it encoded; the address bar
  follows the inputs; a subject over the cap is refused, not truncated; the display cap draws its
  limit and reports the true total; a group that did not take part is shown as such; the page keeps
  painting while a runaway pattern runs; stopping a runaway leaves a working page; a respawn is
  faster with the warm spare than without it.
- Expected values worth reading out of the detail strings rather than trusting the boolean: the
  examples check must say `all 8 examples agree with regex 2026.9.10`; the display-cap check must
  show `200 drawn of N found, 200 <mark> elements in the page` with N well above 200; the runaway
  check must report tens of timer ticks on both threads (see the correction above - as first written
  it asked for animation frames, which this page does not get).
- `window.__checks.measurements`: `stopToNextAnswerOnScreenMs` (the page's Stop-to-answer recovery,
  which includes the 250 ms debounce), `respawnWithoutSpareMs` and `respawnWithSpareMs` (raw pools,
  no debounce). Record all three in the notes file with the browser and its version. There is no
  published figure for a .NET WebAssembly respawn, so these are the slice's own measurements and the
  ROADMAP wants them.

**Run 2, served at a repository subpath** - the whole point of shipping no `<base href>`. Make the
publish sit under a subpath and load `http://localhost:8091/fuzzy-regex-cs/checks.html`:

```powershell
mkdir .scratch/subpath-root/fuzzy-regex-cs
xcopy .scratch\wasm-s71-browser\wwwroot .scratch\subpath-root\fuzzy-regex-cs /E /I /Y
cd .scratch/subpath-root
python -m http.server 8091
```

Expect the same `CHECKS GREEN`. If it is red at the subpath and green at the root, the no-base-href
decision is wrong and the fix belongs in this slice, not the next one.

**Then check the page by hand, with the browser's cache disabled**, because that is the only run in
which subresource integrity is actually exercised: `http://localhost:8090/` answers within a second
or two, clicking each sidebar example changes the highlighted subject, and copying the URL into a new
tab reopens the same three inputs.

**Record the evidence in S70's format** in the notes file: the URL, the verdict, the six checks with
their detail strings, the three measurements, and the browser version.

## Findings from the documentation (all fetched 2026-09-16)

All quotations are from
<https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/github-pages?view=aspnetcore-10.0>
unless stated.

- **`.nojekyll` is mandatory and the reason is exact**: "The default GitHub Action, which deploys
  pages, skips deployment of folders starting with underscore, the `_framework` folder for example.
  To deploy folders starting with underscore, add an empty `.nojekyll` file to the root of the app's
  repository." The ROADMAP said this and it is confirmed.
- **The `*.js binary` rule, verbatim**: "Git treats JavaScript (JS) files, such as
  `blazor.webassembly.js`, as text and converts line endings from CRLF to LF in the deployment
  pipeline. These changes to JS files produce different file hashes than Blazor sends to the client.
  The mismatches result in integrity check failures on the client." **The scenario differs from ours
  and that is worth recording rather than copying blindly:** it assumes the published assets are
  committed to a Git branch, whereas this workflow publishes in CI and uploads directly, so no
  framework JavaScript passes through Git. `*.js binary` therefore protects the *committed*
  JavaScript (`worker.js`, the vendored Vue build) and is cheap insurance; the live risk moves to
  anything that rewrites a published file after publish, and to the checkout's line-ending settings.
- **Base href.** The documented recipe uses the third-party
  `SteveSandersonMS/ghaction-rewrite-base-href` Action, pinned by SHA, because a Blazor app's
  `index.html` is generated. **This demo's `index.html` is ours**, so the lazier route is taken: one
  `<base href="/fuzzy-regex-cs/">` in the file, with the local server served from a matching path so
  the two cannot drift. Nothing third-party to pin. A divergence from the doc's recipe, not the
  ROADMAP's design.
- **The deploy actions**: `actions/upload-pages-artifact@v3` with `path:`, then
  `actions/deploy-pages@v4`, needing `pages: write` ("to deploy to Pages") and `id-token: write`
  ("to verify the deployment originates from an appropriate source"), plus the
  `environment: {name: github-pages, url: ${{ steps.deployment.outputs.page_url }}}` block -
  <https://github.com/actions/deploy-pages>, <https://github.com/actions/upload-pages-artifact>.
- **Pages serves the uncompressed files.** "GitHub Pages doesn't natively support using
  Brotli-compressed resources." The publish writes `.br` and `.gz` beside each asset and Pages
  serves neither, so the bytes on the wire are the uncompressed ones. Record that figure next to
  S70's on-disk baseline; the wire one is what a visitor waits for. The documented workaround is a
  `decode.js` Brotli decoder wired into boot resource loading, **not** taken in v1: measure first,
  then decide whether the download is worth a decoder.

---

## Closing notes (2026-09-19, sitting 5)

**What landed.** A demo page that is a real front-end project: Vite 8.3.0, Vue 3.5.43, TypeScript
6.0.3 in strict mode, Tailwind 4.3.3, Vitest 5.0.1, every version pinned and installed with
`npm ci`, building into the .NET web root so one publish carries page, worker and runtime. Three
inputs, highlighted spans, a group table that distinguishes a group that did not participate from
one that matched empty, eight worked examples whose answers a C# test pins, the case in the URL
fragment, caps enforced in the page as well as in `Run`, and a warm spare worker. Around it:
`tools/build-demo-web.ps1`, a smoke script that parses the published page rather than naming its
assets, a Pages workflow that installs Node from `.nvmrc`, `demo/README.md`, the root README's link,
and screenshots at both widths. The browser verdict page is green at a server root and at
`/fuzzy-regex-cs/`, which is what makes one artefact deployable to both.

**The one box left open**, deliberately, is the live URL: `pages.yml` has never run, because the
owner pushes and only the owner can set Settings > Pages > Source = GitHub Actions. Everything up to
that point is verified locally. **S72 should start by opening the deployed URL** with the browser
cache disabled - that is the only run in which subresource integrity is really exercised - and by
watching `gh run watch` on the first `pages.yml` run.

**What was surprising.** Three sittings of green builds, a green ratchet and a green browser verdict
all coexisted with a dev server that could not boot the demo at all: Vite's html fallback rewrites
the URL before a post-hook middleware sees it, so `GET /worker.js` returned the HTML page, and the
build output living in the middleware's own fallback root meant the page came back as the last
production bundle. Nothing in the production path can see either. The lesson for the next slice that
adds a dev-time convenience is that `npm run dev` needs a test as much as `npm run build` does, and
that the test must create the state it guards against - the built page is gitignored, so a test
relying on one being present is a test that can never fail on a fresh checkout.

**What the next slice should know.**

- `npm run dev` now works: the page from source with HMR, and `worker.js`, `examples.json`,
  `checks.html` and `_framework/` from the publish or the web root behind it. A publish has to exist
  for the runtime; `tools/run-wasm-smoke.ps1` produces one.
- One displayed match must stay exactly one `<mark>`, and the phrase "did not participate" must stay
  in the group table: `checks.html` asserts both, so a component rewrite that changes either
  silently changes what the verdict page means.
- The respawn figures are this project's own measurements, taken twice now on the same machine:
  about 69 ms with the warm spare against 164 ms without, and about 414 ms from Stop to the next
  answer on screen, which includes the 250 ms debounce.
- `docs/STATUS.md`'s upstream-commit line is generated from `git -C upstream rev-parse HEAD`, and in
  a worktree whose submodule is not checked out that silently answers with our own HEAD. Sitting 5
  checked the submodule out here and the committed line is correct again, but **the generator still
  needs to fail loudly instead of falling back**; it is the top open item in STATE.md.

**Review.** Three blind passes over this slice's unreviewed changes: **12 findings raised, 12
reproduced, 12 fixed.** Pass 1 read the whole sitting-3 diff, 42 files that had had no review at all,
and raised 4. Pass 2 read the fixes, which had added exported surface and a new test file the first
reviewer never saw, and raised 5 - including the finding that the new regression test could not go
red on a fresh checkout. Pass 3 read the ordering fix and the rewritten test and raised 3. Every
finding was reproduced in this session before any code changed; unusually, all twelve survived that
gate, which is explained by the first two passes reading code nobody had reviewed and the third
reading code an hour old. No fourth pass was needed: pass 3's changes are covered by tests that were
each proved to fail without their fix, and by the independent verifier, which re-ran every number in
the sitting-5 notes from the commit-ready tree and returned CONFIRMED on all of them but the publish
byte count, which it corrected from 8,007,899 to 8,009,524.
