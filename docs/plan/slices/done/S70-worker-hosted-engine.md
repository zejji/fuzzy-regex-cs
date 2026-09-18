---
slice: S70
phase: 9
title: The engine in a Web Worker - a wasmbrowser project, one [JSExport], and terminate() proven
delivers: []
---

# S70 - The demo's engine half

> **BOUNDED PROBES ONLY (owner rule, 2026-09-18, after the machine crashed twice on unbounded
> probes from this slice).** No probe project, no `dotnet run` of anything but the harness; exercise
> `DemoEngine.Run` only through TUnit tests with subjects under 100 characters and repeat products
> under 10,000. Any process that compiles or matches an untrusted or pathological pattern runs with
> `DOTNET_GCHeapHardLimit=0x40000000`, a wall-clock timeout and a subject-length cap, and no probe is
> delegated to a subagent without those limits written into its prompt. Do not re-measure the
> compile blow-up: it is sliced as S56b on `main`.

**Per-sitting notes: `docs/plan/slices/notes/S70-sittings.md`.** Four sittings: 1 interrupted,
2 and 3 GREEN checkpoints, 4 the close.

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

- [x] `demo/FuzzyRegex.Demo.Wasm` publishes clean, is in the solution, exposes one `[JSExport]`, its
      JSON contract pinned by JIT tests. (36 contract tests; whole solution builds Release with 0
      warnings.)
- [x] Harness proves round trip, `terminate()` plus respawn, and a responsive page; baseline recorded
      with the comparability caveat. **HARNESS GREEN in Chromium** - all six checks, including 31
      animation frames during the runaway and no answer 3.5 s after `terminate()`. Warm-time
      baseline **418.3 ms** from `new Worker(...)` to first answer. Driven by Playwright MCP by the
      orchestrator, 2026-09-18 15:56, against the committed publish (served md5 matched disk);
      evidence in `.claude/driver/s70-browser-evidence.json`.
- [x] `tools/run-wasm-smoke.ps1` committed and exercised; zero trim warnings from a publish taken
      from a cleared `obj/` (S53's verifier: an incremental re-publish does not re-emit them).
      **Restated against the committed code in sitting 4** via the new `-OutDir`, the publish
      directory still being held open by the server the browser leg needed: GREEN, 22 files,
      7,278,441 bytes, 44 integrity endpoints, zero warnings, and the linker proven to have run
      (`obj/.../linked/Link.semaphore` timestamped inside the run, `obj/` deleted at its start).
- [x] Ratchet GREEN, blind review, commit. **Ratchet GREEN: 6343 passing, 0 failing, 0 skipped,
      baseline 6235 unchanged.** Three blind passes across the slice (sittings 2, 3, 4), the last
      over the tooling no earlier reviewer saw; the independent verifier ran over the commit-ready
      tree. **Hunt:** a pathological pattern that freezes the page
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

## Closing notes (2026-09-18, four sittings)

**What landed.** The demo's engine half, whole: `demo/FuzzyRegex.Demo.Wasm` (a reduced
`wasmbrowser` project, in `FuzzyRegex.slnx` under `/demo/`), one `[JSExport] Run(pattern, flags,
subject)` over a plain `internal static` that 36 JIT tests pin, `worker.js` in the Microsoft
sample's shape, `harness.html`, `tools/run-wasm-smoke.ps1`, and three probes holding the smoke
script's own failure branches down. Ratchet GREEN: 6343 passing, 0 failing, 0 skipped, baseline
6235 unchanged.

**The demo's baseline, all of it this slice's own numbers.** Published 22 files, 7,278,441 bytes on
disk (6.94 MB), 2,076,631 bytes of gzip variants (1.98 MB) - the wire figure where Pages negotiates
gzip, Brotli not being served by Pages - and 44 integrity endpoints recomputed and matched. Warm
time from `new Worker(...)` to the first answer: **418.3 ms**, Chromium on this machine, with the
runtime already in the browser cache, so it is a floor for a first visit rather than a prediction of
one. **None of this is comparable with S53's 6.65 MB**: that is a Native AOT win-x64 executable and
this is an IL bundle interpreted in a browser - a different compiler producing a different unit.

**The browser leg was run by the orchestrator, not by a slice session** (Playwright MCP over
Chromium, orchestrator, 2026-09-18 15:56; evidence in `.claude/driver/s70-browser-evidence.json`).
Three sittings each spent a whole turn being denied the browser tools and exited with no commit, so
the leg was taken out of the slice session. **HARNESS GREEN**, `window.__harness.ok === true`, all
six checks: the round trip returns per-error-type counts the page cannot compute
(`index=0 length=6 counts=(1,1,1)`, matching upstream); each reply carries its `requestId`; a parse
error arrives as `{"error":"missing )"}` rather than a rejection; **31 animation frames painted in
the 500 ms the runaway ran**; `terminate()` left no answer after 3.5 s, past the 2 s an unkilled
worker answers on; a respawned worker gives `[[1,1],[4,2],[8,3]]`, matching upstream. The served
`harness.html` was md5-compared against the file on disk, so the harness that ran is the harness
committed. Only console error: a 404 for a favicon that is not shipped.

