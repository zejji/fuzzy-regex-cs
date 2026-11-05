# Current state

**Updated:** 2026-11-05 by Claude Code (1.2.0 released; post-release bumps).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26), 1.2.0 (2026-11-05, release run 42013144639).
Main: VersionPrefix 1.3.0, PackageValidationBaselineVersion 1.2.0.
**Now:** phase 11, release 1.3 (performance, `ROADMAP.md` row 11). The list and end date are not
fixed yet: the owner decides them from the profile note and the BDN note below.

## Phase 11: work done so far, waiting on the owner's list

Profile `docs/plan/2026-11-04-phase11-profile.md`. BDN
`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-05-phase11-integration-vs-main.md`
(integration branch against main: geomean 0.961, best 0.650, nothing slower beyond noise). Order
rule and the D141 shared cache: `DECISIONS.md` 2026-11-04 (the cache is for the owner to confirm).

| Item | Branch | Head | State |
|---|---|---|---|
| D139 + D140 compile without exceptions | `perf/d140-flags` | be41758f | green, not merged |
| D141 ASCII answers | `perf/ascii-fill` | 31643d2a | green, not merged |
| D138 lazy `MatchState` fields | `perf/d138-lazy-state` | 123f57a5 | green, not merged |
| D143 plain error budget | `perf/d143-error-permitted` | bd5a1eb4 | green, not merged |
| D144 filter on non-ASCII text | `perf/d144-filter-ascii` | 717b174f | green, blind review clean |
| D145 fuzzy partial prefilter | `perf/d145-partial-filter` | eb555b33 | green, proof + blind review clean |
| Integration of D138-D144 | `perf/phase11-integration` (worktree `p11`) | 27a147d9 | green; D145 merge staged, uncommitted |

"Green" = ratchet GREEN and oracle 3 seeds with only D142 diverging (identical on main).

## Rules

- Commits under the `.scratch/s1024/commit.lock` mutex at 12 GB free (the .cs inspection needs ~9 GB);
  start `.scratch/s1024/watchdog.ps1` and `jbguard.ps1` before builds.
- Timing only on a quiet machine, LibreHardwareMonitor running, peer sessions told first.
- Every perf merge: differential against main, oracle, allocation check, KNOWN-DEFECTS row.
- Before pushing: run `tools/check-ratchet.ps1`, then `git push --no-verify` (the hook's long run
  lets GitHub drop the SSH connection, exit 141).

## In flight

- `p11`: D145 merge staged, uncommitted (`git -C .claude/worktrees/p11 status`). Finish with
  `git commit`, then ratchet and oracle.
- Deferred defects: D134, D135, D136, D137, D142, D146-D149.
- Owner, at the end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner: the phase 11 list and end date; confirm or reject the D141 shared cache.

**Next action:** finish the D145 merge in `p11` (commit, gates); add a fuzzy partial row to the BDN
suite. Merge the phase 11 branches to main only once the owner has fixed the list.
