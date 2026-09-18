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

## Sitting 3 (2026-09-18) - the plan to finish in one sitting

This is the third sitting, so the port-slice rule applies: write down how the rest gets done in
one pass, then do that. The five open items below are all *evidence* items - no engine design is
outstanding - so the route is one ordered run, not another round of judgement:

1. `tools/run-wasm-smoke.ps1` with no `-SkipClean`, once, blocking. Restates the zero-trim-warning
   claim against the committed code and re-measures the size baseline. Its publish output is also
   what the browser leg then serves, so this has to be first.
2. The browser leg: serve the published `wwwroot` on `127.0.0.1:8080`, drive `harness.html` with
   the session's Playwright tooling, read `window.__harness` and `bootToFirstAnswerMs`. This is
   the item the previous sitting could not run at all.
3. The missing-artefact branch, made testable rather than reasoned: the script publishes before it
   checks, so a deleted file is restored before the check sees it. Add a `-SkipPublish` switch,
   delete one published `.wasm`, watch the branch throw.
4. Ratchet, then one blind pass over the delta this sitting adds (the switch is new tooling), then
   the independent verifier over the commit-ready tree.
5. Close: tick the boxes, move the slice file, closing notes, `STATE.md`, `DECISIONS.md`, commit.

What made this three sittings rather than one was not the work but the environment: sitting 1 was
interrupted with everything uncommitted, and sitting 2 could not run a browser. The lesson worth
carrying is narrower than "be faster" - **a slice whose done-criteria need a tool the session may
not have granted should check for that tool in its first five minutes**, because sitting 2 did all
the engine work before discovering it could not finish the slice.

### What happened

The plan above survived contact except for its first two items, both for the same reason as last
time: **the environment, not the work.** Item 3 landed in full.

- **The missing-artefact branch fires, and is now proven to.** `-SkipPublish` (new switch) makes it
  reachable; `tools/probes/wasm-smoke-missing-artefact.ps1` exercises it. Verdict, 2026-09-18:
  `exit 1, reported dotnet.native.030iq1ikbj.wasm missing, printed no GREEN; the publish checks
  GREEN again after the restore.` Deleting ONE file was reported as "2 file(s)" until the count
  was made distinct - the manifest names each asset by a fingerprinted route and a plain one.
- **Both controls on that probe behave** (`tools/probes/wasm-smoke-branch-controls.ps1`), which is
  what makes the line above evidence rather than an assertion. See the Review section.
- **Ratchet GREEN**: 6343 passing, 0 failing, 0 skipped, baseline 6235 unchanged. (The counts moved
  from sitting 2's 6305/6197 because `phase8-docs` merged in, not because this sitting added tests.)
- **Smoke GREEN, but incrementally** (`-SkipClean`): 22 files, 7,278,441 bytes on disk, 2,076,630
  gzip, 44 integrity endpoints recomputed. Identical to sitting 2's baseline bar 7 bytes of gzip.
- **The clean publish did NOT run**, so the zero-trim-warning claim is still the one sitting 2 left.
  A stray `python -m http.server 8080` (PID 37516) from sitting 2's review still has the published
  `wwwroot` as its working directory and holds it open, so the script's clean step fails with
  `The process cannot access the file '...\publish\wwwroot'`. Publishing INTO the directory works -
  only deleting it does not - which is why the incremental run above succeeded. The owner has been
  asked to kill it; this session does not kill processes.
- **The browser leg did NOT run, for the third sitting.** The Playwright MCP tool is present in the
  session but not permission-granted: `browser_navigate` returns "Claude requested permissions to
  use mcp__plugin_playwright_playwright__browser_navigate, but you haven't granted it yet."
  Everything else is ready for it: the stray server on 8080 is in fact serving the freshly published
  web root (`Invoke-WebRequest http://127.0.0.1:8080/harness.html` returns **200**), so once the
  grant lands the harness can be driven without starting a server at all - and the kill above must
  therefore wait until AFTER the browser leg, not before it.

The sitting-2 lesson repeated itself exactly, so it is worth stating as a rule rather than a
regret: **check for the tools a slice's done-criteria require before doing the work that depends on
them.** Two sittings have now ended with the engine half finished and the browser half untried.

### Review (sitting 3)

One blind pass, Opus, dispatched blind over this sitting's delta only (`-SkipPublish` and the new
probe) and waited for in-turn. It carried the bounded-probe limits the slice requires.
**7 findings raised, 6 reproduced and fixed, 1 not sustained.**

