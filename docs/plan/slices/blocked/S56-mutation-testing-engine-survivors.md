---
slice: S56
phase: 6
title: Mutation testing, part two - read the engine's survivors and kill each with a test or record why it is equivalent
delivers: []
---

# S56 - The engine's survivors

Consumes the reports the orchestrator produced overnight with `tools/run-stryker.ps1 -Queue` over
S55's chunk list. The session spends no time running Stryker: it reads, judges and writes tests.
This is the instrument that says whether Phase 7 can rewrite the engine safely.

## Before launch (orchestrator)

Every chunk in `tools/stryker-queue.json` has a report under `TestResults/stryker/<chunk>/`. The
queue runs detached on the quiet machine; a chunk that exceeded its time budget is re-split and
re-queued rather than skipped. The session's first check lists the chunks with a report and refuses
to start if any is missing.

## Scope

- **Survivor triage, chunk by chunk.** For each surviving mutant: reproduce by hand (apply the
  mutation, run the suite, watch it stay green), then decide: a missing test, written test-first
  so the mutant dies - with the mutant's line and operator named in the test's comment; or
  equivalent, recorded in `docs/plan/mutation/<date>-engine.md` with the reason; or dead code the
  port carries for line-for-line fidelity (`default` arms, upstream's unreachable branches), which
  is recorded as such and is not a coverage failure.
- **Fuzzy and BESTMATCH first**, then verbs and partial, then the rest: those are the areas where
  Phase 5's wave found the most, and where Phase 7's rewrites will land.
- **Timeouts as mutants**: a mutant that makes the suite hang is the worst outcome and Stryker
  reports it as a timeout; each one is a place where a per-test `[Timeout]` and an assertion are
  owed, not a place to ignore.
- Two or more sittings are expected; checkpoint by chunk with the survivor table so far in
  STATE.md, and record the mutation score per chunk before and after.

## Verification

- Per-chunk score before and after in the closing notes; every survivor accounted for in the
  mutation document; every new test named with its mutant.

## Done when

- [ ] Every engine chunk's survivors killed, recorded equivalent, or recorded as fidelity dead code.
- [ ] Mutation document committed; scores quoted; no chunk skipped.
- [ ] Ratchet GREEN, blind review (hunt: an "equivalent" survivor that changes a fuzzy count; a
      killing test that asserts the mutant's answer rather than the right one), commit.
