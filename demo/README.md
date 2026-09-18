# The browser demo

A single page that runs this library, compiled to WebAssembly, entirely in the visitor's browser.
Nothing is sent anywhere: there is no server side, and the three inputs live in the URL fragment,
which [RFC 3986 section 3.5](https://www.rfc-editor.org/rfc/rfc3986#section-3.5) keeps on the client.

Live at <https://zejji.github.io/fuzzy-regex-cs/> once Pages is enabled (see "Deploy to GitHub Pages"
below).

## What is in here

| Path | What it is |
| --- | --- |
| `FuzzyRegex.Demo.Wasm/DemoEngine.cs` | The `[JSExport]` surface. Takes pattern, flags and subject; returns one JSON answer. Owns the engine-side caps. |
| `FuzzyRegex.Demo.Wasm/wwwroot/worker.js` | Boots the runtime inside a Web Worker and answers one request at a time. The engine never runs on the page's thread. |
| `FuzzyRegex.Demo.Wasm/wwwroot/index.html` | The page itself: three inputs, the highlighted subject, the match and group tables, the examples sidebar. |
| `FuzzyRegex.Demo.Wasm/wwwroot/app.js` | The Vue application. Debounces input, drives the worker pool, formats the answer. |
| `FuzzyRegex.Demo.Wasm/wwwroot/lib/` | The parts that are testable without a browser: `caps.js`, `fragment.js`, `highlight.js`, `pool.js`. |
| `FuzzyRegex.Demo.Wasm/wwwroot/vendor/` | Vue, vendored. No CDN and no bundler; see "Re-vendoring Vue". |
| `FuzzyRegex.Demo.Wasm/wwwroot/examples.json` | The eight worked examples in the sidebar. Their answers are pinned by a test, so this file is not free-form copy. |
| `FuzzyRegex.Demo.Wasm/wwwroot/checks.html` | The browser-side verdict page. Drives the real `index.html` in an iframe and prints CHECKS GREEN or CHECKS RED. |
| `FuzzyRegex.Demo.Wasm/wwwroot/harness.html` | The engine-only harness from the previous slice. Kept because it isolates the worker from the page. |
| `tests/` | `node --test` tests for `lib/` and for the page's own state machine in `app.js`, run with fake workers and no browser. |

Two more things live outside this folder because they are repository-wide:
`tools/run-wasm-smoke.ps1` (publishes and checks the artefact set) and
`.github/workflows/pages.yml` (deploys).

## Run it locally on Windows, from a fresh clone

### Prerequisites

- **.NET SDK 10.0.400 or a later patch of it.** `global.json` pins that band with
  `rollForward: latestPatch`, so a 10.0.4xx SDK works and an older one does not. Check with
  `dotnet --version`.
- **The `wasm-tools` workload.** The SDK does not carry it. Install it once per machine:

  ```powershell
  dotnet workload install wasm-tools
  ```

  On Windows this writes into the SDK's own folder, so run it from a terminal that is allowed to:
  an elevated PowerShell if the SDK is installed under `C:\Program Files`.
- **Python 3**, only to serve the published files. Any static file server will do; Python's is used
  below because it is the one the browser runs in this repository were done with.
- **Node 20 or later**, only to run the JavaScript tests. Not needed to run the page.

### Publish and serve

```powershell
git clone https://github.com/zejji/fuzzy-regex-cs.git
cd fuzzy-regex-cs
dotnet publish demo/FuzzyRegex.Demo.Wasm -c Release
cd demo/FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot
python -m http.server 8080
```

Then open <http://localhost:8080/>. The first answer should appear within a second or two of the
page settling; the status line tells you which state it is in ("starting the engine", "matching",
or the match count and how long it took).

Serve the **publish** output, not the `wwwroot` source folder. The source folder has no
`_framework/`, so the page loads and the engine never starts.

The checked version of the same thing:

```powershell
pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/demo-publish
```

That publishes **and** asserts the artefact set: every file the static web assets manifest names is
on disk, every `integrity` hash matches a SHA-256 recomputed from the file, nothing unnamed is left
over, and `.nojekyll` is at the web root. Serve `.scratch/demo-publish/wwwroot` the same way.
`.scratch/` is gitignored. This is what CI runs, so it is the one that tells you whether a deployment
would work.

Editing the page is publish-and-refresh either way: this project has no launch profile and no dev
server, because what is served on Pages is a trimmed publish and anything else would be a different
artefact from the one under test. A page-only change (HTML, `app.js`, `lib/`) can be made against an
already-published folder by copying the file over and refreshing, but commit the source, not the
copy.

If a change to a `.js` or `.html` file seems not to take, it is the browser's cache, not the
publish: the demo's own files are served under their own names rather than fingerprinted ones. Hard
refresh (Ctrl+F5).

### Run the tests

```powershell
pwsh -File tools/run-demo-js-tests.ps1
```

