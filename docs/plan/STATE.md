# Current state

**Updated:** 2026-11-24 04:30 by Claude Code (budget regression attributed; builder fixing).
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
| 11-13 | Myers on short subjects; repeat memo `HashSet`; abandoned-state design note | - | done: keep the scan (`2026-11-06-myers-short-subjects.md`); keep the memo (`2026-11-14-repeat-memo-share.md`); note `2026-11-05-abandoned-matches-state-design.md` | 13: recommends (a) unless a re-measure after D138 shows a gap > ~150 ns / 1 KB |
| 14 | Judge unjudged oracle rows (DECISIONS 2026-11-14) | judge-a, judge-b | merged to main a4201953 2026-11-14: all nine port right (F137-F145) | - |
| 15 | Triage pre-1.0 perf rows D33, D68, D71, D82, D99, D105, D116, D118, D123 | - | D105 merged (F146: aa04c939 inliner, e9161a9c widths; 500-group compile Release 95.8 to 9.7 ms; ratchet GREEN 10,302); D118 closed (F147, linear since before 1.2.0); D71, D99, D116, D123 re-measured 2026-11-15, unchanged since 1.2.0, still open (notes in their rows); D33 pinned by a test | D68 merged (F151, recursive calls save only what their body can write); D71 profiled (`2026-11-17-d71-fork-cost-profile.md`); option A merged (key hashed once, X1 5-10% faster); fewer-keys option E researched 2026-11-22: X1 linear but judged-wrong grid rows, not built, D71 closed 2026-11-23 (owner, option H); D82 closed 2026-11-18 (F152: every named row below the 2026-09-27 baseline); D163: D11 part merged to main 57e674fd 2026-11-22 (840 B a compile); left: Teddy and ASCII tables lazy + size gate, needs a quiet timing window; D99 merged (F155, main 8887e325, gates GREEN 2026-11-18); D116 was already fixed 2026-11-16 (F148); its remainder is measured in `2026-11-22-d116-options.md`: recommends option G (`\G` patterns, small, compile time) after D167 and D163; fuzzy lookbehind bodies (A) and recursive calls (C) stay as the ceiling. Option G (`perf/d116g-anchor-memo` 7660a4ab, gates GREEN, F148 row) merged to main efc9771f 2026-11-22; left: quiet-machine timing for D163 Teddy part only (D11 part and D116g timed 2026-11-23) |
| 16 | Bounded search and a work budget (owner 2026-11-21, spec amendment 38, DECISIONS 2026-11-21) | - | D166 merged to main 227dcc16 (F156), D167 merged c31bf0c8 (F157), D168 options A merged bcc8a64e and B/C/D merged f78fd9eb (2026-11-22, not pushed); D169 open | A, C: timed 2026-11-23 (same as before, budget mode bounded); B: design note written (`2026-11-22-work-budget-design.md`), owner to choose API from note; D168: mechanism found (`2026-11-22-d168-quadratic-leading-repeat.md`), options A-D merged and timed 2026-11-23 (faster); D169: WPR profile first |

"Green" = ratchet GREEN and oracle 3 seeds diverging only on known rows identical on main. Profile:
`docs/plan/2026-11-04-phase11-profile.md`. BDN: `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-05-phase11-integration-vs-main.md`.

## Rules

- Intermediate commits and batch merges: `FUZZY_WIP_SKIP_INSPECTIONS=1`; merges into main `--no-ff`; when work
  finishes, inspect main's tip and fix all findings (AGENTS.md). Inspections at >= 11 GB free under `commit.lock`;
  start `.scratch/s1024/watchdog.ps1` and `jbguard.ps1` before builds.
- Timing only on a quiet machine, LibreHardwareMonitor running, peer sessions told first.
- Every perf change: WPR profile before/after, differential against main, oracle, allocation check,
  KNOWN-DEFECTS row; numbers pasted from measured output only.
- Before pushing: run `tools/check-ratchet.ps1`, then `git push --no-verify` (a long hook run drops the SSH push, exit 141). Pushes need the owner.

## In flight

- Quiet-machine timing done 2026-11-23 (1c01df05, `.scratch/timing-1123/results.md`): D168 A-D and D116g faster on
  their targets, D166, D167, D163 (D11 part) same; nothing slower. AC sleep restored to 30 min.
- Pushed main 0d311b31 2026-11-23 (inspections GREEN, ratchet GREEN 10,526); CI GREEN (runs 45272733556, 45272733580).
- D169 merged to main 76b7a1fe 2026-11-23 (blind review 350,289 cases, 0 differences; ratchet + inspections GREEN).
- Work budget (item 16 B): `feat/work-budget` 651ac24d, all gates GREEN (review findings 1-5, D172, D173 fixed). Not
  merged: with no budget, short no-match fuzzy calls are ~4 ns (3-4%) slower in 6 quiet retakes, cause not found
  (`.scratch/budget/timing/quiet-retake-2026-11-24.txt`). Attributed 2026-11-24 (`.scratch/attr-1124/`): a 64-bit
  idiv in the filter's sweep charge (~2 ns) and per-call tick recomputation (~1%); builder fixing both, then retake. Owner decides: accept, or fix per attribution (rule: unavoidable cost to owner).
- D172, D173 registered (unpolled set walk; unpolled fuzzy filter candidate loop), both reproduced on main.
- D71 option E: worktree and branch removed 2026-11-23; the unsound patch stays at `.scratch/d71b/option-e.patch`.
- Deferred: D134, D136, D137, D142; oracle rows D161, D162. Parked: transpositions (post-1.3). Owner, end of phase 11: delete the WPR scheduled tasks.

## Waiting on the owner

- Work budget merge: accept ~4 ns (3-4%) on short no-match fuzzy calls without a budget, or pursue the fix the
  attribution finds. Also review DECISIONS 2026-11-24 (nullable budget; residual CS0121 on literal null/default).

**Next action:** when the budget builder hands back: verify the four fixes and D172/D173 witnesses myself, re-review
the delta, merge, gates, push. Owner to review: DECISIONS 2026-11-24 (nullable budget, residual CS0121).