Fixed, and all six were the same kind of defect - a check that could pass without checking:

- the probe tested "non-zero exit AND the filename appears in the output", but the integrity-
  MISMATCH branch also exits non-zero and also names the file. **Control A stages exactly that** and
  confirms the old test could not tell them apart: corrupting one byte gives `exit 1, names the file
  True, missing wording False, mismatch wording True`. The probe now keys on the branch's own
  wording;
- the probe never checked the publish was GREEN *before* it deleted anything, so it would have
  reported the branch firing against a publish that was already broken - its perturbation proving
  nothing. It now runs a control first and refuses. **Control B stages that too**: with a file
  already deleted, the probe exits 1, refuses, and does not claim a firing;
- the restore was announced, never verified; a third run after the restore now has to be GREEN;
- the backup lived at a fixed `%TEMP%\<name>`, which two concurrent worktrees would collide on -
  now a per-run GUID directory;
- an interrupted run left the publish short a file and the next run silently picked a different
  victim (subsumed by the control run above);
- `-SkipPublish` printed a verdict indistinguishable from a real run's, so no log could tell a
  publish-and-check from a check over whatever was on disk. The banner now says which it was, and
  the `.DESCRIPTION` says which claim the switch drops.

Not sustained: that the missing-artefact count of "2 file(s)" for one deleted file was the probe's
problem. It was the smoke script's, one line away, and is fixed there by counting distinct files.

Both controls are committed as `tools/probes/wasm-smoke-branch-controls.ps1` rather than described,
because S18's and S22's controls are already unreproducible from their prose. Re-running it is
three seconds and needs only an existing publish.

**The delta produced by those fixes has not itself had a blind pass** - it is tooling the reviewer
never saw. That pass belongs with the browser-leg work, before the closing commit, not to this
checkpoint.

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
2. ~~**The smoke script's new missing-artefact branch has not been seen to fire.**~~ **DONE in
   sitting 3**: `-SkipPublish` made it reachable and two probes now hold it down. Not open.
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
   **Still open after sitting 3, same cause, now identified precisely**: PID 37516,
   `C:\Python314\python.exe -m http.server 8080 --bind 127.0.0.1`, whose working directory IS the
   published `wwwroot`. Kill it *after* the browser leg has used it, then run the clean publish.

## Sitting 4 (2026-09-18) - the close

Both blockers cleared without this session opening a browser or killing anything.

### The browser leg

Run by the orchestrator, not by this session: **Playwright MCP over Chromium, orchestrator,
2026-09-18 15:56**, recorded in `.claude/driver/s70-browser-evidence.json`. Three sittings had each
spent their whole turn being denied the browser tools and exited with no commit, so the leg was
taken out of the slice session entirely.

What it served matters as much as what it found: `harness.html` came from the stray
`python -m http.server 8080` (PID 37516) serving the demo worktree's published `wwwroot` at commit
`b40dadd`, and the **served file's md5 was compared against the file on disk** - so the harness that
ran is the harness that is committed, not an older publish.

Verdict **HARNESS GREEN**, `window.__harness.ok === true`, all six checks passing:

- round trip returns the engine's own answer: `index=0 length=6 counts=(1,1,1)`, which upstream also
  gives - the page cannot compute per-error-type counts, so this is not an echo;
- each reply carries the `requestId` it was asked with (`#1 -> 3 matches, #2 -> 2 matches`);
- a parse error crosses as JSON, not as a rejection: `{"error":"missing )"}`;
- **31 animation frames in the 500 ms the runaway pattern ran** - the page kept painting;
- `terminate()` killed the runaway: no answer after 3.5 s, past the 2 s an unkilled worker would
  have answered on;
