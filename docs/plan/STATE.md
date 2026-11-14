# Current state

**Updated:** 2026-11-14 15:55 by Claude Code (item 8 oracle running; D29 part 2 ratchet running).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26), 1.2.0 (2026-11-05, release run 42013144639).
Main: VersionPrefix 1.3.0, PackageValidationBaselineVersion 1.2.0.
**Now:** phase 11, release 1.3 (performance). No fixed end date; aims to be fairly complete. The list
is in `ROADMAP.md` row 11 and `DECISIONS.md` 2026-11-05. Every choice rests on research,
measurement, WPR profiles and benchmarks.

## Phase 11 checklist

| # | Item | Branch | State | Left to do |
|---|---|---|---|---|
| 1-7 | D138-D145, D141 rework, D135 (integration) | `perf/phase11-integration` 2c45e127 | merged to main 2026-11-06 (owner), pushed | - |
| 8 | Grouped fuzzy repeats (D154) | `perf/item8-required-literal` ee646911, worktree `req` | main merged in (cd4d89a8); hit rows settled 2026-11-14 (A-B-B-A x4: B/A 1.000-1.010, A-A 0.990-1.003; near miss 316,680 to 216 ns, `.scratch/p11-item8/abba2.tsv`); blind review clean; review-gap differential written, mutants caught | review-gap test committed 40bd953b (ratchet GREEN, 10,283); oracle 3 seeds running (branch, then main) | merge into main |
| 9 | D29 + D135 compile allocation | D135 `perf/d135-open-group-counts` 4b0bd431 | D135 merged; D29 part 2 on `perf/d29-expanded-folds`, worktree `d29`: ignore-case compile 33,904 to 17,816 B (row D29) | D29 ratchet (running), oracle, commit, merge |
| 10 | `(?b)` 544 B per call | - | not started | |
| 11 | Myers on short subjects | - | done: keep the scan (`2026-11-06-myers-short-subjects.md`: skipping it is 21-59x slower on a short near miss) | - |
| 12 | Repeat memo `HashSet` (~3%) | - | not started | |
| 13 | Design note: reuse an abandoned `Matches` state (b') | - | done: `2026-11-05-abandoned-matches-state-design.md` | recommends (a) unless a re-measure after D138 shows a gap > ~150 ns / 1 KB |

"Green" = ratchet GREEN and oracle 3 seeds diverging only on known rows identical on main. Profile:
`docs/plan/2026-11-04-phase11-profile.md`. BDN: `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-05-phase11-integration-vs-main.md`.

## Rules

- Commits under the `.scratch/s1024/commit.lock` mutex at 12 GB free (the .cs inspection needs ~9 GB);
  start `.scratch/s1024/watchdog.ps1` and `jbguard.ps1` before builds.
- Timing only on a quiet machine, LibreHardwareMonitor running, peer sessions told first.
- Every perf change: WPR profile before/after, differential against main, oracle, allocation check,
  KNOWN-DEFECTS row; numbers pasted from measured output only.
- Before pushing: run `tools/check-ratchet.ps1`, then `git push --no-verify` (the hook's long run
  lets GitHub drop the SSH connection, exit 141). Pushes need the owner.

## In flight

- AC sleep 30 min normally; set Never for overnight runs and restore (owner 2026-11-06).
- Deferred defects: D134, D136, D137, D142, D146-D149, D151-D153. New, not yet sized: D155 (reverse edit tests). Parked: transpositions (post-1.3).
- Owner, at the end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner

- Transpositions design (`2026-11-05-transpositions-design.md` section 10): 10.1-10.6, 10.13, 10.15,
  10.16 decided; 10.8-10.12, 10.14, 10.17 have recommendations awaiting answers. Parked post-1.3.

**Next action:** wait for two detached jobs, then act on each:
- oracle, PID 21200 (`.scratch/p11-item8/oracle-both.ps1`; logs `oracle-req.log`, `oracle-main.log`): if the
  branch diverges only on rows that main shares, merge item 8 into main.
- D29 ratchet, PID 7448 (log `.scratch/d29/ratchet-20261114.log`, ends `EXIT n`): if GREEN, commit on the
  branch with STATUS.md and status-stamp.txt, run its oracle, merge.
AC sleep set to Never at 15:50 for these runs: restore it with `powercfg /change standby-timeout-ac 30`.
Watchdog PID 32968, jbguard PID 28336.
