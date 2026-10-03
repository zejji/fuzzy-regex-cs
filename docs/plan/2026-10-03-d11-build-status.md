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
| 9, part 2 | S57d and HitEnd deleted, the pin moves of design section 5, X2's witness on the merged head, Z1/Z2 green without HitEnd; D69; the lookaround memo (A11-A13) left to a design step (see below) | a2603d76 (re-review F1-F4), 597b6081 (D69), 10300f80, 54b9ea0c | not reviewed |
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

## Chunk 9 part 2 (2026-10-03): S57d deleted, pins moved, X2 witnessed; the lookaround memo not built

- **Deleted** (design 4.5): `MatchState.HitEnd`, `HitEndMatchPos`, `NoteBoundaryAtTruncationPoint` and
  its 8 calls, the snapshot's two fields, and the escalation block in `DoMatch` with its
  `ClearGroups`. The oracle's `boundary-at-the-end-of-the-text` entry (S57d's signature) and its
  control test went with it; its example row now agrees with upstream.
- **Pin moves** (design section 5), the 11 tests that went red, each re-judged with the port's own
  judge before editing: fullmatch None for `aa\B`, `a{2}\B`, `True\B`, `a?\B`, `(?r)\Ba`,
  `b\B(?(?<![\w\s])\p{Ll})` and `(?r)\b(?(?!\p{L}).|[^a])\K(\s)`; None for L7, C4, X1 (blocked-end
  rows) and `(?(DEFINE)(?<c>(?=a\b)))(?&c)*b` match. Every moved row now equals the judge. The other
  rows of section 5 had moved in earlier chunks.
- **Turned green, moved out of `OpenDefectTests`**: D38's anchored `\b` rows (now rows of
  `PartialConditionRuleTests`'s D38 test) and D69's two `\X` rows. Every guard row of section 7
  (V1, V2, D1-D3, N7, N8, Q1-Q3, T1-T6, G1) stays green without S57d, and so does S4. **Turned red**
  (explicit tests only): the five `(?w)` rows W1-W3, W5, W6 of the "reported" test, which only S57d
  answered; they are chunk 10's (exact `(?w)` at the edge).
- **X2 witness on the merged head**: `FailureMemoTests.A_partial_call_keeps_the_guards_a_blocked_condition_test_let_through`
  (X2 over 'a' * 40 + 'c', step-bounded) and the four `A_partial_call_keeps_the_memo_in_its_second_pass`
  rows all go red with `KeepsFailureMemo`'s `PartialSide` test put back (planted, 5 failures).
- **Grids** (Debug, against D69's commit): item 0 2 rows moved, memo 3, held-advance 175, every one
  onto the judge; 0 away, 0 new phantoms, 0 asserts.
- **The lookaround memo (A11-A13) is not built; a design question.** Its stated gate is S4 "in
  under twice the first pass's time". Measured without it (Release, 2026-10-03): S4 at n = 1000 is
  2.6 ms against 2.5 ms for the completed text ('...c' + 'zb') and 6.3 ms for a failing full search
  ('...c' + 'b'); at n = 3000 19.8 / 19.2 / 50.5 ms. The design's other search rows are within about
  2.2 times the completed text and below the failing full search: `(?:(?(?=.*z)a|a))*b` 1,835 / 824 /
  7,979 ms at n = 1000, `(?:(?=.*z)a)*b` 1,624 / 765 / 23 ms. Amendment 1's failure memo in the second
  pass is what brought these down. The row the memo would still help is X1 (`(?:(?=(.*)z|())a)*b\1\2`
  over 'a' * 1000 + 'c'): 2.9 s to the budget's phantom, where design 7 asks under 100 ms; every fork
  re-scans `.*`. A11 belongs to the lookaround guard rule, which chunk 6 did not build (its gate
  failed), and A12 exists. Options for the design step: (a) build the memo as designed (replay of a
  body's successes with captures, counts and the blocked flag; most of its code is capture-list
  replay and frame emulation); (b) a step budget per attempt next to the fork budget, which bounds
  X1 directly; (c) accept X1's cost, since it is a C5 phantom row. Recommendation: (b) or (c); (a)
  adds the largest mechanism for one pathological row.
- **Z1 and Z2** answer P(0, 1) through RC5 alone; Z2 (`(?:a\B){i<=1}` fullmatch 'a') joins
  `PartialEdgeReadTests.An_insertion_at_the_edge_runs_out_of_text`.
- **Final checks** (on 10300f80 plus the Z2 row): ratchet GREEN (8137); oracle, Release, each seed
  alone under the test lock: seed 7 GREEN (agree 7394, expected 178, timeout 2, resource 4,
  undefined 2, diverge 0 of 7580), seed 4242 GREEN (agree 7406, expected 169, resource 5, diverge 0),
  seed 20260927 GREEN (agree 7393, expected 183, resource 4, diverge 0). Doc examples: all ok.
  Perf, 4fba2ff2 against 10300f80, Release, `DOTNET_TieredCompilation=0`, the same harness built
  against each tree, partial calls over 10,760 rows (item 0, memo, 2,000 held-advance rows), 3
  alternating runs, per-row median of 9 batches: total 16.9 ms old, 16.6 ms new (0.985); every
  file and operation 0.97-1.00; 2 of 181 rows over 10 us read 11-12% slower (10-19 us; the run-to-run
  spread). A call without partial runs no changed code: the deleted notes ran in it before
  (`TryMatchDefaultBoundary` called one unconditionally), and D69's checks sit behind a negative
  status in partial-only arms.
- **Not done here, for the merge**: KNOWN-DEFECTS (D26's row still credits chunk 6's retry, D38
  moves to Completed with its C4 forms registered under D18, D69 to Completed; the `(?w)` rows W1-W3,
  W5, W6 are misses until chunk 10); the new ledger entry and draft report for upstream's lookaround
  phantom (design section 5), left to the merge so its number does not collide with main's.

## Fix round 2 and the lookaround memo (2026-10-03, brief `brief-d11-fixround2-memo-2026-10-03.md`)

Review answered: `.claude/driver/review-d11-chunk9-part2-2026-10-03.md`.

- **Step 0, merge main** (dce1bdbd, 359cf248): D52/D54, D50, D63, D65, D66. `OracleComparer` keeps
  `composeWithD11` and forwards main's three new switches in the composed runs; D11's second-pass
  ablation counts as a named ablation (D52/D54 off with it), as main's rule asks. Ratchet after
  the merge: 3 red, all the review's F2 rows.
- **Step 1, F2**: the `{e<=1}` rows of D38's test re-judged on the merged head, from 0 and from 1,
  against the port's non-partial judge (continuations up to 3) and the brute judge. Port answer =
  port judge on all six calls: match and search from 0 F(0, 1) (one deletion of the yes-branch's
  exactly matched `a`, ledger 42, now that D52 matches the test exactly), fullmatch from 0
  P(0, 2), every call from 1 P(1, 2). The brute judge differs only through ledger 42 (with
  `SkipExactDeletionRetry` the anchored call from 0 answers P(0, 2), the reviewer's value) and
  ledger 64 (upstream's fuzzy test refuses 'ba' from 1). Pins moved to their own test,
  `PartialConditionRuleTests.A_fuzzy_conditional_with_an_exact_test_deletes_its_branch_item`; the
  wrong "ledger 42" reasoning in the D38 summary is gone. No other `{e<=k}` conditional row in the file.
