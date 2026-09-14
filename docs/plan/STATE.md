# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S46 IS A CHECKPOINT** (sitting 1, 2026-09-14). Ledger entry 12 is FIXED - the `END_FUZZY`
backtrack arm's trailing-insertion guard double-counted, so `(?b)` lost any match needing two or
more trailing insertions **at every budget**, not just below 2n-1 as the entry claimed. Entries 13
and 9's port half needed no work: both were already closed by S43, verified not assumed. Ratchet
GREEN at 5,947 tests, 5,947 passing, 0 skipped.

**What is LEFT, and why the slice file stays in `slices/`: the POSIX-with-fuzzy exclusion is not
lifted.** The slice's own gate fails - `record-oracle.py --rows` over `(?p)(?:abc){e<=1}` on `'axc'`
exits 139 and writes nothing. But the cheap way out IS measured and written up in ledger entry 9:
only `fuzzy_changes` faults, `fuzzy_counts` is safe, and `_describe_match` reads the changes only
when the counts are non-zero - so omit `fuzzyChanges` on a POSIX row and the faulting access is
never touched. Needs recorder + `OracleWave` + `OracleComparer` + the generator changed together,
with `finditer`, `sub` and `split` guarded too, because a missed path kills the wave.

**THE DEFAULT WAVE IS RED AT HEAD AND HAS BEEN SINCE BEFORE S46 - 15 untriaged rows.** Proven by
re-consuming the identical waves with HEAD's engine in `.claude/worktrees/s46-head`: HEAD gives
3 + 2 + 10 at seeds 7 / 4242 / 20260914 at 6000 rows; with S46 the same waves give 3 + 2 + 9.
Seed 7: 74944, 85165, 100366. Seed 4242: 76664, 89364. Seed 20260914: 73704, 73996, 77515, 84933,
97786, 99121, 101977, 104530, 106411. **This is the S40a pattern again and wants its own slice.**
S45 recorded the wave green at 6300 rows with `expected` 4/1/2; the full default list gives 63/67/42
at 6000, so that figure was not the full list. Rule 7a needs a row-count half (DECISIONS).

**NEVER RUN CONTROL S46-B AGAINST THE SUITE.** Its mutant HANGS `dotnet run --project
tests/FuzzyRegex.Tests` - 21 seconds becomes past 600, at 19 GB resident - and the hung host then
holds `bin/Debug/.../FuzzyRegex.dll` and blocks every later Debug build. That happened this sitting
and the driver's allowlist has no `Stop-Process` or `taskkill` to clear it; the way round is
`-Configuration Release`, which the ratchet and the oracle both accept. Against the WAVE the same
control is safe and takes 12 seconds.

**Next: finish S46** (POSIX half only), then S47. **S49** still needs a `gh` tracker snapshot.

**Blockers:** none. **Known bugs in this port:** ledger 11, 14 (S47); 5's remaining
door (S48); the issue sweep (S49, S50). Entries 7, 12 and 9's port half are CLOSED; 13's port half
was never open.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate).
Ledger: `docs/plan/upstream-reports/LEDGER.md`, 15 entries, nothing filed.

**Owed maintenance, five items.** (1) `FOLD_TURKIC`'s share of the `case-folding` rotation is too
small. (2) `turkic-default-folding`'s predicate has one false positive no predicate can close.
(3) **Five** broken control sites, not six - S46 fixed S42-2G, whose `before` quoted the line this
slice changed. (4) PORTMAP's `_regex.c` line references are stale after the sync and need an owner
decision first. (5) NEW, found by S46's blind review and confirmed against HEAD:
`python tools/record-oracle.py --self-check` exits 1 on "an interpreter limit rather than a judgement
about the pattern: was recorded as if it were upstream's answer". It predates S46.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing since Phase 4's close).
