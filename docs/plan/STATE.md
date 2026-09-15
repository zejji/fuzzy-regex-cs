# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS A CHECKPOINT (2026-09-15, sitting 16). Ratchet GREEN, 6120 / 6120 / 0 skipped, 6012 distinct
ids, baseline 6012** - one new test. `Matcher.cs`'s only change is a comment.

**THE OWNER HAS RULED AND S52 IS NOT SPLIT AND NOT CAPPED** (orchestrator, 2026-09-15). "No unjudged
row" means the rows S52 has ALREADY produced: the sweep's 37 and the gate's 3, of which 104366 goes
to S52c/S52d and counts as judged. **Run no further sweeps inside S52** - the 20-seed sweep and the
6000-row gate are S57's. Multiple sittings are fine. Then close S52.

**GROUP A IS SETTLED AND IT WAS TWO DEFECTS. Five of the 37 rows are judged and pinned** into
`bestmatch-loses-a-candidate` (sweep rows 3, 17, 20, 23, 37). Replay: `pwsh -File
tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl` gives **expected 5, diverge 32**.

**What settled it is a PORT-SIDE control, and sitting 17 should reach for the same instrument first.**
This port has already fixed both candidate mechanisms, so each fix goes back one line at a time and
the rows sort themselves. Control A (restore upstream's doubled `END_FUZZY` term) moves exactly rows
3, 17, 20, 23, 37. Control B (delete the two running-total lines from `RestoreBestMatch`) moves
exactly rows 10, 12, 18, 35. Both are written out in the sitting-16 notes with their exact tallies.
Reasoning from a row's SHAPE is what went wrong three sittings running: row 20 has none of group A's
signature and is group A's defect, and four rows that have the signature are not.

**SITTING 17's FIRST JOB: pin rows 18 and 35, then look at 12 and 10.** All four are control B's -
the stale running totals after a POSIX restore. 18 and 35 are the STRONGER reproduction of ledger
entry 16: `None` under POSIX-and-BESTMATCH together, the same match back when either flag alone
goes, on a plain `fullmatch`/`match` with no scan (`python
tools/probes/upstream-bestmatch-sweep-group-a.py` prints every ablation for those two, and for the
open 4 and 15; rows 10 and 12 are not in it). **12 is POSIX and BESTMATCH too; 10 carries POSIX with
no BESTMATCH at all**, so it is entry 9's family rather than 16's. The open question for 18 and 35 is
which entry hosts them: `posix-fuzzy-contradicts-its-own-flagless-answer` keys on `posixFreeOutcome`
AND on this port's answer matching it, and row 18's posix-free answer carries change positions the
drawn side has none of (ledger 9). Check that before choosing.

**Rows 4 and 15 are open and neither control moves them.** Both `(?b)(?r)` partial searches whose
flagless answer is a PARTIAL - entry 13's shape - and sitting 15's `endpos=0` worry stands.

**Also owed on S52:** the other 28 rows (sitting 15's groups B, C, D, F, G less the judged). **Carried**
(full list in sitting 10's notes): `upstream-bestmatch-free-answer.py`'s unguarded `fuzzy_changes`
read, which sweep row 18 would trip; the `_regex.c` citation reconciliation; `port-tests/SKILL.md`'s
stale `FuzzyRegex.Search(...)`; `record-oracle.py --self-check`; `run-controls.py`; control sites
S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `quantifiers-long`'s filler margin; `oracle.yml`'s weekly
sweep verdict rule; the `pos`/`endpos`-versus-`codepointSlice` fix. **Open for the owner:**
`slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
