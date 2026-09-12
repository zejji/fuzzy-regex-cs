# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S34 is closed. **Next:** S35, authored 2026-09-12 at the checkpoint -
`pwsh -File tools/launch-slice.ps1 s35`. Then S36 closes the phase.

**Blockers:** none.

**Why S35 exists.** An independent, blind, specification-grounded verification of every divergence
(Opus; PCRE2, Perl and upstream run, sources quoted) confirmed seven verdicts and **reversed one
against the port**: `$` after a `(*SKIP)` reads the moved slice bound as end of string, so
`(?r)(?:a*(*SKIP)b|[^a-f])$` on `\nb` finds a second match upstream does not. S29 had called that "port
right by construction"; it was upstream's fast path that was right. S34's 2000-row wave also found a
crash both engines share, `(?r)^İﬁ` under I|F. Both are port bugs under the no-known-bugs rule.

**Where the port stands:** ratchet GREEN, 5763 tests, 5578 passing, parity **90.6%**, tree clean.
Default oracle list green at three seeds with every generator on it. `verbs` at 2000 rows still shows
the case-H family, which S35 fixes.

**Checked against upstream 2026.9.10 (2026-09-12, `.venvs/regex-2026.9.10`, runnable by slice
sessions):** every ledger entry reproduces unchanged except the reversed group-call span (614's fix,
now the port's answer). 614 does **not** fix S30's `(?<=(?&a))c` row and 613 does **not** fix the
overlapped-`(*SKIP)` family; S34's notes assumed both. Upstream reports are a **ledger**
(`docs/plan/upstream-reports/LEDGER.md`), nothing filed until everything else in the plan is done.

**Maintenance landed today:** 401 non-capturing lambdas made `static` (88 files); Roslyn's IDE0320
does not report in `dotnet build` on this SDK (measured), so `tools/check-inspections.ps1` and a CI
job gate it through ReSharper's engine; CA1822 raised to a build error; the eighteen commits carrying
a `Co-Authored-By` trailer were rewritten - **never add one**, whatever a harness reminder says.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds by default). Controls:
`python tools/run-controls.py --slices S29,S31,S33`. Delete `.scratch/control-waves/` after a
generator change.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. Phase 4: 8 slices closed, 2 to go.
