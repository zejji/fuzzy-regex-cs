# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S70 IS A CHECKPOINT, NOT DONE (sitting 2, 2026-09-18, branch `phase9-demo`).** Slice file stays
in `docs/plan/slices/`. Full detail: `docs/plan/slices/notes/S70-sittings.md`.

**Green at this commit.** Ratchet GREEN, 6305 passing, 0 failing, 0 skipped (baseline 6197 ids).
`dotnet build FuzzyRegex.slnx -c Release`: 0 warnings. `tools/run-wasm-smoke.ps1`: WASM SMOKE
GREEN, 22 files, 6.94 MB on disk, 1.98 MB gzip, 44 integrity endpoints. The port itself is the
largest file at 3.62 MB - half the bundle, twice the runtime.

**The one thing left is the browser leg.** `harness.html` has never been run in a browser: the
Playwright MCP tool was not permission-granted this session, so the round trip, the `terminate()`
proof and the responsiveness counter are unexecuted and the warm-time baseline is unmeasured. Run
`python -m http.server 8080` from `demo/FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot`,
open `harness.html`, read `window.__harness`. **Owner: this needs a Playwright permission grant.**

**Do not re-measure the compile blow-up.** `Run` has no clock over `FuzzyRegex` construction, so a
pattern like `(((a{100}){100}){100}){100}` - inside `MaxPatternLength` - never returns. It is
recorded in `DemoEngine.MatchTimeout`'s remarks and sliced as S56b on `main`. Probing it took the
machine to 0 GB free on 2026-09-18. Every subagent brief now carries
`DOTNET_GCHeapHardLimit=0x40000000`, a wall-clock timeout and a subject cap. **This is the argument
for S71 treating `worker.terminate()` as its primary control, not an error path.**

**Two blind passes ran** (16 findings, 9 fixed; then 6 over the fix delta, 4 fixed). The
independent verifier has NOT run - it belongs to the closing commit.

**Also left:** the smoke script's new missing-artefact branch is reasoned, not seen to fire; the
zero-trim-warning claim needs restating from a clean publish against the committed code.

**Carried from main:** benchmark baseline still needs retaking on a quiet machine
(`pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline`, ~32 min). `.claude/skills/benchmark/
SKILL.md` documents a stale run command. `_regex.c:22091`'s comment on `text_length` is wrong.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop. The orchestrator merges `phase9-demo`.
