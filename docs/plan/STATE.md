# Current state

**Updated:** 2026-11-14 20:40 by Claude Code (session handover; brief `.claude/driver/next-session-brief-2026-11-14.md`).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26), 1.2.0 (2026-11-05, release run 42013144639).
Main: VersionPrefix 1.3.0, PackageValidationBaselineVersion 1.2.0.
**Now:** phase 11, release 1.3 (performance). No fixed end date; aims to be fairly complete. The list
is in `ROADMAP.md` row 11 and `DECISIONS.md` 2026-11-05. Every choice rests on research,
measurement, WPR profiles and benchmarks.

## Phase 11 checklist

| # | Item | Branch | State | Left to do |
|---|---|---|---|---|
| 1-7 | D138-D145, D141 rework, D135 (integration) | `perf/phase11-integration` 2c45e127 | merged to main 2026-11-06 (owner), pushed | - |
| 8 | Grouped fuzzy repeats (D154) | `perf/item8-required-literal` | merged to main 579d0644 2026-11-14 (gates in the D154 row; oracle rows D156-D158 identical on main), not pushed | - |
| 9 | D29 + D135 compile allocation | `perf/d29-expanded-folds` | D135 merged; D29 part 2 merged to main 441c8d8d (ignore-case compile 33,904 to 17,816 B) | remaining D29 bytes spread thin; judge after item 10 |
| 10 | `(?b)` 544 B per call (D159) | `perf/item10-bestmatch-alloc`, worktree `item10` | built: warm (?b) IsMatch 512 to 0 B; ratchet GREEN 10,298; differential identical to main | committed 1c2058b7; oracle 3 seeds, merge |
| 11 | Myers on short subjects | - | done: keep the scan (`2026-11-06-myers-short-subjects.md`: skipping it is 21-59x slower on a short near miss) | - |
| 12 | Repeat memo `HashSet` (~3%) | - | done: keep it (`2026-11-14-repeat-memo-share.md`: memo 0.08-0.13% of the grouped hit rows, WPR) | - |
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

**Next action:** item 10 oracle 3 seeds then merge; tidy KNOWN-DEFECTS (move 10 finished rows) and triage the
pre-1.0 perf rows; judge oracle rows D146-D149, D151-D153, D156, D158 in 1.3 (owner 2026-11-14). Transpositions:
answers on the decision page (files `.scratch/decision-review/transpositions/`), then fold-back. Details: the brief.
