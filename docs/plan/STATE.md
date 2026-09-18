# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase 8 (`docs` worktree, branch `phase8-docs`). S64 closed 2026-09-18.** README.md is now the
complete getting-started page (status, install, three worked samples pinned by
`tests/FuzzyRegex.Tests/Docs/ReadmeSamples.cs`, differences from Python `regex` and from
`System.Text.RegularExpressions`, thread safety, licensing, "where the docs are"). Every
repo-relative link in it is now an absolute `github.com/zejji/fuzzy-regex-cs/blob/main/...` URL,
because nuget.org does not rewrite relative links itself (DECISIONS.md, 2026-09-18). Symbol
packages ship (`IncludeSymbols`/`SymbolPackageFormat=snupkg` on `FuzzyRegex.csproj`).

**Next: S65**, `docs/plan/slices/S65-comparison-completeness-and-convention-tests.md` -
`COMPARISON.md` checked row by row against `DIVERGENCES.md`'s SHIPPED rows and the two convention
tests. Two things S65 (or S66) should pick up from S64's closing notes:
- `README.md`'s `<!-- demo-link -->` placeholder (in "A browser demo is planned before 1.0.") is
  S71's to replace, not S65's.
- Package metadata gaps found but left open (S64's csproj edit was scoped to the two symbol-package
  lines only): no `Authors`, no `PackageProjectUrl`; `PackageLicenseFile` used instead of
  `PackageLicenseExpression` (valid SPDX would be `Apache-2.0 AND CNRI-Python`, not changed since
  it's a choice, not a gap). S66 (pack/validate/dry-run) or the owner should settle these.

Ratchet: GREEN, 6272/6272, baseline 6164 (updated this session). `docs/STATUS.md` regenerated.

**Not carried from the pre-fork STATE.md**: this branch forked from `main` at the S55 Stryker
checkpoint. That content belongs to the mutation-testing worktree working Phase 6, not to Phase 8;
it will reconcile at merge time, which is expected under the parallel-worktree plan (ROADMAP.md,
2026-09-18 entry).
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
