# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S46 IS DONE** (sitting 2, 2026-09-14) and its file is in `slices/done/`. The POSIX-with-fuzzy
exclusion is LIFTED: `_describe_match` records `fuzzyCounts` and omits `fuzzyChanges` on a POSIX
pattern, and both sides render that row as `fuzzy=(s,i,d)[changes unavailable upstream]`. One guard,
because all four recorder doors funnel through that function. Ratchet GREEN at 5,947 tests.

**NEW KNOWN BUG IN THIS PORT, found by lifting it, minimised, NOT fixed - the next engine slice's
work.** `regex.compile(r'(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}', regex.POSIX)
.fullmatch('+ aBA')`: upstream counts `(0,1,1)`, this port `(1,1,1)` for the same span. Drop POSIX
and this port answers `(0,1,1)` too, so POSIX is losing an error count. Needs POSIX AND `(?e)`.
Hypothesis, unproven: `RestoreBestMatch` restores `FuzzyCounts`/`FuzzyChanges` but not
`state.TotalErrors` or `state.TotalCost`, which this port's ranking reads. No `ExpectedDivergences`
entry - this port is wrong, so it stays RED. It is `interactions` seed 31337 row 3343.

**THE DEFAULT WAVE IS RED AT HEAD AND WAS BEFORE S46 - 14 untriaged rows, re-measured this sitting
and unmoved.** 6000 rows, three seeds: 3 + 2 + 9. Seed 7: 74944, 85165, 100366. Seed 4242: 76664,
89364. Seed 20260914: 73704, 73996, 77515, 84933, 97786, 99121, 101977, 104530, 106411. (Sitting 1
said 15; that counted a HEAD row S46 itself retired.) **This is the S40a pattern and wants its own
slice**, now with the row 3343 bug beside it.

**Next: S47.** **S49** still needs a `gh` tracker snapshot.

**Blockers:** none. **Known bugs in this port:** ledger 11, 14 (S47); 5's remaining door (S48); the
issue sweep (S49, S50); the POSIX `(?e)` count above. Ledger entries 7, 12 and 9's port half are
CLOSED; 13's port half was never open.

**NEVER RUN CONTROL S46-B AGAINST THE SUITE.** Its mutant hangs `dotnet run --project
tests/FuzzyRegex.Tests` past 600s at 19 GB, and the hung host then blocks every later Debug build.
Use `-Configuration Release`. Against the WAVE it is safe.

**Delete `.scratch/control-waves/<generator>-*` for any generator whose code a slice changed before
running its controls** - `run-controls.py` reuses a wave on disk, and a stale one made S46-D read as
a weak control at two seeds of three until the files were deleted.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate).
Ledger: `docs/plan/upstream-reports/LEDGER.md`, 15 entries, nothing filed.

**Owed maintenance, five items.** (1) `FOLD_TURKIC`'s share of the `case-folding` rotation is too
small. (2) `turkic-default-folding`'s predicate has one false positive no predicate can close.
(3) Five broken control sites. (4) PORTMAP's `_regex.c` line references are stale after the sync and
need an owner decision first. (5) `python tools/record-oracle.py --self-check` exits 1 on "an
interpreter limit rather than a judgement about the pattern: was recorded as if it were upstream's
answer"; it predates S46.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing since Phase 4's close).
