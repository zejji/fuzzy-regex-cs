# Current state

**Updated:** 2026-11-03 07:18 by Claude Code (BDN stalled by sleep; 07:40 stop).
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
| 13 | Release 1.2 | main | CHANGELOG, notes draft, version, README done (bb408535) | BDN tonight, notes' performance section, gates (ratchet, oracle, AOT, pack); owner tags |

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

- Item 13 BDN: NOT DONE. Rerun from 02:28 (4 GHz cap) stalled: the laptop entered Modern Standby at 02:31/02:54
  (screen-off timeout) and slept through the night; only 5 of 91 benchmarks of pass 1 ran. Stopped 07:16; cap removed
  (PROCFREQMAX/PROCFREQMAX1 = 0, boost 2, max state 100). Next try: keep the machine awake (powercfg standby and
  screen timeouts off, or `powercfg /requestsoverride`), 4 GHz cap, and a progress heartbeat (benchmarks done) every 15 min.
- Guards: watchdog PID 32488, jbguard PID 9944. Deferred: D134, D135 (1.3), D136, D137.
- Gates on main (bb408535/ffca17d6): Release ratchet, AOT suite + smoke (win-x64), rehearsal, doc examples, tag check GREEN;
  oracle 7, 4242 green, 20261102 one row = D137 (harness, deferred); pack validation only fails Source Link 404s
  for unpushed commits (re-run after the push). Oracle: `.venvs/regex-2026.9.29/Scripts` first on PATH.

## Waiting on the owner: a timing window for item 13's BDN (machine kept awake); a push of main; tag 1.2 after the notes.

**Next action:** item 13: rerun the BDN in the owner's next window; then compare (v120 vs v110, v120 vs d82base for D82), write the note, fill the release notes' performance section.
