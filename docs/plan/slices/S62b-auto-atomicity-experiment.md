---
slice: S62b
phase: 7
title: EXPERIMENT - auto-atomicity and auto-possessification, a compile-time rewrite behind PCRE2's guard list, kept only if the oracle is identical and the win clears the floor
delivers: []
---

# S62b - EXPERIMENT: make loops nothing can backtrack into atomic

**This slice is an experiment, and "reverted, recorded" is one of its two valid endings** (spec
amendment 28, owner decision 2026-09-19: no stone should be left unturned). It runs after S62 so the
inner-loop numbers are already banked and this rewrite's win is attributed to the rewrite, and
before S63 so whatever it decides is inside the gate run rather than after it.

The technique is the one both production backtrackers already ship
(`docs/plan/2026-09-18-optimisation-research.md` §2, "The biggest bet, proposed as a NEW slice").
.NET's `RegexNode.EliminateEndingBacktracking`: *"If we find backtracking construct at the end of
the regex, we can instead make it non-backtracking ... since nothing would ever backtrack into it
anyway."*, with the standing warning beside it: *"The correctness of this optimization depends on
nothing being able to backtrack into"*. PCRE2's pcre2api on auto-possessification: *"it turns a+b
into a++b in order to avoid backtracks into a+ that can never be successful."* It is the cheap
attack on the class this port measured as catastrophic - `(a|a)*b` at 10.6 s for n=24 (S54) - and
S62c attacks the same class from the general side.

It is a compile-time graph rewrite in `Engine/Optimiser.cs` (`OptimisePattern:21`, beside
`SkipOneWayBranches:55` and `SetTestNodes:613`), so it is a structural divergence from upstream's
shape and carries a `sync-divergence:` marker and a `SYNC-DIVERGENCE.md` row with its measured gain
and its re-align instruction, exactly as S62 item 6 requires.

**The premise is fragile in this engine, and the guard list is the slice.** Fuzzy errors, the
backtracking verbs, group calls and recursion, conditionals, variable-length lookbehind and
`lastindex` reporting each give something a way back into a loop this rewrite would have sealed.
PCRE2's guard list is ported **verbatim**, as the precondition set, not paraphrased:

- *"A non-greedy iterator must never be possessified."*
- *"If the bracket is capturing it might be referenced by an OP_RECURSE so its last iterator can
  never be possessified if the pattern contains recursions."*
- *"Fixed-length lookbehinds can be treated the same way, but variable length lookbehinds must not
  auto-possessify their last iterator."*
- and the bounded-analysis precedent, which is the rule for every case the analysis cannot settle:
  *"the check just stops, leaving the remainder of the pattern unpossessified."*

To those, this port's own three, which PCRE2 does not need: **nothing inside or reachable from a
fuzzy section (`MaxErrors > 0`) is ever possessified**, because an error budget lets a later step
demand a retry of an earlier one; **no rewrite at all if the pattern contains `(*SKIP)`, `(*PRUNE)`,
`(*COMMIT)` or `(*MARK)`** anywhere, whose answers and last-mark value are pinned permanently in
`Gaps/Engine/BacktrackingVerbTests.cs`; and **conditionals are never rewritten**, because which arm
ran is itself a backtrack-visible decision. The analysis is bounded: where it cannot prove the
premise it **stops**, it never guesses, and stopping leaves the pattern exactly as upstream compiled
it.

## Scope

1. **Test-first, before any optimiser code.** A failing test per shape, proven failing without the
   fix: `a+b` (PCRE2's own example, the possessification case) and `(a|a)*b` (this port's
   catastrophic case) assert that the rewrite happened - the marking is observable on the compiled
   graph, never inferred from a timing - with the answers asserted unchanged first. The guard list
   gets one **negative** test per clause: a non-greedy iterator, a capturing group reached by a group
   call and by recursion, a variable-length lookbehind, a fuzzy section, each of the four verbs, and
   a conditional, each asserted to come out **unrewritten**. A guard with no test is a guard that
   does not exist.
