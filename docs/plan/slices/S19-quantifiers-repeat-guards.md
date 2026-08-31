---
slice: S19
phase: 3
title: Quantifiers - greedy, lazy, the ONE fast paths, and repeat guards
delivers: [quantifiers]
---

# S19 - Quantifiers: greedy, lazy, the ONE fast paths, and repeat guards

Needs S16-S18. The biggest single behaviour of the engine: 179 tests wait on `quantifiers`, and
the guard machinery it lands is what keeps pathological patterns from exponential re-entry.

## Scope

All line references are `upstream/src/_regex.c`.

- **Main-switch cases**: `GREEDY_REPEAT` (`:13176`), `LAZY_REPEAT` (`:13557`),
  `GREEDY_REPEAT_ONE` (`:13313`), `LAZY_REPEAT_ONE` (`:13693`), `END_GREEDY_REPEAT` (`:12525`),
  `END_LAZY_REPEAT` (`:12760`).
- **Backtrack cases**: the shared `GREEDY_REPEAT_ONE`/`LAZY_REPEAT_ONE` guard block (`:15707`),
  `GREEDY_REPEAT` / `LAZY_REPEAT` (`:15778`), `GREEDY_REPEAT_ONE` (`:15815`) with its
  per-character retreat sub-switch (`:15907-16342`), `LAZY_REPEAT_ONE` (`:16445`) with its
  advance sub-switch (`:16533-17107`), `BODY_END` (`:15282`), `BODY_START` (`:15312`),
  `MATCH_BODY` (`:17177`), `MATCH_TAIL` (`:17223`), `TAIL_START` (`:17377`).
- **Guards**: `RE_GuardList` (`:320`), `insert_guard_span` (`:9296`), `delete_guard_span`
  (`:9328`), `is_guarded` (`:9340`), `guard` (`:9378`), `guard_repeat` (`:9446`), `guard_range`
  (`:9464`), `guard_repeat_range` (`:9534`), `is_repeat_guarded` (`:9559`), `reset_guard_list`
  (`:3363`), the real bodies of `push_guard_data` / `pop_guard_data` and `push_repeat_data` /
  `pop_repeat_data` / `push_repeats` / `pop_repeats` (`:2541-2589`, `:2713-2763`),
  `dealloc_repeats` (`:18630`). S15 already computed `RE_STATUS_BODY`/`RE_STATUS_TAIL` statuses
  in `add_repeat_guards`; this is where they are consulted.
- **Bulk stepping**: the `match_many_*` family for the opcodes landed so far - ANY (`:3537`),
  CHARACTER (`:3803`), PROPERTY (`:4045`), RANGE (`:4485`), SET (`:4737`) - which the
  `*_REPEAT_ONE` cases call; `node_matches_one_character` (`:3433`) and `locate_test_start`
  (`:3476`) as far as REPEAT_ONE construction needs them.
- **Timeout with teeth**: a gap test that a known-catastrophic pattern (e.g. nested quantifiers
  over a non-matching subject) raises the timeout instead of hanging - the S16 plumbing's first
  real exercise.

## Verification

- **Un-skip `needs:quantifiers`** (179 tests), reading each skip's prose first; stragglers
  retag with prose.
- **Oracle wave**: generated quantifiers - `* + ? {m,n} {m,}` in greedy and lazy forms, nested,
  over classes and groups and literals, including empty-body repeats (`(a?)*`-shaped, where the
  guards are what terminates) and repeats of capturing groups (comparing all captures, which is
  where S18's save/restore either holds or breaks). Zero divergences; negative control.
- **The prefilter contingency, named**: `locate_required_string` and the string-search family are
  deferred to Phase 7 (DECISIONS 2026-08-31). If any test un-skipped in this slice times out
  rather than fails, that deferral is the first suspect - the sanctioned response is to port
  `string_search`/`locate_required_string` (`:5231-6918`, `:11082`) into this slice and say so in
  the closing notes, not to skip the test.

## Done when

- [ ] `quantifiers` delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green including empty-body and captured-repeat probes; counts quoted.
- [ ] Catastrophic-pattern timeout gap test in and proven to fail with the timeout removed.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a guard keyed on the wrong position
      (body vs tail), `{m,n}` counts compared with the wrong inclusivity at the boundaries,
      a lazy repeat that advances past `max_count`, backtrack pops mismatched with pushes in the
      REPEAT_ONE sub-switches), commit.
