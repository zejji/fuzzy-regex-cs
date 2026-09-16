---
slice: S63
phase: 7
title: The v1.0 performance gate run for real, both baselines refreshed on a quiet machine, and Phase 7 closed
delivers: []
---

# S63 - The gate, and the close

The gate exists already, written down in three places that agree (spec section 11,
`.claude/skills/benchmark/SKILL.md`, ROADMAP's Phase 7 row); what has never happened is a run of it
against a finished engine. This slice runs it, records the result, and closes the phase - including
the case where the gate does not pass, which is a triage exercise and not a licence to keep
optimising indefinitely.

**The gate, exactly**: for every workload in the suite, our median (BenchmarkDotNet) is at or below
the Python `regex` median (pyperf) for the equivalent operation, with one tolerance - no more than
**10% of workloads** may be slower, and **none by more than 1.25x**. A workload is one named case
(pattern plus corpus plus operation), never an average; workloads Python cannot express (Span
overloads and the like) are measured and excluded, and the published table says which and why.

## Scope

1. **Both baselines refreshed on a quiet machine**, same machine and same session for the two
   sides, driver idle, nothing else running - the skill's rule, and the condition S54's review hunt
   was written to catch. .NET: the full suite, `--job long` for the gate run (a long run is cheaper
   than a wrong conclusion), `--exporters json`, and `--runtimes net10.0 net11.0` if an 11 runtime
   is installed, one `net10.0` build under both, as S54 established. Python:
   `python bench/baselines/measure_python.py`, with `python -m pyperf check` run on the result and
   its verdict recorded beside the numbers. A baseline that fails `check` is not evidence.
2. **Same subject, same operation, same answer.** Before a pairing is recorded, the two sides are
   asserted to return the same thing - same match count, same spans. A faster number for a
   different answer is not a number, and this repo already owns the machinery that defines "the
   same answer".
3. **Results committed** under `bench/baselines/<machine-id>/` with everything needed to re-derive
   them: the machine description, the `regex` and Python versions, the .NET SDK and runtime
   versions, the git SHA, the job, the artifacts folder, the noise floor in force, and the date.
   The raw BDN full JSON and pyperf JSON go in, not just the table.
4. **The gate verdict, per workload**, as a committed table: our median with spread, Python's median
   with spread, the ratio, pass or fail, and the excluded rows with their reason. The
   `System.Text.RegularExpressions` column goes beside it as the number a .NET user will actually
   compare against, marked as not part of the gate.
5. **Failures triaged, not absorbed.** Every workload over the line becomes exactly one of: a
   follow-up slice with a named hypothesis and a mechanism from `OPTIMISATION-TECHNIQUES.md`, or an
   owner decision (accept the gap, with the reason and the number). Nothing is left as "known
   slow". The triage list goes in the closing notes and, where it defers work, in
   `OPTIMISATION-NOTES.md` with a `ponytail:`/`Phase 7` comment at the line.
6. **The notes file swept.** Every `OPTIMISATION-NOTES.md` row whose optimisation was implemented in
   S59-S62 is deleted together with its source comment, in this commit if an earlier slice missed
   it; `grep -rn -i 'ponytail:\|Phase 7' src/FuzzyRegex --include=*.cs` and the file must agree row
   for row when the slice closes. Rows that remain are deliberate, dated deferrals, and the file
   says so.
7. **Phase 7 close notes**: what moved and by how much per workload, what was declined and why, the
   negative results, the state of `SYNC-DIVERGENCE.md` (every row with its measured gain and its
   re-align instruction), the AOT binary size against the 6,972,928-byte baseline, and the refreshed
   startup timings on a quiet machine, which S53 explicitly asked for and never had.

## Verification

- `pwsh -File tools/compare-benchmarks.ps1` against the committed baseline at S58's floors, for the
  whole suite, time and allocated bytes/op per workload - the phase's cumulative delta, reported
  per workload and never averaged.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict;
  the whole phase is void if an answer moved.
- `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/<Class>/*"` for
  `BacktrackingVerbTests`, `PartialMatchingTests`, `ReverseMatchingTests` and `OptimiserTrapsTests`,
  green and named in the commit message.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN with the
  binary size and startup timings recorded on the quiet machine.
- `pwsh -File tools/check-sync-divergence.ps1` GREEN.
- `pwsh -File tools/update-public-api.ps1` run once to prove the surface is unchanged since S59; any
  diff at this point is a finding, because the API was frozen at S53b.

## Done when

- [ ] Both baselines refreshed on a quiet machine, same session, with `pyperf check` recorded; raw
      JSON committed with machine, versions, SHA, job and noise floor.
- [ ] Every pairing asserted to give the same answer before its timing was recorded.
- [ ] Per-workload gate table committed, with the excluded rows and the `Regex` column, and the
      verdict stated in the terms the gate defines.
- [ ] Every failing workload triaged into a named follow-up slice or a written owner decision.
- [ ] OPTIMISATION-NOTES and the source comments agree row for row; implemented rows and comments
      deleted; remaining rows dated and deliberate.
- [ ] `SYNC-DIVERGENCE.md` complete, every row carrying its measured gain and re-align instruction.
- [ ] Phase 7 close notes written into this file, ROADMAP's Phase 7 row updated with the outcome,
      DECISIONS entry for the gate verdict.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a baseline taken with the
      driver still running; a gate table that averages or silently drops a failing workload; a
      Python pairing that answers something different; a `pyperf check` warning recorded as a pass;
      an OPTIMISATION-NOTES row deleted although its comment is still in the source), commit.
