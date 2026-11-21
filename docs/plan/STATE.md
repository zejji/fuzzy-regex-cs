# Current state

**Updated:** 2026-11-21 21:45 by Claude Code (D166 fix built and proven, uncommitted; D160 oracle running; CI green on 40f09940).
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
| 15 | Triage pre-1.0 perf rows D33, D68, D71, D82, D99, D105, D116, D118, D123 | - | D105 merged (F146: aa04c939 inliner, e9161a9c widths; 500-group compile Release 95.8 to 9.7 ms; ratchet GREEN 10,302); D118 closed (F147, linear since before 1.2.0); D71, D99, D116, D123 re-measured 2026-11-15, unchanged since 1.2.0, still open (notes in their rows); D33 pinned by a test | D68 merged (F151, recursive calls save only what their body can write); D71 profiled (`2026-11-17-d71-fork-cost-profile.md`); option A merged (key hashed once, X1 5-10% faster), D71 open for the rest of the memo's cost; D82 closed 2026-11-18 (F152: every named row below the 2026-09-27 baseline); new D163 (compile-per-call rows 1.09-1.18x slower, +9-13% bytes): profile and bisect; D99 merged (F155, main 8887e325, gates GREEN 2026-11-18); then pick the next fix: recommended D116 (design note with options first) |
| 16 | Bounded search and a work budget (owner 2026-11-21, spec amendment 38, DECISIONS 2026-11-21) | - | registered: D166 (prefix sweeps unpolled), D167 (`CountOne`, fuzzy piece searches), D168 (`[ab]*\d` quadratic, inherited), D169 (ignore-case screen vs .NET) | A: D166 test-first with `ExpiresOnCheck`, then D167; B: design note (API, unit, scope) before code; D168, D169: WPR profile first |

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

- CI GREEN on 40f09940 (run 44948175075, 2026-11-21).
- judge-row D160 fix in `.claude/worktrees/judge-d160` (branch `fix/judge-row-d160`, uncommitted). Gates: ratchet
  GREEN 10,397 (`.scratch/judge-d160/ratchet.log`); oracle EXIT 1 (`oracle.log:3501`): one
  DIVERGE, row 3862 (fuzzy change positions only). Opus subagent checking branch regression vs known row (read-only);
  act on its verdict: fix and rerun the oracle, or commit (status stamp) and merge.
- D166 (item 16 A) in `.claude/worktrees/d166` (branch `fix/d166-chunked-sweeps`, UNCOMMITTED: commit waited for RAM,
  5 GB free). `StringSearch` and both leading-literal sweeps (`SweepLeadingStrings`) poll per `SweepChunkLength`
  (64 Ki, or 4m) start positions, overlap m - 1; `LeadingStringsMaxLength` now always set. New
  `Gaps/Api/PrefixSweepPollTests.cs` (25 tests): 4 sweeps (single, alternation, ignore-case, outlier) red without the
  fix, green with; one-chunk controls; boundary tests fail when the overlap is removed (mutation, 11 fail); outlier
  route proven by reverting that site alone. Left: commit, full suite + ratchet, oracle, perf differential vs main +
  WPR + allocation check (non-hostile and hostile rows), KNOWN-DEFECTS D166 row, merge.
- D164/D165 judged port right, merged to main 88767f58 (F153, F154); ratchet GREEN 10,388 on main. Tooling gap:
  `tools/judge-row.py` stores a reversed fuzzy row's change positions in port terms, `JudgedDivergenceTests` wants
  upstream's (D160); row 3889 was hand-edited. Fix needs the port answer in upstream terms, which the report lacks.
- Deferred: D134, D136, D137, D142; oracle rows D161, D162. Parked: transpositions (post-1.3). Owner, end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner: nothing (transpositions decided 2026-11-14, parked post-1.3).

**Next action:** (1) D160: on `ORACLE EXIT` commit, merge; (2) D166: commit WIP when RAM allows, then gates (ratchet, oracle 3 seeds) and perf differential/WPR/allocation via subagents, KNOWN-DEFECTS row, merge; (3) D167; (4) D163.
