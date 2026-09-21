# What Phase 6 hands Phase 7

Written by S57 on 2026-09-21, the coverage-backstop and phase-close slice. Optimisation is where
silent behaviour change is likeliest, so this file is the set of numbers Phase 7 regresses against
and the rules it may not break. Everything here was measured; each item says where, so a Phase 7
slice can re-run it rather than trust it.

Phase 7 had already started when this was written (S60 landed the required-string prefilter on
2026-09-20, commit `791d628`). Nothing below is new to S60; it is collected so the next slice does
not have to find it.

## 1. The rule that outranks every optimisation

**Phase 7 ports upstream's start optimisations without importing their answers** (owner rule,
2026-09-12; DECISIONS, design spec amendment 16).

`locate_required_string` and the `search_start_*` family change what upstream answers on `(*SKIP)`
patterns and on some partial matches, and the research says those answers are wrong: PCRE2 10.47
agrees with this port with its own optimiser on and off. So a prefilter here may skip positions it
can prove hold no match, and may not decide a match.

- **These test files are PERMANENT**: `tests/FuzzyRegex.Tests/Gaps/Engine/BacktrackingVerbTests.cs`,
  `PartialMatchingTests.cs` and `ReverseMatchingTests.cs`. A slice that turns one red has ported a
  bug. They are never "inverted later".
- **One arm cannot be switched off from Python.** `search_start`'s per-position `min_width` check
  (`upstream/src/_regex.c:8429-8438`) is not reachable from the Python API, so the oracle's
  prefilter-free recording switch cannot neutralise it - that switch reaches
  `locate_required_string` only (`PREFILTER_FREE_GENERATORS`, `tools/record-oracle.py`; the
  generators it covers are `verbs` and `partial-sliced`).
- **The one `search-start-*` entry left in `ExpectedDivergences` is to be re-judged, not deleted.**
  `search-start-partial` (`ExpectedDivergences.cs:2341`) pins rows where upstream's unported
  prefilter answers differently. Its sibling `search-start-skip-slice` is already gone, but for the
  other reason: S35 fixed this port after an independent verification reversed S29's verdict, the
  entry's own example row stopped diverging, and the staleness alarm reddened the run
  (`ExpectedDivergences.cs:36-43`). No `search_start` arm has been ported yet. As each arm lands,
  the rows the entry explains
  either stop diverging - in which case the entry goes, and the strict list proves it - or still
  diverge, in which case the entry's reason has changed and must be rewritten.

## 2. What Phase 7 regresses against

### Benchmarks

Baselines and the noise floor are committed under
`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/`, taken by S58 on 2026-09-19 at
`--job medium`, BenchmarkDotNet 0.15.8, .NET 10 Release. `noise-floor.md` in that folder explains how
the floor was taken - two runs of an unchanged tree, largest per-workload ratio - and why it is
per workload and never an average. The comparison tool is `tools/compare-benchmarks.ps1`.

The folder id is derived from what BenchmarkDotNet reports about the machine, so these numbers
describe that machine and no other. `--job short` is banned for decisions: the Phase 7 research
measured a 21% error bar on it against the existing backtracking benchmark
(`docs/plan/phase7-research/SUMMARY.md:6`, 2026-09-16; carried into the owner notes of the same
date). S58 took the baselines at `--job medium` for that reason.

### Correctness

| instrument | the number Phase 7 must not worsen | where |
| --- | --- | --- |
| ported suite skips | **zero**, in every area | `docs/STATUS.md` |
| oracle, default wave | GREEN at three seeds | `tools/run-oracle.ps1` |
| oracle, deep wave | GREEN at three seeds, 126,080 rows a seed | `tools/run-oracle.ps1 -Count 6000` |
| mutation | **7,342 mutants tested, 0 survived** | per-chunk `reports/mutation-report.json` under `.claude/worktrees/stryker/TestResults/stryker/` |
| line coverage | 878 uncovered lines, 257 members holding one, **0 wholly-unentered opcode arms** | re-take with the command below; S57 measured it over 6,475 tests |

Two cautions about the last two rows, both learned the hard way:

