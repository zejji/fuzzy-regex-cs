# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S47 IS CLOSED** (sitting 3, 2026-09-14). Suite 5,953, ratchet GREEN, default wave GREEN at three
seeds. Next in the queue is **S47b**, then S47c, S48.

**Sitting 3 fixed ledger entry 14**: PCRE2's positional recursion guard, ported with one deliberate
difference - it fails the PATH where PCRE2 fails the MATCH, so left recursion keeps its one-step
unrolling and `(?P<g1>(?:ab)?(?&g1)?)` answers (0, 4) where PCRE2 errors. `MatchState.ActiveCalls`
plus `OpenCalls` (key and sstack depth) and `Matcher.CloseCallsAbove`. The 1GB bound stays as the
backstop for the branching the guard cannot see and now has a test of its own. The
`INTERACTION_FUZZY_WRAPPERS` exclusion is lifted and paid for itself: drawn row 72179 was a real
divergence the guard closes.

**THE FIRST THING THE NEXT SESSION SHOULD DO: a blind pass over sitting 3's own fix.** The blind
review found a real defect - a `(*PRUNE)`/`(*SKIP)` inside an open call leaked a guard key and lost a
match upstream finds - and the fix for it (`OpenCalls`, `PopOpenCall`, `CloseCallsAbove`, six call
sites, the new `GroupCallTests` leak test) is UNREVIEWED. Shutdown landed 15 minutes after it went
green. Closing notes in `slices/done/S47-*.md` have the reproduction and all three controls.

**Two of S47's three controls are invisible to the wave**, which is a finding for S52: no generator
draws a backtracking verb inside a CALLED group inside an atomic group, lookaround or conditional,
so only the suite can see a regression in the guard's bookkeeping.

**The 6000-row three-seed gate is RED at 19 rows and takes ~6.5 minutes now** (it was 14 rows and
~1 minute). Proven not the guard's: the three saved waves consumed by the pre-guard engine give the
identical rows at seeds 7 and 4242 and one MORE at 20260914. The 5 extra rows are the RNG shift the
generator change caused, none holding a group call. Still untriaged, still want their own slice.
Plus the POSIX `(?e)` count bug S46 found (`interactions` seed 31337 row 3343).

**Blockers:** none. **Known bugs:** ledger 11's mechanisms C and D; 5's remaining door (S48); the
issue sweep (S49, S50); the POSIX `(?e)` count above.

**NEVER RUN CONTROL S46-B AGAINST THE SUITE** - its mutant hangs `dotnet run` past 600s at 19 GB.
Use `-Configuration Release`. **Delete `.scratch/control-waves/<generator>-*` for any generator whose
code a slice changed before running its controls.** **After editing `Matcher.cs` with a script, run
`dotnet csharpier format` on it or the build fails IDE0055 on lines you never touched.**

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate).
Ledger: `docs/plan/upstream-reports/LEDGER.md`, 15 entries, nothing filed.

**Owed maintenance, five items.** (1) `FOLD_TURKIC`'s share of the `case-folding` rotation is too
small. (2) `turkic-default-folding`'s predicate has one false positive no predicate can close.
(3) Five broken control sites. (4) PORTMAP's `_regex.c` line references are stale after the sync and
need an owner decision first. (5) `python tools/record-oracle.py --self-check` exits 1 on a
pre-S46 message.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing since Phase 4's close).
