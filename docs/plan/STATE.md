# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S40a, STILL OPEN after session 1. Launch: `pwsh -File tools/launch-slice.ps1 s40a`.
Its file now carries a long "Session 1" section - **read that before re-planning it**, because two of
its four items rested on premises that measurement overturned.

**What landed:** the recorder per-row timeout (a hanging upstream row is now a recorded `timeout`
outcome, not an unwritten wave - it fires for real at seed 4242); ledger entries **10** (the
`(*SKIP)`-in-an-atomic hang, already fixed upstream by `b77694a`, this port never hung - 70 of 1296
grid calls hang on 2026.7.19, 0 here) and **11** (a fuzzy match reporting changes that contradict its
own counts - UPSTREAM'S, S38 had already pinned it, the one-line fix was measured and reverted);
and one engine fix: **`DoMatch` now restores the slice at the start of every match**, which is the fix
ledger entry 5 proposes for upstream. Seed-7 wave 4 divergences -> 3.

**The exit gate is NOT met, and it is far bigger than S40a supposed.** 6000 rows a generator at three
seeds gives **3 + 5 + 7 = 15 diverging rows**, not four; S40 saw four because only seed 7 ever
completed. Eleven have never been triaged. **Row 93133 (`recursion`, `subf`) is a CRASH** -
`ArgumentException: capture index out of range` out of `Substitution.ExpandField` where upstream
answers `sub 0`. Both blind reviews confirmed none of the fifteen is caused by this session's diff.

**Do not re-try two things without reading why they were reverted**, both written out at the code:
clearing the fuzzy change list in `basic_match`'s `start_match` (turns S38's two pinned rows red), and
restoring the slice before the partial retry in `DoMatch` (fixes row 97927, introduces a reversed one,
and turns S37's permanent pinned answer red - that answer is itself the same defect).

**Blockers:** none technical. The remaining work wants splitting, and the split is an owner decision -
S40a's closing section proposes four pieces, crash first.

**Where the port stands:** ratchet GREEN, 5825 tests, 5770 passing, parity **97.2%**, baseline updated
to 5662. 55 skipped, all `(?e)`/`(?b)`.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Rows per generator is **`-Count`**;
`-Rows` is a path to a JSONL file and still re-records - use `-SkipRecord` to consume one. A 6000-row
seed takes ~18s to record and ~8s to consume, so the whole gate is about two minutes.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`): nothing filed until
everything else in the plan is done. Entries 10 and 11 are new; 11 is an INHERITED bug, so by the
owner's 2026-09-12 rule it joins entry 7 on Phase 6's sweep list.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` trails local and needs a push.