**Surprising, and the next slice's problem.** `Run` has no clock over `FuzzyRegex` *construction*.
`(((a{100}){100}){100}){100}` is inside `MaxPatternLength`, spends its time in the constructor where
`MatchTimeout` does not apply, and never returns; it took this machine to 0 GB free on 2026-09-18.
Recorded in `DemoEngine.MatchTimeout`'s remarks and sliced as **S56b** on `main`
(`docs/plan/2026-09-18-repeat-unrolling-investigation.md`) - do not re-measure it. It is also the
sharpest argument for the ROADMAP's design: `worker.terminate()` is the only thing that recovers a
wedged construction, so **S71 must treat terminate as its primary control, not as an error path.**

**Also for S71:** the harness caps nothing about rendering. The worker keeps the page responsive
while the engine runs, but a result with hundreds of thousands of matches is rendered by the main
thread, and that freeze is still available to anyone who asks for it.

**Review.** Three blind passes, one per working sitting, each dispatched blind and waited for
in-turn. Sitting 2: findings on the JSON contract and the stranger-controlled inputs, which is where
`MaxFlagsLength` and `MaxQuotedTokenLength` came from. Sitting 3 (over the `-SkipPublish` and
missing-artefact-probe delta): **7 raised, 6 reproduced and fixed, 1 not sustained** - the
"2 file(s)" miscount was the smoke script's, not the probe's, and was fixed there. Sitting 4, over
the tooling no earlier reviewer had seen (sitting 3's own fixes, plus `-OutDir`): **5 raised, 5
reproduced, 5 fixed** - a guard that would have let `-OutDir tools` delete the script's own
directory; compressed variants never checked for existence (41 of 42 deletable with the script still
GREEN); no check from disk back to the manifest (an orphan file inflating the printed baseline to 23
files and 8.36 MB); a zero-warning claim the code never made; and `.Sum` throwing on an empty
pipeline under `Set-StrictMode -Version Latest`. Every finding in that pass reproduced, which is
unusual - all five were checks that passed without checking. The fixes are covered by a new probe
(below) rather than by prose, and the two older probes still pass against the changed script. The
independent verifier (amendment 16(d)) then re-ran the slice's quoted numbers from the committed
files.

**Controls, re-runnable rather than described.** These are scripts, not paragraphs, because S18's
and S22's are already unreproducible from their prose:

> `tools/probes/wasm-smoke-missing-artefact.ps1` - deletes one published `.wasm`, expects the
> missing-artefact branch's own wording, restores it, and re-checks GREEN. Result:
> `exit 1, reported dotnet.native.030iq1ikbj.wasm missing, printed no GREEN`.
>
> `tools/probes/wasm-smoke-branch-controls.ps1` - stages the two mistakes the probe above could have
> made (a corrupted byte, which the integrity branch catches and names identically; a start against
> an already-broken publish). Result: `BOTH CONTROLS BEHAVED`.
>
> `tools/probes/wasm-smoke-artefact-checks.ps1` (new, sitting 4) - on a per-run temp copy of the
> publish: control GREEN, then 41 of 42 compressed variants deleted, restored, then one orphan
> `.wasm` added and removed, then a closing control. Result: `BOTH ARTEFACT CHECKS FIRED: 41
> compressed variants deleted gives exit 1 and the missing wording; one orphan file gives exit 1 and
> the orphan wording; the copy checks GREEN again once both are undone.` Needs only an existing
> publish; takes seconds.

All three were run last against the code being committed, after the final fix.

**The clean publish, and why the script grew `-OutDir`.** The zero-trim-warning claim needs a
publish from a cleared `obj/`, and the publish directory was held open for two sittings by the
`python -m http.server` (PID 37516) whose job was to serve the browser leg. Rather than kill it,
`tools/run-wasm-smoke.ps1 -OutDir <dir>` publishes elsewhere, clearing `obj/` and that directory and
leaving `bin/` alone - `obj/` being where the linker's state lives. Final run,
`-OutDir .scratch/wasm-clean-publish`: **WASM SMOKE GREEN**, the figures above, zero warnings now
enforced by a scan of the publish output rather than inferred from its exit code. The claim is not
vacuous: `obj/Release/net10.0/linked/Link.semaphore` is timestamped 16:14:57, inside a run whose
first act was deleting `obj/`, so the trimmer did run.