2. **The S54 catastrophic corpus** as the before-and-after subject: `(a|a)*b` and `(a+)+b` across
   n=18 to n=24, both already pinned, so a shape moving between the flat class and the exponential
   class is visible in either direction.
3. **The rewrite itself** in `Engine/Optimiser.cs`, one pass after the existing markings, marking
   loops as atomic/possessive where the guard list clears them. No new public surface (frozen since
   S53b); no new opcode where `ATOMIC` plus the greedy repeat already expresses it, which is how
   upstream lowers a possessive repeat (`_regex_core.py:3034`, DECISIONS 2026-09-11).
4. **Measured before and after, per workload, never averaged**, against the committed baseline at
   S58's floors, plus the catastrophic corpus, which the suite does not contain.
5. **The exit, decided on the numbers and stated here in advance**: if the oracle disagrees **once**,
   or the win is under the S58 noise floor on every workload, the rewrite is **reverted** and the
   result recorded - the numbers, the shapes, the reason - in `OPTIMISATION-NOTES.md` with a
   `ponytail:`/`Phase 7` comment at the line, so it is never re-attempted blind (the S19 precedent,
   `Matcher.cs:8894`). That is a successful experiment, not a failed slice. If it stays, it carries
   its `sync-divergence:` marker and its ledger row.

## Verification

- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict.
  **This is the deciding check**: one disagreement ends the experiment, because an optimisation that
  changes an answer has ported a bug (ROADMAP, owner rule 2026-09-12).
- `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*' --job medium
  --exporters json --artifacts artifacts/bench/<date>-S62b-<before|after>`, compared with
  `pwsh -File tools/compare-benchmarks.ps1` at S58's floors; the S54 catastrophic shapes timed
  separately and reported at each n, not folded into a suite average.
- `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/<Class>/*"` for
  `OptimiserTrapsTests`, `BacktrackingVerbTests`, `PartialMatchingTests` and `ReverseMatchingTests`,
  green and named in the commit message. Those pins are permanent: a red one means the rewrite ported
  an upstream bug, and the fix is the guard, never the test.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN, binary size
  reported against 6,972,928 bytes.
- `pwsh -File tools/check-sync-divergence.ps1` GREEN - if the rewrite lands, the marker and the row
  must pair; if it is reverted, neither exists.
- `pwsh -File tools/update-public-api.ps1` run once to prove the surface is unchanged.

## Done when

- [ ] Every shape and every guard clause has its test, written first and proven failing (or proven
      unrewritten) before the optimiser changed.
- [ ] PCRE2's four guard quotes sit in the source beside the code they justify, verbatim and cited;
      the port's own three guards - fuzzy sections, the four verbs, conditionals - are implemented
      and tested.
- [ ] The analysis stops rather than guesses wherever it cannot prove the premise, and a test pins
      one such pattern as deliberately unrewritten.
- [ ] Before and after measured per workload at S58's floors, plus `(a|a)*b` and `(a+)+b` at n=18 to
      n=24 reported per n.
- [ ] Oracle GREEN at three seeds with `ExpectedDivergences` strict, or the slice exits by reverting.
- [ ] **Either** the rewrite landed with a `sync-divergence:` marker paired to a
      `SYNC-DIVERGENCE.md` row carrying its measured gain and its re-align instruction, **or** it was
      reverted and the negative result recorded in `OPTIMISATION-NOTES.md` with its numbers. Both are
      valid endings of an experiment.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a guard clause with no
      negative test; a fuzzy section rewritten because the loop itself carried no error budget; a
      pattern rewritten although a verb appears somewhere other than the loop; an analysis that
      guessed where it should have stopped; a timing used as the proof that the rewrite happened; a
      permanent pin edited to make the rewrite look sound), commit.
