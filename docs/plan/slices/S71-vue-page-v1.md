---
slice: S71
phase: 9
title: Demo v1 - the Vue page the ROADMAP specified, live on GitHub Pages
delivers: []
---

# S71 - v1, and a link that works

S70 proved the engine answers from a worker. This slice is the deliberately small page around it
(ROADMAP, 2026-08-31) plus the deployment that makes it a link the README can carry. Nothing widens
the scope: accounts, persistence, analytics and anything server-side stay out; the feature tour is
v2 (S72).

## Scope

- **Vue 3, vendored as an ESM build under `demo/wwwroot/vendor/`**, imported by a plain
  `<script type="module">`. No build step, no bundler, no CDN: a CDN makes the demo's availability
  someone else's uptime and a bundler makes it someone else's toolchain. Record the exact version
  and its SHA-256 in a one-line comment beside the import.
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

- [ ] Page delivers all seven v1 items above; Vue vendored with its version and hash recorded.
- [ ] `pages.yml` green and the live URL answering; README link landed.
- [ ] Ratchet GREEN, blind review, commit. **Hunt:** an integrity failure after line-ending
      conversion - a Windows runner checking out with `core.autocrlf`, or any step that rewrites a
      published file between publish and upload, both of which produce a page that boots on the
      developer's machine and 404s or fails SRI on Pages; a base href that works at the repository
      subpath and breaks at the root, or the reverse, because it was hard-coded in two places; the
      worker URL resolved relative to the page rather than the base, which is the same bug wearing a
      different hat; a match-count cap that caps the array but still builds one DOM node per match
      before slicing; a fragment long enough to be silently dropped by the browser.

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
