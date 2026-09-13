# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S40a is next, then S41. Launch: `pwsh -File tools/launch-slice.ps1 s40a`.
S40 landed the `{...:test}` constraint - `fuzzy_ext_match`, `folded_char_at` and
`fuzzy_ext_match_group_fld` - so **every fuzzy seam except ENHANCEMATCH and BESTMATCH is gone**.
Parity **91.1% -> 97.2%**, skips **174 -> 55**, and all 55 are S41's (30) and S42's (25).

**S40a exists because S40's last verification item found four defects, none of them S40's**
(design spec amendment 19). Do the recorder first; the other three are only measurable after it.
1. **Upstream hangs**: `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')` never returns on 2026.7.19. Needs
   all three of a leading optional item, an atomic group, `(*SKIP)` inside. The recorder has no
   per-row timeout, so one such row kills a whole wave silently - `regex` takes `timeout=`.
2. **Four rows of the 6000-row wave diverge at seed 7** (`partial` 97927/98956, `partial-sliced`
   103926, `verbs` 117679), **proven to be HEAD's** by consuming the same saved wave with HEAD's
   engine in a worktree: HEAD gives the same four, with 1432 `unsupported` where this tree gives 0.
3. **A fuzzy section in a lookbehind reports `FuzzyChanges` that contradict its own `FuzzyCounts`**:
   `(?<=(?:[ab][cd]){e<=1})$` on 'axc' is `(1,0,0)` with a *deletion* at 1, upstream `subs=[2]`.
   Plain `(?r)` is correct, so it is not the reversal. S38/S39 territory.

**Two upstream asymmetries S40 ported as written, so nobody "fixes" them:** `fuzzy_ext_match` has
no `SET_*_REV`/`SET_*_IGN_REV` arm and `fuzzy_ext_match_group_fld` has no `SET_*_IGN` arm, so those
tests constrain nothing; `.` is a no-op everywhere. Measured, and pinned by
`Gaps/Engine/FuzzyTestConstraintTests.cs`.

**A control can read zero because no *test* discriminates, not because the path is cold** - S40's
control E, third failure mode after S38's and S39's. See its closing notes before trusting a zero.

**Blockers:** none. S40's own scope is complete and green.

**Where the port stands:** ratchet GREEN, 5821 tests, 5766 passing, parity **97.2%**, 30 areas at
100%. 55 skipped, all `(?e)`/`(?b)`.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Rows per generator is **`-Count`**;
`-Rows` is a path to a JSONL file and still re-records - use `-SkipRecord` to consume one.
**Delete `.scratch/control-waves/<generator>-<count>-<seed>.jsonl` after widening a generator.**
Controls: `python tools/run-controls.py --slices S40 --seeds 3`.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`): nothing filed until
everything else in the plan is done. Entry 7 and S40a's `(*SKIP)` hang are on Phase 6's list.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` trails local and needs a push.
