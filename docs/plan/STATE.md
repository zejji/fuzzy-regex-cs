# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS A CHECKPOINT (2026-09-15, sitting 17). Ratchet GREEN, 6121 / 6121 / 0 skipped, 6013 distinct
ids, baseline 6013** - one new test. `src/` is untouched this sitting.

**THE OWNER HAS RULED AND S52 IS NOT SPLIT AND NOT CAPPED** (orchestrator, 2026-09-15). "No unjudged
row" means the rows S52 has ALREADY produced: the sweep's 37 and the gate's 3, of which 104366 goes
to S52c/S52d and counts as judged. **Run no further sweeps inside S52** - the 20-seed sweep and the
6000-row gate are S57's. Multiple sittings are fine. Then close S52.

**NINE OF THE 37 ARE JUDGED. 28 TO GO, and they are sitting 15's groups B, C, D, F and G less the
judged.** Replay: `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl`
gives **expected 9, diverge 28**. Judged: rows 3, 17, 20, 23, 37 into `bestmatch-loses-a-candidate`
(sitting 16) and rows 10, 12, 18, 35 into `posix-fuzzy-contradicts-its-own-flagless-answer`
(sitting 17).

**THE LESSON SITTING 17 PAID FOR, AND IT IS THE SAME ONE AS SITTING 16's IN A NEW COSTUME: ASK
UPSTREAM, DO NOT READ THE FLAGS.** Sitting 16 had rows 12, 18 and 35 down as ledger 16's
POSIX-and-BESTMATCH conjunction because all three carry both flags. **Row 12 is not**: deleting its
`(?b)` leaves upstream's answer character for character, so POSIX alone moves it and it is entry 9's
family with row 10. The ablation costs three seconds and
`python tools/probes/upstream-bestmatch-sweep-group-a.py` now carries all six rows the two port-side
controls separated. One control reaching four rows is not evidence that the four are one defect -
`RestoreBestMatch`'s two running totals serve both arms.

**SITTING 18's FIRST JOB: pick the next group from sitting 15's table and judge it the same way** -
ablate upstream, then pin. **Rows 4 and 15 are still the two nobody can place**: both `(?b)(?r)`
partial searches whose flagless answer is a PARTIAL (entry 13's shape), neither control moves
either, and sitting 15's `endpos=0` worry stands. Consider taking them with a fresh instrument
rather than a fourth pass of the same reasoning.

**Carried** (full list in sitting 10's notes): `upstream-bestmatch-free-answer.py`'s unguarded
`fuzzy_changes` read; the `_regex.c` citation reconciliation; `port-tests/SKILL.md`'s stale
`FuzzyRegex.Search(...)`; `record-oracle.py --self-check`; `run-controls.py`; control sites
S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `quantifiers-long`'s filler margin; `oracle.yml`'s weekly
sweep verdict rule; the `pos`/`endpos`-versus-`codepointSlice` fix. **Open for the owner:**
`slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
