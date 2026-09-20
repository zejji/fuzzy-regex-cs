# S55 mutation triage - API layer, parser, Engine/Substitution.cs

Survivor triage for the three chunks S55 scopes: `*.cs` (API layer), `Parsing/*.cs`, and
`Engine/Substitution.cs`. Stryker counts `Timeout` as detected (same as `Killed`) - a timeout
under `additional-timeout 30000` means the mutant made a test genuinely hang, not that the test
was merely slow, since `tools/stryker-queue.json`'s config already gives it 30s of headroom above
normal plus the suite's load-sensitive classes sit out via `[SkipUnderStryker]`.

All final numbers below are from the `stryker-queue` worktree
(`.claude/worktrees/stryker/TestResults/stryker/<chunk>/reports/mutation-report.json`), Release
build, coverage-analysis off, 2 runners, `FUZZYREGEX_TEST_THREADS=4`. That worktree's
`stryker-config.json`, `tools/run-stryker.ps1`, `tools/stryker-queue.json` and
`tests/FuzzyRegex.Tests/TestThreadsLimit.cs` carry the settings that produced these numbers but
stay on branch `stryker-queue`, not copied into `main` - `tools/stryker-report-from-dump.py` is
the one file both share, already identical and committed here in 5bb7af2. Discarded as INVALID
(per-test coverage analysis mislabelled survivors as Timeout): `api-pertest-4runners`,
`api-failed-*`, and any score recorded before 2026-09-17 13:00.

## API layer (`*.cs`)

Report: `.claude/worktrees/stryker/TestResults/stryker/api/reports/mutation-report.json`.

| Status | Count |
|---|---|
| Killed | 237 |
| Ignored | 44 |
| CompileError | 20 |
| **Survived** | **0** |

237/237 killed, 0 survivors. Supersedes the sitting-3 baseline (192 killed, 45 Timeout, 0
Survived) - re-run under the corrected 2-runner/coverage-off settings, same 237-mutant scope,
no survivors either way.

## Parsing (`Parsing/*.cs`)

Run in two pieces: an exhaustive first pass recovered from a `dotnet-dump` snapshot after the
test host was killed mid-run (`tools/stryker-report-from-dump.py`, `replacement`/`coveredBy`
fields omitted from the recovered JSON), then its undecided mutants re-run to completion.

**`parsing-recovered`** (`.claude/worktrees/stryker/TestResults/stryker/parsing-recovered/reports/mutation-report.json`):
1629 Killed, 14 Timeout, 3 RuntimeError, 543 Pending (undecided when the dump was taken).

**`parsing-remaining`** (finished 2026-09-18 15:22, the 543 `Pending` mutants re-run):
527 Killed, 15 Timeout, 1 RuntimeError, 0 Survived.

Combined parser result: 2156 Killed, 29 Timeout, 4 RuntimeError, 0 Survived across the
2189-mutant scope. Mutation score 100% - no survivors.

**Timeout (29 total):** the orchestrator's brief for this sitting described these as
hand-checked in an earlier sitting as genuine hangs, but no record of that check survives in
the notes. Independently spot-checked instead: of the 15 in `parsing-remaining`, 12 are
`Logical mutation`s on `RegexFlags.IsSpecial` (`RegexFlags.cs:250-251`, whose own doc comment
says `EndOfSource` membership "is how `parse_sequence`'s loop terminates"), 2 are `Negate
expression`s on the `while` guards in `TrimPythonWhitespace` (`ParseFunctions.cs:3062, 3067`),
and 1 is a `Boolean mutation` in `Source.cs:199`; the 14 in `parsing-recovered` sit in similar
loop-bearing spots (`Info.cs:152-159`, `Nodes.cs:1279`, `ParseFunctions.cs:199-2103`). Consistent
with genuine hangs from a removed loop-termination condition, not merely slow tests padded by
`additional-timeout`.

**RuntimeError (4 total):** all four are in `src/FuzzyRegex/Parsing/ParseFunctions.cs`, and all
four read `statusReason: "The test host crashed or became unreachable while testing this
mutant"`. Neither the JSON nor `run.log` names a `StackOverflowException` anywhere - that
diagnosis is an inference from the mechanism below (an uncaught stack overflow is the one
failure mode .NET cannot classify, only kill the process for), not a captured fact. Both sites
are recursive parsing functions where the mutation removes the thing that bounds the recursion:

- **3 mutants at `ParseSetItem`, line 2193** (`if (version == RegexFlags.Version1 &&
  source.MatchText("["))`, guarding the version-1 nested-set branch): a `Logical mutation`
  (`&&` to `||`), a `Negate expression`, and a `String mutation` on the `"["` literal. Any of
  the three can make the branch enter without consuming a `[` from the source, so the recursive
  call into `ParseSetUnion` -> `ParseSetItem` re-enters the same branch on the same unconsumed
  input - unbounded recursion, stack overflow.
- **1 mutant at `FloatToRational`, line 2281** (`Arithmetic mutation` on `1.0 / error`): this is
  the continued-fraction recursion the surrounding comment already documents as relying on
  `error` staying finite and at least `0.0001` in magnitude so the recursive call terminates.
  Changing the operator breaks that invariant and the recursion no longer converges - stack
  overflow.

These are correctly-detected mutants (the test suite genuinely fails on them, just by crashing
the host instead of returning a red result Stryker can score), not survivors and not equivalent
mutants - no test change needed.

## Engine/Substitution.cs

Report: `.claude/worktrees/stryker/TestResults/stryker/substitution/reports/mutation-report.json`.

| Status | Count |
|---|---|
| Killed | 261 |
| Ignored | 329 |
| CompileError | 48 |
| **Survived** | **0** |

261/261 killed, 0 survivors. Sitting 3's dead attempt (`MSB4236`, before the machine crash) was
a stale environmental failure, not evidence about this chunk's mutants.

## Summary

All three chunks S55 scopes - API layer, parser, `Engine/Substitution.cs` - are fully decided:
237 + 2156 + 261 = 2654 mutants killed or timed-out-as-detected, 4 RuntimeError (explained above,
correctly detected), **0 survivors anywhere**. No new tests were needed; nothing to record as
an equivalent mutant either, since nothing survived. `tools/stryker-queue.json` holds 60 entries:
`engine-rand-01` through `engine-rand-59` (random-partition windows spanning 12 engine files -
`Engine/Matcher.cs`, `MatchState.cs`, `NodeCompiler.cs`, `Optimiser.cs`, `Iteration.cs`,
`ByteStack.cs`, `GuardList.cs`, `CharacterIndex.cs`, `MatchLimits.cs`, `Node.cs`,
`PatternObject.cs`, and `Unicode/UnicodeCasing.cs` - wider than this slice's original 8-file
scope, widened in a later sitting) plus the already-finished `parsing-remaining` as the 60th
entry. The 59 unrun `engine-rand-*` chunks (about 7,300 mutants total) are running detached in
the `stryker` worktree (branch `stryker-queue`) for S56/S57 to pick up; measured rate from
`parsing-remaining` (543 mutants in 4h05m at 2 runners = 2.2 mutants/min) puts that queue at
roughly 55 machine-hours at 2 runners.
