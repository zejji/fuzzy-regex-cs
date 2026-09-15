# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 6) - the SIXTH, run interactively by the owner
under a 47-minute deadline.** Ratchet GREEN; **no `.cs` file was touched**, so the suite is exactly
sitting 3's 6,109 / 6,109 / 0 skipped, baseline 6001.

**The item taken: sitting 5's open Debug/Release decision. Both oracle runners now default to
Release** (`tools/run-oracle.ps1`, `tools/sweep-seeds.ps1`). The two configurations differ in speed
alone - nothing under `src/` or in any build file is conditioned on `DEBUG` - while
`OracleComparer.RowTimeout` is 10 seconds of wall clock, so a Debug consumer made the gate measure
the build. CI already passed `-Configuration Release` at all three call sites.

**The other half of that decision went the other way, on measurement.** The four long generators
were added to the default `-Generator` list and the wave run at its three seeds in Release: RED at
all three, 13 diverging rows, **every one in `quantifiers-long` or `partial-long`** and the 21 short
generators clean at all three seeds. **Eleven of the 13 are this port exceeding the row timeout in
RELEASE**, so Release moves the threshold without clearing it. The long generators are back OFF the
default list and `run-oracle.ps1`'s `.PARAMETER Generator` says why.

**Sitting 7's first job: the two of the 13 that are NOT timeouts.** Both `partial-long`, both a
partial `search` carrying a verb, upstream reporting a near-whole-subject partial where this port
answers a short match at the far end - seed 7 row 6997 and seed 20260915 row 7094, written out with
their patterns in the slice file's sitting-6 notes. Unjudged. Not assumed to be an existing family.

**Owed on S52 before it closes:** a blind review and an independent verifier covering sitting 4's
and sitting 5's delta (neither has had either), a first pass over sitting 6's two review fixes, and
the padding-side negative control sitting 4 specified, in **Release**.

**Still untouched in S52:** the timeout rows, the `oracle.yml` CI job, the 20-seed sweep run, the
6000-row three-seed gate.

**Owed, carried:** the repo-wide `_regex.c` citation reconciliation - `:14545`/`:14551`/`:20903`/
`:18160` are stale against the 2026.9.10 pin, `:14553`/`:14555`/`:20927-20928`/`:18159` are right,
and a sweep cannot follow the count. Never write a tracked file from a Python helper on Windows
without `newline=""`, and build `tests/FuzzyRegex.OracleTests` too.
`.claude/skills/port-tests/SKILL.md` still teaches `FuzzyRegex.Search(...)`;
`record-oracle.py --self-check` exits 1 (pre-existing since S43); `tools/run-controls.py` cannot
measure a control that mutates the recorder (S42-2A); broken control sites S32-B and S38-A; S35-A and
S29-A/D thin; PORTMAP `_regex.c` lines stale.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
