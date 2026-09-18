# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S70 IS DONE (four sittings, 2026-09-18, branch `phase9-demo`).** Slice file moved to
`docs/plan/slices/done/`; per-sitting detail in `docs/plan/slices/notes/S70-sittings.md`.

**Green at this commit.** Ratchet GREEN, 6343 passing, 0 failing, 0 skipped, baseline 6235
unchanged. `tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-clean-publish`: WASM SMOKE GREEN from a
cleared `obj/`, 22 files, 7,278,441 bytes on disk, 2,076,631 gzip, 44 integrity endpoints, zero
warnings. All three probes in `tools/probes/wasm-smoke-*` fire and re-run in seconds.

**What sitting 4 closed.** The browser leg, run by the orchestrator with Playwright over Chromium
(evidence in `.claude/driver/s70-browser-evidence.json`): HARNESS GREEN, all six checks, **warm
baseline 418.3 ms** from `new Worker(...)` to first answer. And the clean publish, via a new
`-OutDir` that publishes away from the directory PID 37516 still holds open - no process was killed.
Blind pass over the unreviewed tooling: 5 findings, 5 reproduced, 5 fixed, all of them checks that
passed without checking. Independent verifier run over the commit-ready tree.

**Next: S71 (`docs/plan/slices/S71-vue-page-v1.md`), the demo's UI half.** Two things S70 learned
that it must design around: `worker.terminate()` is the PRIMARY control, not an error path, because
a pathological pattern can wedge `FuzzyRegex` *construction*, where `MatchTimeout` does not apply;
and rendering a result with hundreds of thousands of matches freezes the main thread, which the
worker does nothing about.

**Do not re-measure the compile blow-up.** `(((a{100}){100}){100}){100}` never returns and took the
machine to 0 GB free on 2026-09-18. Recorded in `DemoEngine.MatchTimeout`'s remarks, sliced as S56b
on `main`.

**Open for the owner.** PID 37516 (`python -m http.server 8080`) is still running and no longer
needed by anything - the browser leg is done and the smoke script no longer needs its directory.
**Carried from main:** benchmark baseline needs retaking on a quiet machine (~32 min);
`.claude/skills/benchmark/SKILL.md` documents a stale run command; `_regex.c:22091`'s comment on
`text_length` is wrong. `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop. The orchestrator merges `phase9-demo`.
