# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S47 IS A CHECKPOINT** (sitting 2, 2026-09-14) and its file is still in `slices/`. **This is its
second checkpoint, so sitting 3 must CLOSE it** - three stop the driver. Ratchet GREEN at 5,948.

**Sitting 2 closed the oracle problem sitting 1 left.** Entry 11's mechanisms A and B are now
accounted for: B by a predicate over upstream's own answer (its counts are componentwise no larger
and its positions are a per-kind PREFIX of ours - a truncated change stack), A by a new recorded
second question, `leakFreeFuzzy`, upstream asked the row again at `match(pos=start, endpos=end)`.
**Default wave GREEN at three seeds**; the 6000-row three-seed gate went 24/23/25 diverging to
3/2/9, and those 14 are exactly HEAD's own untriaged rows.

**SITTING 3 IS ENTRY 14 AND NOTHING ELSE.** The port already bounds the blowup
(`InvalidOperationException: ... backtracking stack exceeded its 1GB limit`, 0.25-1.52s) and its
three tests now name that exception instead of accepting any. What is LEFT is the correctness fix
PCRE2 named: a match-time guard on re-entering a recursion at a subject position it is already at
(`PCRE2_ERROR_RECURSELOOP`, measured by `tools/probes/pcre2-bounds-an-unbounded-recursion.py`).
**It is not a transcription** - PCRE2's guard is positional, not a progress proof, so a shape
re-entering one position where a different branch would still have succeeded changes answer, and
that must be measured against a wave. Then lift `INTERACTION_FUZZY_WRAPPERS`' exclusion of a
self-recursive call round a fuzzy section, re-run the wave, close the slice.

**THE 14 UNTRIAGED GATE ROWS ARE STILL UNTRIAGED and still want their own slice** (they predate
S47): seed 7 74944, 85165, 100366; 4242 76664, 89364; 20260914 73704, 73996, 77515, 84933, 97786,
99121, 101977, 104530, 106411. Plus the POSIX `(?e)` count bug S46 found (`interactions` seed 31337
row 3343). `Our_own_change_positions_always_agree_with_our_own_counts` is RED at seed 4242 at 6000
rows on mechanism D, on purpose, until C and D are fixed - that is S48's neighbour, not this slice.

**Blockers:** none. **Known bugs:** ledger 11's C and D; 5's remaining door (S48); the issue sweep
(S49, S50); the POSIX `(?e)` count above.

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
