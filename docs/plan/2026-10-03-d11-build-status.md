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

## Chunk 9 fix round final checks (2026-10-03, on ee76cb60, no engine change)

Oracle (Release, each seed alone under the test lock): seed 7 GREEN (agree 7393, expected 179,
timeout 2, resource 4, undefined 2, diverge 0 of 7580); seed 4242 GREEN (agree 7406, expected 169,
resource 5, diverge 0); seed 20260927 GREEN (agree 7393, expected 183, resource 4, diverge 0).
`tools/check-doc-examples.ps1`: all examples ok, exit 0.

Perf, af012080 (parent of f74e096e) against f74e096e, Release, `DOTNET_TieredCompilation=0`, same
harness built against each tree, partial calls over the 8,760 item-0 and memo grid rows
(`.scratch/fr/rows.jsonl`, `rows2.jsonl`), 3 alternating runs each, per-row median of 9 timed
batches, median across runs. Machine idle (CPU load 1-2%, nothing else running). Answers differ on
0 rows. Total 16.3 ms old, 16.0 ms new (ratio 0.981); by operation 0.972 to 0.985, nothing above 1.0.
Of 204 rows over 10 us, 4 read 10-13% slower (rows 588, 613, 623, 1262 of rows.jsonl, 11-40 us each);
run-to-run spread on the same row was 10-15%, so this is timing noise, not a path regression.

## Re-review findings F1-F4 (2026-10-03, `.claude/driver/review-d11-fixround-2026-10-03.md`)

- F1: D26's test comment now credits RC5 (chunk 3, 494b35b8). `UndeterminedFuzzyRetry` has its own
  witness, `PartialLookaroundRuleTests.Only_the_failing_side_of_an_undetermined_fuzzy_test_reaches_the_insertion`:
  match `(?:\B(?(?<=^)(*FAIL))){i<=1}` over '' P(0, 0) and search `a(?:\B(?(?<=a)(*FAIL))){i<=1}` over
  'a' P(0, 1); with the retry planted out they answer None and P(1, 1) (both rows red). Upstream: None
  and P(1, 1). The register's D26 row still names chunk 6 (not edited here).
- F2: D38's C4 rows pinned red in `OpenDefectTests.A_blocked_cut_keeps_the_alternatives_made_after_the_read`
  (7 rows, all red). Not fixed: the design's C4 rule (section 6) keeps every outcome of a blocked
  construct, and extending `Held` to a blocked success is unsound when the blocking construct's
  committed path can still fail later; D18's upgrade.
- F3: `MatchState.ForkBudgetAnswered` (Debug only) is set by `ForkBudgetPartial`; `BasicMatch` asserts
  that a partial returned with the budget spent is the budget's own. Planted (flag never set), 3 of
  `PartialForkBudgetTests` go red.
- F4: events `HeldFailedTheCondition` and `MustAdvanceLifted`; `d11-port-judge-grid.cs` takes a
  `finditer` operation and lists every event's row count; `d11-held-advance-grid.py` (8,584 rows)
  reaches them: 581 rows with `HeldFailedTheCondition`, 432 with `MustAdvanceLifted`.
- Grids (Debug): item 0 and memo, 0 answers changed against the fix round, 0 asserts. Ratchet GREEN (8120).

## D69 (2026-10-03): a repeat's backtracking arm answered a run-out at once

A one-character repeat's backtracking arm inside a lookaround body answered the partial at once
where it reached the edge, skipping chunk 8's `RunOutInALookaroundBody`. Four sites, each routed
to `runOut` and each with its own witness (planted out one at a time, every listed row red):
GREEDY_REPEAT_ONE's string tail (`(?=b[^a]*cab)(*FAIL)` over 'bca'), LAZY_REPEAT_ONE's
`IsTailPartial` (`(?=b.*?c)(*FAIL)` over 'b'), its `MatchOne` (`\X`'s lazy `ANY_ALL`, the finditer
row `(?<=b)(?=\X)` over 'abb': P(3, 3)), and its tail's `TryMatch` (`(?=b.*?cd)(*FAIL)` over 'bc').
Tests: `PartialLookaroundRuleTests.A_lazy_repeat_that_backtracks_to_the_edge_in_a_body_runs_out_there`
and `A_scan_whose_lazy_repeat_backtracks_to_the_edge_in_a_body_reports_the_edge`. The two `\X`
match rows (`(?=\X)(*FAIL)` over 'b' and its reversed form) are fixed in the second pass but S57d's
escalation still answers them (the grapheme boundary sets HitEnd), so they wait in `OpenDefectTests`
for part 2.
Other arms checked: the general GREEDY_REPEAT / LAZY_REPEAT backtracking arms answer no partial;
the fuzzy retry arms (`RetryFuzzyMatchItem` and kin, `InsertBeforeAFailedLookaround`) return a
partial status directly too, but routing them through `runOut` changed no answer over 3,903 rows
(the item-0 grid, a 672-row fuzzy-body grid, a 231-row zero-width-at-the-edge grid), so they are left
as they are; no witness found. Lead, not D69: a lookaround whose first body item is a literal runs
out at the item's start test before its frame is pushed (`(?=b)(*FAIL)` over '' is P(0, 0), judge
None, upstream P(0, 0)); `RunOutInALookaroundBody` finds no open lookaround there.
Grids (Debug): item 0 and memo 0 answers changed; held-advance 120 finditer rows moved, all onto the
judge, 0 away, 0 new phantoms. Ratchet GREEN (8131).
