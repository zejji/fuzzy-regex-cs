# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S70 IS A CHECKPOINT, NOT DONE (sitting 3, 2026-09-18, branch `phase9-demo`).** The slice file
stays in `docs/plan/slices/`. Full detail: `docs/plan/slices/notes/S70-sittings.md`.

**Green at this commit.** Ratchet GREEN, 6343 passing, 0 failing, 0 skipped, baseline 6235
unchanged. `tools/run-wasm-smoke.ps1 -SkipClean`: WASM SMOKE GREEN, 22 files, 7,278,441 bytes on
disk, 2,076,630 gzip, 44 integrity endpoints.

**What sitting 3 added.** The smoke script's missing-artefact branch is no longer reasoned: a new
`-SkipPublish` switch makes it reachable, `tools/probes/wasm-smoke-missing-artefact.ps1` fires it
(exit 1, names the file, no GREEN, GREEN again after the restore), and
`tools/probes/wasm-smoke-branch-controls.ps1` shows that probe would have been wrong twice over
without its own controls. One blind pass over that delta: 7 findings, 6 fixed, 1 not sustained.

**Two things block the close, both needing the owner, neither being code:**

1. **The browser leg, unrun for a third sitting.** The Playwright MCP tool is in the session but
   not permission-granted (`browser_navigate` returns "you haven't granted it yet"). Everything
   else is ready: the server on 127.0.0.1:8080 already serves the freshly published web root
   (`harness.html` returns 200), so the grant is the only missing piece. Read `window.__harness`
   and `window.__bootToFirstAnswerMs`.
2. **The clean publish, for the zero-trim-warning claim.** PID 37516
   (`python.exe -m http.server 8080`, a stray from sitting 2) has the published `wwwroot` as its
   working directory and holds it open, so the script's clean step cannot delete it. **Kill it only
   AFTER the browser leg has used it** - it is the server that leg needs.

**Then, to close:** one blind pass over the browser-leg delta, the independent verifier (amendment
16(d)) over the commit-ready tree, tick the last two "Done when" boxes, move the slice file.

**Do not re-measure the compile blow-up.** `Run` has no clock over `FuzzyRegex` construction, so
`(((a{100}){100}){100}){100}` never returns; recorded in `DemoEngine.MatchTimeout`'s remarks and
sliced as S56b on `main`. It took the machine to 0 GB free on 2026-09-18.

**Carried from main:** benchmark baseline needs retaking on a quiet machine (~32 min);
`.claude/skills/benchmark/SKILL.md` documents a stale run command; `_regex.c:22091`'s comment on
`text_length` is wrong. **Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main`
needs a push; `stash@{0}` (S54 sitting 1's rescue stash) is safe to drop. The orchestrator merges
`phase9-demo`.
