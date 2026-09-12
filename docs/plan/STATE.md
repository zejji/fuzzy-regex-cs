# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S35 is closed. **Next:** S36, the Phase 4 close -
`pwsh -File tools/launch-slice.ps1 s36`. It is the last slice of the phase.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5768 tests, 5583 passing, parity **90.6%**, tree clean.
Default oracle GREEN at three seeds, 6000 rows each. Release build and ReSharper inspections clean.

**What S35 did.** Two port bugs fixed. (1) `$` under MULTILINE read a bound a `(*SKIP)` had moved:
`TryMatchEndOfLine` now reads `TextEnd` like every other assertion. S29's "port right by
construction" verdict is REVERSED - upstream's slow path was wrong on both sides. The whole
`search-start-skip-slice` family stopped diverging, its staleness alarm reddened the run, and the
entry is deleted; two surviving rows are a different mechanism, now
`overlapped-skip-stale-slice-reversed`. (2) `Sequence.FixFullCasefold` sliced the unfolded run with
folded offsets, which crashed on `(?r)^İﬁ` and silently answered `None` to `ﬁaﬁ` against `fiafi`.

**Known bug left open, and it is the only one:** ledger entry 7 - `İ` never reaches the full fold,
because upstream's expansion inventory is not lower-cased where the text it is sought in is. Fixing
it properly means changing the folding tables, so it needs a slice of its own before 1.0. **Owner
decision wanted:** does Phase 6's sweep take it, or does it get its own slice?

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds by default). Controls:
`python tools/run-controls.py --slices S29,S31,S33,S35`. Delete `.scratch/control-waves/` after a
generator change. S35-A fires at 2 of 5 seeds - a thin control, and the widening it wants (a
trailing `$` share in `_verb_pattern`) is named in S35's notes as Phase 6 oracle hardening.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`, seven entries now).
Nothing is filed until everything else in the plan is done, and the owner approves the text first.
Every entry re-verified against 2026.9.10 on 2026-09-12: 614's fix does **not** cover S30's
`(?<=(?&a))c`, and 613's does **not** cover the overlapped-`(*SKIP)` family.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. Phase 4: 9 slices closed, 1 to go.
