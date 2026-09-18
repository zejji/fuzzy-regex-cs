# S70 sittings

Per-sitting notes for `S70-worker-hosted-engine.md`. The slice file stays its spec; this is where
each sitting records what it did.

## Sitting 1 (before 2026-09-18, interrupted)

Left the worktree dirty and uncommitted: the whole `demo/FuzzyRegex.Demo.Wasm` project, the
harness, `tools/run-wasm-smoke.ps1`, `tools/probes/demo-json-contract-expectations.py` and
`tests/FuzzyRegex.Tests/Gaps/Demo/DemoEngineContractTests.cs`. Nothing was committed, so nothing
of sitting 1 survives except the files themselves. It had written the contract tests but not
finished the engine: two of them were red when sitting 2 picked the work up, which is the expected
shape of test-first work interrupted mid-slice.

## Sitting 2 (2026-09-18) - CHECKPOINT, not done

### What was red on arrival, and why

- `One_match_cannot_carry_unbounded_capture_spans` - `Search` capped the number of matches and
  nothing else, so a single match could carry arbitrarily many capture spans. A 20-deep nesting of
  `(\w)` over 5,000 characters produced an answer with `truncated: false`; the test's own notes
  record 128 MB of JSON for a legal 100,000-character subject.
- `The_whole_walk_shares_one_time_budget_rather_than_one_per_match` - took 32 s and returned
  success. `EnumerateMatches`'s timeout is documented as a per-STEP budget
  (`src/FuzzyRegex/FuzzyRegex.cs:947-951`), so a walk of a thousand matches was getting a thousand
  budgets.

### What landed

- **`MaxSpans = 50_000`**, a budget over every group and capture in the whole answer, spent by
  `Describe` and refilled nowhere. A clipped match is always the last one, and any clip sets
  `truncated: true`.
- **A stopwatch `Search` polls itself**, rather than a `CancellationTokenSource`. In the browser
  the walk occupies the worker's only thread until it returns, so a timer callback could not run
  until the deadline no longer mattered. The two timeout tests now also assert the call *returned*,
  not merely that it said "timed out": asserting on the message alone is what let a 32-second call
  pass.
- **`MaxFlagsLength = 200` and `MaxQuotedTokenLength = 40`** (from the blind review). `flags` was
  the one stranger-controlled input with no cap, and the unknown-flag error quoted the token back
  in full; the reviewer got a 12,000,064-character answer out of a 2,000,000-character flag list,
  because JSON escaping multiplied it six-fold.
- **A failed boot is now an answer.** `worker.js` imports `_framework/dotnet.js` dynamically
  inside its try rather than with a static top-level import, because a static import is evaluated
  before the module registers its `message` listener - so a missing runtime file aborted the
  worker before it could report anything, and every request vanished. `failBoot` answers the queue
  and posts `{ready: false}`, guarded so it cannot fire twice, nor demote a healthy worker when a
  stray error arrives after boot.
- **The harness always reaches a verdict.** `ask()` has a 20 s timeout and `spawn()` listens for
  worker `error` and `{ready:false}`; before this, renaming `dotnet.js` left `window.__harness`
  unassigned and the page reading "running..." forever - a broken demo that looked like a loading
  one, which is the single confusion this harness exists to prevent.
- **`tools/run-wasm-smoke.ps1` fails on a missing artefact.** Manifest entries whose file was
  absent were skipped, so every `.wasm` could be deleted and the script still printed GREEN.
- **`.serena/` added to `.gitignore`** - the Serena MCP server writes a symbol cache into the
  working tree whenever a session connects, and an unignored root entry fails a slice.

### Evidence, all re-run against the committed tree

- `pwsh -File tools/check-ratchet.ps1` - **GREEN, 6305 passing, 0 failing, 0 skipped**; baseline
  updated to 6197 distinct ids.
- `dotnet run --project tests/FuzzyRegex.Tests -c Release -- --treenode-filter "/*/*/DemoEngineContractTests/*"`
  - **36 passing**, 2.4 s. The same filter took 32 s before the walk had a clock.
- `dotnet build FuzzyRegex.slnx --configuration Release` - **0 warnings, 0 errors**, whole
  solution, per S53's review finding 1.
- `pwsh -File tools/run-wasm-smoke.ps1` - **WASM SMOKE GREEN**.
- `python tools/probes/demo-json-contract-expectations.py` - reproduces every expectation quoted in
  the test file's provenance block against `regex 2026.9.10`, including
  `(?:foobar){i<=1,d<=1,s<=1}` on `xfoobat` giving counts `(1, 1, 1)` and `\p{Deseret}+` giving
  UTF-16 index 2 length 4 where upstream counts codepoints 2 and 2.

### The demo's size baseline (its own, not comparable with S53's 6.65 MB)

S53's figure is a Native AOT win-x64 executable; this is an IL bundle interpreted in a browser -
different compiler, different unit. Measured 2026-09-18 from the committed tree:

