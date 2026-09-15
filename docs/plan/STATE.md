# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 2).** Suite 6,103 / 6,103 / 0 skipped, ratchet
GREEN, baseline 5,981 -> **5,995**. The slice file stays pending and its sitting-2 notes carry the
detail. **Five of sitting 1's eleven divergences are judged, classified and pinned; six are not.**

**Landed.** (1) **`pruneOutcome`**, a fourth recorder control: upstream's answer with every
`(*SKIP)` spelled `(*PRUNE)`, the control four `ExpectedDivergences` entries already rest on and
none could read. Measured not to move anything else (198 rows gain only the key, 0 existing fields
change). (2) Seed 7 rows 24430 and 24916 into `posix-fuzzy-contradicts-its-own-flagless-answer`;
24916 is a NEW symptom - POSIX moves neither cost nor span, it invents a zero-width MATCH, and only
the replacement count says so. **Ledger entry 23.** (3) Seed 4242 row 24256 into
`turkic-default-folding-without-spans`. (4) A new entry
**`turkic-default-folding-from-the-pattern-side`** for rows 25482 and 34508, where the Turkic letter
is in a `\L<name>` list and in the pattern, not the subject. Its control swaps U+0130 for U+00DF,
U+FB00 and U+01F0 - same fold LENGTH, no `T` row - so it isolates the Turkic rows where "take the
U+0130 away" does not. Three gap tests, three probes.

**Sitting 1's triage was wrong on two rows.** 24256 is Turkic, not POSIX (flags `0x4002` carry no
POSIX bit). 34508 is Turkic in the PATTERN, not `search-start-partial` - upstream's own `match`
reports the same partial, so `searchOnlyPartial` is false and that entry rightly declines it.

**Sitting 3's first job: the six `(*SKIP)` rows** - seed 7 24018, 24737, 25854, 38151 and seed
20260915 24224, 38101. All six are in `tools/probes/upstream-skip-carried-slice-doors.py` with
their controls, and on every one upstream's own `(*PRUNE)` answer IS this port's. Three already
have the second half of the standard (24018: a `match` is one attempt, so `(*SKIP)` must prune as
`(*PRUNE)` does, and upstream's `(*SKIP)` answer is its verb-free one; 25854 and 38151: upstream's
own stepwise scan lands on this port's answer span for span). **24737, 24224 and 38101 do not** -
the probe's docstring says exactly what each still owes. **The default 300-row wave reaches four of
the six**, so this is the whole of the remaining red, not a 2000-row problem.

**Then, still unstarted in S52:** long-subject generators, timeout rows, the 20-seed sweep, the
6000-row three-seed gate.

**Owed, carried:** `.claude/skills/port-tests/SKILL.md` still teaches `FuzzyRegex.Search(...)`;
`record-oracle.py --self-check` exits 1 (pre-existing since S43); `tools/run-controls.py` cannot
measure a control that mutates the recorder (S42-2A); broken control sites S32-B and S38-A; S35-A
and S29-A/D thin; PORTMAP `_regex.c` lines stale. **Open for the owner:** `slice-log.jsonl` marks
S26 `failed`; `origin/main` needs a push.
