# Current state

**Updated:** 2026-11-01 10:24 by Claude Code (items 3 and 4 gates running in sequence).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26). Main's `src/` equals 1.1.0's.
**Now:** phase 10, release 1.2 (correctness), scope frozen 2026-11-01 (`ROADMAP.md`; decisions in
`DECISIONS.md` 2026-11-01). Next, phase 11: release 1.3 (performance).

## Release 1.2 checklist, in merge order

| # | Item | Branch | State | Left to do |
|---|---|---|---|---|
| 1 | v1.0 vs v1.1 benchmark note | `docs/v110-bench` | **merged** 5ac48534 | - |
| 2 | Oracle workflow gate split | `ci/oracle-gate` | **merged** 002650bc | - |
| 3 | D128 oracle reuse test | `fix/reuse-c` | main merged in (555ac016); gate running | read gate, merge; amend D128 text after #4 |
| 4 | D129 + D130 run-out fit | `fix/insertion-runout` | main merged in; gate queued | read gate, merge |
| 5 | D132 condition-test run-out | `fix/d132` | fix, reviews done | inspection, oracle 7/4242, D130-grid differential, merge |
| 6 | D121 conditional restore | `fix/d121` | timing done; change accepted | inspection, merge |
| 7 | D80 ablation labels | `fix/d80-labels` | row 41769 restored (uncommitted) | oracle 7/4242 consume, inspection, merge (conflicts with #3) |
| 8 | D101 + frame cut (kept) | `fix/d101-rebase-cut`, `review/d101-cut-surrogate` 917b9de2 | timing settled | carry 184c256b onto the cut line, ratchet, non-partial A-B-B-A, full BDN, merge |
| 9 | D122 continuation memo phantom | `fix/d122` | red tests, reviewed sizing | build option 1 test-first, gates, merge |
| 10 | D133 enclosing-section maxima checked when an error is made (both engines) | `fix/atomic-lookbehind` (rows) | blind review: real defect | register, verify, grid scope, second blind review, fix test-first, gates |
| 11 | D131 committing construct + D81 run-out | `fix/d131` (WIP) | design v3 + review limits | implement test-first, gates (may share #10's root) |
| 12 | D125 lastindex through a reversed call | `fix/d125` | red tests | root cause, fix, gates |
| 13 | Release 1.2 | main | - | full BDN vs 1.1.0 incl. D82's rows, CHANGELOG, notes, `docs/RELEASING.md` |

Every merge: JetBrains inspection (several Junie commits used `--no-verify`), ratchet, oracle 7 and
4242, KNOWN-DEFECTS row updated. Commit or merge to main once verified per `docs/VERIFICATION.md`;
pushes need the owner.

## Rules until 1.2 ships

- Work only on the list. No new oracle seed or review sweeps; a new find goes into KNOWN-DEFECTS as
  deferred unless it is a wrong answer outside partial matching x nested fuzzy sections x
  committing constructs.
- Start `.scratch/s1024/watchdog.ps1` and `jbguard.ps1` before builds; commits under the
  `.scratch/s1024/commit.lock` mutex at 14 GB free; timing only on a quiet machine with
  LibreHardwareMonitor running.

## In flight

- Uncommitted in `.claude/worktrees/d80` (`fix/d80-labels`): row 41769 restored
  (`JudgedDivergences.jsonl`, KNOWN-DEFECTS D80, LEDGER 81 list); needs the seed 7/4242 consume.
- Running: guards (`.scratch/s1024/guards.pid`); item 3 gate PID 32788 (`.scratch/s1101/gate-reuse-c.ps1`,
  ratchet then oracle, logs beside it). Green: merge `fix/reuse-c`. Red: read the log, fix on the branch.
  Queued behind it: item 4 gate PID 4380 (`gate-runout.ps1`, worktree `oracle-gate`, main merged 09973505).
- Overnight timing results and item 10 (D133) research notes: `.scratch/s1024/NIGHT-1101-PLAN.md`.

## Waiting on the owner: nothing.

**Next action:** item 3: read the gate result, merge `fix/reuse-c`; then item 4.
