# Current state

**Updated:** 2026-11-06 05:45 by Claude Code (item 8 profiled; required-literal screen proposed).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26), 1.2.0 (2026-11-05, release run 42013144639).
Main: VersionPrefix 1.3.0, PackageValidationBaselineVersion 1.2.0.
**Now:** phase 11, release 1.3 (performance). No fixed end date; aims to be fairly complete. The list
is in `ROADMAP.md` row 11 and `DECISIONS.md` 2026-11-05. Every choice rests on research,
measurement, WPR profiles and benchmarks.

## Phase 11 checklist

| # | Item | Branch | State | Left to do |
|---|---|---|---|---|
| 1 | D139 + D140 compile without exceptions | `perf/d140-flags` be41758f | green | merge via #7 |
| 2 | D141 ASCII answers | `perf/ascii-fill` 9e31db88 | done: merged into p11 e9c4161a | - |
| 3 | D138 lazy `MatchState` fields | `perf/d138-lazy-state` 123f57a5 | green | merge via #7 |
| 4 | D143 plain error budget | `perf/d143-error-permitted` bd5a1eb4 | green | merge via #7 |
| 5 | D144 filter on non-ASCII text | `perf/d144-filter-ascii` 717b174f | green, blind review clean | merge via #7 |
| 6 | D145 fuzzy partial prefilter | `perf/d145-partial-filter` eb555b33 | green, proof + review clean | merge via #7; add a BDN fuzzy partial row |
| 7 | Integration to main (owner approved) | `perf/phase11-integration` 2c45e127, worktree `p11` | all gates green: ratchet (10,246), BDN 0.959 vs main, nothing slower (`bench/baselines/.../2026-11-06-phase11-integration-vs-main.md`), oracle 3 seeds (only D151-D153, identical on main) | owner: merge to main and push |
| 8 | Grouped fuzzy repeats `(?:(\w+) jumps over){e<=2}` | - | profiled (`2026-11-06-grouped-fuzzy-repeats-profile.md`) | build the required-literal screen, test-first |
| 9 | D29 + D135 compile allocation | D135 `perf/d135-open-group-counts` 4b0bd431 | D135 merged into p11 2c45e127 (ratchet GREEN, oracle: known rows only) | D29 |
| 10 | `(?b)` 544 B per call | - | not started | |
| 11 | Myers on short subjects | - | done: keep the scan (`2026-11-06-myers-short-subjects.md`: skipping it is 21-59x slower on a short near miss) | - |
| 12 | Repeat memo `HashSet` (~3%) | - | not started | |
| 13 | Design note: reuse an abandoned `Matches` state (b') | - | done: `2026-11-05-abandoned-matches-state-design.md` | recommends (a) unless a re-measure after D138 shows a gap > ~150 ns / 1 KB |
Waiting on the owner: item 7's merge to main and push (all gates green).

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

- Machine free for timing to 07:40 (owner 2026-11-05); AC sleep off. BDN night 2 done 03:22, CPU cap restored.
- Item 11 experiment: worktree `p11-myers` (env FUZZYREGEX_SCAN_MIN, never to be committed), harness `.scratch/p11-myers/`.
- Deferred defects: D134, D136, D137, D142, D146-D149, D151-D153. Parked: transpositions (post-1.3).
- Owner, at the end of phase 11: delete the WPR scheduled tasks.

**Next action:** item 8: build the required-literal screen (design in the profile note), test-first; D29.
