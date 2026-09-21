---
slice: S57c
phase: 6
title: The word-start anchor before a fuzzy section, fixed with a pin the backtracking engine saves and restores
delivers: []
---

# S57c - Ledger entries 19 and 20, which are one bug

The first of the two inherited bugs Phase 6's fix list still holds. S50 proved entries 19 and 20 are
the same defect, built two fixes for it and reverted both; this slice builds the third, which S50's
own notes describe.

**The symptom.** A zero-width position assertion before a fuzzy section stops the section matching at
position 0, so `\m` denies a match the same pattern makes one character later (upstream issue 563).
The same freeze is reached a second way: `BESTMATCH` re-anchors its candidate walk at the match start
when the budget is loosened, so `{e<=2}` loses a match `{e<=1}` found (upstream issue 564). Both
reproduce identically on `regex` 2026.9.10 and here, measured 2026-09-14
(`docs/plan/upstream-reports/LEDGER.md`, entries 19 and 20).

**What is already settled, and must not be re-derived.** S50 established the rule the fix needs, and
a blind review proved it right by killing the version without it: lift upstream's prohibition at the
anchor only where a zero-width assertion held there **and** fails one character on, because only then
is starting one character later a different match rather than this one minus an insertion. A first
version that lifted the rule whenever any assertion had held reddened upstream's own `test_fuzzy`
rows 51, 52, 54 and 56.

**What S50 got wrong, twice.** It held the answer in a bare `MatchState` field that the backtracking
engine never saves or restores, so it was wrong in both directions: an assertion that held on a path
the engine then abandoned still pinned the anchor, and the two backtrack-arm clears that fixed the
abandoned paths also discarded a pin set before and outside the construct. Ten of twelve probed
repeat shapes diverged. Every one of those rows is pinned in
`tests/FuzzyRegex.Tests/Gaps/UpstreamIssues/InheritedIssueTests.cs`, so this slice cannot repeat
either mistake without the suite saying so.

## Scope

1. **Choose between the two designs S50 named, on evidence rather than taste**, and write the choice
   down before implementing it:
   - the pin becomes part of the backtracking state, pushed and popped with the frames that abandon a
     path, or
   - a compile-time analysis - "every path from the start node to this fuzzy item passes a position
     assertion" - combined with the same dynamic one-step-on test.

   The first is local and costs per-frame bytes on the hot path S61 is about to measure; the second
   costs nothing at match time and has to be right about every path. Whichever is taken, say what the
   other would have cost.
2. **Keep S50's rule exactly**, including its narrowing. `test_fuzzy` rows 51, 52, 54 and 56 are the
   instrument that says the narrowing survived.
3. **Entry 20 comes with entry 19 or the slice is not done.** S50 measured that entry 19's one-clause
   change turned entry 20's row green as a side effect; prove that again rather than assume it, and
   if it does not hold, entry 20's `BESTMATCH` re-anchoring route is this slice's work too.
4. **The pins invert, and the inversion is the proof.** `InheritedIssueTests` asserts the inherited
   answers - `A_word_start_anchor_before_a_fuzzy_section_still_does_not_match_at_position_zero` and
   `Loosening_a_fuzzy_budget_still_loses_a_match_the_tighter_one_found`. Fixing the bug turns them
   red. Rewrite those two to assert the fixed answers with the upstream call and answer quoted beside
   them, and **keep every regression row the two reverted attempts broke**, unchanged.
5. **A deliberate divergence, recorded as one.** This port then answers where upstream does not, so
   `docs/DIVERGENCES.md` gains a row (what, why, where decided, how a user gets upstream's
   behaviour) and `ExpectedDivergences` gains an entry with a recorded discriminator, in the same
   commit. Write the negative control before the entry (DECISIONS 2026-09-12): an entry with no
   discriminator is a silencer.
6. **Nothing else.** Entry 21 is S57d's and entry 18 is S61's.

## Verification

- `python tools/probes/issue-563-anchor-rule.py` re-run, before and after, with both outputs quoted.
- `dotnet run --project tests/FuzzyRegex.Tests -c Release -- --treenode-filter
  "/*/*/InheritedIssueTests/*"` green, and the same for `FuzzyMatchingTests` and
  `FuzzyCountsAndChangesTests`.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, and
  `pwsh -File tools/run-oracle.ps1 -Count 6000` GREEN at three seeds, because this changes a fuzzy
  answer and the default wave is the shallower instrument.
