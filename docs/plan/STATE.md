# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S71 IS IN FLIGHT - CHECKPOINT after sitting 2 (2026-09-18, branch `phase9-demo`).** Slice file
stays in `docs/plan/slices/`; per-sitting detail in `docs/plan/slices/notes/S71-sittings.md`.

**Green at this commit.** Ratchet GREEN: 6365 passing, 0 failing, 0 skipped, 6257 distinct ids,
baseline 6257. 34 `node --test` tests GREEN via `tools/run-demo-js-tests.ps1`.

**The browser leg is DONE and it is green at both layouts, before and after the review fixes.**
`checks.html` ran for the first time ever: **CHECKS GREEN, 9 of 9, at
`http://localhost:8090/checks.html` and 9 of 9 at `http://localhost:8091/fuzzy-regex-cs/checks.html`**
(Chrome 153.0.0.0, Playwright MCP; load with a cache-busting query). The subpath
run settles the no-base-href decision in its favour - one artefact boots at both. Measurements:
`stopToNextAnswerOnScreenMs` 440.7/438.7, `respawnWithoutSpareMs` 249.5/242.7/271.5,
`respawnWithSpareMs` 193.8/119.3/144.6 - a respawn is ~250 ms, ~150 ms with the spare. Two real
bugs were found and fixed in `checks.html`: a fragment-only `src` assignment is a same-document
navigation so the run hung for ever, and one check counted `requestAnimationFrame` frames, which this
browser drives on a bare page (31 per idle 500 ms) but not on the demo page, which boots two
WebAssembly workers (1, then 0). It now counts timer ticks on both threads: 32 and 32.

**BLOCKER, and the next sitting cannot start without it: `npm` is not in the driver's Bash
allowlist.** `npm --version` is refused before it runs ("This command requires approval"). The owner
withdrew the no-build-step rule at 20:55 (spec amendment 27) and wants Vite + Vue 3 + TypeScript
strict + Tailwind, `npm ci`, no CDN. None of it was written blind - an uninstalled, un-type-checked,
untested Vite project is not progress. Grant `Bash(npm *)` (and `npx`) and it can start. Not the
network: `registry.npmjs.org` answers 200 from here. Versions were read from the registry today and
are listed in the notes for pinning; installed Node is v24.16.0. Settle `vue-tsc`'s TypeScript peer
range before pinning TS 7.0.2.

**Also not done, deliberately:** the Design bar (professional look). Restyling a page the toolchain
amendment replaces is wasted work; the UI/UX research, the sources and the two page-specific design
decisions are written up in the sitting-2 notes, ready to implement. `.scratch/s71-v1-before-1280.png`
and `-390.png` are the "before" screenshots.

**Open items, in the order they bite.** 1. A stray `.github/workflows/pages.yml` sits in the MAIN
checkout and must be deleted before `main` is touched, or the merge lands a second copy. 2. The
README demo link 404s until the owner pushes and sets Settings - Pages - Source = GitHub Actions.
3. `docs/STATUS.md:9` says "Parity against upstream commit `611be7e3eda...`", which is this repo's
HEAD, not an upstream commit - generator bug, pre-existing, and the SHA moves with every commit. 4. `DemoEngine.cs` calls capture lists "an mrab-regex
feature the built-in engine does not have"; .NET has `Group.Captures`, so the sentence is wrong.
5. The independent verifier (amendment 16 limb d) is still owed at the closing sitting.

**Carried from main:** benchmark baseline needs retaking on a quiet machine (~32 min);
`.claude/skills/benchmark/SKILL.md` documents a stale run command; `_regex.c:22091`'s comment on
`text_length` is wrong; `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop. **S56** stays blocked on the Stryker
engine queue in the `stryker` worktree; do not start another Stryker run on `main`.
