# Current state

**Updated:** 2026-11-22 by Claude Code (D163 D11 part fixed on branch, gates green; merge waits for RAM).
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
| 15 | Triage pre-1.0 perf rows D33, D68, D71, D82, D99, D105, D116, D118, D123 | - | D105 merged (F146: aa04c939 inliner, e9161a9c widths; 500-group compile Release 95.8 to 9.7 ms; ratchet GREEN 10,302); D118 closed (F147, linear since before 1.2.0); D71, D99, D116, D123 re-measured 2026-11-15, unchanged since 1.2.0, still open (notes in their rows); D33 pinned by a test | D68 merged (F151, recursive calls save only what their body can write); D71 profiled (`2026-11-17-d71-fork-cost-profile.md`); option A merged (key hashed once, X1 5-10% faster), D71 open for the rest of the memo's cost; D82 closed 2026-11-18 (F152: every named row below the 2026-09-27 baseline); D163: D11 part fixed on `perf/d163-d11-nodes` 9e8dc5de (840 B a compile, gates green), merge waits for RAM; left: Teddy and ASCII tables lazy + size gate, needs a quiet timing window; D99 merged (F155, main 8887e325, gates GREEN 2026-11-18); then pick the next fix: recommended D116 (design note with options first) |
| 16 | Bounded search and a work budget (owner 2026-11-21, spec amendment 38, DECISIONS 2026-11-21) | - | registered: D166 (prefix sweeps unpolled), D167 (`CountOne`, fuzzy piece searches), D168 (`[ab]*\d` quadratic, inherited), D169 (ignore-case screen vs .NET) | A: D166 test-first with `ExpiresOnCheck`, then D167; B: design note written (`2026-11-22-work-budget-design.md`), owner to choose API from note; D168: mechanism found (`2026-11-22-d168-quadratic-leading-repeat.md`), option A built on `perf/d168-skip-through-firstset` f9a29fbf (on the D167 branch, ratchet GREEN 10,452): left oracle 3 seeds, differential vs main, WPR, allocation, SYNC-DIVERGENCE row, merge after D167; then options B (`[ab]+\d`), D (possessive), C (lazy); D169: WPR profile first |

"Green" = ratchet GREEN and oracle 3 seeds diverging only on known rows identical on main. Profile:
`docs/plan/2026-11-04-phase11-profile.md`. BDN: `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-05-phase11-integration-vs-main.md`.

## Rules

- WIP commits off main: `FUZZY_WIP_SKIP_INSPECTIONS=1` (no inspection). Merges into main: `--no-ff`; the
  pre-merge-commit inspection runs under the `.scratch/s1024/commit.lock` mutex at 14 GB free (~10 GB, peer priority);
  start `.scratch/s1024/watchdog.ps1` and `jbguard.ps1` before builds.
- Timing only on a quiet machine, LibreHardwareMonitor running, peer sessions told first.
- Every perf change: WPR profile before/after, differential against main, oracle, allocation check,
  KNOWN-DEFECTS row; numbers pasted from measured output only.
- Before pushing: run `tools/check-ratchet.ps1`, then `git push --no-verify` (the hook's long run
  lets GitHub drop the SSH connection, exit 141). Pushes need the owner.

## In flight

- CI GREEN on 40f09940 (run 44948175075, 2026-11-21).
- judge-row D160 tooling fix merged to main 2a4c5d6b (branch 895c07c8; recorded on F150), not pushed.
- D166 (item 16 A) in `.claude/worktrees/d166` (branch `fix/d166-chunked-sweeps`, WIP 4ac2d751, main merged in). `StringSearch` and both leading-literal sweeps (`SweepLeadingStrings`) poll per `SweepChunkLength`
  (64 Ki, or 4m) start positions, overlap m - 1. `Gaps/Api/PrefixSweepPollTests.cs` (25 tests, red/green and mutation
  proven). `OptimiserTrapsTests.An_alternation_of_literals...` made `[NotInParallel]`: its 25 ms budget is now
  observed inside the sweep and failed once in the full suite (passed 3/3 alone). Ratchet GREEN 10,422 (pre-merge).
  Perf (`.scratch/d166/perf.cs`, Release, worktree `d166-main`): answers identical on 29 rows; 0 B per call on main
  and fix; WPR (`.scratch/d166/prof-*.txt`): IndexOf 39.3% main vs 39.0% fix, no new frame; A-B-B-A timing NOT
  SETTLED twice (A-A spread 0.26-2.5x, `summary-1.txt`, `summary-2.txt`): retake on a quiet machine.
  Branch 2a9bdb9c (main merged in): ratchet GREEN 10,422; oracle 3 seeds only row 3862 (D170), block identical to
  the judge-d160 run. 10 ms timeout, hostile rows: main ran 68-642 ms and answered false, fix threw at 14-64 ms
  (`.scratch/d166/budget-*.txt`). Register row written (D166, fixed on branch). BLOCKED: the merge into main
  needs 14 GB free; free RAM stayed 11.8-13.6 GB from 22:55 to 00:15 (peer session). Then: `git merge --no-ff
  fix/d166-chunked-sweeps` on main under commit.lock; move the row to Completed with `python .scratch/d166/register.py
  <merge hash>` (writes F156); item 16 row: D166 merged.
- D167 (item 16 C) in `.claude/worktrees/d167` (branch `fix/d167-charged-walks` e331a095, from the D166 branch).
  `CountOne` charges its walk to the 256-turn gate (`Matcher.ChargeTurns`); the filter's piece searches poll in D166's
  chunks. Gates GREEN: `WalkPollTests` (25, red/green and mutation proven), ratchet GREEN 10,447, oracle 3 seeds only
  row 3857 (new D171, same answer on main), answers identical and 0 B per call vs base (`.scratch/d167/perf.cs`), WPR
  no new hot frame. Timing not settled (busy machine). Register row written (D167 fixed on branch). Merge after D166:
  `git merge --no-ff fix/d167-charged-walks` on main under commit.lock at 14 GB, then
  `python .scratch/d167/register.py <merge hash>` (writes F157); STATE item 16: D167 merged, timing retake owed.
- D163 D11 part in `.claude/worktrees/d163` (branch `perf/d163-d11-nodes` 9e8dc5de, committed): ratchet GREEN 10,398,
  oracle only D171, bytes in the D163 row. Left: merge (inspections at >= 12 GB free); BDN time on a quiet evening.
- D170, D171 judged port right on `test/judge-d170-d171` 17691546 (worktree `.claude/worktrees/judge-d170`): F159
  (ledger 11 A, finditer leak), F160 (ledger 83, `(?e)` + `\K`); blind review did not refute; F158 left for D163.
  Gates GREEN: ratchet 10,397; oracle seeds 7, 4242, 20261121, 20261122 diverge 0 (`.scratch/judge-d170/`). Left: merge.
- Deferred: D134, D136, D137, D142; oracle rows D161, D162. Parked: transpositions (post-1.3). Owner, end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner

- Item 16 B: choose the work budget API (C, A, B or D) and exception (X1-X3) from
  `2026-11-22-work-budget-design.md` section 10 (recommended: C, a per-call budget object; X3, a new exception).

**Next action:** (1) D166: merge into main once free RAM >= 14 GB (gates GREEN, see In flight), then register F156 and STATE item 16; timing retake on a quiet machine (morning); (2) D167; (3) D163.
