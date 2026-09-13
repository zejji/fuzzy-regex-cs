# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**PHASE 5 IS COMPLETE** (S43, `d5c2500`, 2026-09-13): ratchet GREEN at 5,869 tests, 5,869 passing,
5,761 distinct ids, **0 skipped**; parity **100.0%**, 1,967 of 1,967 ported upstream tests. Default
wave GREEN at three seeds, 6,000 rows a generator; `fuzzy` and `interactions` GREEN at 99991.
Fuzzy matching works end to end; `(?e)` and `(?b)` rank by COST (owner decision, upstream issue 470).
Phase 5 measured: 11 slices, 14 sessions (1.27), median 69.9M tokens.

**Phase 6 is AUTHORED - S44-S57, fourteen slices in `docs/plan/slices/` - and awaits the owner's
review. S44 (upstream sync to 2026.9.10) is next.** Three slices need the orchestrator first, each
says so under "Before launch": S44 the wheel into the oracle interpreter, S49 a `gh` snapshot of the
tracker, S55 Stryker installed and S56 the overnight chunk queue run.

**Before S44 launches, the process improvements agreed 2026-09-13 land as maintenance:** a
deadline file per sitting; a hook injecting remaining time and orchestrator messages into the
running session; rollback to the last green in-session commit rather than session start; the
heartbeat alarm keyed to the sitting; a per-test `[Timeout]`; a bounded `check-ratchet.ps1` run.

**Blockers:** none. **Known bugs in this port**, all on Phase 6's fix list with a slice each:
ledger 7 (S45); 12, 13, 9's port half (S46); 11, 14 (S47); 5's remaining door and any other shared
entry (S48); whatever the issue sweep reproduces (S49, S50).

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate). Controls:
`python tools/run-controls.py --slices S37,...,S43 --seeds 2`. Upstream 2026.9.10 for probes:
`.venvs/regex-2026.9.10`. Ledger: `docs/plan/upstream-reports/LEDGER.md`, 14 entries, nothing filed.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing pushed since Phase 4's close).
