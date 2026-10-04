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

## The two run-out misses, decision (a) (2026-10-04, brief `brief-d11-runout-fixes-2026-10-04.md`)

Owner decision: fix both and accept the extra limit-C2 phantoms (ledger-21 ruling: a refused
completion is the worse error; the phantoms are D19's to remove).

- **Step 1, the lazy repeat** (Matcher.cs, LAZY_REPEAT_ONE's backtracking arm). The arm now keeps
  its entry when the count has reached the text held but not the repeat's maximum and the next
  character lies in the text to come (`RunsOutPast`; false under the oracle's pre-D11 ablation), so
  the next try runs out there. Witnesses, red before and green after
  (`PartialEdgeReadTests.A_lazy_repeat_that_reaches_the_edge_by_backtracking_runs_out`, 4 rows):
  `.*?(?<=c)` match 'aa' P(0, 2), `.{0,3}?(?<=c)` 'aa' P(0, 2), `(?r)\X` fullmatch '्क' P(0, 2),
  `(?r)(?=c).*?` match 'aa' P(0, 2); upstream None to all four. Plant-outs: without the maximum
  clause the control `.{0,2}?(?<=c)` 'aa' goes red; without the reversed half the two reversed rows
  go red. Assert A14: the try past the edge runs out (without `RunsOutRight` in `RunsOutPast` a
  call without partial would keep a dead entry and fire it). The pinned seed-7 row
  `^(\p{Lu}+?)(?(?<=\p{Nd})[A-Z])\m$` moves from None/P(4, 4) to P(0, 4) on both doors, a C2 phantom
  (judge None), and is re-pinned as such.
- **Step 2, the greedy repeat's end** (END_GREEDY_REPEAT, upstream :12575). In the second pass a
  body's run-out at the end of the text now goes through the run-out rule instead of becoming a
  failure (the count goes back first, as on the failure path); the ablation keeps upstream's
  failure. Every complete match there is the first pass's (A1), so nothing is lost. Witnesses
  (`A_greedy_repeat_keeps_its_body_run_out_at_the_end_of_the_text`, 3 rows, red on step 1 alone):
  `(?:.\b)+` fullmatch 'a ' and '1,' P(0, 2), `(?r)(?:\b.)+` ' a' P(0, 2). The run-out rule's
  part: `A_greedy_repeat_body_run_out_in_a_lookaround_goes_on_after_it` (3 rows, None): the first
  two go red when the arm answers the partial at once (that plant moved 59 rows of 21,685 in the
  witness, repeat, lazy and chunk-10 grids away); the third, `(?!(?:.\b|.){1,3}b)` match 'ab', goes
  red without the count put back (no row of those grids moved; a 22,197-row targeted grid of
  bounded repeats over alternations inside lookarounds moved 122 away). Assert A15: only a partial
  call's second pass gets a PARTIAL there.
- **Performance** (Release, `DOTNET_TieredCompilation=0`, medians of 9 batches over 3 alternating
  runs, 6d059c83 against the fix; machine shared with other agents' oracle runs): calls without
  partial over 20 KB of mixed text, `.*?\.` 1.001, `\w+?\b` 0.989, `[a-z]{1,8}?[^a-z]` 0.974,
  `(?:\w\b|\W)+` 0.993, `(?:wo)+rd` 0.979, `\X` 0.998; partial calls over the 17,767 lazy and
  repeat grid rows whose answer did not change 0.984. The added test runs only when a lazy
  repeat's count reaches the text held, and the greedy change only on a PARTIAL status.
- **Step 3, grids** (MAXW 2; before = 6d059c83, after = both steps; Debug except the full grid's
  answers, which are Release and Debug alike):

  | grid | rows | onto judge | away | other | misses before -> after |
  |---|---|---|---|---|---|
  | lazy | 13,650 | 382 | 410 | 64 | 332 -> 0 |
  | repeat | 5,000 | 2 | 25 | 0 | 2 -> 0 |
  | chunk 10 (Debug) | 3,020 | 1 | 4 | 5 | 1 -> 0 |
  | full (Release) | 51,648 | 62 | 137 | 42 | 51 -> 0 |

  Every away and other row is limit C2 by tool (`.scratch/ro/classify.py`): its answer is a
  partial that a run-out outside any body answered (new Debug event `RunOutAnswered`: the rest of
  the pattern is presumed) and one of the two mechanisms fired (`LazyRepeatPastTheEdge`,
  `GreedyBodyRunOutKept`); 0 unclassified. The "other" rows are earlier search starts (for example
  `(?r)(?=bc)[ab]+?` search 'ab' P(0, 2), judge P(0, 0)), C2 at a phantom start. Spot-checked by
  hand: `(?:ab)*(?<=c)` search 'ab' (P(0, 2): `(?<=c)` never follows `(?:ab)*`), `(?:\w\B)*`
  fullmatch 'a'. Rule 11, rows where each mechanism fired, all grids: lazy past the edge 1,561
  (forward 831, reversed 730; match, fullmatch and search each over 190), 19 of them inside a body;
  greedy run-out kept 232 (reversed 15), 112 inside a body. Asserts: 0 rows.
- **Found here, not fixed (pre-existing, upstream shares it)**: `(?=(?:ab)+$)(*FAIL)` and
  `(?=(?:ab)+?$)(*FAIL)` match 'ab' answer P(0, 2), judge None, upstream P(0, 2); no run-out
  event fires, so a PARTIAL reaches the caller past the run-out rule by another `return` (D69's
  class). Also `(?=(?:.\b)+?$)(*FAIL)` 'a ' P(0, 2), judge and upstream None: END_LAZY_REPEAT's
  body try returns a PARTIAL directly (`return bodyStatus`).
- **Step 5, texts for the register and the ledger.**
  - *D19, add to the row:* "More (D11 fix round 3, review B1/B2, 2026-10-04): an undetermined
    boundary closing `\X` extends the cluster into the text to come and presumes the rest: search
    `(?:\X){d<=1}(*FAIL)|c` over 'a' P(0, 1) (main, both judges, upstream P(1, 1)), match
    `\X(*FAIL)|c` 'a' and fullmatch `(?:\X){d<=1}\b` '\r' P(0, 1) (judges and upstream None; main
    P(0, 1) through S57d). The run-out fixes of 2026-10-04 (decision (a)) add C2 phantoms by design:
    a lazy repeat backtracked to the edge and a greedy body's run-out at the end now take a
    character to come, `.*?(?<=b)`-shaped rows (410 + 64 of a 13,650-row lazy grid, 137 + 42 of the
    51,648-row full grid), and the pinned `^(\p{Lu}+?)(?(?<=\p{Nd})[A-Z])\m$` is P(0, 4) on both
    doors."
  - *D11, add to the row:* "Limit C2 is reachable through a cluster and a trailing insertion:
    chunk 4's `\X` read and RC5 let a `\X` or a trailing insertion run out and presume the rest
    (236 rows of B1's 8,820-row family grid: 172 `\X` search starts, 64 trailing insertions such as
    `(?:ab){i<=1}(*FAIL)` match 'ab' P(0, 2)); upstream shares C2 for greedy repeats, not for these.
    Both run-out misses fixed 2026-10-04 (decision (a)): full grid misses 51 -> 0."
  - *Ledger entry at merge (upstream divergence, step 1 and step 2):* upstream bounds a lazy
    one-character repeat's backtracking by the text held (`LAZY_REPEAT_ONE`, the entry is kept only
    while `count < max_count`, with `max_count` capped by the text) and turns a greedy body's
    run-out at the end of the text into a failure (`_regex.c:12575`), so it refuses partials that
    a continuation completes: `.*?(?<=c)` match 'aa' (with 'c' to come), `(?r)\X` fullmatch '्क'
    (a consonant to come, GB9c) and `(?:.\b)+` fullmatch 'a ' ('b' to come) answer None upstream
    (2026.9.10) and P(0, 2) here, the port judge's answer. Pinned by the two
    `PartialEdgeReadTests` above; the oracle's pre-D11 ablation keeps upstream's bounds.
- **Step 4, checks** (code 5b4f52d2 plus the count witness row): ratchet GREEN in Debug (8,253)
  and Release (8,171 passing, the rest the Debug-only counter skips). Doc examples: all ok.
  Oracle, Release, each seed alone under the test lock: seed 7 GREEN (agree 7387, expected 185,
  timeout 2, resource 4, undefined 2, diverge 0 of 7580), seed 4242 GREEN (agree 7399, expected
  176, resource 5, diverge 0), seed 20260927 GREEN (agree 7390, expected 186, resource 4, diverge
  0). Performance above, no path slower. **Next:** the blind review of fix round 3 + chunk 10 +
  this step together; then chunk 11. Open, for the review: the two pre-existing lookaround
  phantoms found in step 3 (a PARTIAL returned past the run-out rule).

## The repeat run-out rule (2026-10-04, brief `brief-d11-repeat-runout-chunk11-2026-10-04.md`)

Review answered: `.claude/driver/review-d11-round3-chunk10-2026-10-04.md` (F1, item 7, S1, S2).

**The rule.** A test that runs out at a choice point stands for its alternative having been
entered and run out at its first item: the arm keeps on the backtracking stack every other
alternative it would have kept had it entered that one, then takes the run-out through `runOut`,
never a `return`. Inside a lookaround body the run-out goes on at the body's end with a touch, and
a blocked end backtracks into the body, so the kept alternatives are tried and a success of one on
the held text decides the construct (design 4.2's blocked-end rule, unchanged); outside every body
the run-out answers the partial as before and the entries go with the attempt. The alternatives
are kept only where the run-out goes on in a body whose outcome feeds a decision
(`RunOutFeedsADecision`, widened by the pre-merge fixes below; this paragraph first said "a negative
body", and its claim that a positive body's kept alternatives "only cost time" was wrong: see the
pre-merge fixes, P1).

F1, item 7 and S1 are this one cause: an arm that answered the partial at once (`return`), or went
to `runOut` with its other alternatives dropped (END_GREEDY_REPEAT's body since 5b4f52d2,
GREEDY_REPEAT_ONE's count at the edge, the BRANCH test), so a negative body's definite success
was never tried. The run-out keeps its place as the first thing tried rather than the last, which
keeps upstream's order (and so its answers and captures) outside bodies. (This first said the order
changes no answer set; it does, for a positive lookaround whose kept success came after a blocked
one: the dedup took it for a repeat. The pre-merge fixes' decided end corrects that.) The
reverse order (definite alternatives first, the run-out as a deferred entry) was weighed and not
taken: a definite alternative tried first reaches the cuts of constructs between the repeat and the
lookaround before the run-out has marked anything, so an atomic group would commit and discard the
deferred run-out, a refusal: `(?!(?>(?:ab)*)(?<=^ab))ab` over 'ab' is completed by 'ab' + 'ab',
and only a new mark kind that blocks inner cuts but not the lookaround itself, plus verb handling,
would make it sound.

Sites (`Matcher.cs`): the four multi-character arms through one helper,
`KeepTheAlternativeThatDidNotRunOut` (the entry is the MATCH_BODY / MATCH_TAIL record the arm
parks when both alternatives could match; the END_ arms push BODY_END first, which replaces
5b4f52d2's `--rpData.Count`); GREEDY_REPEAT_ONE's count at the edge (its smaller counts and the
tail at the held count, a BRANCH entry); GREEDY_REPEAT_ONE's backtracking arm
(`KeepTheSmallerCounts`); BRANCH's first-alternative test. LAZY_REPEAT_ONE's three run-outs keep
nothing: at the edge taking a character runs out too, and short of it only a string test runs out,
leaving fewer characters than the string, so every longer count's test fails or runs out (no row
of a 5,824-row grid moved with the longer counts kept). The fuzzy retry returns: see step 3.

**Cost (the orchestrator's question, 2026-10-04).** Per run-out the arm pushes O(1) more: one
MATCH_BODY or MATCH_TAIL record (BODY_END at the END_ arms replaces the count decrement), or one
GREEDY_REPEAT_ONE and one BRANCH entry; no new state, nothing outside a partial call's second pass
(every change sits behind a PARTIAL status, which only the second pass produces). What can grow is
the later exploration of the kept alternatives, and they are exactly the alternatives the first
pass explores at the same choice point, where the test fails instead of running out. So, per
attempt, a body's exploration is bounded by the first pass's exploration of the same body on a
completed text plus its run-outs, and the forks it adds (a positive lookaround's further blocked
successes) are bounded by the dedup (same captures and counts) and the per-attempt budget
256 (|t| + 1). A negative lookaround only gains decisions: an unblocked success ends it with no
fork. So the ordering cannot change a pattern's complexity class; with nesting depth d the bound is
the first pass's own order on the completed text, which is O(n^(d+1)) for d nested lookarounds over
repeats that scan to the end.

Measured before building (prototype, Release, tiering off, median of 3, head b811c5ea against the
rule, `.scratch/d11rr/cost/`), match over 'a' * n + 'c' or 'ab' * n/2, n = 125 / 250 / 500 / 1000:

| row | head ms at 1000 | rule ms at 1000 | growth (both) |
|---|---|---|---|
| X1 `(?:(?=(.*)z\|())a)*b\1\2` | 395 | 424 | linear (budget) |
| `(?:(?=(a*))a)*b\1` | 3.5 | 3.7 | quadratic |
| `(?:(?!(a*)c)a)*b` over 'a' * n | 3.7 | 7.9 | quadratic (head drops the smaller counts) |
| `(?:(?=((?:ab)*))ab)*c\1` | 20.7 | 21.5 | quadratic |
| `(?:(?!(?:ab)*(?<=a))ab)*c` | 24.9 | 26.6 | quadratic |
| `(?:(?=(?:ab)*(?<=b))ab)*c` | 20.4 | 21.7 | quadratic |
| search `(?=(.*)z)a` over 'a' * n | 88.8 | 91.2 | quadratic |
| nested `(?:(?=(?:(?=(?:ab)*(?<=b))ab)*)ab)*c` | 20.6 | 1 GB stack limit | see below |

The nested row is the deliberate adversary (a repeat inside a lookahead inside a repeat inside a
lookahead inside a repeat), and it does change, but not through the rule: with the kept
alternatives planted out (only the `return`s routed to `runOut`) it costs the same (n = 500 780 ms
against 730 ms; n = 1000 the 1 GB backtracking-stack limit in both). Head answered it in 20 ms only
because GREEDY_REPEAT's body test returned the partial at once (item 7) at the first lookahead that
reached the edge. Routed through the run-out rule, every blocked lookaround keeps its body's
entries (design 4.2, amendment 2), so the inner lookahead keeps O(n) entries per instance, the
middle one O(n^2) and the outer path O(n^3): time 8 / 62 / 730 ms at n = 125 / 250 / 500 against
9 / 54 / 428 ms for the first pass over the completed text ('ab' * n/2 + 'c', the pattern's own
cubic order; 3.3 s at n = 1000), and memory past the stack's 1 GB limit at n = 1000, which the first
pass never reaches. The one-character twin (`[ab]*` innermost) never went through item 7 and costs
the same on head and the rule (687 / 749 ms at n = 1000, no limit hit). **For the register (owner):**
a partial call's second pass keeps every blocked body's entries, so stack memory grows as
n^(depth + 1) for nested lookarounds over repeats; options: (a) accept, as D71 accepted X1's time;
(b) count stack bytes in the fork budget so the call answers the budget's C5 partial instead of
the stack limit; (c) the performance phase. Recommendation (b) if the owner wants no exception
from a partial call, else (a).

- **Steps 1-2, witnesses** (Debug, the port's non-partial judge over continuations of up to three
  characters; red at b811c5ea, green after): `PartialLookaroundRuleTests.A_run_out_at_a_choice_point_keeps_the_other_alternatives`
  (11 rows: F1 and its empty, `..` and `.*` rests, S1's four, the branch `(?!ab|a)` 'a', END_LAZY's
  `(?!(?:ab)*?c|ab)` 'ab', the retreat `(?![ab]{0,2}aa)` 'aa'), `A_repeat_test_that_runs_out_in_a_body_goes_on_after_the_lookaround`
  (11 rows, one or more per item-7 site), `An_atomic_group_around_a_repeat_that_runs_out_keeps_the_lookahead_undetermined`
  (`(?!(?>(?:ab)*)(?<=^ab))ab` 'ab' P(0, 2), completed by 'abab'; upstream and main None, a refusal).
  Limit C2, red in `OpenDefectTests.A_run_out_in_a_lookahead_presumes_the_rest_of_its_body`: F1's
  possessive and atomic forms (P(0, 2), judge None: the group commits to the run-out's longer path
  where it exists, and deciding that the rest still holds is D19's) and F1's search (P(2, 2), its
  value before 5b4f52d2; the attempt from inside the text to come). Plant-outs (Debug, witness
  rows): the shared keep out, 7 rows red (F1 x 4, `(?!(?:ab)+)ab`, `{1,3}`, search); GREEDY_REPEAT's
  body `return` back, 4 red; END_LAZY_REPEAT's body `return` back, 6 red; GREEDY_REPEAT_ONE's
  kept counts out, 2 red (S1's one-character rows); BRANCH's kept entry out, 1 red; the
  backtracking arm's smaller counts out, 86 of a 5,824-row grid of bounded repeats before a string
  (`.scratch/d11rr/retreat.jsonl`). The fuzzy retry returns (`if (status < 0) return status;` after
  `RetryFuzzyMatchItem` and kin): a temporary `Debug.Assert` that no PARTIAL passes them in the
  second pass never fired over 52,704 rows (the look grid below and 18,144 fuzzy-in-body rows), so
  they are left as they are, no witness.
- **Step 3, the grid gap and S2.** New grid `tools/probes/d11-runout-look-grid.py` (34,560 rows:
  repeats, a branch and fuzzy sections in negative and positive lookaheads whose tail is decided on
  the held text, `(?<=b)`, `(?<=a)`, `(?<!b)`, `(?<=^ab)`, empty; atomic and possessive forms;
  six subjects, three doors). Debug, MAXW 2, b811c5ea against the rule: 2,754 onto the judge, 0
  away, 560 other (every one a search whose phantom start moved later, to P(|t|, |t|), the attempt
  from inside the text to come), misses 0 both, mismatches 7,335 -> 4,581, 0 asserts. Rule 11, rows
  with the construct / rows where the alternatives were kept: negative multi-character 5,400 /
  1,616, one-character or branch 10,800 / 3,169, atomic or possessive 7,020 / 2,649, fuzzy 2,700 /
  0 (a fuzzy test node is never consulted); positive 8,640 / 0, as the restriction says.
  **S2 fixed**: the engine records which mechanism produced the run-out that answered
  (`MatchState.NoteRunOutCause` / `NoteRunOutTaken`, Debug only; events
  `RunOutAnsweredByLazyPastTheEdge`, `RunOutAnsweredByGreedyBodyKept`), and
  `tools/probes/d11-classify-runout.py` labels a row C2 only on those. Re-classified, 6d059c83
  against the rule: lazy grid 410 away / 64 other, repeat 25 / 0 (and 91 onto, was 2), chunk 10
  4 / 5, full 137 / 42: **every one still C2 by the causal label, 0 unclassified**. F1's `.*` row,
  which the old tool labelled C2, is None now.
- **Step 4, chunk 11, the port grid of 8.1** (`tools/probes/d11-port-grid.py`, seeds 1-3, 15,000
  random + 15,000 shaped patterns' rows each, 90,011 rows; flags `(?w) (?wr) (?a) (?wa) (?p) (?b)
  (?e) (?r) (?m)`, `\X`, `{e<=1}`, `{i<=1}`, a Hebrew letter, '"', ',', ':', the three fixed
  subjects, fuzzy sections in bodies, group calls and recursion in bodies, the model's three named
  semantics rows; Debug, MAXW 2; main 2c51e356 by answers only): misses 0 / 0 / 1 (main 537 / 490 /
  559), 0 asserts, parse errors 6,325 (random patterns the port rejects). Every counter non-zero but
  `MemoReplay` and `GuardRefused` (the memo and the guard rule were not built) and `BudgetFired`
  (no row needs it; X1 and the budget tests cover it). Moved against main: 4,204 onto the judge;
  away 1,367: C2 787 (the answering run-out lay outside every body), C2 in a body 92 (a run-out
  inside a body presumed the rest of the body: mostly the attempt from inside the text to come),
  C3 478 (a second-pass success that read the text to come twice or more), C4 10 (a blocked cut
  kept both outcomes; D18); 0 unclassified. "Other" 352, same classes. The one miss is
  pre-existing (main answers None too): search `(?wr)(?:^(*SKIP)(?<=\b$^)|(\Z(*SKIP))(*FAIL))`
  over '' answers None where '\n' before it completes the match at (0, 0) (upstream 2026.9.10:
  None, and (0, 0) for '\n'); a reversed search with `(*SKIP)` from inside the text to come, not
  the run-out rule's class. **For the register (owner):** a row for it (D11's search rule 3).
- **Step 4, section 9 benchmarks** (`.scratch/d11rr/bench/`, one harness built against the rule,
  b811c5ea and main 2c51e356; Release, tiering off, median of 5 timed calls, median of 3 alternating
  runs; the machine shared with another agent's test run). Calls without partial, 100 KB of mixed
  text: atomic, possessive, lookbehind/lookahead, lookahead in a repeat, verbs, verbs in a repeat,
  lazy, a failing search: 0.96-1.05 against b811c5ea and 0.96-1.10 against main (the 1.10 is
  `(?:a(*PRUNE)b|ab)+` at 5 ms, 1.05 against b811c5ea; no changed code runs without a PARTIAL
  status). Partial calls: f9b67f9's eight cases at 10 KB (and `(?w)(?:a\B)+` at 100 KB) 0.92-1.05
  against b811c5ea, 0.50-1.00 against main; section 9's rows, the verb-heavy partial search and the
  run-out adversaries 0.95-1.05 against b811c5ea except four rows:

  | row | rule ms | b811c5ea ms | the first pass over a completed text, ms |
  |---|---|---|---|
  | `(?:(?!.*z)a)*b` over 'a' * 1000 + 'c' | 6.5 | 2.7 | 6.7 ('a' * 1000 + 'b') |
  | search `a*(?!.*z)b` over 'a' * 1000 + 'c' | 2,214 | 886 | 2,133 ('a' * 1000 + 'cb') |
  | `(?:(?!(a*)c)a)*b` over 'a' * 1000 | 7.0 | 3.1 | 6.8 ('a' * 1000 + 'b') |
  | `(?:(?=(?:(?=(?:ab)*(?<=b))ab)*)ab)*c` over 'ab' * 125 | 51 | 1.1 | 54 at 'ab' * 125 + 'c' |

  The first three are negative bodies whose one-character repeat reached the edge: b811c5ea
  dropped the repeat's smaller counts, so the negative stayed undetermined after one scan; the rule
  tries them, which is the work the first pass does over the completed text, and the time now equals
  it. The fourth is item 7's routing (above, "Cost"). So these rows are over the 10% budget against
  b811c5ea, at the first pass's own cost; the answers are unchanged on all four. **Decided
  (orchestrator, 2026-10-04): accepted.** Each costs what the first pass costs over the completed
  text, so it is the price of a decided answer, not overhead: b811c5ea was faster only because it
  dropped the alternatives a negative body needs (the first three) or answered through item 7's
  bypass (the fourth, now bounded by the stack budget below).

## Merge texts (step 5 of the repeat run-out brief; the owner edits the register and the ledger)

**Register (`docs/KNOWN-DEFECTS.md`):**

- *D11, to Completed:* "Fixed on `maint/d11-partial-boundary` (design
  `docs/plan/2026-09-28-partial-boundary-design.md`, chunks 1-11, amendments 1-3; status note
  `docs/plan/2026-10-03-d11-build-status.md`). The second pass of a partial call decides reads of
  the text to come three-valued (M1), marks paths that read it and blocks the cuts and decisions
  that depend on them (M2), turns a second-pass success into the partial (M3) and bounds its forks
  (M4); a run-out inside a lookaround body goes on after the body, and a choice point keeps its
  other alternatives before a run-out in a body whose outcome feeds a decision (the repeat
  run-out rule, 2026-10-04, and the pre-merge fixes).
  Port grid of design 8.1 (90,011 rows, three seeds, Debug): misses 0 / 0 / 1 against main's
  537 / 490 / 559, the one pre-existing (D73 below); every row that moved away from the judge is
  one of the limit classes, after the pre-merge fixes and by cause (P2 below): 1,359 rows
  away from the judge, 435 labelled by cause alone (C2 by a mechanism 15, C2 with nothing blocked in
  the call 349, C3 with nothing blocked 71), the other 924 by their events (C2 in or after a body
  592, C2 after a blocked construct 187, C3 after a blocked construct 137, C4 8), none of them P1's
  shape (forcing every body to keep its alternatives moves none). Limits left: C2 is D19; C3, C4 and C5 are
  D18. Limit C2 is reachable through a cluster and a trailing insertion (chunk 4's `\X` read and
  RC5: 236 rows of B1's 8,820-row family grid, such as `(?:ab){i<=1}(*FAIL)` match 'ab' P(0, 2));
  upstream shares C2 for greedy repeats, not for these. The two run-out misses of fix round 3 are
  fixed (decision (a), full grid misses 51 -> 0)."
- *D11-R1, to Completed:* "Fixed by the repeat run-out rule (2026-10-04): a repeat's test that runs
  out inside a lookaround body goes through the run-out rule; `(?=(?:(a))*b)[xy]` over 'aa' is
  None."
- *D26, D36, D38, D69, D70, to Completed*, each "fixed on the D11 branch, merged with D11": D26 by
  chunk 3's RC5 and the undetermined fuzzy retry (`PartialLookaroundRuleTests.Only_the_failing_side_of_an_undetermined_fuzzy_test_reaches_the_insertion`,
  not chunk 6 as the row says); D36 by chunks 6-7; D38 by chunk 9's fix round (`WMarkKind.Held`),
  its C4 forms registered under D18 (`OpenDefectTests.A_blocked_cut_keeps_the_alternatives_made_after_the_read`);
  D69 by 597b6081; D70 by fix round 2 (`PartialEdgeReadTests.A_search_with_no_first_set_still_tries_a_start_in_the_text_to_come`).
- *D19, add:* "More (D11, 2026-10-04): B1/B2's `\X` rows (search `(?:\X){d<=1}(*FAIL)|c` over 'a'
  P(0, 1), match `\X(*FAIL)|c` 'a' and fullmatch `(?:\X){d<=1}\b` '\r' P(0, 1)); decision (a)'s
  phantoms by design (410 + 64 of a 13,650-row lazy grid, 137 + 42 of the 51,648-row full grid,
  every one C2 by the causal label; the pinned `^(\p{Lu}+?)(?(?<=\p{Nd})[A-Z])\m$` is P(0, 4)); and
  inside a lookahead's body, `(?!(?:ab)*+(?<=b))ab` and `(?!(?>(?:ab)*)(?<=b))ab` match 'ab' and
  search `(?!(?:ab)*(?<=b))ab` 'ab' (`OpenDefectTests.A_run_out_in_a_lookahead_presumes_the_rest_of_its_body`)."
- *D18, add:* the port grid's C3 and C4 rows by cause (P2 below: 71 + 137 C3, 8 C4).
- *New, D73:* "Partial search from inside the text to come with a reversed `(*SKIP)`: search
  `(?wr)(?:^(*SKIP)(?<=\b$^)|(\Z(*SKIP))(*FAIL))` over '' answers None (main and the D11 branch;
  upstream 2026.9.10 None) where '\n' before it completes the match at (0, 0). Found by D11's 8.1
  port grid, 2026-10-04; one row of 90,011."
- *D11's row, add (performance, closed by the stack budget):* "A partial call's second pass keeps
  every blocked lookaround body's entries, so its stacks grow as n^(depth + 1) for lookarounds
  over repeats nested `depth` deep; `(?:(?=(?:(?=(?:ab)*(?<=b))ab)*)ab)*c` over 'ab' * 500 threw at
  the 1 GB backtracking-stack limit. Fixed before the merge (orchestrator's option (b)): past 256 MB
  on the two stacks the attempt answers the fork budget's partial (C5), so stack memory is at
  most 256 MB plus 256 steps' pushes, whatever n and the depth."

**Ledger (`docs/plan/upstream-reports/LEDGER.md`, DRAFT - NOT FILED, numbers after 66 at merge):**

1. *Upstream's lookaround phantom* (design section 5, the entry the merge was to write): upstream
   ends the whole match with a partial when anything inside a lookaround body runs out of text,
   so a lookahead that no continuation satisfies still yields a partial: `(?=..)[ab]` match 'x',
   `(?!.a|x)x` 'x' (P(0, 1) upstream, None here); and from a repeat's or a branch's test node,
   `(?=(?:ab)+$)(*FAIL)`, `(?=x(?:ab)*cd)(*FAIL)` 'xc', `(?!(?:ab)+)ab` 'ab', `(?!ab|a)` 'a',
   `(?![ab]{0,2}aa)` 'aa' (P upstream, None here). Root: every `return RE_ERROR_PARTIAL` path in
   `basic_match` (the try-match sites at `_regex.c:12571-12597`, `:13235-13258`, `:12755-12780` and
   their lazy twins), with no notion of the lookaround around it. Pinned by
   `PartialLookaroundRuleTests` (the four tests named above and chunk 6's).
2. *Decision (a)* (text in "The two run-out misses", step 5 above): upstream bounds a lazy repeat
   by the text held and turns a greedy body's run-out at the end into a failure (`:12575`), which
   refuses completions: `.*?(?<=c)` match 'aa', `(?r)\X` fullmatch '्क', `(?:.\b)+` fullmatch 'a ';
   and inside a lookahead, `(?!(?>(?:ab)*)(?<=^ab))ab` match 'ab' (completed by 'abab'; upstream
   None).
3. *D70* (text in fix round 2, step "D70" above): no start inside the text to come for a pattern
   with no first set (five search rows, None upstream).
4. *The definitions the design took* (decisions 1 and 3): `\b` over '' is P(0, 0) on match and
   search (upstream None), and `\Z` at the edge is false in the second pass, so `a\Zb` over 'a' is
   None (upstream and PCRE2 P(0, 1)); ledger 21 and 27 gain notes that S57d's escalation is gone.

Every entry gets its `ExpectedDivergences` pin (the oracle's pre-D11 ablation keeps upstream's
behaviour for the rows of 1-3), DIVERGENCES.md and COMPARISON.md rows, and a draft report; those
edits are the merge's, after the owner's approval of the texts.

## Checks (step 4's oracle and step 5, on d33dbf57, main 610542e6 merged)

- Merge of local main (b3819910..610542e6): conflicts only in the register (both sides kept: the
  branch's D11-R1 row and main's D70-D72), STATUS.md, the stamp and the baseline's header; main's
  engine change (the oracle-only call-memo key control) merged cleanly.
- Ratchet GREEN in Debug (8,278) and Release (8,196 passing, the rest the Debug-only skips).
  `tests/parity-baseline.json` regenerated with `-UpdateBaseline` (8,170 ids; no removals, so no
  `-AcceptRemovals`), CRLF normalised. `tools/check-doc-examples.ps1`: all ok.
- Oracle, Release, each seed alone under the test lock: seed 7 GREEN (agree 7387, expected 185,
  timeout 2, resource 4, undefined 2, diverge 0 of 7580), seed 4242 GREEN (agree 7399, expected
  176, resource 5, diverge 0), seed 20260927 GREEN (agree 7390, expected 186, resource 4,
  diverge 0). Waves `partial,partial-sliced,interactions,verbs,matrix` at seeds 101-104: diverge 0
  each (agree 1416 / 1436 / 1419 / 1444 of 1,500; expected 72 / 58 / 71 / 51).
- `(?=(?:(a))*b)[xy]` over 'aa' (D11-R1): None, the judge's answer.

**Chunk 11's gate:** no row moved away from the judge that is not C2, C3 (or C4, D18's, 10 rows) by
the classifier; misses 1 of 90,011, pre-existing on main (D73); oracle diverge 0; the 10% budget
held on every path but the four partial rows of the benchmark table, each at the first pass's own
cost over the completed text. **Next:** the pre-merge blind review.

## The second pass's stack budget (2026-10-04, orchestrator's decision (b) on the nested adversary)

**The bound, on paper, before building.** A blocked end moves the body's saved-stack entries onto
the backtracking stack and REOPEN moves them back, so neither changes the bytes the two stacks hold
together; the total grows only by ordinary pushes, at most P bytes a step, where P is the largest
single step's push (a frame's captures, repeats and fuzzy state: O(pattern size), independent of
n). Unbounded, the second pass of lookarounds over repeats nested d deep holds O(n^(d + 1)) bytes
(each blocked body keeps its entries for the continuations where it matched otherwise). With a
budget B checked at the cancellation gate (every 256 steps), the stacks hold at most B + 256 P
bytes, whatever n and d. B = 256 MB (`Matcher._secondPassStackBudget`), a quarter of the 1 GB limit;
a stack throws when its doubling would reach the limit, at a count of 512 MB, so the budget fires
first for any P under 1 MB. Past B the attempt sets `ForkBudgetSpent` and answers
`ForkBudgetPartial` (a C5 phantom at worst; a partial never refuses a completion). The fork-budget
constant is unchanged.

**Measured** (Release, tiering off), `(?:(?=(?:(?=(?:ab)*(?<=b))ab)*)ab)*c` match:

| text | partial call | plain call, same text | plain call, text + 'c' |
|---|---|---|---|
| 'ab' * 125 | 167 ms, P(0, 250) | 0 ms, None | 51 ms, F(0, 251) |
| 'ab' * 250 | 629 ms, P(0, 500) | 0 ms, None | 377 ms, F(0, 501) |
| 'ab' * 500 | 407 ms, P(0, 1000) (the budget; threw before) | 0 ms, None | 3.0 s, F(0, 1001) |
| 'ab' * 1000 | 420 ms, P(0, 2000) (the budget) | 0 ms, None | 24.3 s, F(0, 2001) |

The plain call is bounded in memory (the first pass keeps one body's entries; its time is the
pattern's own cubic order on the completed text, and on the held text the required 'c' rejects it
at once). The partial call's time is now bounded by the budget too (about 0.4 s to reach 256 MB).

**Witness:** `PartialForkBudgetTests.A_second_pass_that_keeps_too_many_bytes_answers_the_budget_partial`
('ab' * 500, P(0, 1000)); with the budget raised out of reach (1 TB) it fails with the 1 GB
`InvalidOperationException` (Debug, 13 s). New Debug event `StackBudgetFired`. **No row moved**
(Debug, against the previous engine): look grid 34,560, port grid 90,011, lazy 13,650, repeat
5,000, full 51,648: 0 moved, `StackBudgetFired` on 0 rows. Ratchet GREEN in Debug (8,279) and
Release (8,197 passing); oracle seed 7 GREEN (agree 7387, expected 185, timeout 2, resource 4,
undefined 2, diverge 0 of 7580). A call without partial pays one test of `PartialSide` every 256
steps.

## Pre-merge fixes (2026-10-04, brief `brief-d11-premerge-fixes-2026-10-04.md`)

Review answered: `.claude/driver/review-d11-premerge-2026-10-04.md` (P1-P5).

**P1, the decision.** The review's rows are positive lookaheads whose outcome a decision reads (a
negative lookahead, a condition, an atomic group or a verb around or after them). Two things went
wrong: the alternatives were not kept there, and when they were (the review's plant) the kept
success came after the run-out's blocked one, so the dedup took it for a repeat and cut and failed;
a third, the verb case, was a verb taint left by the blocked path's skipped `(*PRUNE)` that stopped
the decided path's `(*PRUNE)` from acting. Option (i) of the brief, built in three parts:

1. **Scope** (`RunOutFeedsADecision`, replacing `RunOutIsInANegativeBody`): keep the alternatives
   when the innermost body is negative or a condition's test, or a positive lookaround with a verb
   anywhere in the pattern or an atomic group, a condition's test or a negative lookaround open
   around it. Asked from `Pstack` at the run-out, O(depth); a plain positive lookahead keeps nothing.
2. **The decided end** (`EndALookaroundInTheSecondPass`): a positive lookaround or test whose end
   is not blocked, whose frame carries a cut taint from an earlier blocked success, and whose
   success leaves the same state for every continuation (no capture read while matching; nothing
   fuzzy, or the lookaround outside every fuzzy section) removes its own taint and cuts, before the
   dedup. Argument: the success read nothing of the text to come, so the construct succeeds for
   every continuation; only the position matters after it, and it goes back to the start; the
   dedup's premise (a later success carries no fewer marks) is false here. New event
   `DecidedAfterBlocked`.
3. **A later verb acts** (`ActingEndsTheScopeAsTheSkippedVerbDid`): a verb whose path is not
   blocked acts although an earlier verb of its scope was skipped, when acting ends the scope as the
   skipped one would have: always for a construct's scope; for the attempt, when the skipped verb is
   the same verb (`MatchState.SkippedAttemptVerb`: a `(*SKIP)`'s position or -1 for `(*PRUNE)`).
   In the continuations where the skipped verb acted the scope ended already; in the others this
   one acts; both end the scope in the same state. The taint still blocks the scope's own decision
   (Q1, T2).

Cost, on paper and measured (Release, tiering off, median of 3; n = 1000):

| part | cost per event | worst row | rule | 5a660a48 | the first pass, completed text |
|---|---|---|---|---|---|
| scope | O(depth) walk at a run-out; the kept alternatives are the first pass's own at that choice point | X2 `(?:(?(?=.*z)a\|a))*b` over 'a' * n + 'c' | 47.7 ms | 3.5 ms | 50.4 ms ('a' * n + 'b', where `.*z` retreats in full) |
| scope | as above | `(?:(?>a(?=.*z)\|ab))*c` over 'ab' * 1000 | 14.8 ms | 6.2 ms | - |
| decided end | O(marks above the frame), once per end; cuts, so it removes work | - | - | - | - |
| verb | O(1) | - | - | - | - |

X2 grows as n^2 in both passes (n = 500 / 1000 / 2000: 13.0 / 49.2 / 201 ms; first pass over
'a' * n + 'b' 12.0 / 50.4 / 194 ms): the second pass now costs what the first pass costs over a
completed text in which the test fails, which is the design's criterion; 5a660a48 was cheaper only
because it dropped the alternatives a condition's test needs. Section 9's plain positive rows pay
nothing (`(?:(?=.*z)a)*b` 0.95, its `|` twin 0.93, S4 0.99, X1 1.00, `(?=(?:a*)*b)` 0.93 against
5a660a48). Adversary: lookarounds nested d deep, each feeding a decision, cost the first pass's
O(n^(d + 1)) over the completed text; the stack budget bounds their memory.

Witnesses (red at 5a660a48, green now):
`PartialLookaroundRuleTests.A_positive_lookahead_decided_on_the_held_text_decides_what_encloses_it`
(9 rows: the review's negative, condition, atomic and `(*PRUNE)` rows, and main's own four
phantoms `(?!(?=ab|a))a`, `(?(?=ab|a)x|a)`, `a(?!(?=b*(?<=b)))b`, `(?!(?=[ab]*(?<=b)))ab`).
Plant-outs (Debug, its rows): the positive keep out, 7 red; the condition clause back to negative
tests only, the 2 condition rows; the decided end out, 7 red; the verb change out, the `(*PRUNE)`
row. The `(*SKIP)` row `(?:(?=(?:ab)*(?<=b))(*SKIP)x|ab)` is None too (grid).

Grids (Debug, MAXW 2): the review's targeted grid (5,760 rows: the four wrappers over the look
grid's cores, five tails, 'a' / 'ab' / 'abab', match and search): rows main answers right and the
branch wrong 153 -> 49, misses 0; 961 rows moved onto the judge against 5a660a48. The 49 are all
possessive forms (`(?!(?=(?:ab)*+(?<=b)))ab`), F1's possessive class: the repeat's run-out inside
the possessive group leaves the rest of the lookahead presumed (C2, D19), and main's None is
upstream's failure at END_GREEDY_REPEAT, which refuses the sibling
`(?!(?=(?:ab)*+(?<=^ab)))ab` over 'ab' (main None; the branch and the judge P(0, 2), 'ab' + 'ab').
The look grid now has the five wrappers (`tools/probes/d11-runout-look-grid.py`, 56,160 rows): misses
0; against main, 213 rows main answers right and the branch wrong, every one through decision
(a)'s mechanisms (209 `GreedyBodyRunOutKept`, the F1 possessive and atomic forms among them; 4
`LazyRepeatPastTheEdge`), the owner-accepted C2 class whose main answers come from upstream's
bounds that refuse completions elsewhere (before the fuzzy widening of the decided end there were
13 more, fuzzy, now 0). Port grid (90,011): misses 0 / 0 / 1 (D73), 69 rows onto the judge, 0
away, 6 other (later phantom starts). Lazy, repeat and full grids: 0 moved. Fuzzy-in-body grids (fz2, fz3, and a new
3,000-row fz4 with fuzzy sections after the lookahead): misses 0; fz4 against main 0 regressions,
51 rows fixed.

**P2.** `.scratch/d11pm/away2.py` labels a moved row only by cause: C2 by a mechanism
(`RunOutAnsweredBy*`), C2 when a run-out outside every body answered and the call blocked nothing
(no blocked cut or end, no undetermined negative, no condition both ways, no skipped verb, no
run-out in a body), C3 when a second-pass success answered, the call read the text to come twice
or more and blocked nothing. Port grid, the rule against main: 1,359 rows away from the judge;
by cause C2-mech 15, C2 349, C3 71; the other 924 are listed (`.scratch/d11pm/away?.txt`) with
their event classes: C2 in a body (357) or a body's run-out then an answering one (235), C2 after
a blocked construct (187), C3 after a blocked construct (137), C4 (8). None has P1's shape:
re-run with every body keeping its alternatives (and the decided end), 0 of the 924 moved. The
review's hand checks stand: `(?r)\n\b` over '' is C3 (two reads of one character), and search
`(?w)((?<=a)(?=(?1)?(?=a\B|))a.)` over '\n' is right on the port ('\n' + 'aab' completes it): the
judge's horizon (MAXW 2) is too short, a judge limit, not a phantom. The closure text's counts are
corrected above.

**P3, for the register (owner), not built here:** "Partial, found by D11's pre-merge review,
2026-10-04, the same on main and upstream: a fuzzy string's run-out at the edge is taken without
its error alternatives, so a negative lookahead whose body matches the held text with an error is
left undetermined: `(?!(?:abc){e<=1})ab` match 'ab' P(0, 2) (deleting 'c' matches 'ab', so no
continuation completes it; judge None), also `{d<=1}`, `(?:abcd){e<=2}`, under `(?i) (?fi) (?b)
(?e)`; 159 rows of a 3,900-row fuzzy-in-body grid. F1's class (a run-out that drops a definite
alternative), not limit C2. Fix: keep the error alternatives at the string's run-out."

**P4.** `PartialLookaroundRuleTests.A_repeat_test_that_runs_out_in_a_lookahead_does_not_answer_for_what_follows`:
`(?=(?:(a))*b)[xy]` over 'aa' None; main bb17c198 answers P(0, 2) (probe), as does upstream.

**Stack budget:** the per-step ceiling is in the code comment now (a step pushes a frame's
captures, repeats and fuzzy state; the budget fires first while a step is under 1 MB).
