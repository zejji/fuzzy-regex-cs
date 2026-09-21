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

- [ ] The design chosen and written down with what the alternative would have cost.
- [ ] Entries 19 and 20 both answer correctly; the probe's before and after quoted.
- [ ] The two inherited pins inverted with their provenance; every regression row the reverted
      attempts broke still green.
- [ ] `DIVERGENCES.md` row and a discriminated `ExpectedDivergences` entry, the negative control
      written first.
- [ ] Ratchet, the default wave and the 6000-row wave green at three seeds; blind review (hunt: state
      that the backtracking engine does not restore; a pin surviving an abandoned path; the
      one-step-on narrowing quietly dropped), commit.