| | |
|---|---|
| published | 22 files, 7,278,441 bytes (6.94 MB) on disk |
| gzip variants | 2,076,623 bytes (1.98 MB) - the wire figure where Pages negotiates gzip; Pages does not serve Brotli |
| largest three | `FuzzyRegex.*.wasm` 3,622,169 - `dotnet.native.*.wasm` 1,483,936 - `System.Private.CoreLib.*.wasm` 1,238,293 |
| integrity | 44 uncompressed endpoints recomputed and matched |

Worth noticing before S71 tunes anything: **the port itself is the largest single file at 3.62 MB,
half the bundle and more than twice the runtime.** WebAssembly AOT is deliberately still off (a
size-against-speed knob with no measurement behind it).

### Review

Two blind passes, both Opus, both dispatched blind and waited for in-turn.

**Pass 1, over the whole slice: 16 findings raised, 9 reproduced and fixed** (the flag-list cap and
its quoting bound; the vacuous cap tests; the timeout tests that never asserted elapsed time; the
subject-cap ordering test that used a valid pattern so never exercised the ordering; the worker's
silent-drop boot failure and its unanswered malformed message; the harness's unassignable verdict;
the smoke script's skipped missing artefacts). **Two were recorded rather than fixed** - see the
open item below. **Five were not sustained**: the `requestId` tautology and the runaway-worker
identity findings describe a harness that already proves liveness by asking a question first, and
the remainder were about the rendering check, which is a measurement and is labelled as one.

**Pass 2, over the fix delta only** (new constants, `TooLong`, the reworked `worker.js` and
`harness.html`, the smoke script change) - required because the fixes added API and changed tooling
that pass 1 never saw. **6 findings, 4 reproduced and fixed**: the missing already-booted guard
(a stray post-boot error demoted a healthy worker into the failure responder), the static import
that ran before any listener existed, the double `{ready:false}`, and `MaxQuotedTokenLength`
missing from the literals test. It also caught that **the runaway check was still vacuous after my
first fix**: it read `runawayAnswered` 500 ms in, when the engine's own 2 s budget meant no answer
could have arrived even from a worker nobody had killed - it passed with the `terminate()` line
deleted. The harness now waits 3.5 s, past the budget an unkilled worker would have answered on.
The two it left unfixed are recorded as accepted: the first timeout test does not itself pin the
new walk deadline (the second one does), and `MaxQuotedTokenLength` has slack in its size bound.

Pass 1's subagent spawned an unbounded probe process that reached 21 GB and took the machine to
0 GB free; the owner authorised killing it. **Every reviewer brief from here carries
`DOTNET_GCHeapHardLimit=0x40000000`, a wall-clock timeout, a subject cap and a ban on probe
projects** - pass 2 carried them and behaved.

## What is left, for the next sitting

1. **The browser leg - the reason this is a checkpoint.** `harness.html` has never been run in a
   browser. The Playwright MCP tool was not permission-granted in this session, so the round trip,
   the `terminate()` proof and the responsiveness counter are all still unexecuted, and the
   **warm-time baseline (`new Worker(...)` to first answer) has never been measured** - the harness
   records it on `window.__bootToFirstAnswerMs`, but nothing has read it. To run it:
   `python -m http.server 8080` from
   `demo/FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot`, then open
   `http://127.0.0.1:8080/harness.html` and read `window.__harness`. Until that is green, the
   slice's second and third "Done when" boxes cannot be ticked honestly: everything above is the
   engine half tested under the JIT, and the interop attribute itself is the one thing only a
   browser exercises.
2. **The smoke script's new missing-artefact branch has not been seen to fire.** It could not be
   tested by deleting a published file, because the script publishes before it checks and the
   publish restores what you deleted. Reasoned, not executed.
3. **`Run` has no clock over `FuzzyRegex` construction, and this is not fixable here.** A pattern
   of the shape `(((a{100}){100}){100}){100}` is well inside `MaxPatternLength` and spends its time
   in the constructor, where `MatchTimeout` does not apply, so `Run` does not return. Recorded in
   `DemoEngine.MatchTimeout`'s remarks. **Do not re-measure it** - that is what crashed the machine
   on 2026-09-18; the blow-up is investigated and sliced as S56b on `main`
   (`docs/plan/2026-09-18-repeat-unrolling-investigation.md`). It is also the sharpest possible
   argument for the ROADMAP's design: `worker.terminate()` is the only thing that recovers a wedged
   construction, so **S71 must treat terminate as its primary control, not as an error path.**
4. **The independent verifier (spec amendment 16 limb (d)) has not run.** Deliberate: it belongs to
   the commit that closes the slice, and this one does not. It should re-run the probe, the smoke
   script and the harness from the committed files.
5. A publish-directory file was locked by a stray `python -m http.server` from the review, so the
   final smoke run used `-SkipClean`. The clean, trim-warning-bearing publish earlier in the sitting
   was green, but it predates the `DemoEngine` changes; **re-run `tools/run-wasm-smoke.ps1` without
   `-SkipClean` next sitting** to restate the zero-trim-warning claim against the committed code.
