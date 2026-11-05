# Current state

**Updated:** 2026-11-05 02:15 by Claude Code (phase 11 profiling started overnight, owner "continue").
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

- Phase 11 step 1 (profile, no code on main) running overnight until 07:40, owner's go 2026-11-04 21:40.
  WPR traces main vs d82base (scratchpad harness `prof/`), findings go to
  `docs/plan/2026-11-04-phase11-profile.md`. Prototypes on `perf/*` branches only; nothing merges
  to main before v1.2.0 is tagged. AC standby/monitor timeouts set to 0: restore to 10 min at 07:40.
- D139 DONE on `perf/d139-firstset` 70bbcf42 (fuzzy compile -34%, code identical over 9,786
  compiles; ratchet green; oracle 7, 4242 green, 20261104 one row = D142, also on main). Waits for
  the 1.2 tag to merge. D140 DONE on `perf/d140-flags` be41758f (on D139; `(?e)abc`
  compile 5.8 to 1.8 us, 309,104 compiles identical; gates green bar D142). D138 DONE on
  `perf/d138-lazy-state` 123f57a5 (`Matches(s)[1]` -600 B, now under 1.1.0; prolog unchanged; gates
  green bar D142; timing indicative, re-time with LHM). Note: AllocationTests lookaround row
  `\b(?=\w*\d)(?!\d+\b)\w+` fails when run alone (27,680 > 27,664 B), on main too; passes in the suite.
  D144 DONE on `perf/d144-filter-ascii` 717b174f (ordinal filter works over non-ASCII text:
  1 MB non-ASCII search 155 ms to 74 us, ASCII scans -12% to -16%; 60k-case differential identical;
  gates green bar D142); blind refutation review clean (~1.1M operations). Integration branch `perf/phase11-integration` (worktree `p11`: D139-D144 merged, gates) building; then a quiet BDN vs main. D145 fuzzy partial prefilter on `perf/d145-partial-filter`
  1cf7a961 (1 MB partial 163 ms to 0.13 ms; reach w+2k, U+1DF95 folds to ss; grid 195k rows 0 diffs,
  W-3 red; ratchet green). Builder resumed: A6/A9 arguments, and oracle seed 20261105 rows 4810,
  5044, 6025, 6099 (filter null for all four) to be run on main and registered. D143 DONE on `perf/d143-error-permitted` bd5a1eb4 (fuzzy matching
  -5% to -12%, ratchet green; oracle 7, 4242 green, 20261104 only D142). D141 DONE on `perf/ascii-fill` 31643d2a (compile -19% to -45%, matching
  flat; gates green bar D142). A stopped builder leaves WIP on its branch.
- Peer session s13-33 (future-ide) agreed: it notices full suites, holds heavy work for my timing window on request.
- Deferred: D134, D135 + D138 (1.3), D136, D137. Gates on main as of ffca17d6 GREEN (pack validation
  Source Link 404s until the push).

## Waiting on the owner: a push of main; tag 1.2 after the notes.

**Next action:** item 13 waits on the owner (push, notes review, tag v1.2.0). Phase 11: owner fixes the candidate list and end date from the profile note; meanwhile prototypes on `perf/*` branches.
