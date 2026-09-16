---
slice: S70
phase: 9
title: The engine in a Web Worker - a wasmbrowser project, one [JSExport], and terminate() proven
delivers: []
---

# S70 - The demo's engine half

The worker is the whole safety design (ROADMAP, 2026-08-31): a public demo invites strangers to type
pathological patterns, and fuzzy matching is combinatorially worse than the exact case, so "the page
never freezes" has to be a property of the browser's scheduler, not of the engine's diligence. This
slice builds the engine half only. No Vue, no styling, no Pages deploy: those are S71, and mixing
them makes a failed boot indistinguishable from a failed render.

## Scope

- **`demo/FuzzyRegex.Demo.Wasm/`**, created from the documented template (`dotnet new wasmbrowser`)
  and then reduced to what this demo needs. The properties are the shipped sample's, not guesses:
  `Microsoft.NET.Sdk.WebAssembly`, `<RuntimeIdentifier>browser-wasm`, `<OutputType>Exe`,
  `<WasmMainJSPath>`, `<PublishTrimmed>true` with `<TrimMode>full`, and
  `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, which the interop **requires** (see Findings).
  `InvariantGlobalization` is already true in `Directory.Build.props`, so it is inherited and must
  NOT be repeated. A project reference to `src/FuzzyRegex`, nothing else. Add it to
  `FuzzyRegex.slnx` under a new `/demo/` folder: S53's review found a project missing from the
  solution reaching a green ratchet without ever being compiled by CI.
- **Exactly one `[JSExport]`**: `string Run(string pattern, string flags, string subject)`. Strings
  in, one JSON string out carrying the matches, each match's span, its groups (index, length,
  captures) and its fuzzy counts. It never throws across the boundary - a managed exception crossing
  into JS arrives as a rejected promise with no useful text - so every failure (parse error, timeout,
  cap breach) comes back as `{"error": "..."}`. The attributed method is a one-line wrapper over a
  plain `internal static string` with the same signature, and *that* is what `tests/FuzzyRegex.Tests`
  pins, so the JSON contract is tested under the JIT on three operating systems and the demo project
  needs no test host. Pin at least: groups and captures, a fuzzy match with counts, a parse error,
  and a `MatchTimeout` firing.
- **`MatchTimeout` is set inside `Run`**, as defence in depth and the fast common-case exit, never
  as the only safety net (ROADMAP). `Run` also refuses a subject over a stated ceiling: the UI's own
  caps land in S71, but a cap enforced only in the page is no cap once somebody drives the worker
  from the console.
- **`demo/wwwroot/worker.js`**, the Microsoft sample's shape verbatim: a module worker that
  top-level-`await`s `dotnet.create()`, calls `getAssemblyExports(config.mainAssemblyName)`, then
  answers `message` events with the request's `requestId` echoed back. No `runMain()`.
- **`demo/wwwroot/harness.html`**, plain HTML, no framework and no styling, proving three things and
  writing each verdict into the DOM and into `window.__harness` for a driver to read: (1) a
  `postMessage` round trip returns the engine's answer and not an echo - assert a value the page
  could not have computed, such as the per-error-type counts of an approximate match; (2)
  `worker.terminate()` on a deliberately pathological pattern kills it and a respawned worker
  answers the next request correctly; (3) a `requestAnimationFrame` counter keeps ticking while the
  runaway runs, which is the page staying responsive stated as something measurable.
- **`tools/run-wasm-smoke.ps1`**, modelled on `tools/run-aot-smoke.ps1` and carrying the same
  MSBuild-node hang workaround (`MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`). It
  publishes, then asserts the artefact set rather than eyeballing it: `_framework/` present and
  non-empty, `dotnet.js` present, every `integrity` entry in the boot manifest matching a hash
  recomputed from the file on disk, `.nojekyll` at the publish root, and the total published bytes
  and largest three files printed. Any missing or mismatched entry fails the script. Those figures
  plus the warm time from `new Worker(...)` to the first answer are **the demo's baseline**, and go
  in the closing notes beside S53's 6.65 MB with the caveat below, never as a comparison against it.

## Verification

- `dotnet workload list` lists `wasm-tools` and `wasm-experimental` (owner installed 10.0.112,
  2026-09-16); `dotnet build FuzzyRegex.slnx --configuration Release` - the whole solution, per
  S53's review finding 1.
- `dotnet run --project tests/FuzzyRegex.Tests` green, including the new JSON-contract tests;
  `tools/run-wasm-smoke.ps1` green (publish clean, artefact set complete, integrity recomputed).
- The browser leg, driven by hand this slice: `python -m http.server 8080` from the published
  `wwwroot`, then `harness.html` in a real browser and in the session's Playwright tooling, reading
  `window.__harness`. **ponytail: no npm or Node toolchain is added for this.** Upgrade path if it
  must gate CI: a Playwright step in S71's Pages workflow, where a runner and artefact exist.

## Done when

- [ ] `demo/FuzzyRegex.Demo.Wasm` publishes clean, is in the solution, exposes one `[JSExport]`, its
      JSON contract pinned by JIT tests.
- [ ] Harness proves round trip, `terminate()` plus respawn, and a responsive page; baseline recorded
      with the comparability caveat.
- [ ] `tools/run-wasm-smoke.ps1` committed and exercised; zero trim warnings from a publish taken
      from a cleared `obj/` (S53's verifier: an incremental re-publish does not re-emit them).
- [ ] Ratchet GREEN, blind review, commit. **Hunt:** a pathological pattern that freezes the page
      anyway - the freeze moving into `dotnet.create()` on the respawn, or into the main thread's own
      rendering of a result with hundreds of thousands of matches, neither of which the worker does
      anything about; a round trip that passes because the harness asserts on a value it supplied
      itself; a `terminate()` whose stale answer still arrives and is rendered because the response
      carries no `requestId` check; `TrimMode=full` dropping a table only one untested feature
      reaches.

## Findings from the documentation (all fetched 2026-09-16)

- **`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` is required**, not advisory: "The JS interop API
  requires enabling AllowUnsafeBlocks". The same page gives `dotnet new wasmbrowser` and
  `dotnet workload install wasm-tools wasm-experimental`, and confirms the ROADMAP's risk note
  verbatim: "These templates are experimental at this time, which means the developer workflow for
  the templates is evolving. However, the .NET and JS APIs used in the templates are supported in
  .NET 8" - <https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0>
- **The worker boot pattern is a shipped sample**, not an invention:
  `dotnet/blazor-samples/10.0/DotNetOnWebWorkersReact` - `dotnet/wwwroot/worker.js` imports
  `{ dotnet } from './_framework/dotnet.js'`, does `await dotnet.create()` then
  `getAssemblyExports(config.mainAssemblyName)`; `react/src/client.js` starts it with
  `new Worker(url, { type: "module" })`, so a module worker is required. Its `QRGenerator.csproj`
  is the property list above.
- **No cross-origin isolation is needed, and the reason is sharper than the ROADMAP's.**
  `dotnet/runtime/src/mono/wasm/features.md`: threads are opt-in via `<WasmEnableThreads>` and need
  `Cross-Origin-Embedder-Policy: require-corp` and `Cross-Origin-Opener-Policy: same-origin`; the
  same file also says "JavaScript interop with managed code via [JSExport]/[JSImport] is currently
  limited to the main thread even if multi-threading support is enabled". So `WasmEnableThreads`
  would not merely be unnecessary, it would break the interop this design rests on. The worker's own
  thread is that runtime instance's main thread, which is why the design works at all.
- **The size figures are not comparable, and the slice must say so.** S53's 6.65 MB is a Native AOT
  win-x64 executable; this is an IL bundle interpreted in a browser - different compiler, different
  unit. Record it as the demo's own baseline. WebAssembly AOT (`RunAOTCompilation`) is deliberately
  NOT turned on: a size-against-speed knob with no measurement behind it yet.
