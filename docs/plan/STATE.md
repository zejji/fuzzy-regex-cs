# Current state

**Updated:** 2026-11-05 21:00 by Claude Code (D145 merge committed in p11; D141 rework committed, timing overnight).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26), 1.2.0 (2026-11-05, release run 42013144639).
Main: VersionPrefix 1.3.0, PackageValidationBaselineVersion 1.2.0.
**Now:** phase 11, release 1.3 (performance). No fixed end date; aims to be fairly complete. The list
is in `ROADMAP.md` row 11 and `DECISIONS.md` 2026-11-05. Every choice rests on research,
measurement, WPR profiles and benchmarks.

## Phase 11 checklist

| # | Item | Branch | State | Left to do |
|---|---|---|---|---|
| 1 | D139 + D140 compile without exceptions | `perf/d140-flags` be41758f | green | merge via #7 |
| 2 | D141 ASCII answers | `perf/ascii-fill` 982cfaf6 | rework committed, ratchet GREEN | A-B-B-A: storage variants (array / shared property arrays `.scratch/d141r/a2` / inline on `Node`, +8 B per node) vs main and 31643d2a; WPR; blind review; oracle 3 seeds |
| 3 | D138 lazy `MatchState` fields | `perf/d138-lazy-state` 123f57a5 | green | merge via #7 |
| 4 | D143 plain error budget | `perf/d143-error-permitted` bd5a1eb4 | green | merge via #7 |
| 5 | D144 filter on non-ASCII text | `perf/d144-filter-ascii` 717b174f | green, blind review clean | merge via #7 |
| 6 | D145 fuzzy partial prefilter | `perf/d145-partial-filter` eb555b33 | green, proof + review clean | merge via #7; add a BDN fuzzy partial row |
| 7 | Integration to main (owner approved) | `perf/phase11-integration` a515bb6f, worktree `p11` | D145 merged (ratchet GREEN) | take #2; ratchet, oracle 3 seeds, allocation, BDN vs main; merge, push (owner) |
| 8 | Grouped fuzzy repeats `(?:(\w+) jumps over){e<=2}` | - | not started | WPR profile first |
| 9 | D29 + D135 compile allocation | - | not started | |
| 10 | `(?b)` 544 B per call | - | not started | |
| 11 | Myers on short subjects | - | not started | A-B-B-A with A-A control decides |
| 12 | Repeat memo `HashSet` (~3%) | - | not started | |
| 13 | Design note: reuse an abandoned `Matches` state (b') | - | not started | note only |

"Green" = ratchet GREEN and oracle 3 seeds with only D142 diverging (identical on main). Profile:
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

- Overnight to 07:40 (owner 2026-11-05): machine free for timing; harness `.scratch/d141r/harness` (outMain, outOld, outA); worktree `d141old` = 31643d2a, delete after. AC sleep disabled.
- Deferred defects: D134, D135, D136, D137, D142, D146-D149. Parked: transpositions (post-1.3).
- Owner, at the end of phase 11: delete the WPR scheduled tasks.

**Next action:** item 2 timing (A-B-B-A + A-A control, LHM temperatures), pick the storage variant, review;
then item 7.
