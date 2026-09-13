# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S44 IS DONE** (sitting 2, 2026-09-13): the pin is **2026.9.10**, commit `7dd71c1`, byte-identity
proven against the wheel raw and the sdist after line-ending folding. Issues 611, 612 and 613
ported; 614 needed nothing. Ratchet GREEN at 5,877 tests, 5,877 passing, 0 skipped. The sync found
a **regression in 2026.9.10 itself** - issue 613's clamp loses a partial match that 2026.8.12 and
PCRE2 both find - pinned, ledgered as entry 15, not filed. Entry 10 CLOSED. Two blind passes, seven
findings, all seven reproduced and fixed, none a port defect.

**ORCHESTRATOR: two processes need killing before any oracle work runs again.**
`FuzzyRegex.OracleTests` PIDs **34428** and **26696** - S42-1B's timed-out consumers, spinning since
22:50 - hold `tests/FuzzyRegex.OracleTests/bin/Debug/net10.0/FuzzyRegex.dll`. Nothing else is
blocked. Once they are gone, the seven controls S44 could not measure are one command:
`python tools/run-controls.py --ids S42-1C,S42-2A,S42-2B,S42-2C,S42-2D,S42-2E,S42-2F --seeds 2`.

**Two sittings have now been lost to the same cause, and it is not a hand edit.** Control S42-1B
mutates `DoBestFuzzyMatch`'s bound to `fewestErrors`, which HANGS rather than diverging;
`run-controls.py` restores in a `finally` a kill never reaches, so a killed control run leaves it
applied and the next suite run spins for ever. **A recovering session's first move is
`git diff src/` read against `tools/controls.json`.** See DECISIONS 2026-09-13, four entries.

**Next: S45** (full case fold of U+0130, ledger entry 7). Nothing in S44 blocks it, and S45 needs
no oracle run to start. **S46-S48** then clear the remaining known bugs; **S49** needs a `gh`
snapshot of the tracker from the orchestrator before it launches.

**Blockers:** none for S45. **Known bugs in this port**, each with a slice: ledger 7 (S45); 12, 13,
9's port half (S46); 11, 14 (S47); 5's remaining door (S48); the issue sweep (S49, S50).

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate) - blocked
until the two PIDs die. Ledger: `docs/plan/upstream-reports/LEDGER.md`, 15 entries, nothing filed.

**Owed maintenance, now three sittings' worth of evidence:** `run-controls.py` must restore on a
kill, kill the consumer's process TREE on timeout, and report a broken control and carry on (six
sites are broken - five unresolvable, S42-2G's mutant will not compile). PORTMAP's `_regex.c` line
references are stale after the sync and need an owner decision first.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing since Phase 4's close).
