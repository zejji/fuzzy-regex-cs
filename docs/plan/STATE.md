# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S47 IS A CHECKPOINT** (sitting 1, 2026-09-14) and its file is still in `slices/`. Ledger entry 11's
invariant is decided and pinned; two of its four mechanisms are fixed. Entry 14 is UNTOUCHED.
Ratchet GREEN at 5,948.

**Entry 11 turned out to be one defect class with FOUR mechanisms, not one bug with two doors.**
Upstream saves the fuzzy COUNTS as a block and unwinds the CHANGES item by item, so anything that
abandons a sub-attempt without backtracking through it desynchronises them. **A** (search restart)
and **B** (a partial match returning from inside a nested section) are FIXED: `start_match` clears
the change list, and `Match.FuzzyCounts` is tallied from the changes on a partial match only.
**C** (POSIX/BESTMATCH candidates) and **D** (a lookaround under `(?e)`) are NOT - the fix is to pair
the list with the counts at all 19 `PushFuzzyCounts`/`PopFuzzyCounts` sites, each needing a
"restore or merge" judgement. Reproductions with flags are in LEDGER entry 11.

**SITTING 2 STARTS WITH THE ORACLE, NOT WITH ENTRY 14.** The fix makes the default wave diverge on
39 rows (B) + 19 rows (A) per 18,000. No narrow predicate exists for the 19 - upstream's leaked
positions look exactly like a port position bug. The design: **a second recorded question**, upstream
asked the same row ANCHORED at the span it reported (`match(pos=start, endpos=end)`), where nothing
can leak; account for a divergence when this port equals that leak-free answer. Recorder field +
`OracleWave` + one `ExpectedDivergences` entry. Then entry 14, then S48.

**Two oracle tests are RED on purpose and both must stay red until the work above lands:**
`The_wave_agrees_with_upstream` (the family above, plus HEAD's own 15) and the NEW
`Our_own_change_positions_always_agree_with_our_own_counts`, which is red at seed 4242 on mechanism
D. That property checks THIS PORT ALONE over every match of every wave row, which is the only
instrument that can see C or D - both engines agree on them.

**THE DEFAULT WAVE WAS ALREADY RED AT HEAD, 15 untriaged rows**, unchanged by S47 and still wanting
their own slice: seed 7 74944, 85165, 100366; 4242 76664, 76681, 89364; 20260914 73704, 73996,
77515, 84933, 97786, 99121, 101977, 104530, 106411. Plus the POSIX `(?e)` count bug S46 found
(`interactions` seed 31337 row 3343).

**Blockers:** none. **Known bugs:** ledger 11's C and D, 14 (S47 sitting 2); 5's remaining door
(S48); the issue sweep (S49, S50); the POSIX `(?e)` count above.

**NEVER RUN CONTROL S46-B AGAINST THE SUITE** - its mutant hangs `dotnet run` past 600s at 19 GB.
Use `-Configuration Release`. **Delete `.scratch/control-waves/<generator>-*` for any generator whose
code a slice changed before running its controls.**

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate).
Ledger: `docs/plan/upstream-reports/LEDGER.md`, 15 entries, nothing filed.

**Owed maintenance, five items.** (1) `FOLD_TURKIC`'s share of the `case-folding` rotation is too
small. (2) `turkic-default-folding`'s predicate has one false positive no predicate can close.
(3) Five broken control sites. (4) PORTMAP's `_regex.c` line references are stale after the sync and
need an owner decision first. (5) `python tools/record-oracle.py --self-check` exits 1 on a
pre-S46 message.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing since Phase 4's close).
