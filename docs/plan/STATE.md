# Current state

**Slice in flight: S60b, `docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`.**
Item 2 landed as a green CHECKPOINT on 2026-09-22; the slice file stays in `docs/plan/slices/`.

## What is done

Upstream's `search_start` dispatcher and its `search_start_*` family are ported as
`Matcher.SearchStart`, `MatchMany` and `SearchStartZeroWidth`, with `MatchState.DoSearchStart` and
a new `Matcher.StepOver` - a `STRING` node's step is its whole length, and this port's positions
count code units, so the step is a walk and not an addition. 14 gap tests in
`Gaps/Engine/SearchStartTests.cs`.

Green: suite 6577/6577, ratchet GREEN (baseline 6469), oracle GREEN at its three default seeds,
AOT tests and smoke GREEN, and the three permanent files the slice names.

## What the next sitting does, in this order

Everything below is spelled out in `docs/plan/slices/notes/S60b-sittings.md`, including the exact
control snippets and the workload predictions. Do not re-derive it.

1. **Triage the benchmarks, after 22:00.** They were forbidden this sitting: the owner was using
   the machine until 22:00 and any timing taken then is noise, so item 2's measurement is
   OUTSTANDING and this checkpoint is not a landing. The slice's gate says a workload regressing
   beyond the noise floor is triaged before the sitting commits, and the triage may revert item 2.
2. **Re-run the four negative controls against the committed code**, plus one fresh seed each.
   The mid-sitting numbers (A 5 red, B 0 red, C 82 red before `StepOver` and green after, D 12
   red) are recorded but not re-run.
3. **The blind review**, which has not run on this code at all. Brief from `docs/VERIFICATION.md`.
4. Only then the slice's remaining items: 3, 6, 8-14, 16 and 17, one sitting each.

## Open question, for the sitting that can measure

Narrowing 3 - the prefilter withheld from any pattern holding a `(*SKIP)` - is kept on an argument
and no measurement supports it. The notes state the experiment that would settle lifting it.