- **Stryker's zero survivors is a mutation score, not a coverage claim.** It is 7,342 mutants tested
  out of 285,965 generated; the rest were `Ignored` by the chunking.
- **The coverage figure is a backstop, never a target.** The question it answers is "is any file or
  branch untested at all", and the answer is no. Eight members remain wholly unreached and each is
  judged in `docs/plan/slices/notes/S57-sittings.md` - three `Seam` markers for capabilities nothing
  has ported, two `ZeroWidthOpcode` throws that are fidelity dead code, two partial arms documented
  as unreachable until Phase 7 restores `try_match`'s test-node arm, and
  `MatchState.GuardRepeatRange`, reachable only from the `GreedyRepeatOne` backtrack case when the
  retreat loop exits with `pos == limit` and no tail match.

To re-take the coverage figure:

```powershell
dotnet run --project tests/FuzzyRegex.Tests --configuration Release -- --coverage `
  --coverage-output-format cobertura
```

Read the report with a script that **deduplicates by line number**: cobertura lists a `<line>` under
every class that contains it, so a naive reader double-counts - S57's first pass reported 824
uncovered lines in `Matcher.cs` where there are 412. Two `DemoEngineContractTests` cases fail under
instrumentation and pass without it, because instrumentation pushes them past the demo's two-second
`MatchTimeout`; that is measured (`tools/probes/demo-cap-timing.cs`), not a flake to chase.

### The timeout poll, already on the hot path

S51 put `check_timed_out` and the `CancellationToken` poll on the matcher's hot loop before Phase 7
deliberately, so that optimisation measures the real loop. Measured then: **8 bytes an operation and
no time the benchmark could resolve**. A Phase 7 slice that removes or moves the poll changes an
observable promise and needs its own decision, not a micro-optimisation note.

## 3. Edge cases an optimiser is tempted to special-case

Pinned by S54 and by the slices before it: zero-width and empty matches, anchors, `MatchTimeout`,
large inputs and pathological backtracking. `Gaps/Engine/OptimiserTrapsTests.cs` holds the megabyte
subjects (`LONG`, `LONG_PARTIAL`, `DENSE`), whose lengths that file asserts against its own
constants so a wrong subject cannot produce a confident wrong verdict.

## 4. Work Phase 6 leaves open

- **Two inherited bugs are scheduled, not fixed**: ledger entries 19 and 20 as S57c, entry 21 as
  S57d. Both sort ahead of S60b, so they run before the next optimisation slice.
- **Ledger entry 18 is S61's item 7.** It is the only inherited bug whose fix is an allocation
  change: one backtracking-stack block per repetition for a body with nothing to backtrack into.
  Phase 6's closing bookkeeping lands with it.
- **Ledger entry 17 is an owner decision**, stated in `docs/plan/STATE.md`. Fixing its two remaining
  orderings means choosing between two options upstream has left unchosen since 2021.
- **`tools/run-controls.py` needs a `suite` mode and a multi-generator wave** before S50's control C,
  S50b's four, S52d's A and S53b's B can be registered - seven controls in all. They signal through
  a test run or through the whole default wave list, and the registry's schema names one generator
  and reads the diverge column. Until then those seven exist only as prose in their slices' notes.
- **S56's scope is empty.** It was scheduled to read the engine's mutation survivors and there are
  none. Recorded as evidence for that call, not as the call itself.

## 5. How to judge a divergence, when Phase 7 finds one

Design spec amendment 16, unchanged: a documented definition, a real run of a second engine, a survey
of comparable libraries where upstream defines nothing, the release history, a blind review, and an
independent verifier over the batch of judged rows. Second engines are installed - PCRE2, Perl, .NET,
`regex` 2026.9.10 and TRE for fuzzy rows; see `docs/plan/OPERATIONS.md`.

The cheapest first step is `python tools/probes/gate-divergence-doors.py --rows <file>`, which puts
every judged family's own control to every row in one run. S57b's six sittings are the worked
example, and its lesson is the one S52 learned first: script the ablations over the whole batch and
read the classification off the output. Reasoning from a row's shape got the wrong answer three times
out of three.
