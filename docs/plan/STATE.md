# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 14).** Ratchet GREEN, **6119 / 6119 / 0 skipped,
6011 distinct ids, baseline 6011** - unchanged, because the one new test is in
`FuzzyRegex.OracleTests` (24 tests now, was 23) which the ratchet does not build. Oracle Release
build clean; tool tests 81/81.

**THE OWNER'S RULING ON SPLITTING S52 IS STILL OWED, and sitting 14 judged nothing.** The case is
unchanged from sitting 13: S52's done-criterion is "no unjudged row", the seed sweep it delivered is
the instrument for producing new ones, and the two fight each other. The 37 sweep rows are still
untriaged (`TestResults/oracle/sweep.jsonl`, waves under `sweep-<seed>/`). Recommended: close S52 on
the hardening tooling and generators, give the sweep's triage its own slice.

**Sitting 14 took the last untouched Scope bullet instead: the TIMEOUT ROWS, and they are done.** A
per-row `timeout` budget (`OracleRow.Timeout`) is now the discriminator between a `timeout` outcome
that is skipped - the blanket 10s, a fact about the recording machine - and one that is COMPARED. Ten
shapes, measured still running at **20x** the 0.25s budget on both engines (80 of 80 shape-by-operation
cells each). `timeout` is ON the default generator list, capped at 80 rows however large `-Count` is.
Probes: `tools/probes/timeout-row-margin.py` (and `--operations`, `--knee`, `--rejected`) and
`timeout-row-margin.ps1`. **Upstream is NOT vulnerable to the textbook `(a+)+$` shapes** - see the
sitting's notes before editing `TIMEOUT_SHAPES`.

**STILL ON THE GATE, 3 rows** (unchanged): seed 7 75921 (sitting 9's, deliberately unpinned); seed 7
76160 (the `bestmatch` family's OWN door rules it out); seed 20260915 104366, scope item 7 of S52c.
Re-run without a gate: `python tools/probes/upstream-gate-drawn-skip-rows.py`, port half
`port-gate-drawn-skip-rows.ps1`.

**RUN THE DEFAULT WAVE BEFORE THE GATE, never after.** **`-Rows <file> -SkipRecord` IGNORES the
file.** **Probe a `verbs` or `partial-sliced` row PREFILTER-FREE.** **Paste the verifier brief - it
carries a do-not-use-git clause** (`docs/VERIFICATION.md`).

**Also owed on S52:** the 20-seed sweep and 6000-row gate (S57); the `pos`/`endpos`-versus-
`codepointSlice` fix (own slice); long generators stay OFF the default list. **Carried** (full list in
sitting 10's notes): the `_regex.c` citation reconciliation, `port-tests/SKILL.md`'s stale
`FuzzyRegex.Search(...)`, `record-oracle.py --self-check`, `run-controls.py`, control sites
S32-B/S38-A/S35-A/S29-A/D, `upstream-reversed-overlapped-skip.py`'s prefilter-on cases, PORTMAP lines,
`quantifiers-long`'s filler margin, `oracle.yml`'s weekly sweep verdict rule. **Ledger 24 is RULED**
(Option B); `S52d-reversed-partial-slice-start.md` is queued after S52c. **Open for the owner:** the
S52 split; `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
