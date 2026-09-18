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
