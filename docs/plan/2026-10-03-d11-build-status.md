# D11 build status, 2026-10-03

Branch `maint/d11-partial-boundary`. Design: `docs/plan/2026-09-28-partial-boundary-design.md`
(section 11.1 lists the chunks; section 12 holds amendments 1-3). Main was merged in at 9177e4ba
(main 6be46e2e), which brings D35, the guard half of X2 that chunk 9's memo half needs.

| chunk | content | commits | review |
|---|---|---|---|
| 1 | `Bstack.Count` helper, `WMarks`, asserts A5-A8, Debug counters, red-test moves | 9edeb8e4 | clean |
| 2 | M1 on the plain predicates, touch mark | 5f3d1842, repairs 50abbe75, 70418ffe, design step fa3880d3, f04db0fc | clean |
| 3 | M3, the prefilter, asserts A1-A2, RC5 brought forward | 494b35b8 | clean |
| 4 | cut rule for atomic groups, possessive repeats, `\X`, `Reopen` | bc9ab56f | clean |
| 5 | verb taint, skipped `(*PRUNE)` and `(*SKIP)` | 29a64c23, repair 600806c0 | clean |
| 6 | lookaround cut rule, dedup, run-out in a body | db1d1f38, repair b734b23a | clean |
| 7 | conditions and `NoBranch`; amendment 2 (blocked end) | 62c3fe16, 992543e5, 9ee671b6, a3f609c1, 1f0aff78, 5e9faed4, 8f4fefe5, tidy e7733fe3 | clean |
| 8 | run-outs in bodies, unknown captures, run-out taint | 82f657cf..50475244, repair 509991e0 | clean (re-review CLEAR, 13,570 rows) |
| 9, part 1 | item 0 (D41 and chunk 6's trailing-insertion regression), second-pass failure memo, fork budget M4, grid scripts | 0356a561, d633272d, 125c1bfd, f7da26d4 | **not reviewed**; brief `.claude/driver/review-d11-chunk9-brief.md` |
| 9, part 2 | lookaround memo (A11-A13), delete HitEnd and S57d's escalation, the pin moves of design section 5 (11 tests go red when S57d goes), X2's witness now that D35 is merged, Z1/Z2 green without HitEnd | - | not built |
| 10 | exact `(?w)` and grapheme M1 through property cores (W1-W6) | - | not built |
| 11 | port grid (8.1), oracle at three seeds and the wave generators, benchmarks (section 9) | - | not built |

Folded in at the merge to main: D53's phantom rows (D18, D19, D28), D38's `{i<=1}` red test (already
answered right here), D26 and D36 (fixed here) and D41 (fixed by 0356a561) move to Completed.

Merge notes (9177e4ba): `PartialBlockedEndTests` now reads group 1 under
`PatternObject.KeepCallCaptures`, because D51 discards a call's captures on return and 16 of its rows
set group 1 only inside a call. D11's named oracle ablation now also has the D51 fix off, as main
asks of every named ablation.

## Chunk 9 review fix round (2026-10-03, f74e096e)

Done, test first (each new test red without its fix, by plant): F1 (D35's assert premise restated:
only a positive test whose end was blocked lets the yes-branch record guards; X2 runs in Debug),
F2 (a fork-budget partial is not restored from an open call's frame), F3 (one fork per construct),
F4 (`PartialForkBudgetTests.Each_fork_is_counted_once`, a row per `CountFork` site, each red when
its site is dropped), F5 (blocked-end rows under both capture rules), D64 review F1 (the attempt
from inside the text to come no longer inherits must-advance), D38 (a test that held unblocked
after a blocked success fails the conditional: `WMarkKind.Held`), D26's four rows as tests.
Ratchet GREEN (8118). Item-0 and memo grids re-run, Debug and Release: 0 answers changed, 0 new
phantoms; the 106 Debug assert rows of the memo grid now answer the judge's value.

Not done yet (next steps): oracle at seeds 7, 4242, 20260927; perf check; then the final report.
Open, not fixed here: D38's anchored `\b` rows answer P(0, 2) through S57d's escalation, as main
(red in `OpenDefectTests`, None with the escalation planted out; part 2 deletes it). Finditer
`(?<=b)(?=\X)` over 'abb' answers P(2, 3) where the judge and upstream say P(3, 3): `\X`'s
`ANY_ALL` runs out at the edge and the partial is answered at once at the anchor (likely D67's
shape, not must-advance). `(?:(?=a)c){i<=1}` match 'b' answers P(0, 1) where the judge says None
(a body run-out in a fuzzy section; predates this round). Scratch: `.scratch/fr/` (grids, probes).
