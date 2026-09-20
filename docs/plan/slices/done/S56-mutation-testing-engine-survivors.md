---
slice: S56
phase: 6
title: Mutation testing, part two - read the engine's survivors and kill each with a test or record why it is equivalent
delivers: []
---

# S56 - The engine's survivors

Consumes the reports the orchestrator produced overnight with `tools/run-stryker.ps1 -Queue` over
S55's chunk list. The session spends no time running Stryker: it reads, judges and writes tests.
This is the instrument that says whether Phase 7 can rewrite the engine safely.

## Before launch (orchestrator)

Every chunk in `tools/stryker-queue.json` has a report under `TestResults/stryker/<chunk>/`. The
queue runs detached on the quiet machine; a chunk that exceeded its time budget is re-split and
re-queued rather than skipped. The session's first check lists the chunks with a report and refuses
to start if any is missing.

## Scope

- **Survivor triage, chunk by chunk.** For each surviving mutant: reproduce by hand (apply the
  mutation, run the suite, watch it stay green), then decide: a missing test, written test-first
  so the mutant dies - with the mutant's line and operator named in the test's comment; or
  equivalent, recorded in `docs/plan/mutation/<date>-engine.md` with the reason; or dead code the
  port carries for line-for-line fidelity (`default` arms, upstream's unreachable branches), which
  is recorded as such and is not a coverage failure.
- **Fuzzy and BESTMATCH first**, then verbs and partial, then the rest: those are the areas where
  Phase 5's wave found the most, and where Phase 7's rewrites will land.
- **Timeouts as mutants**: a mutant that makes the suite hang is the worst outcome and Stryker
  reports it as a timeout; each one is a place where a per-test `[Timeout]` and an assertion are
  owed, not a place to ignore.
- Two or more sittings are expected; checkpoint by chunk with the survivor table so far in
  STATE.md, and record the mutation score per chunk before and after.

## Verification

- Per-chunk score before and after in the closing notes; every survivor accounted for in the
  mutation document; every new test named with its mutant.

## Done when

- [x] Every engine chunk's survivors killed, recorded equivalent, or recorded as fidelity dead code.
      (There are none: 4,673 in-window mutants tested across 59 chunks, 0 survived.)
- [x] Mutation document committed; scores quoted; no chunk skipped.
- [x] Ratchet GREEN, blind review (hunt: an "equivalent" survivor that changes a fuzzy count; a
      killing test that asserts the mutant's answer rather than the right one), commit.

## Closing notes (2026-09-20, one sitting)

**Zero survivors in the engine.** All 59 `engine-rand-*` chunk reports were present as the
orchestrator said. Counting only the mutants inside each chunk's own character windows - a
report's totals span whole files, including regions another chunk owns - gives Killed 4,613,
Timeout 46, RuntimeError 14, **Survived 0**, plus CompileError 533 and Ignored 356. No killing
test was owed and no mutant is recorded as equivalent, because nothing survived. Full numbers,
per-method breakdowns and the two caveats below: `docs/plan/mutation/2026-09-20-engine.md`.
Per-sitting detail, the scripts and the false starts: `docs/plan/slices/notes/S56-sittings.md`.

**The 46 Timeouts and 14 RuntimeErrors were judged on the merits, with probes, not by reasoning.**
All 46 hangs remove a loop's termination condition (24 of them the `VisitedAg` mark in
`Optimiser.AddRepeatGuards`, 3 the capacity doubling in `ByteStack.PushBlock`, 3 the `List.Add`
in `PatternObject`'s grow-on-demand accessors). All 14 RuntimeErrors remove or reverse the
`args.Code` instruction-pointer advance in a `NodeCompiler.Build*` function.
`tools/probes/s56-mutant-behaviour.py` applies any of four of them to the real source, runs a
.NET 10 file-based probe app and restores the file; it shows the two hangs still running after
60 s where the unmutated engine answers in under 100 ms, `BuildString`'s rewind throwing
`IndexOutOfRangeException` from `CompileArgs.get_Op()`, and `BuildGroupCall`'s rewind answering
`false` where the baseline answers `true`. What crashed the Stryker test host is *not* recorded
anywhere and is not asserted - S55's verifier caught exactly that overreach.

**The slice's `[Timeout]` requirement was already met and is now guarded.** It asks for a
per-test `[Timeout]` wherever a mutant hangs; `tests/FuzzyRegex.Tests/AssemblyTimeout.cs` has
carried `[assembly: TUnit.Core.Timeout(120_000)]` since 2026-09-13, so every test in the suite is
bounded. The gap was that nothing noticed if it disappeared:
`tests/FuzzyRegex.Tests/Gaps/Engine/HangBoundTests.cs` now asserts the attribute and its value,
and was watched failing with the bound changed to 600,000 ms before being committed.

**Two caveats that matter more than the score, both new findings:**

1. **A tenth of the in-window mutants never compiled.** 533 CompileError, concentrated in
   `Matcher.cs::BasicMatch` (123) and `DoEnhancedFuzzyMatch` (62). The second is Stryker's Safe
   Mode: one unattributable mutation causing `CS0165 Use of unassigned local variable 'status'`
   makes it drop every mutant in the method, in all 59 chunks. Nothing is proven about that code.
2. **The engine moved under the three-day queue.** Chunks 01-24 mutated an uncommitted working
   tree; S56b and S60 landed mid-run. 1,048 lines of today's `Matcher.cs`, `PatternObject.cs`,
   `NodeCompiler.cs` and `FuzzyRegex.cs` have never been mutated by any run. This is checkable
   only because each report embeds the source it mutated; `tools/stryker-topup-windows.py` does
   the diff and emits the gap as queue entries. `tools/stryker-queue.json` now ends with
   `engine-topup-01..09` (54 windows), **queued and not run** - the orchestrator's brief forbade
   relaunching the queue. They are offset-based and go stale the moment those files change:
   regenerate rather than trust them.

**Correction for the record:** the orchestrator's brief said the `substitution` chunk exited -1
with 1,107 compile-error mutants and no report. It did not.
`TestResults/stryker/substitution/run.log` ends `[17:13:07 INF] The final mutation score is
100.00 %`, both reports were written 2026-09-17 17:13, and the JSON holds 261 Killed / 0
Survived. S55's substitution numbers stand unchanged.

Also committed: `stryker-config.json` concurrency 4 -> 2, which is the setting every report in
this queue was actually produced under.

**Review:** one blind pass (`general-purpose`, opus) over the whole diff, briefed to hand over
reproductions only. Five checks, **one finding, reproduced and fixed**: the mutation document
named `Iteration.Enumerate` as holding a Timeout mutant, where the mutant is in `Iteration.Split`
at line 439. Root cause is the attribution script's signature regex, whose return-type character
class has no `?`, so `internal static string?[] Split(` is not recognised as a declaration and
its mutants are charged to the method above. Re-running every Timeout, RuntimeError and
CompileError attribution with `?` added moved exactly that one
Timeout row and split one CompileError row (`Enumerate` 4 into `Split` 3 + `EnumerateSplits` 1,
so 91 methods rather than 90); the totals 46 / 14 / 533 and every other method name are
unchanged. The reviewer's other four checks reproduced clean: the probe restores all four source
files under normal exit, hang and exception; `HangBoundTests` fails with `Expected 2m, but found
10m` when the bound is changed to 600,000 ms; all 54 top-up windows are in range and line-aligned
against `git cat-file blob HEAD:<file>`, and re-running the generator reproduces them; the queue
parses as 71 entries, 69 chunks, no duplicates.

Fixing that finding turned the scratch aggregation into committed tooling -
`tools/stryker-inwindow-summary.py`, so every table here can be re-derived rather than trusted
(`python tools/stryker-inwindow-summary.py engine-rand`) - and new tooling is unreviewed code, so
**a second blind pass ran over that file alone** (`general-purpose`, opus, five checks). It found
two real defects, both reproduced and both fixed:

- **Window membership tested the mutant's first character, not its whole span**, where Stryker
  takes a mutant only when the span fits. The reviewer's cross-tab against Stryker's own verdicts
  showed the difference exactly: 165 mutants the reports mark `Removed by mutate filter`, plus 32
  CompileError, were being counted as in-window. Corrected, the table reads CompileError 533 and
  Ignored 356 rather than 565 and 521. **`tested` = 4,673 and `survivors` = 0 are untouched** - no
  Killed, Timeout, RuntimeError or Survived mutant crosses a window edge.
- **A constructor is not matched as a declaration**, so three mutants in `CharacterIndex`'s
  constructor were attributed to whatever method preceded it. Fixed by making the return-type
  group optional. A remaining limitation is recorded in the script rather than engineered around:
  a mutant on a field initialiser has no enclosing method and is charged to the method above it
  (two such mutants, both in `MatchState.cs`, neither Killed, Survived, Timeout nor RuntimeError).

The reviewer's other findings were synthetic - malformed queue entries, a report with no `files`
key, two chunks whose windows overlap the same mutant - and it proved none of them reachable from
this queue or these 60 reports (`chunk but no mutate: 0`, `window strings without a {a..b} span:
0`, `overlapping adjacent pairs across chunks: 0`). They are not defended against, deliberately:
this is a one-shot analysis tool over files a run produces, not a parser for untrusted input.
No third pass was run - the fixes are the second pass's own findings, and running a reviewer over
a reviewer's corrections is the critique loop the workflow forbids.

**Verifier** (amendment 16 limb (d), fresh `general-purpose` opus, briefed only with the
commit-ready tree): all eight claim groups CONFIRMED, including both hang probes exceeding 60 s
against 63 ms and 81 ms baselines, `IndexOutOfRangeException` from `CompileArgs.get_Op()`,
`groupcall` answering false against a true baseline, the 54 top-up windows regenerating identical
to the queue, Safe Mode and `CS0165` in 59/59 logs, `CompileUnderVersion` 177 times, the
`substitution` correction, the revision hashes (`30e0178`, `5a4855a`, and chunks 01-24 matching no
commit), `HangBoundTests` passing, and the ratchet at 6,460/6,352 GREEN. It reported two
incidental discrepancies, both fixed here: the sittings note still said `DoEnhancedFuzzyMatch` had
64 CompileError mutants (62), and `Matcher.cs` was called 10,329 lines when it is 10,328 at the
mutated revision and 10,716 at HEAD. Probe timings quoted to the millisecond varied by 1-3 ms
between runs and are now written as approximations.

No control was run - this slice changed no engine code, so there is no fault to inject and
nothing to reproduce under that rule. What S57 should know: `NoCoverage` is absent from these
reports because coverage analysis was off, not because every line is reached; line coverage is
S57's instrument and this one says nothing about it.
