# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S34 is closed. **Next:** S35, the phase close -
`pwsh -File tools/launch-slice.ps1 s35`. It is the last slice of Phase 4.

**Blockers:** none for S35. Two things are handed to Phase 6 and must not be lost:

1. **A crash both engines share**, found by S34's `-Count 2000` run and NOT fixed:
   `regex.compile('(?r)^İﬁ', regex.I | regex.F)` raises `IndexError` from
   `String.get_firstset` (`_regex_core.py:4036`) on an empty `String` node, and this port raises
   `IndexOutOfRangeException` from `Nodes.cs:2099`. Drafted as upstream report 6; there is no
   upstream answer to port, so it belongs to the issue sweep.
2. **This port carries upstream's pre-613 `GreedyRepeatOne` backtrack code.** Upstream fixed it in
   2026.8.30 (`b77694a`); the sync slice ports it test-first. Issue 614 (`9398a6d`) is likewise
   already fixed upstream, and two `ExpectedDivergences` entries are pinned against the older
   release waiting for it.

**Where the port stands:** ratchet GREEN, 5763 tests, 5578 passing, baseline 5470, tree clean.
**Every generator is on the default oracle list, `verbs` included, and the default run is green at
three seeds.** Six named families in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`; read its
remarks before adding a seventh, and read the `port-slice` note below.

**Oracle:** `pwsh -File tools/run-oracle.ps1` - now three seeds by default (7, 4242, the run's
date), and green only when all three are. **Raise `-Count` too**: 300 rows per generator is what the
default gives, 2000 is what found item 1 above. Controls:
`python tools/run-controls.py --slices S29,S31,S33`; delete `.scratch/control-waves/` after any
recorder or generator change.

**Outstanding, needs the owner:** S34 could not edit `.claude/skills/port-slice/SKILL.md` - the
harness refused write access - so the three-seed rule landed in `docs/VERIFICATION.md` (rule 7a)
only. The skill should point at it.

**Upstream report drafted, NOT filed:** `docs/plan/upstream-reports/2026-09-12-draft.md`, now six
issues; item 5 is marked HOLD until the sync can test it against 2026.9.10. The owner approves the
text first.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. Phase 4: 8 slices closed, parity 76.5% to 90.6%.