- A negative control that restores the fault and fires on the wave, recorded with its snippet,
  generator, row count and two seeds.

## Done when

- [x] The design chosen and written down with what the alternative would have cost.
- [x] Entries 19 and 20 both answer correctly; the probe's before and after quoted.
- [x] The two inherited pins inverted with their provenance; every regression row the reverted
      attempts broke still green.
- [x] `DIVERGENCES.md` row and a discriminated `ExpectedDivergences` entry, the negative control
      written first.
- [x] Ratchet, the default wave and the 6000-row wave green at three seeds; blind review (hunt: state
      that the backtracking engine does not restore; a pin surviving an abandoned path; the
      one-step-on narrowing quietly dropped), commit.

## Closing notes

Three sittings: the engine and its tests in the first, the documentation and the review in the
second, and the third recovered the second's uncommitted work and landed the lot. The per-sitting
record is in `docs/plan/slices/notes/S57c-sittings.md`; this is what a later reader needs.

**What landed.** `Optimiser.FindAnchorGuards` collects a pattern's leading position assertions at
compile time into `PatternObject.AnchorGuards`, and `Matcher.AnchorIsPinned` permits a fuzzy
insertion at the search anchor only where a guard holds at the anchor and fails one character on.
That narrows upstream's blanket ban (`permit_insertion = !search || text_pos != search_anchor`,
`_regex.c:10214`) and fixes upstream issues 563 and 564, which are one bug. Ledger entries 19 and 20
are marked fixed. The divergence from upstream is recorded in `DIVERGENCES.md`, `COMPARISON.md` and
`PORTMAP.md`, and there is deliberately no switch to restore upstream's answer.

**The pin belongs to the compiled pattern, not to the match state.** S50 twice tried a
`MatchState` field and twice reverted it: the backtracking engine neither saves nor restores that
field, so the pin outlived the path that set it. `AnchorGuards` is immutable and computed once, so
there is nothing to restore. The alternative - pushing and popping the pin with each backtracking
frame - would have put a cost on the hot path for a rule that fires on a handful of patterns; S61
is where that trade would have to be measured.

**Surprises, both worth knowing.**

1. *A control can be right and still not fire.* Control A drops the one-character-on half of the
   test, so it differs from the shipped rule only where an assertion holds at the anchor **and still
   holds one character on**, inside a fuzzy section that reaches the anchor: `\b(?:abc){i<=2}` over
   `'x abc'` is the shape, where `\b` holds at 0 and at 1. That is too narrow for a random generator
   to hit. The control reddens four suite tests and changes nothing at all on a 2000-row wave at
   three seeds; a new generator, `fuzzy-anchored`, was written to reach the shape and did not, and
   the reason is written up in the sitting notes. The directed-row file
   `tools/probes/s57c-one-step-on-rows.jsonl` is what holds the rule's shape instead.
2. *An ablation-keyed oracle entry is blind to a change in its own rule's shape.* Break the rule and
   the entry absorbs the broken answers rather than reporting them. The entry's Reason text says so
   in the source, and the table below is the measurement.

**For the next slice.** `fuzzy-anchored` is deliberately off `run-oracle.ps1`'s default generator
list: it is red at seed 1234567 on a row about where `(?b)(?r)` records an insertion, which is not
this rule and needs amendment 16's ceremony of its own. That is slice **S57e**, and it has two
hold-out paragraphs to delete, one in each tool. Design spec amendment 36 and the matching ROADMAP
paragraph record the hold-out.

### Review

Six blind passes reported, and one was interrupted before it could. Sitting 2's pass raised four
findings, all four reproduced and all four fixed: the `fuzzy-anchored` generator red at seed 1234567, an untracked
row file that a shipped comment named, "upstream gives [] to all four" over five rows, and a
"forty-three" that was 46. Its independent verifier, over the four judged rows, returned everything
CONFIRMED except one figure: the control reddens `test_fuzzy` rows 51 and 56, not the "51, 52, 54 and
56" S50 had measured against a different implementation of the pin. That correction is the only edit
to `Matcher.cs` since the checkpoint commit. Sitting 2 then started a second pass over the delta and
was interrupted before it recorded the outcome, so nothing is claimed for it.

