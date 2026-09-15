# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 13).** Ratchet GREEN, **6119 / 6119 / 0 skipped,
6011 distinct ids, baseline 6011**; `dotnet build tests/FuzzyRegex.OracleTests -c Release` clean.
Sittings 11 and 12 were both killed before committing; sitting 13 applied the orchestrator's stash,
re-derived every claim in it and committed it. **The gate is 6 -> 3:** seed 7 74413 joined
`end-of-line-reads-a-skip-moved-slice`, seed 4242 76778 and 77119 joined
`bestmatch-walk-truncated-by-a-skip`, the first rows a GENERATOR has drawn into that family. No
engine code changed, no new gap test (all three joined families that already pin).

**FOR THE OWNER - S52 SHOULD BE SPLIT, and sitting 14 should not start judging without the ruling.**
The seed sweep ran for the first time (8 seeds, 2,000 rows a generator, 336,000 rows;
`pwsh -File tools/sweep-seeds.ps1 -SeedCount 8 -Count 2000 -MasterSeed 20260915` re-draws the same
eight) and it is **RED at all eight, 37 diverging rows** - interactions 21, verbs 5, fuzzy 4,
partial 4, partial-sliced 2, conditionals 1. S52's done-criterion is "no unjudged row" and a sweep at
fresh seeds is the instrument for making them, so as written S52 can never close. Recommended: close
S52 on the hardening tooling and generators it delivered, give the sweep's triage its own slice.
Tallies in `TestResults/oracle/sweep.jsonl`, waves under `TestResults/oracle/sweep-<seed>/`.

**STILL ON THE GATE, 3 rows.** Seed 7 75921 (sitting 9's, deliberately unpinned); seed 7 76160 (the
`bestmatch` family's OWN door rules it out - `(*PRUNE)` still times out at 300s, anchored door points
the wrong way); seed 20260915 104366, now scope item 7 of S52c. All four drawn rows re-run without a
gate: `python tools/probes/upstream-gate-drawn-skip-rows.py`, port half
`port-gate-drawn-skip-rows.ps1`.

**RUN THE DEFAULT WAVE BEFORE THE GATE, never after.** **`-Rows <file> -SkipRecord` IGNORES the
file.** **Probe a `verbs` or `partial-sliced` row PREFILTER-FREE.** **Paste the verifier brief - it
carries a do-not-use-git clause** (`docs/VERIFICATION.md`).

**Also owed on S52:** the `timeout` rows generator (needs S51's); the 20-seed sweep and 6000-row gate
(S57); the `pos`/`endpos`-versus-`codepointSlice` fix (own slice); long generators stay OFF the
default list. **Carried** (unchanged, full list in sitting 10's notes): the `_regex.c` citation
reconciliation, `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`, `record-oracle.py
--self-check`, `run-controls.py`, control sites S32-B/S38-A/S35-A/S29-A/D,
`upstream-reversed-overlapped-skip.py`'s prefilter-on cases, PORTMAP lines, `quantifiers-long`'s
filler margin - plus NEW: `oracle.yml`'s weekly sweep is red every Thursday until its verdict rule
tells a known family from a new one. **Open for the owner:** the S52 split; `slice_start` versus
`text_start` (ledger 24); `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
