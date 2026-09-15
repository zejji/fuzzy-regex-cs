# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 9) - the NINTH.** Ratchet GREEN, **6117 / 6117 /
0 skipped, 6009 distinct ids, baseline 6005**. Default wave GREEN at three seeds.

**Sitting 9 did STATE.md's first job: five of the thirteen gate divergences are judged, classified
and pinned, and the 6000-row three-seed gate is 13 -> 8** (3 / 3 / 2 at seeds 7 / 4242 / 20260915;
expected 254 -> 259). No engine code changed. Two are ledger entry 11's counts-versus-changes class
(row 74510 into `atomic-group-leaks-a-change-position`; row 74345 into a NEW entry, mechanism **G**,
where upstream's change list is of the wrong KIND for its own counts). Three are the Turkic `T`
rows (118133 and 75528 into `turkic-default-folding-without-spans`; 88716 into a NEW entry,
`turkic-default-folding-read-by-a-lookaround`).

**SITTING 10'S FIRST JOB: the eight remaining rows.** Sitting 8's table still describes seven of
them correctly - do not re-derive it. Six are `(*SKIP)` slice rows (seed 7 74413, 76160; seed 4242
76778, 77119, 119927; seed 20260915 74889), one is the empty-slice partial (seed 20260915 104366,
the only one with no verb, no fuzzy cost and no Turkic fold), and one is seed 7 75921, which sitting
9 measured fully and deliberately did NOT pin - its sitting-9 notes say exactly why and what is
missing. Regenerate with `pwsh -File tools/run-oracle.ps1 -Count 6000` then
`python tools/probes/gate-divergence-doors.py`.

**RUN THE DEFAULT WAVE BEFORE THE GATE, never after.** `run-oracle.ps1` writes one
`report-<seed>.txt` per seed whatever the row count, so a 300-row run overwrites the gate's reports
and the remaining rows stop being regenerable.

**A CONTROL THAT SWAPS A LETTER MUST SWAP FOR ONE THE PATTERN CANNOT TELL APART OTHERWISE.** Sitting
9 lost two drafts to this: `h` and `i` for a dotless `ı` changed ASCII-ness, which the row's own
`\p{ASCII}` reads, and both "controls" reproduced the divergence for the wrong reason.

**THE VERIFIER BRIEF NOW CARRIES A DO-NOT-USE-GIT CLAUSE - paste it (`docs/VERIFICATION.md`,
"Verifier brief").** Sitting 9's verifier reverted a four-character control with `git checkout --`
and discarded the whole slice's work in `ExpectedDivergences.cs`; it was rebuilt and proved
identical against a dump it had taken from the good build, and every claim re-run, but only by luck.

**Also still owed on S52:** the `timeout` rows generator (needs S51's `timeout` comparison), the
recorded 20-seed sweep run, and the `pos`/`endpos`-versus-`codepointSlice` fix (a wave-format change
wanting its own slice). The four long generators stay OFF the default `-Generator` list.

**Owed, carried:** the repo-wide `_regex.c` citation reconciliation (`:14545`/`:14551`/`:20903`/
`:18160` stale, `:14553`/`:14555`/`:20927-20928`/`:18159` right). Never write a tracked file from a
Python helper on Windows without `newline=""`. `.claude/skills/port-tests/SKILL.md` still teaches
`FuzzyRegex.Search(...)`; `record-oracle.py --self-check` exits 1 on the interpreter-limit guard
(pre-existing since S43); `tools/run-controls.py` cannot measure a control that mutates the recorder;
broken control sites S32-B and S38-A; S35-A and S29-A/D thin; PORTMAP `_regex.c` lines stale;
`quantifiers-long`'s filler margin is 28 -> 32, worth re-deciding.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
