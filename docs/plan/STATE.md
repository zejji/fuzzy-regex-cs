# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 7) - the SEVENTH.** Ratchet GREEN, **6111 / 6111 /
0 skipped, baseline 6001 -> 6003**. Tool tests 81/81. Default wave GREEN at three seeds.

**Sitting 7 cleared everything STATE.md owed.** The two `partial-long` rows are judged: both are
`search-start-partial` and the port is right - upstream's partial covers the whole searched region,
its own `match` over that span answers None, and the anchor sweep, the verb-free spelling and
`(*PRUNE)` all give this port's answer. They are rows 6 and 7 of `_searchStartElsewhereRows`, two
new permanent gap tests, and two cases in the family's own probe. **Both delta-debug from 3,363 and
18,759 characters down to THREE astral codepoints**, so the long generators found them as an
alphabet widening, not a length one. The blind review (4 findings, 4 fixed), the second pass over
the fix delta (1 finding, fixed) and the independent verifier all ran; the padding-side negative
control, owed since sitting 4, is run, committed as a probe and fires at two seeds.

**SITTING 8'S FIRST JOB: the 6000-row three-seed gate is RED - 29 unjudged divergences.** Run here
for the first time (`pwsh -File tools/run-oracle.ps1 -Count 6000`, ~9 min): 14 + 5 + 10 of 126,000
per seed. The same three seeds at 300 rows a generator are GREEN, so this is row count finding what
seed count does not. Do NOT transcribe them - re-run the gate, then
`python tools/probes/gate-divergence-triage.py` prints the table from the reports it leaves.
Measured shape: `partial` 11, `interactions` 10, `partial-sliced` 4, `verbs` 2, `conditionals` 1,
`fuzzy` 1; 21 carry a `(*SKIP)`, 16 are reversed, 9 fuzzy. **Start with seed 7 row 118133**, the one
row where UPSTREAM errors (`IndexError: list index out of range` on a `subf`) rather than answering
- but check S40a's ruling on the `{1[2]}` sibling first, which judged that error legitimate.

**Also still owed on S52:** the `timeout` rows generator (scope item, needs S51's `timeout`
comparison) and a recorded **20-seed sweep run** - `sweep-seeds.ps1` and the `oracle.yml` Thursday
cron both exist and are committed, only the run is missing. Earlier STATE.md text calling the CI job
untouched was stale. The four long generators stay OFF the default `-Generator` list: 11 of their 13
divergences are this port exceeding the row timeout in Release, a Phase 7 performance question.

**Owed, carried:** the repo-wide `_regex.c` citation reconciliation - `:14545`/`:14551`/`:20903`/
`:18160` are stale against the 2026.9.10 pin, `:14553`/`:14555`/`:20927-20928`/`:18159` are right,
and a sweep cannot follow the count. Never write a tracked file from a Python helper on Windows
without `newline=""`, and build `tests/FuzzyRegex.OracleTests` too.
`.claude/skills/port-tests/SKILL.md` still teaches `FuzzyRegex.Search(...)`;
`record-oracle.py --self-check` exits 1 (pre-existing since S43); `tools/run-controls.py` cannot
measure a control that mutates the recorder (S42-2A); broken control sites S32-B and S38-A; S35-A and
S29-A/D thin; PORTMAP `_regex.c` lines stale. `quantifiers-long`'s base-alphabet filler rests on a
much thinner margin than sitting 4 recorded (28 -> 32, not 0 -> 32) and is worth re-deciding.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
