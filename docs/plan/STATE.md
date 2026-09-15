# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 5) - the FIFTH, run interactively by the owner
under a 45-minute deadline.** Scoped by the orchestrator to one item. Ratchet GREEN; **no `.cs` file
was touched**, so the suite is exactly sitting 3's 6,109 / 6,109 / 0 skipped, baseline 6001.

**The item taken: sitting 4's two unjudged `partial-long` divergences. Neither is a divergence.**
Sitting 4 read them as `\K`/`\M` reading a stale bound and named
`end-of-line-reads-a-skip-moved-slice` as the hypothesis. The report already said otherwise: the port
does not answer differently, it throws `RegexMatchTimeoutException` - it never finishes inside
`OracleComparer.RowTimeout` (10s). Given time it gives upstream's exact answer on both rows.

**Measured, by two committed probes** (`tools/probes/port-long-subject-cost.ps1` and
`upstream-long-subject-cost.py`, which time each engine at truncated lengths): the two engines are in
the SAME complexity class - both quadratic on row 307, 4/15/53/217/861 ms upstream against the port's
32/120/369/1,304/5,214 in Release - and the port is a constant factor slower, 5-8x in Release and up
to 75x in Debug. **`run-oracle.ps1` defaults to Debug**, and that is the whole of the red:
`-SkipRecord -Configuration Release` over the IDENTICAL rows gives `diverge 0 of 600` against Debug's
`diverge 2 of 600`. Nothing pinned, no `ExpectedDivergences` entry, nothing owed upstream.

**Sitting 6's first job is the decision this leaves open.** The four long generators still must NOT
join `run-oracle.ps1`'s default list, and the reason is now sharper than "unjudged": their verdict
depends on the build configuration. Three options are written out in the slice file's sitting-5
notes (Release-only for the long generators; a longer row timeout for them; or default the runner to
Release, the smallest change but it moves every slice's gate). Whichever is chosen, the
Debug/Release sensitivity has to be written where a slice will read it.

**Owed on S52 before it closes:** a blind review and an independent verifier covering sitting 4's
AND sitting 5's delta (neither ran either sitting - no time), and the padding-side negative control
sitting 4 specified, which must be run in **Release** or a collapsed `walked` column cannot be told
from a timed-out row.

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
