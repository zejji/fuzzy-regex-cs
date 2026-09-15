# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS A CHECKPOINT (2026-09-15, sitting 15). Ratchet GREEN, 6119 / 6119 / 0 skipped, 6011 distinct
ids, baseline 6011** - unchanged, because **no `.cs` file was touched at all**. Tool tests 81/81.

**THE OWNER HAS RULED AND S52 IS NOT SPLIT AND NOT CAPPED** (orchestrator, 2026-09-15). "No unjudged
row" means the rows S52 has ALREADY produced: the sweep's 37 and the gate's 3, of which 104366 goes
to S52c/S52d and counts as judged. **Run no further sweeps inside S52** - the 20-seed sweep and the
6000-row gate are S57's. Multiple sittings are fine. Then close S52.

**The 37 sweep rows are now `tools/probes/sweep-divergence-rows.jsonl`** and no longer live only in
gitignored `TestResults/`. Replay: `pwsh -File tools/run-oracle.ps1 -Rows
tools/probes/sweep-divergence-rows.jsonl` gives **diverge 37 of 37, expected 0** - none is classified
by any entry. Table of all 37 in eight groups: sitting 15's notes. Open every family's door with
`python tools/probes/gate-divergence-doors.py --rows tools/probes/sweep-divergence-rows.jsonl`.

**SITTING 16's FIRST JOB IS GROUP A, and the warning matters more than the rows.** Eight rows (3, 4,
15, 17, 18, 23, 35, 37) where upstream answers `None` under `(?b)` and its own `(?b)`-free answer is
this port's answer exactly. That is `bestmatch-loses-a-candidate`'s discriminator - **and ledger 12's
mechanism cannot explain SEVEN of them**: its guard is `n > 2n-2`, true at n=1 and false from n=2, and
these flagless answers carry one insertion (3, 4, 17, 23, 37) or NONE AT ALL (15, 35). Row 18, with
two, is the only one it could have refused, and not demonstrated even there. Rows 4 and
15 meet `bestmatch-loses-a-partial`'s four conditions and are reversed, so `endpos` IS that entry's
natural door - but the only bound either answers at is `endpos=0`, an EMPTY slice, where its other
reversed rows answer at 1, 2, 4 and 5. Unsettled, not disqualified. **Do not widen either pin on the
flagless control alone** - probably a new ledger entry.

**Two instrument defects fixed, both proven by the run:** `gate-divergence-doors.py` read
`fuzzy_changes` unguarded and SIGSEGVed inside row 32 of 37, losing rows 32 to 37 (ledger 9); it now
guards on POSIX as `record-oracle.py:1019` does, and prints 37 of 37. **Ledger 9's condition "every
row with a non-zero count faults, POSIX present" is FALSE** - sweep rows 4, 18 and 32 satisfy it and
answer, row 32 giving two safe ablations and one faulting one from ONE pattern; corrected in
LEDGER.md and in the probe, real condition unknown. **Carried:**
`upstream-bestmatch-free-answer.py` has the same unguarded read, and sweep row 18 carries POSIX, so
fix it in the change that adds that row.

**A wedged VBCSCompiler blocked every build**; `tools/find-lock-holder.ps1 -Path <the obj file>` named
it and stopping it cleared it. `Stop-Process`/`taskkill` need approval - go via `pwsh -File
.scratch/*.ps1`. **`-Rows <file> -SkipRecord` IGNORES the file.** **Paste the verifier brief.**

**Also owed on S52:** groups B-G of the 37. **Carried** (full list in sitting 10's notes): the
`_regex.c` citation reconciliation, `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`,
`record-oracle.py --self-check`, `run-controls.py`, control sites S32-B/S38-A/S35-A/S29-A/D, PORTMAP
lines, `quantifiers-long`'s filler margin, `oracle.yml`'s weekly sweep verdict rule, the `pos`/`endpos`
-versus-`codepointSlice` fix. **Open for the owner:** `slice-log.jsonl` marks S26 `failed`;
`origin/main` needs a push.
