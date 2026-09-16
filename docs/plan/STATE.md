# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S54 IS CLOSED (sitting 2, 2026-09-16).** Slice file in `docs/plan/slices/done/` with its closing
notes and three ticked boxes. Ratchet GREEN 6262 / 6262 / 0, baseline 6118 -> 6154. Per-sitting
detail, including what sitting 1's rescue stash held: `docs/plan/slices/notes/S54-sittings.md`.

**Phase 7 now has something to regress against:** 30 benchmarks in `bench/FuzzyRegex.Benchmarks`,
committed medians and allocations in `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/`,
`tools/compare-benchmarks.ps1` as the gate, and 36 permanent optimiser-trap pins in
`Gaps/Engine/OptimiserTrapsTests.cs`. Two blind passes raised 14 findings; all 14 reproduced and
were fixed. The verifier confirmed 9 of 11 claims and corrected 2 numbers.

**THE BASELINE NEEDS RE-TAKING ON A QUIET MACHINE, and that is the orchestrator's job** (the slice
scope says so). 13 of the 30 rows have a minimum far below their median, so the medians record this
machine's contention: the verifier got a RED from an unchanged binary, spread 0.57x-1.89x. One
command, `pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline`, 32 minutes, nothing else
running. Until then read a RED as "measure it again", not as "the change regressed". The baseline
names the affected rows in its own `contended` array and the script prints a CAUTION banner.

**NEXT: S55** (`docs/plan/slices/S55-mutation-testing-tooling-and-calibration.md`). Its file says
the orchestrator must install Stryker before launch.

**The trap to know:** BenchmarkDotNet finds its project by searching down from the working
directory's nearest solution file, and every git worktree under `.claude/worktrees/` carries a
copy, so a hand-run from the repository root executes ZERO benchmarks.
`bench/FuzzyRegex.Benchmarks.slnx` plus the working directory the script sets is the fix.

**Carried, newest first:** `.claude/skills/benchmark/SKILL.md` still documents the old run command,
which now fails without executing anything - editing it was outside this session's write
permissions. `Replace` takes upstream's `\1` template syntax, `ReplaceFormat` takes .NET's `$1`.
Plus a PRE-EXISTING `Options` disagreement (`regex.compile('(?V0)a').flags` is `0x6020` against our
`0x2020`). The C comment at `_regex.c:22091` is wrong about `text_length`. Plus S53's list:
`check-ratchet.ps1` can hang on a wedged MSBuild node; run `dotnet build` on the SOLUTION before
committing a slice that adds a project. Plus S52c/S52d's: two unjudged oracle rows;
`record-oracle.py --self-check` RED on one pre-existing guard; the `_regex.c` citation
reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`; control sites
S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `oracle.yml`'s weekly sweep verdict rule.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` is S54 sitting 1's rescue stash, now fully superseded and safe to drop.
