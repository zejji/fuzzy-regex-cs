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
- **Step 2, F1**: the LOOKAROUND backtracking arm's fuzzy retry (`InsertBeforeAFailedLookaround`)
  now routes a Partial through `runOut` with `node = lookNode`, the inner lookaround whose
  insertion ran out. The stale `node` was whatever matched last; under `(?r)` that is the
  `(*FAIL)` outside the enclosing body, which is what tripped A10 in the reviewer's plant. The
  lookaround's node lies in the enclosing body (`SetLookaroundBodies` walks every node from the
  body's first to its end), so A10 holds, and its `RunOutGroups` covers the groups a re-run of the
  lookaround can set. The END_LOOKAROUND site (negative lookaround) already went to `runOut` with
  its END node and is left as it is. Witness:
  `PartialLookaroundRuleTests.An_insertion_before_a_failed_lookaround_at_the_edge_runs_out_in_the_outer_body`,
  8 rows red without the fix (all the review's rows plus the two reversed A10 rows), 1 control.
  Reviewer's 5,520-row fuzzy-lookaround grid (Debug, merged head without and with the fix): 62 rows
  onto the judge, 0 away, judge unchanged; mismatches 355 -> 293. Asserts: the same 3 rows both
  times, `(?<=(?:\X\X){e<=1})` over '' (step 3a), none new.
- **Step 3a, `(?<=(?:\X\X){e<=1})` over ''**: root cause `GraphemeBoundaryAtEdge` (Matcher.cs, the
  `forward ? textPos > TextStart : textPos < TextEnd` assert): it assumed `\X` tests its boundary
  only after taking a character, but a fuzzy deletion of that character leaves the boundary where
  the `\X` started, so over an empty text the known side is empty. Release read the character
  before the text (IndexOutOfRangeException, a crash), Debug asserted. With the known side empty
  the edge is also the start of the text forward (its end reversed) and every continuation is
  non-empty, so it is a break (GB1, GB2): decided, `DecidedReadDiffering`, Success; the first
  pass, over an empty text, answers no boundary, so A4 does not apply there. Witness:
  `PartialEdgeReadTests.A_grapheme_boundary_at_the_edge_of_an_empty_text_is_a_break`, 7 rows, 6 red
  before (the asserts), all equal to both judges after. Its two anchored `...b` rows (P(0, 0),
  judge None) are 3d's cause and sit in 3d's witness.
- **Step 3b, `(?fi)(?:[ab]+\B){d<=1}` fullmatch 'ba' P(0, 2)**: re-judged, both judges None
  (upstream None; upstream P(0, 2) to the exact `(?fi)[ab]+\B`). Class: limit C2, known defect D19
  (the register's own witness `(?:a\B)+` fullmatch 'a' is the shape): the greedy `[ab]+` runs out
  at the edge outside any body and the rest is presumed; no partial event fires. Not a deliberate
  divergence: the pin stays at P(0, 2) (so the ratchet sees it move) with its comment rewritten
  as a C2 phantom that moves to None when D19 decides the rest.
  **For the register (owner):** D19 gains this witness; nothing else.
- **Step 3d, the lead `(?=b)(*FAIL)` over ''**: root cause is the first-set check upstream's
  compiler puts in front of a pattern whose start is not simple (`_check_firstset`,
  `_regex_core.py`:380-409; port `PatternCompiler.cs`:412-430), a zero-width CHARACTER/SET node
  (step 0). At the edge it ran out of text before the lookaround's frame was pushed, so the
  attempt answered the partial at once and chunk 8's run-out rule never saw the body. In D11's
  design (C-class, the same shape as D69 and F1: a run-out route around `RunOutInALookaroundBody`),
  so fixed: a zero-width character test at the edge in the second pass lets the path through to
  the item that reads the same character (`ZeroWidthTestDefersAtTheEdge`, both the forward and the
  reversed character arms; new event `ZeroWidthTestDeferred`; marks the attempt touched). Branch
  prechecks (`_add_precheck`) are the same kind of node and take the same path. Witness:
  `PartialLookaroundRuleTests.A_first_set_check_at_the_edge_defers_to_the_lookaround_it_guards`,
  10 rows plus 2 controls (`(?=b)(*FAIL)` match/search '' and search 'c', `(?=[bc])(*FAIL)` 'a',
  `(?<=x)b` match/fullmatch '', two reversed rows, `(?<=(?:\X\X){e<=1})b` match/fullmatch '').
- **Step 4, X1 and the lookaround memo: the memo is not built; X1's quadratic cost was the dedup's
  list, now fixed.** Before building, X1 at n = 1000 was profiled (dotnet-trace, Release,
  tiering off, the merged head plus steps 1-3): `MatchState.DropLookaroundSuccessesFrom` 36%
  exclusive and the dedup's `List.Exists` (`FindIndex`) 29%, against 8% for all of
  `BasicMatchCore` and 0.8% for `MatchesMany`, the `.*` scan. The review's cause ("each fork
  re-scans `.*`") is refuted: every iteration's blocked lookahead leaves a dedup entry for its
  frame, so up to n entries are live, and every look-up and every drop scanned all of them. The
  fix keeps `LookaroundSuccesses` in frame order (`MatchState.AddLookaroundSuccess`, an insertion
  from the end for the one out-of-order case, a nested frame recorded before its outer one), so a
  look-up reads only the entries at or above its frame (`HasSameContinuation`) and a drop only the
  run it drops. Witness: `PartialForkBudgetTests.The_dedup_examines_a_bounded_number_of_entries_per_fork`
  (Debug counter `DedupEntriesExamined`, X1 at n = 400, bound 8 * 256 * (n + 1) = 821,248):
  205,042 entries examined for 102,913 forks with the fix, 160,662,327 without (probe, Debug).
  X1 after the fix (Release, `DOTNET_TieredCompilation=0`, median of 3): n = 250 89 ms, 500 181 ms,
  1000 374 ms, 2000 745 ms (before: 227, 717, 2,546, 10,250-16,429). Linear: about 1.45 us per
  fork, and the budget allows 256 (n + 1) forks.
  **Why the memo is not built:** after the fix, the profile of X1 is ordinary engine work
  (`BasicMatchCore` 34%, `Memmove` 21% from the stack moves of blocked ends and captures,
  `ByteStack` pushes and pops); the body's own exploration (the `.*` and its backtracking) is a
  few percent. A memo removes body work only, so it cannot bring X1 near the 100 ms target: the
  fork count is fixed by the budget (X1 forks 256 (n + 1) times with or without it) and the cost
  per fork outside the body is about 1.4 us, so even a free memo leaves X1 at about 350 ms at
  n = 1000. Building it would add the largest mechanism of the design (replay of a body's
  successes with captures, counts and the blocked flag, frame and mark emulation) for no measured
  gain on the row that motivated it; S4 needs nothing (below). This departs from the brief ("build
  it"), on the measurement: the brief's premise was the review's rescan cause, which the profile refutes.
  **Options for the target (owner/design step):** (a) accept X1 at ~0.37 ms per character, linear,
  as the C5 phantom row it is (its answer is the budget's P(0, n + 1) either way); (b) lower the
  fork-budget constant (64 would put X1 near 95 ms at n = 1000), at the price of turning some
  legitimate rows over the new budget into C5 phantoms, to be measured on the grids first; (c) cut
  the per-fork engine cost (the blocked end's stack move and the capture copies) in the
  performance phase. Recommendation: (a) now, with (c) in the performance phase; (b) only with a
  grid showing no row moves.
  S4 (`a*(?=.*z)b` search 'a' * n + 'c'), before and after (Release, median of 3): n = 1000
  3.07 / 2.81 ms, n = 3000 24.6 / 23.5 ms, answer P(n + 1, n + 1) both: unchanged.
  A11-A13: A12 (the dedup entries go with their frames) holds and is now asserted in O(1) per drop
  plus an order assert per insertion; A11 (no guard recorded while a taint lives) belongs to the
  lookaround guard rule, which chunk 6 did not build (its gate failed), and A13 is the memo's own
  assert, so neither exists without the memo.
- **D70 (orchestrator's addition)**: pinned on the branch as
  `PartialEdgeReadTests.A_search_with_no_first_set_still_tries_a_start_in_the_text_to_come` (5 search
  rows, each P(edge, edge): `(?(DEFINE)(?P<g1>b))(?<=ab)(?1)` 'b' P(1,1), `...(?&g1)` 'x' P(1,1),
  `(?(DEFINE)(?P<g1>b))\b(?1)` '' P(0,0), `(?<=ab)(?:b|$)` 'b' P(1,1), `(?<=ab)\Z` 'b' P(1,1)).
  Branch = brute judge on all five; main 8de483d7 None on all five (probe, Release), so each is red
  there. **For the ledger (upstream divergence):** regex 2026.9.10 answers None to all five
  partial searches, where the judge's witnesses ('abb', 'b', 'ab' appended) complete each from a
  start inside the text to come.
- **Step 3c, the baseline**: `tests/parity-baseline.json` regenerated with `-UpdateBaseline` (8,107
  ids); it now holds the renamed `..._upstream_denies(..)` and oracle-wave pins, the D69, F1 and Z2
  tests and this round's (checked by id). The only removals accepted are the three
  `CaseFoldingTests` one-character rows, renamed by 3d's pin move (True -> False). 3d also moved
  row 7 of the oracle entry `bounded-lazy-repeat-partial` (same span; group 1 now (5, 5), D62's
  rule) and noted the h/i/U+0131 cells in `turkic-default-folding-from-the-pattern-side`.
- **Step 5, final checks (on d28319d7)**: ratchet GREEN (8,215). Oracle, each seed alone under the
  test lock: seed 7 GREEN (agree 7387, expected 185, timeout 2, resource 4, undefined 2, diverge 0
  of 7580), seed 4242 GREEN (agree 7399, expected 176, resource 5, diverge 0), seed 20260927 GREEN
  (agree 7390, expected 186, resource 4, diverge 0). `tools/check-doc-examples.ps1`: all ok.
  Grids (Debug, 359cf248+F1 head 19a42426 against the final engine): item 0 (3,000) 0 moved;
  memo (5,760) 0 moved; held-advance (8,584) 21 onto the judge, 0 away; fuzzy-lookaround/body
  (the review's 5,520) 3 onto the judge (the 3a assert rows), 0 away; asserts 3 -> 0. Events
  reached: `ZeroWidthTestDeferred` in all four (153 / 531 / 20 / 451 rows), `DecidedReadDiffering`
  3 rows (fz). Perf (Release, tiering off, 359cf248 against the final engine, 10,760 partial rows,
  3 alternating runs, per-row median of 9): total 20.5 / 20.4 ms (0.994), every file 0.98-1.02,
  0 of 253 rows over 10 us more than 10% slower, answers identical. The machine was shared with
  other agents' jobs. A call without partial runs no changed code (every change sits behind the
  second pass's run-out tests or the dedup's `Count != 0`). X1 and S4 as in step 4.
  **Next:** the round's review; the X1 target decision (options in step 4).

## Fix round 3 and chunk 10 (2026-10-04, brief `brief-d11-round3-chunk10-2026-10-04.md`)

Review answered: `.claude/driver/review-d11-fixround2-2026-10-04.md`. Owner decision: X1's linear
cost (366 ms at n = 1000) is accepted until the performance phase (register D71).

- **Step 1, L1**: `MatchState.DedupEntriesExamined` now counts in every build (it was `#if DEBUG`,
  so CI's Release ratchet read 0 and the bound held for nothing), and
  `PartialForkBudgetTests.The_dedup_examines_a_bounded_number_of_entries_per_fork` asks for a count
  in 1..821,248, so a count that stopped being kept fails too. A structural count, not a wall bound
  (D13). The count runs only in the dedup, which only a partial call's second pass reaches. Witness,
  Release: with the old whole-list scans planted back (look-up and drop), the test reads
  160,662,327 and fails; with the fix it passes. Ratchet (Debug) GREEN, 8,215.
- **Step 2, B1** (search `(?:\X){d<=1}(*FAIL)|c` over 'a' P(0, 1); main, both judges and upstream
  P(1, 1)). Bisected: fcfb406d P(1, 1), bc9ab56f (chunk 4) P(0, 1). Root cause: chunk 4's
  `GraphemeBoundaryAtEdge` (Matcher.cs, the `UndeterminedRead` at its end) makes the boundary
  closing `\X` after 'a' or '\r' undetermined, as it must (a combining mark or an LF can join the
  cluster); its failing side takes the next character, runs out at the edge outside any body, and
  the rest of the pattern is presumed to match. That is limit C2 (D19), the way `.+(*FAIL)|c`
  search 'a' has always answered (port, main, upstream: P(0, 1)). Main's P(1, 1) is right only
  because main's `\X` never reads the text to come, which also makes it refuse real completions:
  search `\X(?<!a)|c` 'a', `(?:\X){d<=1}(?<!a)|c` 'a' and `\X(?<![a\r])|c` '\r' are P(1, 1) on main
  and upstream, P(0, 1) on the branch and the judge. **No engine change**: removing the read brings
  those misses back, and deciding the rest after a run-out is D19's, out of D11's scope by design
  decision 4. Family grid (8,820 rows: 15 multi-character items, exact and under `{d,e,i,s<=1}`, with
  `(*FAIL)`, `(?!)`, `(?<=c)z` tails, alone and `|c`, 6 subjects, 3 doors; branch Debug with the
  port judge over continuations up to 3 with U+0301 and '\r'; main 27b9de33 and upstream by probe):
  236 rows differ from main where main equals the judge, all one of two siblings of this class:
  172 `\X` rows (search start moved to the run-out's) and 64 match/fullmatch rows where a trailing
  insertion runs out (`(?:ab){i<=1}(*FAIL)` 'ab' P(0, 2); RC5, chunk 3; main None). Tests: the truth
  rows red in `OpenDefectTests.A_cluster_or_insertion_that_runs_out_presumes_the_rest_of_the_pattern`
  (5 rows); the sound side pinned in
  `PartialCutRuleTests.A_search_reports_the_start_whose_cluster_text_to_come_can_extend` (3 rows,
  red with chunk 4's read planted back to the held text's answer). **For the register:** D19 gains
  B1's rows; D11's row notes that the `\X` read and RC5 make C2 reachable through a cluster and a
  trailing insertion (236 grid rows), shared by upstream for greedy repeats, not for these.
- **Step 3, B2** (match `\X(*FAIL)|c` over 'a' P(0, 1), fullmatch `(?:\X){d<=1}\b` over '\r'
  P(0, 1); upstream and both judges None). Same mechanism as B1 and in the same class: the boundary
  closing `\X` is undetermined after 'a' (an Extend joins, GB9; a Control breaks, GB5) and after
  '\r' (an LF joins, GB3; anything else breaks, GB4); its failing side extends the cluster into the
  text to come and runs out, and the rest (`(*FAIL)`, or `\b` with the fullmatch end) is presumed:
  limit C2, D19. It predates D11 on main through S57d's escalation (main 27b9de33 answers P(0, 1)
  to all three anchored rows), and the branch reaches it through chunk 4's read. Chunk 10's exact
  grapheme M1 does not move it: forward, the edge test was already exact (only GB4/GB5 decide it, and
  only after a Control or LF), and both rows' boundaries are genuinely undetermined. **Not in D11's
  scope; no change.** The rows are in B1's red test (`OpenDefectTests`). **For the register:** D19
  gains these rows; D19 already covers the class, no separate row.
- **Step 4, B3** (`OracleComparer.Run` composes an ablation with D11's only when `ablate` is not
  null, `tests/FuzzyRegex.OracleTests/OracleComparer.cs`:418). Five named ablations pass only
  switches: `RunWithTheUpstreamReverseGrapheme`, `...FuzzyConditionTest`, `...ConditionalTests`,
  `...FoldedRuns` and `...CallCopies` (:1169, :1320, :1346, :1371, :1427). **It hides nothing.** The
  composed run is consulted only after the ablation alone has failed to agree, and it can only
  turn that row into an agreement (:437-:476 return `alone` otherwise); so leaving an ablation
  uncomposed can only leave a row reported as a divergence, never explain one away. A partial row
  that needed "this fix off and D11 off" would show at the oracle as a divergence, and the three
  seeds have diverge 0 (step 5 of fix round 2, rerun in step 6 here), so no row needs it today.
  **No change**: composing them would be the step that could hide something, and nothing asks for
  it. If a future wave shows such a divergence, the fix is to key the composition on "any named
  ablation" (`namedAblation` or either conditional switch) instead of `ablate`.
- **Step 5, chunk 10** (exact `(?w)` and grapheme M1 through property cores). `AtDefaultBoundary`
  is split into `DefaultBoundaryNear` (WB3-WB4, the two neighbours) and `DefaultBoundaryFar`
  (WB5-WB999 over the values WB4 leaves), and `AtGraphemeBoundary` loses GB3-GB9b to
  `GraphemeBoundaryNear`; the text versions call them, so there is one copy of each rule. In a
  partial call's second pass the three `(?w)` tests go to `DefaultWordTestInAPartialCall`, which
  enumerates the unknown places of the window: the character beside the edge over 27
  representatives (one per Word_Break x Extended_Pictographic x `\w` class of Unicode 17), a place
  further out (WB6/WB7/WB7b/WB7c/WB11/WB12's second character, WB4's walk, the regional-indicator
  parity) over the values it can take; decided when every combination agrees, else undetermined.
  Forward only the character after a mid-type right neighbour, or the edge itself, can be unknown;
  reversed, any leftward walk (WB4's skip, the character before a mid-type left neighbour, the RI
  count) that reaches the edge is open. Under `(?a)` the tests are the plain word tests with their
  M1. A reversed grapheme boundary inside the held text is undetermined when GB9c's, GB11's or
  GB12's walk reaches the edge and no held-text rule keeps the cluster together
  (`ReversedGraphemeBoundaryReadsTheTextToCome`); this replaces chunk 4's SHORTCUT. Forward
  grapheme reads were already exact (every GB rule looks right only at the neighbour).
  Assumptions (rule 10): Word_Break 0 is Other, what the first pass reads past the end, and the
  values run to Regional_Indicator; the representatives cover every class exactly
  (`PartialWindowReadTests.Every_character_class_the_default_word_rules_read_has_a_representative`
  scans all code points); a decided `(?w)` answer differs from the first pass only over an empty
  text (Debug.Assert A4 in `DefaultWordTestInAPartialCall`); the rules read a character only
  through those three properties (Matcher.cs, `DefaultBoundaryNear`/`Far`, `AtDefaultWordStartOrEnd`).
  Witnesses (rule 12), each red with its part planted out (dev-tree probe, Debug, 21 rows): whole
  mechanism 13 red; WB4's right skip W3; the inside-window read W1, W2, W3, W5, W6; the edge
  `(?w)a\B` 'a'; the empty text 3 rows; `(?a)` routing `(?wa)a\B` 'a'; reversed edge
  `(?wr)\Ba` 'a'; reversed WB4 walk '\u0301a'; reversed WB7 ':a'; reversed RI parity RI x 3; GB9c,
  GB11, GB12 one row each. The quick bound `WindowCanReachTheEdge` (performance, below): each of
  its five clauses red when weakened (W3; ':a' crashes reading before the text; RI x 3;
  ':́a'; '́:a'). The `\m`/`\M` word-class shortcut changes no answer by construction
  (it returns only where `DefaultWordTest` is false for every combination), so it has no witness.
  Tests: `PartialWindowReadTests` (24 rows, 20 red on the old engine with a stub for the
  representatives, 4 controls green); W1-W3, W5, W6 leave `OpenDefectTests`.
  Grid (Debug, 3,020 rows: `(?w)`, `(?wr)`, `(?wa)`, `(?wi)` with `\b \B \m \M` in six shapes
  over 32 subjects, reversed `\X` shapes; judge the port's non-partial matcher over continuations of
  up to 2 from a 19-character alphabet), against af3e3c49: misses 83 -> 1, 176 rows onto the judge,
  22 away and 14 other, every one checked by hand as limit C3 (a word test and `$` or the item after
  it read the same character independently) or C2 (a repeat takes a character to come and the rest
  is presumed); 0 asserts. Rule 11 (rows with the construct / rows where it read the text to come):
  `(?w)` `\b` 175/29, `\B` 208/89, `\m` 178/44, `\M` 193/43; `(?wr)` 182/31, 191/49, 178/39,
  201/24; `(?wa)` 159/27, 182/28, 171/25, 171/12; `(?wi)` 185/33, 217/83, 202/36, 174/37; reversed
  `\X` 44/5; `DecidedReadDiffering` 11 rows. Full grid (Release, 51,648 rows): 51 misses, all of two
  pre-existing causes found here, below. B1's family grid (8,820 rows): 0 moved.
  **Found here, not chunk 10's:** (1) a one-character lazy repeat that reaches the edge by
  backtracking never runs out (LAZY_REPEAT_ONE's backtracking arm keeps an entry only while
  `count < maxCount`, the text held; upstream's bound): `.*?(?<=c)` match 'aa' None, judge P(0, 2),
  upstream None; it also stops a reversed `\X` from taking a character to come (`(?r)\X` fullmatch
  'क्क'[1:]...). Red in `OpenDefectTests.A_lazy_repeat_that_reaches_the_edge_by_backtracking_still_runs_out`.
  The one-line fix moved 382 rows of a 13,650-row lazy grid onto the judge and 390 away (all C2),
  so it waits for a decision. (2) a greedy repeat's end turns its body's run-out into a failure at
  the end of the text (upstream :12575), and the tail's touched success then backtracks under
  fullmatch: `(?:.\b)+` fullmatch 'a ' None, judge and main 27b9de33 P(0, 2) (main through S57d),
  upstream None - a D11 regression against main. Keeping the run-out (and routing it through the
  run-out rule) moved 2 rows of a 5,000-row repeat grid onto the judge and 25 away, all C2, so it
  waits with (1). Red in `OpenDefectTests.A_greedy_repeat_keeps_its_body_run_out_at_the_end_of_the_text`.
  Performance (Release, `DOTNET_TieredCompilation=0`, medians of 9 batches, 3 alternating runs,
  af3e3c49 against chunk 10; machine shared with another agent's oracle run): calls without
  partial over 20 KB of mixed text, `(?w)\b` 1.019, `(?w)\b\w+\b` 1.016, `(?w)\m\w` 1.000, `\X`
  1.005, `(?r)\X` 1.012, `\b` 0.951; partial calls over the grid's 2,808 rows whose answer did not
  change 1.081 (it was 1.45 before the quick bound and the reordered representatives).
- **Step 6, checks (on 44842b2d)**: ratchet GREEN in Debug (8,242) and Release (8,160 passing,
  the rest the Debug-only counter skips; the L1 test runs). Doc examples: all ok. Oracle, Release,
  each seed alone under the test lock: seed 7 GREEN (agree 7387, expected 185, timeout 2, resource
  4, undefined 2, diverge 0 of 7580), seed 4242 GREEN (agree 7399, expected 176, resource 5,
  diverge 0), seed 20260927 GREEN (agree 7390, expected 186, resource 4, diverge 0). Performance
  as in step 5. **Next:** the blind review of this round; then the decision on the two run-out
  bounds found in step 5 (lazy repeat, greedy repeat end), then chunk 11.
