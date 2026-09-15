# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 10) - the TENTH.** Ratchet GREEN, **6119 / 6119 /
0 skipped, 6011 distinct ids, baseline 6011**. Default wave GREEN at three seeds. **Sitting 10
judged two of the eight gate divergences and the 6000-row gate is 8 -> 6** (3 / 2 / 1 at seeds
7 / 4242 / 20260915; expected 259 -> 261). No engine code changed. Seed 4242 row 119927 joined
`overlapped-skip-stale-slice-reversed` through a NEW second arm - upstream's own stepwise walk, for
a row whose match count is unchanged and only a CAPTURE'S END moved. Seed 20260915 row 74889 is a
new entry, `reversed-skip-invents-a-match`: upstream's `(*SKIP)` finds a complete match its own
`(*PRUNE)` and verb-free spellings both miss, and pruning cannot create a match.

**FOR THE OWNER, and sitting 11 should not touch row 104366 until this is answered: it is NOT pinned
because THIS PORT is inconsistent too.** Upstream holds two rules for a reversed partial that runs
out of text - node handlers read `text_start` (0), `search_start` reads `slice_start`
(`_regex.c:18442`, `:6747`, `:8400`) - and so does this port: over the two probes' 33 cells the
engines differ on 10, 3 of them the port's own way. Probable reading: `text_start = 0` is the
`^`/`\A` open-start rule and the partial handlers reuse it for a different question; if so it is an
INHERITED BUG to fix (some thirty sites), not a divergence to pin. Ledger entry 24; both probes are
committed as `tools/probes/{upstream,port}-reversed-partial-ignores-the-slice-start.*`.

**SITTING 11'S FIRST JOB: the five remaining `(*SKIP)`-family rows** - seed 7 74413, 76160 and
75921, seed 4242 76778 and 77119. Sitting 8's table still describes four of them; 75921 is sitting
9's, measured and deliberately unpinned. Regenerate with
`pwsh -File tools/run-oracle.ps1 -Count 6000` then `python tools/probes/gate-divergence-doors.py`.

**RUN THE DEFAULT WAVE BEFORE THE GATE, never after** (one `report-<seed>.txt` per seed whatever the
row count). **`-Rows <file> -SkipRecord` IGNORES the file**; a rows control is `-Rows <file>` alone.
**Probe a `verbs` or `partial-sliced` row PREFILTER-FREE** or upstream gives this port's answer.
**The verifier brief carries a do-not-use-git clause - paste it** (`docs/VERIFICATION.md`).

**Also owed on S52:** the `timeout` rows generator (needs S51's `timeout`), the recorded 20-seed
sweep run, the `pos`/`endpos`-versus-`codepointSlice` fix (its own slice). Long generators stay OFF
the default `-Generator` list. **Carried:** the repo-wide `_regex.c` citation reconciliation
(`:14545`/`:14551`/`:20903`/`:18160` stale, `:14553`/`:14555`/`:20927-20928`/`:18159` right); never
write a tracked file from a Python helper on Windows without `newline=""`, and re-escape `\uXXXX`
after an edit, because the editing tool resolves them; `port-tests/SKILL.md` still teaches
`FuzzyRegex.Search(...)`; `record-oracle.py --self-check` exits 1 on the interpreter-limit guard
(pre-existing since S43); `tools/run-controls.py` cannot measure a control that mutates the
recorder; broken control sites S32-B and S38-A; S35-A and S29-A/D thin;
`upstream-reversed-overlapped-skip.py`'s three original `verbs` cases compile WITH the prefilter, so
two entries' 2026-09-12 facts are untested prefilter-free; PORTMAP `_regex.c` lines stale;
`quantifiers-long`'s filler margin 28 -> 32, worth re-deciding.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