That runs `demo/tests/*.test.js` under `node --test`. It refuses Node below 20, where the test
runner exits 0 having run nothing.

The C# side of the demo is covered by the main suite, in
`tests/FuzzyRegex.Tests/Gaps/Demo/`: `DemoExamplesTests` pins every sidebar example's answer against
the upstream `regex` module, and `DemoCapsTests` reads `wwwroot/lib/caps.js` as text and checks its
numbers against `DemoEngine`'s, so the page's limits and the engine's cannot drift apart silently.

Neither of these is wired into `tools/check-ratchet.ps1` as a demo-specific step: the JS tests run in
`pages.yml`, and the C# ones are ordinary tests in the ordinary suite.

## Deploy to GitHub Pages

### The one-off repository setting

Do this once, by hand, in the repository on github.com. No workflow can do it for you.

1. **Settings**, then **Pages** in the left-hand menu.
2. Under **Build and deployment**, set **Source** to **GitHub Actions**.

There is nothing else to configure: no branch, no folder. Until this is set, the deploy job fails
with `Get Pages site failed`, which reads like a permissions problem and is not.

### What the workflow does

`.github/workflows/pages.yml`, named "Demo (GitHub Pages)", runs on pushes to `main` that touch
`demo/`, `src/`, either of the two demo scripts or the workflow itself, and on a manual
**Run workflow** (`workflow_dispatch`). It is deliberately separate from `ci.yml`: it gates nothing,
so a broken demo can never block a library merge.

The build job, in order:

1. Checks out the repository with submodules and installs the SDK named in `global.json`.
2. Installs the `wasm-tools` workload.
3. Runs the page's Node tests.
4. Runs `tools/run-wasm-smoke.ps1`, which publishes and then asserts the artefact set described
   above. If anything it checks is wrong, nothing is uploaded.
5. Checks that `.nojekyll` reached the publish root. Without it, Pages runs Jekyll, Jekyll drops
   every underscore-prefixed folder, and the site serves a 404 for its own `_framework/` while
   reporting a successful deployment.
6. Uploads the publish root as the Pages artefact.

The deploy job then publishes that artefact to the `github-pages` environment. It needs the
`pages: write` and `id-token: write` permissions, which are granted in the workflow file.

There is no base-href rewrite step, which is a deliberate difference from Microsoft's documented
Blazor-on-Pages recipe. Every URL in this page is relative to the document and the worker is resolved
against `import.meta.url`, so the same artefact boots at `https://<user>.github.io/<repo>/`, at the
root of a local server, and at a fork's preview path, with nothing to keep in step.

### How to tell it is live

1. **Actions** tab, the "Demo (GitHub Pages)" run. Both jobs green, and the `deploy` job shows a
   `github-pages` environment link. That link is the site's URL.
2. Open it. Expect <https://zejji.github.io/fuzzy-regex-cs/>. The status line should reach a match
   count, and clicking an example in the sidebar should change the highlighted subject.
3. For a verdict rather than an impression, open `/checks.html` at the same origin, for example
   <https://zejji.github.io/fuzzy-regex-cs/checks.html>. It drives the real page in an iframe and
   prints **CHECKS GREEN** or **CHECKS RED**, with the detail in `window.__checks.checks`.

A deployment that ran but left the site unchanged is almost always the browser cache or the CDN;
open the URL in a private window before investigating anything else.

## Re-vendoring Vue

The page uses Vue 3, vendored as a single ES module: no CDN, so the page has no third-party runtime
dependency at load time, and no bundler, so what ships is what was reviewed.

Currently `vue@3.5.43`, `dist/vue.esm-browser.prod.js`, 173,163 bytes, SHA-256
`877f675a8c5f347073b4d5437439a042b984d81fc5da2770eb7e6d320d5017f3`. That hash is repeated in the
comment above the import in `app.js`; both must be updated together.

To take a new version:

1. Download `https://registry.npmjs.org/vue/-/vue-<version>.tgz` and check its SHA-512 against the
   `dist.integrity` field of `https://registry.npmjs.org/vue/<version>`. Do not skip this: it is the
   only check on what you are about to commit.
2. Extract `package/dist/vue.esm-browser.prod.js` from the tarball to
   `FuzzyRegex.Demo.Wasm/wwwroot/vendor/vue.esm-browser.prod.js`. Take the file's bytes as they are;
   do not reformat it and do not let an editor change its line endings.
3. Record the new byte count and SHA-256 in the `app.js` comment and in this file.
4. Re-run the Node tests and the smoke script, and open the page.

`.gitattributes` marks `*.js binary` so Git does not normalise line endings in it. A line-ending flip
would change the file's hash and, in the published output, break the subresource-integrity check that
`run-wasm-smoke.ps1` recomputes.

The build used is the **full** one, with the template compiler, not `vue.runtime.esm-browser.prod.js`:
the page's markup is in `index.html` and is compiled in the browser, which keeps the page readable as
HTML rather than as a string inside a script.
