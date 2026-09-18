---
slice: S55
phase: 6
title: Mutation testing, part one - Stryker.NET tooling, calibration, and the API and parser layers
delivers: []
---

# S55 - Mutation testing tooling and calibration

**In progress, sitting 2 checkpoint (2026-09-17).** Per-sitting detail, calibration numbers, the
orchestrator's withdrawn "blocked" claim and its evidence:
`docs/plan/slices/notes/S55-sittings.md`. Tooling built and working (`tools/run-stryker.ps1`,
`stryker-config.json`, `tools/stryker-queue.json`). API-layer chunk (`*.cs`) done: 237 mutants,
0 survived. Parser chunk (`Parsing/*.cs`, 2189 mutants) left running in the background past this
sitting's deadline; its report will be under `TestResults/stryker/parsing/` for the next sitting
to read and triage. `Engine/Substitution.cs` not yet run.

The exit gate's third instrument (ROADMAP; amendments 12 and 14): the only one that answers "would a
regression actually fail a test?". Stryker.NET reruns the suite per mutant, so the engine will take
hours; the owner has allowed long runs provided they improve the outcome, and asked for many small
runs rather than one big one. This slice builds the tooling and calibrates; S56 and S57 spend the
time.

## Before launch (orchestrator)

`dotnet tool install -g dotnet-stryker` - the driver cannot install tools. Confirm Stryker reaches
TUnit through the Microsoft Testing Platform runner on this SDK (it is a preview path); if it does
not, the slice's first job is to find the supported invocation and record it.

## Scope

- **Calibrate on one small file first**: mutate `src/FuzzyRegex/FuzzyCounts.cs` (or the smallest
  engine file with logic), record wall time, mutant count and score, and from that number set the
  chunk size for the engine: files or `_regex.c` regions whose run fits comfortably inside one
  detached job. Write the numbers into the slice and into `tools/run-stryker.ps1`'s header.
- **`tools/run-stryker.ps1`**: runs Stryker over one `--mutate` glob, writes the JSON and HTML
  reports to `TestResults/stryker/<chunk>/`, is resumable and detached-friendly (log file,
  progress line per mutant batch), and refuses to run while the driver is running tests. A
  `-Queue` mode runs a list of chunks in sequence overnight and never stops on one chunk's failure.
  Config in `stryker-config.json`: thresholds off (a measurement, never a merge gate), `--since`
  supported for later incremental runs.
- **Run the API layer and the parse-error paths now**: `src/FuzzyRegex/*.cs`, `Parsing/`, and
  `Engine/Substitution.cs`, the two places the oracle cannot reach. Read every survivor: a survivor
  is either a missing test (write it, test-first, so the mutant dies) or equivalent (record why).
  Quote the score before and after.
- **Queue the engine**: write the chunk list for `Engine/Matcher.cs` (by opcode region),
  `MatchState.cs`, `NodeCompiler.cs`, `Optimiser.cs`, `Iteration.cs`, `ByteStack.cs`, `GuardList.cs`
  and `Unicode/UnicodeCasing.cs` into `tools/stryker-queue.json` for the orchestrator to run
  detached overnight before S56.

## Verification

- Calibration numbers quoted; API and parser reports committed under `TestResults/stryker/` is
  gitignored, so the scores and the survivor table go in the closing notes and a
  `docs/plan/mutation/<date>-api-parser.md`; new tests kill named mutants.

## Done when

- [x] Tooling committed and exercised; calibration recorded; chunk list written.
- [x] API and parser survivors each killed by a test or recorded as equivalent with the reason.
- [x] Ratchet GREEN, blind review (hunt: a survivor "equivalent" that a test could in fact
      distinguish; a chunk boundary that splits a function), commit.

## Closing notes (2026-09-18, sitting 4)

All three in-scope chunks - API layer (`*.cs`), `Parsing/*.cs`, `Engine/Substitution.cs` - ran to
completion with **zero survivors**: 237/237, 2156+29(Timeout)+4(RuntimeError)/2189, and 261/261
respectively. Nothing needed a new test or an equivalent-mutant writeup, since nothing survived.
The 4 RuntimeError mutants (all in `ParseFunctions.cs`, all crash-the-host `statusReason`s) are
recursion-bound-removing mutations in `ParseSetItem` and `FloatToRational`, correctly detected by
crashing rather than a scored result. Full numbers, per-chunk reports and the RuntimeError
mechanism analysis: `docs/plan/mutation/2026-09-17-api-parser.md`. Per-sitting detail across all
four sittings, including the MSB4276/Buildalyzer root cause, the repeated-`-m` glob bug, the
orchestrator's withdrawn "Stryker can't run TUnit" claim and its reproduction, and the
2026-09-18 machine crash and dump recovery: `docs/plan/slices/notes/S55-sittings.md`.

Surprising: two independent environment failures across the four sittings (a Buildalyzer
MSBuild-resolution failure, then a full machine crash mid-run) were both recovered from without
losing calibration evidence, the second via a `dotnet-dump` snapshot of the running test host
rather than a re-run. Also surprising: an orchestrator-relayed claim from a different worktree,
that Stryker cannot run TUnit's Microsoft Testing Platform runner at all, did not reproduce even
once, let alone twice, in this worktree on this commit - it was checked rather than acted on.

The `stryker-queue` worktree's engine chunks (`engine-rand-01`..`engine-rand-59`, ~7,300
mutants across 12 files, wider than this slice's original 8-file engine scope - widened in a
later sitting) continue running detached for S56/S57; not this slice's concern.

**Review:** one blind pass (`caveman:cavecrew-reviewer`, sonnet) checked every factual claim in
the closing diff against the four raw Stryker JSON reports and the source at the cited line
numbers - 0 findings, everything confirmed. A second, independent verifier pass (fresh
`general-purpose` agent, opus, briefed separately and told not to read the reviewer's output)
re-derived the same numbers from scratch and found 6 real discrepancies the reviewer's pass had
missed: the ratchet claim said "not re-run" when it had in fact been run and regenerated
`docs/STATUS.md`; the engine-queue was cited as "60 engine-rand chunks" when it is 59 plus the
already-finished `parsing-remaining`; the engine file list was stale (8 files named in the
original scope vs. 12 actually in the queue); "StackOverflowException" was stated as fact when
it is an inference from the crash mechanism, not a captured log line; the Timeout mutants'
"hand-checked in an earlier sitting" provenance had no surviving record (fixed by an independent
spot-check instead); and the "Ignored (outside mutate filter)" table label overclaimed a
specific reason the JSON did not uniformly support. All six were fixed in this doc and the
sittings note; no second review pass was run over that delta, since the fixes were factual
corrections against the verifier's ground-truth JSON/queue-file reads, not new code, public API,
or tooling - the same category the skill exempts from a repeat pass. No control was run (no
engine code touched this slice), so there is nothing to reproduce under that rule.
