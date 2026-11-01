# Current state

**Updated:** 2026-11-02 00:05 by Claude Code (item 8 timing PASSED, merging; owner: work overnight, pause all by 07:40 Mon 11-02).
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
| 8 | D101 + frame cut (kept) | `review/d101-cut-surrogate` | main merged 35ff4cd4; ratchet, inspections, oracle 7/4242 GREEN; timing PASSED (A-B-B-A 0.998, BDN re-take ~1.006, 0 rows past floor) | merge, KNOWN-DEFECTS rows |
| 9 | D122 continuation memo phantom | `fix/d122` | main merged 4968f469; option 1 built, ratchet GREEN, grid 72/72 to the judge, blind review no defect, UNCOMMITTED | review follow-up rows, commit (inspection), oracle 7/4242, merge |
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

- Item 8 timing evidence: `.scratch/s1024/NIGHT-1101-PLAN.md` (23:55 entry), `.scratch/s1101/bdn-item8-compare2.out`.
- Item 9: option 1 fix uncommitted in `.claude/worktrees/d122` (11 files; copy in the session scratchpad
  `fixbackup2`); evidence `.scratch/s1024/d122/`. Blind review: no defect; follow-ups in NIGHT-1101-PLAN (22:00).
- Oracle: `.venvs/regex-2026.9.29/Scripts` first on PATH. Message peer s06b-e8 before heavy steps.
- Overnight timing results and item 10 (D133) research notes: `.scratch/s1024/NIGHT-1101-PLAN.md`.

## Waiting on the owner: nothing. Owner 11-01: continue overnight, use all time, pause everything by 07:40.

**Next action:** merge item 8 to main (`.scratch/s1101/merge8.ps1`), KNOWN-DEFECTS rows, then item 9 review follow-ups and commit.
