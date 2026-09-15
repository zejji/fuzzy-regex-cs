# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 8) - the EIGHTH.** Ratchet GREEN, **6113 / 6113 /
0 skipped, baseline 6003 -> 6005**. Tool tests 81/81. Default wave GREEN at three seeds.

**Sitting 8 did STATE.md's first job: the 29 gate divergences. Sixteen are judged, classified and
pinned, and the 6000-row three-seed gate is 29 -> 13** (6 / 3 / 4 at seeds 7 / 4242 / 20260915;
expected 238 -> 254). No engine code changed - all sixteen are upstream diverging in families this
port is already judged right about: 6 `search-start-partial`, 6 `partial-retry-reversed-slice`,
2 `partial-retry-carried-slice-forward` (whose symptom they WIDEN - the spans differ, where the
entry said they agree), 2 `bestmatch-loses-a-candidate`.

**SITTING 9'S FIRST JOB: the thirteen remaining rows.** The slice file's sitting-8 notes carry a
table with every door already put to each one - do not re-derive it. Three look Turkic (seed 7
118133, seed 20260915 75528 and 88716), three look like fuzzy change-position accounting (seed 7
74345, 74510, 75921), so expect to close them in groups. Two are named as deliberately NOT pinned
(seed 4242 76778 and 77119) and why. Regenerate with `pwsh -File tools/run-oracle.ps1 -Count 6000`
then `python tools/probes/gate-divergence-doors.py`.

**RUN THE DEFAULT WAVE BEFORE THE GATE, never after.** `run-oracle.ps1` writes one
`report-<seed>.txt` per seed whatever the row count, so a 300-row run overwrites the gate's reports
and the thirteen rows stop being regenerable.

**New this sitting:** `tools/probes/gate-divergence-doors.py`, which puts every judged family's own
control to every diverging row in one run, reading the row from the wave and this port's answer from
the report so nothing is transcribed. And a recorder fix: `--rows` read a recorded row's slice in the
wrong units, so a wave row fed back recorded a DIFFERENT SLICE. `codepointSlice` wins now; the
residual sharp edge (a slice cut made in `pos`/`endpos` is ignored) is in the code and wants its own
slice to close.

**Also still owed on S52:** the `timeout` rows generator (needs S51's `timeout` comparison) and a
recorded 20-seed sweep run. The four long generators stay OFF the default `-Generator` list.

**Owed, carried:** the repo-wide `_regex.c` citation reconciliation (`:14545`/`:14551`/`:20903`/
`:18160` stale, `:14553`/`:14555`/`:20927-20928`/`:18159` right). Never write a tracked file from a
Python helper on Windows without `newline=""`. `.claude/skills/port-tests/SKILL.md` still teaches
`FuzzyRegex.Search(...)`; `record-oracle.py --self-check` exits 1 on the interpreter-limit guard
(pre-existing since S43); `tools/run-controls.py` cannot measure a control that mutates the recorder;
broken control sites S32-B and S38-A; S35-A and S29-A/D thin; PORTMAP `_regex.c` lines stale;
`quantifiers-long`'s filler margin is 28 -> 32, worth re-deciding.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push. Commit
`5eabf44` (a concurrent session) swept this sitting's three `DECISIONS.md` lines into its own commit.
