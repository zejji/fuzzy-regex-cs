---
slice: S05
phase: 1
title: Port upstream tests - hg_bugs and the tail
delivers: []
---

# S05 - Port upstream tests: regression corpus (lines 3084-4540)

Follow the `port-tests` skill. This is the last Phase 1 slice.

## Scope

`upstream/regex/tests/test_regex.py` lines 3084 to the end.

- `test_hg_bugs` (3084-4411, about 1,327 lines) - upstream's regression corpus, one assertion per
  historical bug. Repetitive and mechanical, which makes it the best candidate in the whole
  phase for a Sonnet subagent working in batches of a few hundred lines.
- `test_fuzzy_ext` (4411) - the extended fuzzy syntax, including per-error-type budgets and
  `{s<=2:[a-z]}` character-constrained errors. Not mechanical. Do this one yourself, and tag it
  with the same fuzzy vocabulary S04 established.
- `test_subscripted_captures`, `test_more_zerowidth`, `test_line_ending` - small and
  straightforward.
- `test_main` - the unittest runner entry point; not ported.

Suggested areas: `Regressions` for `test_hg_bugs` (keep the upstream bug identifier in the test
name where the source names one), then reuse the existing areas for the rest.

## Watch for

- **Batching `test_hg_bugs` needs a check, not trust.** Give each subagent a fixed line range,
  the `port-tests` skill and the tag vocabulary already in use, then spot-check its output
  against the source before accepting it. A subagent inventing a new capability tag or a wrong
  index is the likely failure, and both are cheap to catch and expensive to leave.
- Several `hg_bugs` assertions are about error *messages* and exception types for bad patterns.
  Port them against `FuzzyRegexParseException`, asserting the offset where upstream asserts a
  position.

## Done when

Same criteria as S02, plus the phase-closing checks:

- [ ] Every one of the 102 upstream test methods is now either ported or listed in PORTMAP.md as
      not ported, with a reason. Verify this by listing them, not by assuming.
- [ ] `docs/STATUS.md` shows the full ported-test count and the capability breakdown - this is
      the number Phase 2 planning works from.
- [ ] Closing notes record the measured tokens per slice for Phase 1 (from
      `docs/plan/slice-log.jsonl`), so `docs/plan/budget.json` can be recalibrated against
      evidence rather than the estimate it currently carries.

## Phase boundary

The driver stops after this slice: the pending queue is empty. The owner reviews
`docs/STATUS.md`, then authors the Phase 2 slices.
