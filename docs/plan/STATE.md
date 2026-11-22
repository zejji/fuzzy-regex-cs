# Current state

**Updated:** 2026-11-22 by Claude Code (seven branches merged to main, not pushed).
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
| 10 | `(?b)` 544 B per call (D159) | `perf/item10-bestmatch-alloc` | merged to main 03788749 2026-11-14 (ratchet GREEN 10,298; oracle 3 seeds: only D156-D158, identical to main; warm (?b) IsMatch 0 B), not pushed | - |
| 11 | Myers on short subjects | - | done: keep the scan (`2026-11-06-myers-short-subjects.md`: skipping it is 21-59x slower on a short near miss) | - |
| 12 | Repeat memo `HashSet` (~3%) | - | done: keep it (`2026-11-14-repeat-memo-share.md`: memo 0.08-0.13% of the grouped hit rows, WPR) | - |
| 13 | Design note: reuse an abandoned `Matches` state (b') | - | done: `2026-11-05-abandoned-matches-state-design.md` | recommends (a) unless a re-measure after D138 shows a gap > ~150 ns / 1 KB |
| 14 | Judge unjudged oracle rows (DECISIONS 2026-11-14) | judge-a, judge-b | merged to main a4201953 2026-11-14: all nine port right (F137-F145) | - |
| 15 | Triage pre-1.0 perf rows D33, D68, D71, D82, D99, D105, D116, D118, D123 | - | D105 merged (F146: aa04c939 inliner, e9161a9c widths; 500-group compile Release 95.8 to 9.7 ms; ratchet GREEN 10,302); D118 closed (F147, linear since before 1.2.0); D71, D99, D116, D123 re-measured 2026-11-15, unchanged since 1.2.0, still open (notes in their rows); D33 pinned by a test | D68 merged (F151, recursive calls save only what their body can write); D71 profiled (`2026-11-17-d71-fork-cost-profile.md`); option A merged (key hashed once, X1 5-10% faster); fewer-keys option E researched 2026-11-22: X1 linear but judged-wrong grid rows, not built, options F/G/H in the D71 row (recommended H); D82 closed 2026-11-18 (F152: every named row below the 2026-09-27 baseline); D163: D11 part merged to main 57e674fd 2026-11-22 (840 B a compile); left: Teddy and ASCII tables lazy + size gate, needs a quiet timing window; D99 merged (F155, main 8887e325, gates GREEN 2026-11-18); D116 was already fixed 2026-11-16 (F148); its remainder is measured in `2026-11-22-d116-options.md`: recommends option G (`\G` patterns, small, compile time) after D167 and D163; fuzzy lookbehind bodies (A) and recursive calls (C) stay as the ceiling. Option G (`perf/d116g-anchor-memo` 7660a4ab, gates GREEN, F148 row) merged to main efc9771f 2026-11-22; left: quiet-machine timing for D163 and D116g |
| 16 | Bounded search and a work budget (owner 2026-11-21, spec amendment 38, DECISIONS 2026-11-21) | - | D166 merged to main 227dcc16 (F156), D167 merged c31bf0c8 (F157), D168 options A merged bcc8a64e and B/C/D merged f78fd9eb (2026-11-22, not pushed); D169 open | A, C: quiet-machine timing retake for D166, D167; B: design note written (`2026-11-22-work-budget-design.md`), owner to choose API from note; D168: mechanism found (`2026-11-22-d168-quadratic-leading-repeat.md`), options A-D merged (gates in the D168 row), quiet-machine timing left; D169: WPR profile first |

"Green" = ratchet GREEN and oracle 3 seeds diverging only on known rows identical on main. Profile:
`docs/plan/2026-11-04-phase11-profile.md`. BDN: `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-05-phase11-integration-vs-main.md`.

## Rules

- WIP commits off main: `FUZZY_WIP_SKIP_INSPECTIONS=1` (no inspection). Merges into main: `--no-ff`; the
  pre-merge-commit inspection runs under the `.scratch/s1024/commit.lock` mutex at 11 GB free (~10 GB, peer priority; owner 2026-11-22);
  start `.scratch/s1024/watchdog.ps1` and `jbguard.ps1` before builds.
- Timing only on a quiet machine, LibreHardwareMonitor running, peer sessions told first.
- Every perf change: WPR profile before/after, differential against main, oracle, allocation check,
  KNOWN-DEFECTS row; numbers pasted from measured output only.
- Before pushing: run `tools/check-ratchet.ps1`, then `git push --no-verify` (the hook's long run
  lets GitHub drop the SSH connection, exit 141). Pushes need the owner.

## In flight

- Merged to main 2026-11-22, not pushed: D166 227dcc16 (+F156 cdc9300a), D167 c31bf0c8 (+F157 827fc7ae), D163 D11 part
  57e674fd, judge D170/D171 804ccc9c (F159, F160), D168 A bcc8a64e, D168 B/C/D f78fd9eb, D116g efc9771f. Ratchet on
  main: GREEN 10,526 on efc9771f (baseline unchanged). Worktrees kept: d166, d167, d163, judge-d170, d168b, d116g.
- D71 option E experiment: `.claude/worktrees/d71b` (branch `perf/d71-next`, uncommitted, patch `.scratch/d71b/option-e.patch`). Unsound (D71 row); delete the worktree once option F or H is decided.
- CI GREEN on 40f09940 (run 44948175075, 2026-11-21); main not pushed since.
- Possible pre-existing lead from the D168 blind review, unverified: partial lazy `[ab]+?ab` over "c1 b " gives
  P(5,5) on main, Python P(3,5); check against KNOWN-DEFECTS before registering.
- Deferred: D134, D136, D137, D142; oracle rows D161, D162. Parked: transpositions (post-1.3). Owner, end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner

- D71: option H or F (D71 row).
- Item 16 B: choose the work budget API (C, A, B or D) and exception (X1-X3) from
  `2026-11-22-work-budget-design.md` section 10 (recommended: C, a per-call budget object; X3, a new exception).

**Next action:** (1) quiet-machine timing (A-B-B-A, peers told) for D166, D167, D163, D168, D116g; (2) owner: work-budget API (`2026-11-22-work-budget-design.md`), D71 option H; (3) remove `.claude/worktrees/d71b` (unsound option E, patch `.scratch/d71b/option-e.patch`); (4) D169: WPR profile first.
