# Current state

**Updated:** 2026-11-05 07:30 by Claude Code (overnight phase 11 run paused at 07:40, owner's instruction).
**Released:** 1.0.0 (2026-10-20), 1.1.0 (2026-10-26). Main carries 1.2's fixes, VersionPrefix 1.2.0.
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
| 10 | D133 enclosing-section maxima checked when an error is made | `fix/d133` | **merged** 3ed7b82c (F122; D136 condition tests deferred); timing nested 0.707, non-partial 1.004 inside A-A | - |
| 11 | D131 committing construct + D81 run-out | `fix/d131-by-d133` | **merged** 9191547b (F123: closed by D133, regression tests; fix/d131 superseded) | - |
| 12 | D125 lastindex through a reversed call | `fix/d125-on-main` | **merged** e23ee6eb (F124; port and upstream; ledger 98) | - |
| 13 | Release 1.2 | main | CHANGELOG, notes, version, README, BDN note (1.2 = 1.1 within ~1%; D138 deferred) done | owner: push main, review notes, tag; then pack validation |

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

- Overnight phase 11 run (owner's go 2026-11-04 21:40, order delegated) PAUSED 07:40. Nothing running.
  Profile note `docs/plan/2026-11-04-phase11-profile.md`. Power restored (no CPU cap, AC 10 min).
- Phase 11 branches, each green (ratchet; oracle only D142) and not merged to main:
  D139+D140 `perf/d140-flags` be41758f (no compile exceptions); D141 `perf/ascii-fill` 31643d2a;
  D138 `perf/d138-lazy-state` 123f57a5; D143 `perf/d143-error-permitted` bd5a1eb4; D144
  `perf/d144-filter-ascii` 717b174f (blind review clean); D145 `perf/d145-partial-filter` eb555b33
  (proof + blind review clean). Integration `perf/phase11-integration` 27a147d9 = D138-D144
  (worktree `p11`); the D145 merge is staged, uncommitted, in `p11`: `git commit`, then gates.
- BDN DONE 06:46 (A-B-B-A main vs 27a147d9): suite geomean 0.961, best 0.650
  (`UncachedFuzzyIsMatch`), nothing slower beyond noise; note
  `bench/baselines/.../2026-11-05-phase11-integration-vs-main.md`. D150 (504 B on one row) traced
  and closed as BDN noise: one launch of four, 0 B per call outside BDN on every tree.
- Deferred: D134, D135, D136, D137, D142, D146-D149. Gates on main as of ffca17d6 GREEN (pack validation
  Source Link 404s until the push).

## Waiting on the owner: a push of main; tag 1.2 after the notes.

**Next action:** item 13 waits on the owner (push, notes review, tag v1.2.0). Phase 11: the owner
reviews the profile note and the BDN note, then fixes the list and end date. Meanwhile: finish the D145 merge in `p11` (`git commit`, gates); add a
fuzzy partial row to the suite; nothing merges to main before v1.2.0 is tagged.
