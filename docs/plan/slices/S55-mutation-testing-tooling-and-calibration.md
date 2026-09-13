---
slice: S55
phase: 6
title: Mutation testing, part one - Stryker.NET tooling, calibration, and the API and parser layers
delivers: []
---

# S55 - Mutation testing tooling and calibration

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

- [ ] Tooling committed and exercised; calibration recorded; chunk list written.
- [ ] API and parser survivors each killed by a test or recorded as equivalent with the reason.
- [ ] Ratchet GREEN, blind review (hunt: a survivor "equivalent" that a test could in fact
      distinguish; a chunk boundary that splits a function), commit.
