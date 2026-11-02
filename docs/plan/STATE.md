# Current state

**Updated:** 2026-11-02 03:40 by Claude Code (items 8, 9 merged; item 10 builder running; owner: pause all by 07:40 Mon 11-02).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26). Main's `src/` equals 1.1.0's.
**Now:** phase 10, release 1.2 (correctness), scope frozen 2026-11-01 (`ROADMAP.md`; decisions in
`DECISIONS.md` 2026-11-01). Next, phase 11: release 1.3 (performance).

## Release 1.2 checklist, in merge order

| # | Item | Branch | State | Left to do |
|---|---|---|---|---|
| 1 | v1.0 vs v1.1 benchmark note | `docs/v110-bench` | **merged** 5ac48534 | - |
| 2 | Oracle workflow gate split | `ci/oracle-gate` | **merged** 002650bc | - |
| 3 | D128 oracle reuse test | `fix/reuse-c` | **merged** 53f27a93 (F115) | - |
| 4 | D129 + D130 run-out fit | `fix/insertion-runout` | **merged** c06653ec (F116, F117) | - |
| 5 | D132 condition-test run-out | `fix/d132` | **merged** 7c84be58 (F118) | - |
| 6 | D121 conditional restore | `fix/d121` | **merged** 925345e8 (F119) | - |
| 7 | D80 ablation labels | `fix/d80-labels` | **merged** 76b5b6c7 (mislabels; row 34508 stays open) | - |
| 8 | D101 + frame cut (kept) | `review/d101-cut-surrogate` | **merged** cc30e761 (F120 D127; D101 partly, 2 rows open) | - |
| 9 | D122 continuation memo phantom | `fix/d122-on-main` | **merged** 769c7ca1 (F121; D134 deferred) | - |
| 10 | D133 enclosing-section maxima checked when an error is made (both engines) | `fix/d133` | registered 10382543; blind review: E right, scope set (`.scratch/s1101/d133/DESIGN.md`); Opus builder: WIP 474109c8, grid 20,580/20,580 = E | builder hand-back, verify, timing (nested non-committing rows), gates, merge |
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

- Item 10: Opus builder (background subagent) in `.claude/worktrees/d133`, hands back by 07:00; WIP commits on fix/d133.
- Oracle: `.venvs/regex-2026.9.29/Scripts` first on PATH. Message peer s06b-e8 before heavy steps.
- Overnight timing results and item 10 (D133) research notes: `.scratch/s1024/NIGHT-1101-PLAN.md`.

## Waiting on the owner: nothing. Owner 11-01: continue overnight, use all time, pause everything by 07:40.

**Next action:** item 10 builder hand-back: verify claims, re-run `.scratch/s1101/d133/port-grid.ps1` on the final build, timing, gates, merge.
