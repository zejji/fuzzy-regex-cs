---
slice: S48
phase: 6
title: Inherited (*SKIP) and partial-matching bugs - ledger entry 5's remaining door, and every other entry this port still shares
delivers: []
---

# S48 - The verb and partial inherited bugs

Phases 4 and 5 judged many `(*SKIP)` and partial divergences as upstream's with the port right;
those need no fix. This slice takes what is left: the entries where the port still shares
upstream's answer.

## Scope

- **Inventory first.** Walk `docs/plan/upstream-reports/LEDGER.md` entries 1-14 and every
  `ExpectedDivergences` entry, and table each as one of: port right (pinned, no work); fixed here
  already (name the slice); still inherited (this slice's list); upstream-only (no port symptom).
  The S43 handover names **entry 5's fifth door** as still inherited, with a proposed fix: reset
  the slice in `init_match` (`_regex.c:3404`) or save and restore it around `do_match`, closing all
  of entry 5's symptoms at once. S40a and S40b already restore the slice at match start and before
  the partial retry; find which door is still open and close it the same way.
- **Judge before fixing**, to amendment 16: PCRE2 10.47 via ctypes for verbs and partial
  (PARTIAL_SOFT and PARTIAL_HARD), Perl 5.42, the pcre2pattern section on backtracking verbs, and
  self-refutation where the same engine's anchored door beats its search. Every fix is a deliberate
  divergence with a narrow entry and a negative control.
- **Ledger entries 1-4**: confirm each is upstream-only with the port pinned right; any that turns
  out to be shared joins the list.
- Anything inherited and not fixable inside the slice is parked as a named blocker in STATE.md
  with the evidence, never quietly left.

## Verification

- The inventory table in the closing notes, one row per entry, with the slice or test that proves
  each classification.
- Waves GREEN at three seeds and 99991; the `search-start-*` and `overlapped-skip-*` controls re-run.

## Done when

- [x] Inventory complete; every "still inherited" row fixed test-first or parked with evidence.
- [x] Entries, controls, ledger updated; nothing filed.
- [x] Ratchet GREEN, blind review (hunt: a fix that moves a permanent Phase 4 pinned answer; a slice
      reset that changes `(?r)` end-of-subject semantics, S35's trap), commit.

## Closing notes - 2026-09-14, one sitting

**The slice's premise was stale and the inventory is what found the real work.** The file said the
S43 handover names "entry 5's fifth door as still inherited" and asked which door was still open.
Every one of the five was already closed: S40a resets the slice once per match (`Matcher.cs:10008`)
and S40b restores both bounds before the partial pass (`:10098-10100`), and each door has its own
`ExpectedDivergences` entry saying the port is right. **There is a sixth door, it was never written
down, and it is the only one this port ever shared.**

### The inventory - ledger entries 1 to 15, one row each

| # | Entry | Classification | What proves it |
|---|---|---|---|
| 1 | `(*SKIP)` retries below the position it committed past | upstream-only; port pinned right, permanently | `BacktrackingVerbTests.Skip_past_a_required_string_tries_a_start_position_upstreams_prefilter_skips` - two assertions. Upstream reaches the defect only through `locate_required_string`, so the oracle cannot see it: `record-oracle.py`'s `PREFILTER_FREE_GENERATORS` (`:281`, applied at `:367`) records `verbs` prefilter-free |
| 2 | A bounded lazy repeat at its maximum reports an impossible partial | upstream-only; port right | `PartialMatchingTests.A_bounded_lazy_repeat_that_reaches_its_maximum_loses_its_partial_here`; `ExpectedDivergences.bounded-lazy-repeat-partial`; PCRE2 10.47 agrees with this port |
| 3 | A reversed search reports a partial its own `match` denies | upstream-only; port right | `PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_finds_no_partial_here`; `ExpectedDivergences.search-start-partial` |
| 4 | A reversed `fullmatch` fails on a slice that is exactly the match | upstream-only; port right | `ReverseMatchingTests.A_reverse_fullmatch_of_a_repeat_over_a_narrowed_slice_succeeds_here`; `ExpectedDivergences.reverse-fullmatch-narrowed-slice` |
| 5 | The carried slice - **six doors** | five closed before this slice (S40a, S40b); **the sixth was SHARED and is fixed here** | `overlapped-skip-stale-slice`, `-reversed`, `-extra-match-reversed`, `-missing-match-reversed`, `partial-retry-reversed-slice`, `partial-retry-carried-slice-forward`, and the new `bestmatch-walk-truncated-by-a-skip` |
| 6 | `IndexError` compiling a reversed, case-folded pattern | upstream-only; fixed here in S35 | `FullCaseFoldSplitTests.A_run_whose_last_chunk_starts_past_its_end_no_longer_builds_an_empty_string_node`, which compiles and matches `(?r)^İﬁ` under `I\|F` where upstream raises |
| 7 | The default folding tables carry CaseFolding.txt's Turkic-only rows | inherited; **FIXED HERE (S45)** | `ExpectedDivergences.turkic-default-folding`; `Unicode/TurkicDefaults.cs` |
| 8 | A group call inside an opposite-direction lookaround loses the match | upstream-only; port right | `ExpectedDivergences.group-call-loses-the-match`; three `GroupCallTests`. Not fixed by issue 614 |
| 9 | A POSIX search of a fuzzy pattern crashes the C engine | crash half closed (S43); **the port-side count bug is STILL OPEN** | Re-measured on the committed code, 2026-09-14 - see below. **Parked to S48b** |
| 10 | `(*SKIP)` in an atomic group after an optional item loops for ever | CLOSED - upstream fixed it, and S44 ported both clamps | `BacktrackingVerbTests.A_skip_inside_an_atomic_group_after_an_optional_item_answers_where_upstream_loops_for_ever` |
| 11 | Fuzzy change positions contradict their own counts | mechanisms A and B **FIXED HERE (S47)**; **C and D still inherited** | `fuzzy-changes-leaked-from-an-abandoned-attempt`, `fuzzy-counts-of-a-partial-are-the-innermost-sections`. **C and D parked to S48b** |
| 12 | `BESTMATCH` loses a match needing two trailing insertions | inherited; **FIXED HERE (S46)** | `ExpectedDivergences.bestmatch-loses-a-candidate` |
| 13 | `BESTMATCH` loses a partial its own `match` still finds | upstream-only; this port never reproduced it, and S47c traced upstream's mechanism to the line | `ExpectedDivergences.bestmatch-loses-a-partial`; `tools/probes/upstream-bestmatch-lost-candidate.py` |
| 14 | A self-recursive call round an empty-matching fuzzy section exhausts memory | inherited; **FIXED HERE (S47)** | `MatchState.ActiveCalls`; `FuzzyRecursionTests` |
| 15 | `(*SKIP)` blocks the one repeat retreat a partial needs | upstream **regression in 2026.9.10**; this port never had it, because S40b restores the bounds | `ExpectedDivergences.skip-blocks-a-repeat-retreat-partial` |

Thirteen closed. Three items left, and they are **one mechanism seen three ways** rather than three
unknowns - the counts are saved and restored as a block while the change list is unwound one item
at a time - so they become **S48b**, authored here, with ROADMAP and design spec amendment 25.

### The sixth door, which is what this slice actually fixed

`do_best_fuzzy_match` walks `start_pos` across the slice, one `basic_match` per candidate. Its loop
guard reads the LIVE bounds (`upstream/src/_regex.c:17625`), `init_match` does not reset them - that
is this very report's subject - and `start_pos` is set to `state->match_pos` at the foot of the loop
(`:17680`). So a `(*SKIP)` that consumed anything leaves `slice_start` **above** the candidate's own
start and the guard is false on the next turn: **the walk ends on its first successful candidate**,
and every better match further along the subject is never attempted. The second pass reads the same
stale bound in its `max_offset` (`:17721`) and in every anchored re-run.

Minimised to four ASCII characters and no flags:

```
(?b)(?:a(*SKIP)b){e<=1}  over 'axab'
  search, as written      (0, 2) one substitution     <- upstream, and this port before
  its own match(2)        (2, 4) NO errors
  (*PRUNE) in its place   (2, 4) NO errors
  the verb deleted        (2, 4) NO errors
```

**No second engine exists for this and none is needed.** PCRE2 has no fuzzy matching at all
(`tools/probes/pcre2-has-no-fuzzy-matching.py`), so what judges it is the verb's own definition plus
self-refutation: a skip point forbids a later attempt *below* it (pcre2pattern, "Verbs that act
after backtracking") and the lost candidate starts *above* it, while `(?b)` promises the fewest
errors among the matches that exist and the one it loses is perfect. `(*PRUNE)` prunes backtracking
identically and moves no bound, which makes the moved bound the cause rather than the pattern's
meaning.

**The fix is not where the ledger's proposed fix puts it.** Entry 5 proposes resetting the slice in
`init_match`; that would be wrong here, because `DoEnhancedFuzzyMatch` (`:17871`) and
`DoBestFuzzyMatch`'s own widened-slice fallback (`:17807`) narrow the slice *deliberately* and then
call it. `Matcher.DoBestFuzzyMatch` restores the caller's slice before each candidate instead, in
both passes. The ledger entry now carries that correction.

**Not rare upstream**: 1,861 of 11,340 shapes in a small alphabet answer differently with `(*SKIP)`
than with `(*PRUNE)` (`python tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py --hunt`).

### What was measured

- **Suite 5,955, ratchet GREEN**, +2 tests and no existing test moved - including every
  `overlapped-skip-*` pin, which is the family that would have gone quiet had the restore reached
  inside an attempt.
- **Default oracle wave GREEN at three seeds** (7, 4242, 20260914), 6,300 rows a seed.
- **The 6000-row three-seed gate is 6 + 4 + 9 = 19**, which is the 19 STATE recorded at HEAD. **None
  of the 19 can be this slice's**: the edit lives only inside `DoBestFuzzyMatch`, which `DoMatch2`
  reaches only for a fuzzy pattern with `BESTMATCH` set (`Matcher.cs:10005`), and no diverging row
  carries the flag bit `0x1000` or an inline `(?b)`.
- **The 6000-row everything gate at seed 99991 is 4 of 126,000**, which is the figure STATE recorded
  at HEAD. The four were also replayed through a worktree at `bb6213a` from the identical saved wave
  and gave the same four rows with byte-identical port answers - **but that wave lived in `.scratch/`
  and is gone, so the replay is NOT reproducible from this commit**, and it is not what the claim
  rests on. What does is the structural argument above: no diverging row can enter the changed
  function at all.
- **Entry 9's port-side bug still reproduces**, which is why it is parked rather than ticked:
  `(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}` over `'+ aBA'`, `fullmatch`, POSIX on,
  gives `(0, 5)` with counts `(1, 1, 1)` here against upstream's `(0, 1, 1)` - and `(0, 1, 1)` here
  with POSIX off, which is the self-refutation.

### Negative controls

**S48-A, `bestmatch-walk-reads-the-verb-moved-slice`**, in `tools/controls.json` - the walk guard and
the per-candidate restore go back to the live slice:

> In `Matcher.cs`, `DoBestFuzzyMatch`, the walk loop reads as three lines plus the guard:
> ```
>             while (callerSliceStart <= startPos && startPos <= callerSliceEnd)
>             {
>                 // The candidate is tried against the caller's slice - see the note at the top.
>                 state.SliceStart = callerSliceStart;
>                 state.SliceEnd = callerSliceEnd;
>
>                 state.TextPos = startPos;
> ```
> replaced by upstream's own two:
> ```
>             while (state.SliceStart <= startPos && startPos <= state.SliceEnd)
>             {
>                 state.TextPos = startPos;
> ```
> Wave: `interactions`, 6000 rows, seeds 7, 4242, 31337 and 99991.
> Result: **it fires ZERO at all four.** Mutated: diverge 4 / 3 / 4 / 3, expected 12 / 20 / 15 / 16.
> Unmutated, on the same cached waves (`.scratch/control-waves/interactions-6000-<seed>.jsonl`
> consumed with `-SkipRecord`): diverge 4 / 3 / 4 / 3, expected 12 / 20 / 15 / 16. Identical.

**That zero is a finding about the generator, not about the fix, and it is recorded rather than
hidden.** The same fault changes upstream's answer on 1,861 of 11,340 hand-built shapes, so the
`interactions` generator draws `(?b)` beside `(*SKIP)` and never where the truncation decides
anything. Handed to S52 in the ROADMAP. What covers the fix meanwhile is the permanent test, the
five rows in `ExpectedDivergences.bestmatch-walk-truncated-by-a-skip`, and
`Every_expected_divergence_still_diverges`, which re-runs all five on every wave.

**The family's existing controls, re-run on the committed code** (the slice asked for the
`search-start-*` and `overlapped-skip-*` controls; those are `ExpectedDivergences` ids and not
controls, so what was re-run is the verb and partial controls that cover the same mechanism):

| Control | Seeds | Divergences |
|---|---|---|
| S29-A SKIP widens the slice instead of moving it | 7, 99991, 20260913, 4242, 314159 | 0, 1, 1, 1, 0 |
| S29-B SKIP moves the wrong end under `(?r)` | same five | 47, 31, 35, 28, 39 |
| S29-C a verb prunes to the bottom of the bstack | same five | 221, 212, 235, 215, 226 |
| S29-D the scanner carries findall's slice_start guard | same five | 1, 0, 2, 0, 0 |
| S35-A dollar-reads-slice-end | 7, 99991, 4242, 20260913, 31, 314159 | 0, 0, 0, 2, 0, 0 |
| S31-D try_match no longer reports a partial | 31, 99991, 4242, 7 | 3, 2, 2, 5 |
| S31-E a lazy repeat that cannot extend | 7, 99991, 31, 4242 | 0, 0, 1, 1 |

S29-A, S29-D, S31-E and especially **S35-A are thin** - S35-A fires at one seed of six, and S29-A at
three of five, its seed-7 zero corrected here after the independent verifier got it. They are not
this slice's doing and they are the standing "owed maintenance" item, but the numbers are recorded
here so the next slice to touch them has them.

**Three broken control sites are repaired**, because they are this slice's own family and a control
that will not resolve is not a control. S31-A, S31-B and S31-C all failed with "the 'before' text
does not appear after the anchor": S40b inserted the slice restore and its fifty-line note into the
exact lines S31-A and S31-B mutate, and `NewMatch`'s `partial:` argument gained a trailing comma.
Re-anchored on the text as it now reads, with the intent kept:

| Control | Seeds | Divergences |
|---|---|---|
| S31-A the do_match fallback is skipped | 31, 99991, 4242 | 15, 10, 12 |
| S31-B `text_pos` is not restored between the two runs | 31, 99991, 4242 | 24, 26, 23 |
| S31-C every match is reported as a partial | 31, 99991, 4242 | 68, 68, 64 |

S31-B's mutation now drops `TextPos` alone and keeps S40b's slice restore, which is the honest
reading of its name on today's code.

### Review

**Two blind passes. The first raised 4 and ALL 4 reproduced; the second raised 1 and it reproduced
too.** All five are fixed.

Pass one, over the whole diff:

1. **The negative control tested nothing.** `Bestmatch_still_lets_a_skip_move_the_slice_within_its_own_attempt`
   had no `(?b)`, so `DoMatch2` routed it to `DoSimpleFuzzyMatch` and it never entered the function
   the slice changed; its comment's claim about the verb-deleted answer was also false, because
   plain fuzzy matching takes the leftmost match either way. Replaced by
   `Bestmatch_still_lets_a_skip_prune_a_candidates_own_alternatives`, a `(?b)` row where the
   pruning decides the answer and the moved bound does not.
2. **An upstream line reference was wrong by 311 lines**: `do_enhanced_fuzzy_match` was cited as
   `:17560`, which is inside `add_to_best_list`. It carried the whole argument for not hoisting the
   fix into `InitMatch`. All ten references this slice introduced were then rewritten and checked
   line by line against the pinned source - `:17590`, `:17625`, `:17680`, `:17700`, `:17721`,
   `:17807`, `:17871`, `:14553`, `:14555`, `:3404` - by a scratch script at the time and again by
   the independent verifier, which re-read each line and confirmed all ten. **No assertion in the
   tree holds them**, so a future sync moves them silently, exactly as it moved the file's older
   pre-sync numbers; those were left alone, being the repo-wide owed-maintenance item.
3. **The new rows file could not be replayed through the runner.** All five rows are perfect
   matches, so `OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` failed
   its non-degeneracy guard and the run reported RED at `diverge 0`. A sixth, deliberately
   non-diverging row was added - `(?b)(?:\w(*SKIP)a|a){e<=1}` over `'a b c'`, both engines `(0, 2)`
   with one substitution at 1 - and the file now runs GREEN with `expected 5, agree 1`.
4. **An unfilled template placeholder had been left in STATE.md.**

Pass two, over the delta pass one never saw (the replacement test, the sixth row, the rewritten
references, the line-ending normalisation):

5. **The replacement test's comment said "position 0 yields nothing at all".** It yields `(0, 2)`
   with one substitution on both engines; what makes the walk go past it is that `(1, 3)` spends no
   errors. The comment now carries all four doors.

**The independent verifier** (spec amendment 16 limb (d)) then re-ran every number in these notes
from the commit-ready tree: the ratchet, the three-seed wave, both 6000-row gates, both probes and
the hunt, the S48-A control against its own unmutated baselines, all the S29/S31/S35 controls,
ledger 9's still-open port bug, every one of the ten upstream line references, and the existence of
every test and `ExpectedDivergences` id the inventory names. **It reported two DIFFERENT and two
COULD NOT RUN, and all four are corrected above rather than kept**: S29-A fires 0 at seed 7 and not
1 (this session read its table from a truncated `tail`); the prefilter-free reference is
`PREFILTER_FREE_GENERATORS` at `record-oracle.py:281`, not `:223`; the HEAD-worktree replay is not
reproducible from this commit and now says so; and the "assertion per line" was a scratch script,
not something in the tree.

### For the next slice

- **S48b is authored and is the last of the inherited-bug group.** Its three items are one
  mechanism; prove entry 9's cause with the `/Od /Zi` build S47c installed rather than fixing on the
  standing hypothesis.
- **The generators cannot see a `(?b)`-plus-verb interaction that changes the answer.** S52 owns it;
  S48-A is the measurement.
- **A ledger entry's "proposed fix" is a hypothesis like any other.** Entry 5's had been read three
  times as "reset it in `init_match`" and would have broken `(?e)`. The sixth door was found by
  reading upstream's C while inventorying, and no wave at any seed would have produced it.