- a respawned worker answers correctly: `[[1,1],[4,2],[8,3]]`, matching upstream.

Only console error: a 404 for `/favicon.ico`, no favicon being shipped.

**The warm-time baseline is 418.3 ms** from `new Worker(...)` to the first answer (Chromium,
this machine, warm). That is the demo's own number. It is not comparable with S53's AOT figures for
the reason the slice file gives, and it is a *warm* figure - the runtime's files were already in the
browser cache, so it is a floor for a first visit, not a prediction of one.

### The clean publish, and the `-OutDir` that made it possible

The zero-trim-warning claim needs a publish from a cleared `obj/`, and for two sittings the publish
directory was held open by PID 37516 - whose job was to serve the browser leg. The owner's rule is
that this session does not kill processes, so the script grew a **`-OutDir`** instead: publish
somewhere else, clear `obj/` and that directory, leave `bin/` alone. `obj/` is where the linker's
state lives, so clearing it is what re-emits the warnings; `bin/` was only ever cleared because it
was where the publish landed.

`tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-clean-publish`: **WASM SMOKE GREEN**, 22 files,
7,278,441 bytes on disk, 2,076,631 gzip, 44 integrity endpoints recomputed. Zero warnings - the only
line in the log matching `warning` is the script's own banner about re-emitting them, and
`Directory.Build.props` sets `TreatWarningsAsErrors`, so one would have failed the publish.

**That claim would be vacuous if the trimmer had not run, so that was checked rather than assumed**:
`obj/Release/net10.0/linked/Link.semaphore` is timestamped 16:00:47, inside this run, and `obj/` was
deleted at its start. The size figures are identical to sitting 3's incremental run bar one byte of
gzip (2,076,631 against 2,076,630), which is the evidence that the two runs published the same app.

`-OutDir` refuses a directory containing the repository, because the clean step deletes what it is
given and `-OutDir .` would otherwise be a working tree deleted by a smoke test.

### Review (sitting 4)

One blind pass, Opus, over the tooling the sitting-3 reviewer never saw - its own fixes, committed
in `b40dadd` - plus this sitting's `-OutDir`. Dispatched blind and waited for in-turn, carrying the
bounded-probe limits. **5 findings raised, 5 reproduced, 5 fixed.** The reproduction gate did not
kill any of them this time, which is unusual and worth saying plainly: every one was a check that
passed without checking, and every one was demonstrable in seconds against a copy of the publish.

- **`-OutDir` could delete the repository's own directories.** The guard refused only a directory
  that IS or CONTAINS the repo root, so `-OutDir tools` - the directory the script itself lives in -
  sailed through to `Remove-Item -Recurse -Force`. Reproduced as logic, not by deleting anything:
  `demo blocks=False`, `tools blocks=False`. Now two gates, the second asking git whether the target
  holds tracked files, because tracked is the definition of "work" here and `.scratch/` and anywhere
  outside the repo are the disposable cases. Verified both ways: `-OutDir src` is refused with
  `holds 44 git-tracked file(s)` and `src/` is still there; a path under `%TEMP%` is allowed through
  to the web-root check. That second test is not incidental - the first version of the fix asked git
  about every path, and `git ls-files` on a path outside the repository exits 128, so the safest
  possible `-OutDir` would have been the one it refused.
- **The compressed variants were never checked for existence**, only excluded from hashing, because
  the `.br|.gz` skip sat above the existence check rather than below it. 41 of 42 deleted still gave
  `WASM SMOKE GREEN`, with the printed gzip figure collapsing from 2,076,631 bytes to 3,796 and
  nothing objecting. Those files are what a visitor downloads. The two lines are now the other way
  round.
- **Nothing looked from disk back to the manifest**, so a file nobody published was counted into the
  size baseline: one duplicated `.wasm` gave `23 files, 8762377 bytes (8.36 MB)` and GREEN, against
  the true 22 and 6.94 MB. It is not hypothetical - an asset's fingerprint changes every build, so
  under `-SkipClean` or a re-used `-OutDir` stale files accumulate rather than being overwritten, and
  the inflated figure is the one that would have gone into these notes as the demo's baseline.
