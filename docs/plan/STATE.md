# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none - S28 landed. **Blockers:** none. **Next:** `pwsh -File tools/launch-slice.ps1 s29`.

**Where the port stands:** ratchet GREEN, nothing failing, overall parity **81.3%** (was 80.6% at
the S28 open) with 24 areas at 100%. Oracle GREEN over all fifteen generators, 4500 rows, zero
divergences and zero unsupported.

**What S28 did.** The lookaround-condition form `(?(?=...)yes|no)`: four cases in
`Matcher.BasicMatch` (`CONDITIONAL` and `END_CONDITIONAL`, advance and backtrack arms each), plus
the repeat stack they need - `PushRepeats`/`PopRepeats`/`PushRepeatData`/`PopRepeatData` and
`GuardList.PushTo`/`PopFrom`. 15 tests un-skipped, `needs:conditionals` gone from the board. No new
state type: `CONDITIONAL` reuses S27's `LookaroundStateData`, and `BuildConditional` has existed
since S13.

**What S30 inherits.** Six of the ten `push_repeats`/`pop_repeats` call sites are still unported and
all six are S30's, inside `GROUP_CALL` and `GROUP_RETURN`; PORTMAP's new row lists them by line.
`push_groups`/`pop_groups` is still entirely S30's, all six sites.

**What is left before fuzzy**: partial 82, recursion 60, verbs 32, POSIX 8. Every one still fails on
a `NotImplementedException` seam, none on a wrong answer.

**A warning worth carrying, from S28's controls.** The repeat stack is nearly invisible to a
differential wave: a repeat's own backtrack entries restore the same state along the same path, so
`pop_repeats` mostly restores what would have been restored anyway. Control C sits at 2 and 8
divergences of 600 and two widenings failed to move it; control E, on the guard lists, fires at 0
and 1 and is kept only as a re-runnable negative result. **S30 must not read a green wave as
evidence its repeat handling is right.**

**And a trap in the tooling.** `tools/run-controls.py` caches waves in `.scratch/control-waves/`.
Widen a generator without deleting the cached wave and you measure the old generator - it reported
byte-identical figures here until the cache was cleared.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; `conditionals` is
on the default list from S28. Controls: `python tools/run-controls.py --slices S28` (or `--check`).

**One oddity still open for the owner:** `slice-log.jsonl` records S26 as `failed` (145.8M tokens)
with its own commit `b778b07` as the abandoned SHA; the commit is real, the log row is stale.
