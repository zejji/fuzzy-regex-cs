# Current state

**Updated:** 2026-11-18 07:27 by Claude Code (morning stop: D164/D165 merged; D163 profiled, options note; D99 gated, uncommitted (free RAM 9.4 GB < 12 GB gate)).
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
| 15 | Triage pre-1.0 perf rows D33, D68, D71, D82, D99, D105, D116, D118, D123 | - | D105 merged (F146: aa04c939 inliner, e9161a9c widths; 500-group compile Release 95.8 to 9.7 ms; ratchet GREEN 10,302); D118 closed (F147, linear since before 1.2.0); D71, D99, D116, D123 re-measured 2026-11-15, unchanged since 1.2.0, still open (notes in their rows); D33 pinned by a test | D68 merged (F151, recursive calls save only what their body can write); D71 profiled (`2026-11-17-d71-fork-cost-profile.md`); option A merged (key hashed once, X1 5-10% faster), D71 open for the rest of the memo's cost; D82 closed 2026-11-18 (F152: every named row below the 2026-09-27 baseline); new D163 (compile-per-call rows 1.09-1.18x slower, +9-13% bytes): profile and bisect; D99 design in progress; then pick the next fix: recommended D116 (design note with options first) |

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

- D99 BUILT AND GATED, UNCOMMITTED in `.claude/worktrees/d99b` (head 56fcabfb): free RAM 8.3-9.8 GB all night, under
  the 12 GB commit gate. Backup `.scratch/d99b/wip-fix2-0548.patch` + `wip-untracked/`. Witness 76,446 to 4,367
  steps; deep recursion within 3-10% of memo-off (`.scratch/d99v/deep2.cs`); ratchet GREEN 10,397; oracle 3 seeds:
  only D164/D165; 0 diffs vs main; blind reviews `.scratch/d99r*/`: no wrong answer. Open: why a pending call's section frame can be off the chain (FrameIndex -2; the memo switches off there).
- D164/D165 judged port right, merged to main 88767f58 (F153, F154); ratchet GREEN 10,388 on main. Tooling gap:
  `tools/judge-row.py` stores a reversed fuzzy row's change positions in port terms, `JudgedDivergenceTests` wants
  upstream's (D160); row 3889 was hand-edited. Fix needs the port answer in upstream terms, which the report lacks.
- Deferred: D134, D136, D137, D142; oracle rows D161, D162. Parked: transpositions (post-1.3). Owner, end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner: nothing (transpositions decided 2026-11-14, parked post-1.3).

**Next action:** (1) when free RAM >= 12 GB: commit D99 (`perf/d99-continuation-memo`) under the commit lock, merge, ratchet + oracle on main, D99 row to F; (2) fix `tools/judge-row.py` (D160 terms); (3) D163: design note `2026-11-18-d163-compile-cost-options.md`: measure the two build costs and per-character savings in a quiet window, then size-gated lazy build or option 3.
