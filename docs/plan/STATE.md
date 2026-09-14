# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S48 IS CLOSED** (2026-09-14, one sitting). Suite 5,955, ratchet GREEN, default wave GREEN at three
seeds. Next in the queue is **S48b**, authored by this slice; then S49.

**The slice's premise was stale.** Entry 5's "fifth door still inherited" was closed by S40b before
S48 began. **There is a SIXTH door and this port shared it**: `do_best_fuzzy_match`'s walk guard
(`:17625`) reads the LIVE slice, `init_match` never resets it, and `start_pos` is the candidate's own
match start - so a `(*SKIP)` that consumed anything ends the `(?b)` walk on its first successful
candidate. `(?b)(?:a(*SKIP)b){e<=1}` over `'axab'` gave a one-error match where the pattern's own
`match(2)` finds a perfect one. Fixed in `DoBestFuzzyMatch` (restore per candidate, both passes) -
**not in `InitMatch`, where the ledger's own proposed fix puts it, because `(?e)` and the widened
fallback narrow the slice deliberately.**

**The inventory is the slice's other half: 13 of the 15 ledger entries are closed.** The table is in
the closing notes with a proof per row. Three items left, one mechanism seen three ways -
**ledger 11 C and D, and ledger 9's POSIX+`(?e)` count bug, which is THIS PORT's and still
reproduces** (`(0,5)` counts `(1,1,1)` with POSIX, `(0,1,1)` without). That is **S48b**, authored,
with ROADMAP and spec amendment 25.

**S48-A fires ZERO** at four seeds over 24,000 `interactions` rows - mutated and unmutated identical
- while the same fault moves upstream on 1,861 of 11,340 hand-built shapes. A measured generator gap,
handed to S52. **S31-A/B/C repaired** (S40b had moved their text); they now fire 10-68 of 600.

**Two blind passes: the first raised 4 and ALL 4 reproduced, the second raised 1 and it reproduced.**
All fixed. The sharpest was a negative control with no `(?b)` in it, which never entered the function
the slice changed - a control routed to the wrong door looks exactly like a control that passes.

**The independent verifier re-ran every number and returned two DIFFERENT and two COULD NOT RUN; all
four are corrected in the notes, not kept.** A control table read off a truncated `tail` was one of
them - read the whole output.

**Untriaged (unchanged):** the 6000-row everything gate at 99991 RED at 4 of 126,000; the three-seed
6000-row gate's 19, none of which can be this slice's (no diverging row carries BESTMATCH or an
inline `(?b)`, and the change is reachable only through it); the promoted fold sweep's 30
`fold_case(FULL)` mismatches, NOT measured.

**Owed maintenance:** `FOLD_TURKIC`'s share of the `case-folding` rotation; **two** broken control
sites left; S35-A and S29-A/D are thin (S35-A fires at 1 seed of 6 - numbers in S48's notes);
PORTMAP's `_regex.c` line references stale after the sync; `record-oracle.py --self-check` exits 1 on
a pre-S46 message. **Still open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main`
needs a push.