Sitting 3 therefore treated the whole delta as unreviewed and ran a fresh pass over
`git diff 45d3f2f`. It raised two findings, both reproduced, both fixed, and both the same kind of
error: a sentence that had drifted from the tree. `STATE.md` said the new generator was off
`record-oracle.py`'s default list as well as `run-oracle.ps1`'s, and it is on `record-oracle.py`'s
`GENERATORS`; the sitting notes twice said `Matcher.cs` was byte-identical to the checkpoint, and the
verifier's correction above had changed its remarks. The pass re-ran both suites, the ratchet, the
doc-example gate, all three waves, the control and every upstream answer quoted anywhere in the
slice, and reproduced each number.

A third pass, over those two fixes and the closing notes, raised six. Five were reproduced and
fixed: surprise 1 above had the control's discriminating shape backwards (it fires where an
assertion holds at the anchor and *still holds* one character on, which is where the control and the
shipped rule disagree), and four maintenance pointers in `STATE.md` had rotted - a line number, a
citation that is in three files rather than one, an item S74 had already fixed, and a process that no
longer exists. The sixth was rejected on reproduction: it read `STATE.md`'s "tree is clean and S57c
is done" as false because nothing was committed yet, which is what that file always says at this
point in a slice.

A fourth pass over those fixes raised three, all reproduced and fixed, and all in this paragraph's
own neighbourhood: a fifth maintenance pointer that had gone stale (`main`'s `pages.yml` is S71's
deliverable, arrived through the `phase9-demo` merge, not a stray copy), a count of the passes that
the paragraph then contradicted, and a cross-reference pointing below to something above it. The
fifth pass raised one, the pass count again, because the sentence had to be rewritten each time a
pass was added to what it counts; the sixth returned clean.

The pattern across all six is worth carrying into the next slice. With the engine settled at the
checkpoint, every surviving finding was a sentence that no longer matched the tree, and most of them
were in the state file or in this record rather than in the slice's own code.

### The negative control, and how to re-run it

**Control A, `anchor-pin-without-the-one-step-on-test`**, registered as `S57c-A` in
`tools/controls.json`, so `python tools/run-controls.py --ids S57c-A` applies and reverts it. In
`src/FuzzyRegex/Engine/Matcher.cs`, in `AnchorIsPinned`, replace

```csharp
            if (
                TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success
                && TryMatchZeroWidth(state, guard, onePastAnchor) == MatchStatus.Failure
            )
```

with

```csharp
            if (TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success)
```

All three figures below were re-run on the committed code, after the last change to it.

- **Wave `fuzzy-anchored`, 2000 rows.** Seed 7: agree 1997, expected 3, **diverge 0**. Seed 4242:
  agree 1992, expected 8, diverge 0. Seed 20260921: agree 1995, expected 5, diverge 0. Identical to
  the unbroken code at every seed, rows included. The control does not fire on a wave, and the
  slice's own Verification section asked for one that does; that ask is not met, and surprise 1
  above is the reason.
- **Ported suite,** `dotnet run --project tests/FuzzyRegex.Tests -c Release`: **4 red of 6510**,
  against 0 on the restored code. `Answers_what_upstream_answers(Fuzzy matching against a word
  list)`, `A_word_start_anchor_before_a_fuzzy_section_matches_at_position_zero_here`,
  `A_fuzzy_named_list_finds_each_word_in_turn` (`test_fuzzy#51`) and
  `A_fuzzy_named_list_searching_backwards_reports_matches_leftmost_first` (`test_fuzzy#56`).
- **Directed rows,** `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57c-one-step-on-rows.jsonl`,
  7 rows, no seed. With the `fuzzy-insertion-at-a-pinned-anchor` entry live: 1 row classified by it
  on the shipped rule, 3 under Control A, both GREEN. With the entry disabled - change its `Applies`
  lambda in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` from
  `static (row, ours) => OnlyTheAnchorPinExplainsIt(row, ours)` to
  `static (row, ours) => row.Number < 0 && OnlyTheAnchorPinExplainsIt(row, ours)`, since `false &&`
  does not build (`error S1125`) - **1 diverge on the shipped rule against 3 under Control A**. That
  is the control firing, and it doubles as proof the mutation reaches the compiled engine rather
  than a stale build.