- **The zero-warning claim was inferred, not made.** The only assertion about the publish was its
  exit code, and `TreatWarningsAsErrors` promotes compiler warnings, not every category MSBuild can
  emit. The script now captures the publish output and throws on any `warning <CODE>` line, so the
  claim in its own `.DESCRIPTION` is one the code checks. (This is also the sitting's own evidence
  made honest: the first clean run's zero-warning claim came from me grepping the log by hand.)
- **`(... | Measure-Object -Sum).Sum` throws on an empty pipeline** under `Set-StrictMode -Version
  Latest` - `The property 'Sum' cannot be found on this object` - so a publish with no `.gz` died
  with an internal PowerShell error after every real check had passed. Both totals now handle it.

The new checks get a committed probe rather than a paragraph, for the reason the directory's other
two exist: **`tools/probes/wasm-smoke-artefact-checks.ps1`**, which copies the publish to a per-run
temp directory, runs a control over it, deletes 41 of 42 compressed variants, restores them, adds an
orphan, removes it, and runs a closing control. `BOTH ARTEFACT CHECKS FIRED: 41 compressed variants
deleted gives exit 1 and the missing wording; one orphan file gives exit 1 and the orphan wording;
the copy checks GREEN again once both are undone.` It needs only an existing publish and takes
seconds. The two older probes still pass against the changed script, controls included.

**Every figure above was re-taken from the code being committed**, after the last fix: a final
`-OutDir .scratch/wasm-clean-publish` run gives WASM SMOKE GREEN, 22 files, 7,278,441 bytes,
2,076,631 gzip, 44 integrity endpoints, zero orphans, and `linked/Link.semaphore` timestamped
16:14:57 inside that run.

### Independent verifier (amendment 16(d))

A fresh Opus subagent, briefed with nothing but the commit-ready tree, re-ran every number these
notes and the closing notes quote. **Ten claims, nine CONFIRMED, one DIFFERENT** - the three probes'
verdicts, all four clean-publish figures, both `-OutDir` guard behaviours, the ratchet's totals
(6343 tests, 0 skipped, baseline 6235), the `Link.semaphore` timestamp, and every figure the closing
notes quote from the browser-evidence file, which it checked as quoted rather than re-running,
having no browser and being told not to seek one. It left both publish directories GREEN and
`git status` byte-identical to how it found it.

**The DIFFERENT is worth keeping, because it is a fact about this suite and not about this slice.**
The verifier's brief made `DOTNET_GCHeapHardLimit=0x40000000` mandatory - the slice's own
bounded-probe rule, written for the demo engine - and under a 1 GB heap the ratchet is RED at 6341
passing and 2 failing:

- `FuzzyRecursionTests.The_stack_bound_is_still_what_catches_a_blowup_the_guard_cannot_see`
- `InheritedIssueTests.A_long_repeated_capture_group_costs_the_backtracking_stack_a_block_per_repetition`

Both die with `OutOfMemoryException` in `ByteStack.Grow` (`ByteStack.cs:306`), which is exactly what
they are for: they deliberately grow the backtracking stack until something stops them, and under
the cap the thing that stops them is the cap. Re-run without it, the ratchet is GREEN at 6343/0/0,
which is the figure these notes record. **So the bounded-probe limits belong on demo-engine probes,
not on the test suite**, and a future session that applies them to `check-ratchet.ps1` will get a
RED that means nothing.

### One thing this sitting got wrong

The first ratchet run was pointed at `C:\...\fuzzy-regex-cs\tools\check-ratchet.ps1` - the main
checkout's copy, not the worktree's. It built and ran **main's** test assembly while sitting in the
worktree, passed 6343 tests, and then failed with `Ratchet: RED - no test report was produced`
because it looked for the report under the main checkout. Worth knowing: a worktree session that
gets the path wrong does not get an error saying so, it gets a green-looking test run about the
wrong tree. The real run, `.claude/worktrees/demo/tools/check-ratchet.ps1`, is GREEN: 6343 passing,
0 failing, 0 skipped, baseline 6235 unchanged.
