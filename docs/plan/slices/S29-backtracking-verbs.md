---
slice: S29
phase: 4
title: Backtracking verbs - (*PRUNE) and (*SKIP)
delivers: [backtracking-verbs]
---

# S29 - Backtracking verbs: `(*PRUNE)` and `(*SKIP)`

Needs S27 and S28: half the verb tests place the verb inside a lookbehind, and the pruning stack
they cut to is the one `push_repeats` maintains. `(*FAIL)` is already the `FAILURE` opcode and its
tests passed on 2026-09-11; this slice is the other two verbs.

## Scope

- **`PRUNE`** (`upstream/src/_regex.c:13894`): `top_bstack(state)` (`:2811`, which is
  `top_size` over `pstack`) then continue. **`SKIP`** (`:14544`): move `slice_start` (or
  `slice_end` when reversed) to `text_pos`, then the same prune. Neither has a backtrack arm -
  they never push, so backtracking never reaches them.
- **The pruning stack `pstack`** already exists as `MatchState.Pstack`, and one push site is
  ported (`Matcher.cs:2418`). Account for every upstream `pstack` site: pushes at `:2591`
  (`push_repeats`), pops at `:2765`, drops at `:2801`, resets at `:3408` and `:15750`. A verb
  cuts to whatever the most recent push recorded, so a missing push makes a verb cut too far and
  a missing pop makes it cut too little - and both pass a test that has no repeat in it.
- **Upstream issue 613**: `(*SKIP)` inside an atomic group leaves a stale backtrack limit, which
  in C reads past the buffer and here would surface as an `IndexOutOfRangeException` or a wrong
  answer. Port faithfully; add the pattern from the issue as a gap test pinning *current* upstream
  behaviour, so Phase 6 has a failing test to fix rather than a report to reproduce.

## Verification

- **Un-skip** `needs:backtracking-verbs` (32 tests, `RegressionsBacktrackingVerbTests.cs`).
- **Oracle generator `verbs`**: `(*PRUNE)` and `(*SKIP)` after a greedy run, after a lazy run,
  inside an alternative, inside an atomic group, inside a positive and a negative lookaround,
  under `(?r)`; compared through search and findall, where `SKIP` moving the slice start is
  observable as the *next* match's start. Zero divergences. Negative controls: `SKIP` not moving
  the slice; `PRUNE` cutting to the bottom of `bstack` instead of the top of `pstack`.
- **Add `verbs` to the default oracle list.**

## Done when

- [ ] Tag delivered or stragglers retagged.
- [ ] Oracle wave green; controls recorded in full.
- [ ] PORTMAP: `PRUNE`, `SKIP`, `top_bstack`, and every `pstack` site accounted for.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a verb reached by backtracking; `SKIP`
      under `(?r)` moving the wrong end), commit.
