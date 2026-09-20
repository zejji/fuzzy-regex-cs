# S56 - per-sitting notes

The slice file is the spec; this is what each sitting did and what it cost.

## Sitting 1, 2026-09-20 (the only one)

Ran in the `stryker` worktree on branch `stryker-queue`, with the orchestrator's brief: every
chunk report is present, do not relaunch the queue, a single targeted re-run of one chunk is
fine, judge timeouts and runtime errors on the merits, commit here and the orchestrator merges.

**The triage itself was cheap and the auditing was not.** The 59 reports contain no survivors at
all, so the slice's nominal work - write a killing test per survivor - was empty within the first
twenty minutes. What took the sitting was establishing that the zero is worth anything:

1. **Whole-file counts are not chunk counts.** A report lists every mutant in every file it
   touched, including the ones outside that chunk's own character windows, which another chunk
   owns. Reading the raw totals double-counts by roughly 8x. The fix maps each mutant's line and
   column to a character offset in the report's own embedded source and keeps only the ones inside
   that chunk's windows. That is where the 4,673 tested / 0 survived comes from, and any future
   reading of these reports needs the same filter - which is why it is committed as
   `tools/stryker-inwindow-summary.py` rather than left in `.scratch/`.
2. **Which source did each chunk actually mutate?** Stryker embeds the full text of every file it
   mutates. `.scratch/s56-source-revisions.py` hashed those embedded sources and matched them
   against `git cat-file` at every commit that touched the file. Chunks 25-59 mutated `Matcher.cs`
   at 30e0178; chunks 01-24 mutated a working tree matching no commit. That is the source-drift
   caveat in the mutation doc, and it is only checkable because of the embedded source.
3. **Safe Mode.** `.scratch/s56-safemode.py` counts the `Safe Mode! Stryker will remove all
   mutations in <method>` lines across all 59 `run.log`s. Attempting to find the offending
   mutation is a dead end: the line number Stryker prints (22896) is in its own generated file,
   not in `Matcher.cs`, which is 10,328 lines at the revision those chunks mutated.
   `.scratch/s56-safemode-site.py` died of `IndexError` proving exactly that.

**The probes.** Four mutants were applied to the real source and run, rather than argued about:
two Timeouts (`Optimiser.cs:186`, `ByteStack.cs:113`) and two RuntimeErrors
(`NodeCompiler.cs:1632` in `BuildString`, `NodeCompiler.cs:1105` in `BuildGroupCall`).
`tools/probes/s56-mutant-behaviour.py` applies one mutation by line number with binary IO,
runs `tools/probes/s56-mutant-behaviour.cs` as a .NET 10 file-based app, and restores the file in
a `finally`. Two iterations were needed to get a probe that proves anything:

- the first `bytestack` case matched a plain string and finished in 58 ms, because it never grew
  the stack. A fuzzy pattern over a 2,000-character subject (`(?:abcdef){e<=3}`) does, and hangs.
- the first `groupcall` case reported `match = False` with nothing to compare it to. Running the
  unmutated baseline first (`True`) is what makes it evidence.

The probe app needed the same analyzer discipline as product code (EPC12, SS033, S6966, then
S8969 on the new test) - fixed on the merits, nothing suppressed.

**The orchestrator's `substitution` claim did not reproduce.** The brief said that chunk exited
-1 with 1,107 compile-error mutants and no report. On disk,
`TestResults/stryker/substitution/run.log` ends `[17:13:07 INF] The final mutation score is
100.00 %`, both reports were written 2026-09-17 17:13, and the JSON holds 261 Killed, 0 Survived.
The 1,107 figure is a whole-solution log line from a different context. S55's numbers stand.

**Cost control.** Every judgement over more than two mutants was scripted and read off the
output, never reasoned row by row: aggregation, in-window filtering, revision identification,
drift measurement, enclosing-method attribution and the top-up window generation are each one
script in `.scratch/` (deleted at commit; the three that earn a permanent home are
`tools/probes/s56-mutant-behaviour.py`, `tools/stryker-topup-windows.py` and
`tools/stryker-inwindow-summary.py`, the last promoted out of `.scratch/` because the blind review
found a defect in it and the document's every table rests on it).

**Left for a later slice, deliberately:** the 54 `engine-topup-*` windows are queued and not run,
because the orchestrator forbade relaunching the queue; and the Safe Mode bisect that would let
`DoEnhancedFuzzyMatch`'s 62 mutants be judged. Both are written up in
`docs/plan/mutation/2026-09-20-engine.md` with their upgrade paths.
