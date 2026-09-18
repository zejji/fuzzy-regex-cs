# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S71 IS IN FLIGHT - CHECKPOINT after sitting 1 (2026-09-18, branch `phase9-demo`).** Slice file
stays in `docs/plan/slices/`; per-sitting detail in `docs/plan/slices/notes/S71-sittings.md`.

**Green at this commit.** Ratchet GREEN: 6357 passing, 0 failing, 0 skipped, 6249 distinct ids,
baseline 6249 (up from 6235 - `DemoExamplesTests` and `DemoCapsTests` are new). 34 `node --test`
tests GREEN via `tools/run-demo-js-tests.ps1`. `tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-s71`:
WASM SMOKE GREEN, 31 files, 7,507,552 bytes on disk, 2,161,333 gzip, 62 integrity endpoints.

**What is left, and it is one thing: the browser leg.** This sitting had no browser tooling, so the
page has never been loaded. `docs/plan/slices/S71-vue-page-v1.md`, "Browser brief for the next
sitting", is the spec: publish to a fresh directory, serve on 8090 (NOT 8080 - PID 37516 is the
owner's), run `checks.html` at the root and again under a `/fuzzy-regex-cs/` subpath, read
`window.__checks.ok/checks/measurements`, and record `stopToNextAnswerOnScreenMs`,
`respawnWithoutSpareMs` and `respawnWithSpareMs` in the notes file. The subpath run is what settles
the no-base-href decision; red there means the fix belongs to this slice.

**Then:** the independent verifier (amendment 16 limb d), the closing notes' Review paragraph,
`git mv` the slice file to `done/`, and the final commit. Nothing is pushed by this session - the
owner pushes and enables Pages.

**Open items, in the order they bite.**
1. A stray `.github/workflows/pages.yml` sits in the MAIN checkout
   (`C:\Users\<user>\source\repos\fuzzy-regex-cs\.github\workflows\pages.yml`), written there
   by mistake; this session's directory restriction refused both `ls` and `rm` on it. Delete it
   before `main` is touched or the merge lands a second copy.
2. The README's demo link 404s until the owner pushes and sets Settings - Pages - Source = GitHub
   Actions (`demo/README.md` has the steps). Expected, not a defect.
3. `docs/STATUS.md` says "Parity against upstream commit aee2430", which is this repo's HEAD, not an
   upstream commit. Generator bug, pre-existing.
4. `DemoEngine.cs` calls capture lists "an mrab-regex feature the built-in engine does not have";
   .NET has `Group.Captures`, so the sentence is wrong.

**Do not re-measure the compile blow-up.** `(((a{100}){100}){100}){100}` never returns; S56b on
`main`. **Carried from main:** benchmark baseline needs retaking on a quiet machine (~32 min);
`.claude/skills/benchmark/SKILL.md` documents a stale run command; `_regex.c:22091`'s comment on
`text_length` is wrong; `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop.
