# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S56b closed on `main` 2026-09-18.** The compile path now has a node budget:
`NodeCompiler.CreateNode` refuses once `PatternObject.MaxNodes` is reached, throwing
`FuzzyRegexParseException`, so `((a{150}){150}){150}` is refused after about 250 MB and 0.7 s
instead of exhausting the machine. The knob is `maxCompiledNodes` on the widest public
constructor, default `FuzzyRegex.DefaultMaxCompiledNodes = 1_000_000`; the budget counts nodes
CREATED, not kept (the optimiser prunes about half a counted repeat's graph afterwards). Full
account, including the overload-capture trap that adding the parameter caused and the post-1.0
counted-repeat candidate: `docs/plan/slices/done/S56b-compile-budget-for-unrolled-repeats.md` and
`docs/plan/slices/notes/S56b-sittings.md`.

Ratchet: GREEN, 6351/6351, baseline updated to 6243 distinct ids (8 added, 0 removed). Oracle
GREEN at seeds 7, 4242 and 20260918, `diverge 0` in each. AOT smoke GREEN (win-x64).

**Next: S56**, `docs/plan/slices/S56-mutation-testing-engine-survivors.md`, still blocked on the
engine mutation queue running detached in the `stryker` worktree (branch `stryker-queue`,
59 `engine-rand-*` chunks, roughly 55 machine-hours at 2 runners, started 2026-09-18). Check for
finished reports before starting its sitting; do not start another Stryker run on `main`.

**Untracked file left behind on purpose:** `.github/workflows/pages.yml` is not part of S56b. It
was written at 16:46 on 2026-09-18 by a concurrent Phase 9 sitting (S71 content) and is NOT
committed here and NOT deleted. Leave it for the sitting that owns it; this slice committed its
own paths explicitly.

**Parallel streams, untouched this session:** Phase 8 (`docs` worktree, branch `phase8-docs`) is at
S68; Phase 9 (`demo` worktree, branch `phase9-demo`) has S70 parked as a checkpoint - see
`docs/plan/slices/notes/S70-sittings.md`, needs a Playwright permission grant.

**Open for the owner (carried, unverified this session):** `slice-log.jsonl` marks S26 `failed`;
`origin/main` needs a push; the benchmark baseline needs retaking on a quiet machine; the GitHub
repo `zejji/fuzzy-regex-cs` is private or unpushed, which blocks S67's registry submissions.
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
