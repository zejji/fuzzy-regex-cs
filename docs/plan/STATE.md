# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none - S27 landed. **Blockers:** none. **Next:** `pwsh -File tools/launch-slice.ps1 s28`.

**Where the port stands:** ratchet GREEN, nothing failing, overall parity **80.6%** (was 76.5% at
the S27 open) with 24 areas at 100%, `Lookaround` and `Various` among them. Oracle GREEN over all
fourteen generators, 4200 rows, zero divergences.

**What S27 did.** Lookaround, both directions and both polarities: four cases in
`Matcher.BasicMatch` (`LOOKAROUND` and `END_LOOKAROUND`, advance and backtrack arms each) and the
`LookaroundStateData` struct. 80 tests un-skipped - 63 `needs:lookaround`, 17 `needs:lookbehind` -
and both tags are now gone from the suite. No parser or compiler change was needed: S15/S16 had
already built it.

**Two things S28 inherits.** `CONDITIONAL` (`_regex.c:12215`) pushes the *same*
`RE_LookaroundStateData`, so `PushLookaroundStateData`/`PopLookaroundStateData` are already there.
But its push also calls `push_repeats`, which is **not ported** - PORTMAP's `push_int8` row lists
all ten of its call sites, every one S28's or S30's. That is S28's real work, not the conditional.

**What is left before fuzzy**: partial 82, recursion 60, verbs 32, conditionals 15, POSIX 8. Every
one still fails on a `NotImplementedException` seam, none on a wrong answer.

**A warning worth carrying, from S27's controls.** Its first control C mutated `subargs.Forward` in
`NodeCompiler.BuildLookaround` and found **zero** divergences in 1200 rows: `args.Forward` is read
only by the repeat builders, so it was never the lookbehind direction mechanism. The direction is
in the *parser*, `Subpattern.Compile(Behind)`. A control that fires at zero measures nothing - check
each one fires before believing the generator has teeth.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; `lookaround` is
on the default list from S27. Controls: `python tools/run-controls.py --slices S27` (or `--check`).

**One oddity still open for the owner:** `slice-log.jsonl` records S26 as `failed` (145.8M tokens)
with its own commit `b778b07` as the abandoned SHA; the commit is real, the log row is stale.
