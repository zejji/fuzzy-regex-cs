# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S54 IS IN FLIGHT, sitting 2 (2026-09-16). This commit is a CHECKPOINT, not the close.** The slice
file is still in `docs/plan/slices/`. Ratchet GREEN 6261 / 6261 / 0, baseline moved 6118 -> 6153.
Per-sitting notes: `docs/plan/slices/notes/S54-sittings.md`.

**Landed here:** the benchmark suite (32 benchmarks over a shared corpus, in `WorkloadBenchmarks`,
`ReferenceBenchmarks` and a `sizing` mode), `tools/compare-benchmarks.ps1`, the upstream probe
`tools/probes/upstream-optimiser-traps.py`, and `Gaps/Engine/OptimiserTrapsTests.cs` - 35 cases,
every upstream-expressible expectation carrying its `regex` 2026.9.10 provenance. S51's
`MatchingBenchmarks` is deleted into the suite.

**WHAT IS LEFT, in order:** (1) run `pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline` with
nothing else on the machine, about 30 minutes, and commit `bench/baselines/<machine-id>/net10.0.json`
with the machine description; (2) re-run it without `-UpdateBaseline` to prove the script is green
against itself; (3) the ratio table for `Regex` versus this port into the closing notes; (4) blind
review, then the independent verifier; (5) close the slice. **No .NET 11 runtime is installed**
(`dotnet --list-runtimes`: 10.0.11 is the newest), so the baselines are .NET 10 only, which the
slice's own scope allows - say so in the closing notes.

**The one trap to know:** BenchmarkDotNet locates its project by searching down from the working
directory's nearest solution file, and every git worktree under `.claude/worktrees/` carries a copy,
so a hand-run from the repository root executes ZERO benchmarks. `bench/FuzzyRegex.Benchmarks.slnx`
plus the working directory `tools/compare-benchmarks.ps1` sets is the fix. DECISIONS 2026-09-16.

**Carried, newest first:** `Replace` takes upstream's `\1` template syntax and `ReplaceFormat` takes
.NET's `$1` - the trap tests caught a benchmark measuring the wrong work. Plus a PRE-EXISTING
`Options` disagreement (`regex.compile('(?V0)a').flags` is `0x6020` against our `0x2020`; the
`FullCase` bit predates S53b). The C comment at `_regex.c:22091` is wrong about `text_length`.
Plus S53's list: `check-ratchet.ps1` can hang on a wedged MSBuild node; run `dotnet build` on the
SOLUTION before committing a slice that adds a project. Plus S52c/S52d's list: two unjudged oracle
rows; `record-oracle.py --self-check` RED on one pre-existing guard; the `_regex.c` citation
reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`; control sites
S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `oracle.yml`'s weekly sweep verdict rule.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
