# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 4) - the FOURTH, past the driver's three-checkpoint
stop, run interactively by the owner under a 65-minute laptop-off deadline.** The orchestrator scoped
it to one item finished properly rather than several started. Ratchet GREEN; **no `.cs` file was
touched**, so the suite is exactly sitting 3's 6,109 / 6,109 / 0 skipped.

**What landed: the four long-subject generators** - `literals-long`, `quantifiers-long`,
`partial-long`, `fuzzy-long` - as a wrapper over the base generator, not four new grammars, so a long
row and a short row differ in one variable. Two rules: the filler pads the side the pattern does not
run off (left for forward, right for `(?r)`, which is what keeps a `partial` row's edge an edge), and
the operation is forced to `search` (the paths are all scans, and it bounds a 20,000-character row's
cost). Subjects 1,000-20,000, median about 10,000.

**Measuring the reach changed the design, before any review asked.**
`tools/probes/generator-long-subject-reach.py` (committed) said `quantifiers-long` reached nothing:
9 of 148 matches walked, median distance 0. No filler spelling fixes that - a nullable quantifier
answers zero-width at offset 0 without reading the text. **The metric was wrong for it**: a repeat
guard is reached by ITERATIONS, so that generator alone draws filler from the base alphabet and the
probe now reports distance AND length. It now reaches a 100+ iteration repeat on 32 of 155, up from 0.

**Sitting 5's first job: two unjudged divergences.** The four-generator wave at 150 rows is RED -
`diverge 2 of 600`, seed 20260915 - both `partial-long`, both reversed, both end-reading:
`(?r)^(\p{Lu}+?)+?(.)??\K` and `(?r)^(?P<g1>[^a]*?)(.*?)*\M`. Sitting 3's
`end-of-line-reads-a-skip-moved-slice` is the obvious hypothesis and only that: neither pattern holds
a `(*SKIP)`. **The four generators are deliberately NOT in `run-oracle.ps1`'s default list** and must
not be added until both are judged. Also owed for these: a blind review and an independent verifier
(neither ran - no time), and a negative control on the wrapper's padding side.

**Still untouched in S52:** the timeout rows, the `oracle.yml` CI job, the 20-seed sweep run, the
6000-row three-seed gate.

**Owed, carried:** the repo-wide `_regex.c` citation reconciliation - `:14545`/`:14551`/`:20903`/
`:18160` are stale against the 2026.9.10 pin, `:14553`/`:14555`/`:20927-20928`/`:18159` are right,
and a sweep cannot follow the count (the right spelling is the MINORITY for the scanner step).
Never write a tracked file from a Python helper on Windows without `newline=""`, and build
`tests/FuzzyRegex.OracleTests` too - the ratchet alone does not see a CRLF flip.
`.claude/skills/port-tests/SKILL.md` still teaches `FuzzyRegex.Search(...)`;
`record-oracle.py --self-check` exits 1 (pre-existing since S43); `tools/run-controls.py` cannot
measure a control that mutates the recorder (S42-2A); broken control sites S32-B and S38-A; S35-A and
S29-A/D thin; PORTMAP `_regex.c` lines stale.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
