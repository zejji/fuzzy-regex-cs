# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS A CHECKPOINT (2026-09-16, sitting 18). Ratchet GREEN, 6123 / 6123 / 0 skipped, 6015 distinct
ids, baseline 6015** - two new gap tests. `src/` is untouched this sitting.

**THE OWNER HAS RULED AND S52 IS NOT SPLIT AND NOT CAPPED** (orchestrator, 2026-09-15). "No unjudged
row" means the rows S52 has ALREADY produced: the sweep's 37 and the gate's 3, of which 104366 goes
to S52c/S52d and counts as judged. **Run no further sweeps inside S52** - the 20-seed sweep and the
6000-row gate are S57's. Multiple sittings are fine. Then close S52.

**FIFTEEN OF THE 37 ARE JUDGED. 22 TO GO**, and they are sitting 15's groups B, C, E, F and G less
the judged, plus group A's residue. Replay:
`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl` gives
**expected 15, diverge 22**. Sitting 18 judged group D's six: rows 25, 29, 36 into
`search-start-partial`, 13 and 22 into `partial-retry-carried-slice-forward`, 24 into
`partial-retry-reversed-slice`.

**THE LESSON SITTING 18 PAID FOR: A SHAPE IS NOT A CLASSIFICATION, AND A CAPPED SWEEP CAN NAME THE
RIGHT ANCHOR BY LUCK.** Sitting 15 filed all six as "`search-start-partial` shape"; they are three
families, and what separates them is a question neither existing door asks - whether upstream's own
search answer is producible by ANY anchored `match(pos, endpos, partial=True)` in the region.
`gate-divergence-doors.py` also stops its sweep after three hits, which finds the leftmost `pos`
forwards but NOT the highest `endpos` on a reversed row, and the reversed argument is about the
highest. Both are now in `tools/probes/upstream-partial-anchor-reachability.py`.

**SITTING 19's FIRST JOB: group C** - sweep rows 16, 26, 30 and 33, four rows of one shape
(overlapped scan, the verb moving a bound between matches) whose argument and probes the four
`overlapped-skip-*` entries and `skip-carried-slice-on-a-scan-with-no-walk` already carry. Take the
uncapped sweep with you. **Rows 4 and 15 are still the two nobody can place.**

**Carried** (full list in sitting 10's notes): `upstream-bestmatch-free-answer.py`'s unguarded
`fuzzy_changes` read; the `_regex.c` citation reconciliation; `port-tests/SKILL.md`'s stale
`FuzzyRegex.Search(...)`; `record-oracle.py --self-check`; `run-controls.py`; control sites
S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `quantifiers-long`'s filler margin; `oracle.yml`'s weekly
sweep verdict rule; the `pos`/`endpos`-versus-`codepointSlice` fix. **Open for the owner:**
`slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
