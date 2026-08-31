---
slice: S18
phase: 3
title: Alternation, capture groups, and the Match object's group surface
delivers: [alternation, groups, named-groups, captures]
---

# S18 - Alternation, capture groups, and the Match object's group surface

Needs S16 (S17 recommended first per the plan order, but the true dependency is only the spine).
Two halves that only make sense together: the engine records group spans, and the `Match` object
exposes them - group tests are unreadable without `m.Groups[1]`. Landing groups before
quantifiers (S19) is deliberate: a repeated capture group exercises `save_captures` /
`restore_groups`, so that machinery must already exist when REPEAT arrives.

## Scope

All line references are `upstream/src/_regex.c`.

- **Engine, main and backtrack cases**: `BRANCH` (`:12079`, `:15354`), `START_GROUP` (`:14569`,
  `:17307`), `END_GROUP` (`:12686`, `:15596`).
- **Group state**: `RE_GroupData`/`RE_GroupSpan`, `save_captures` (`:17403`), `restore_groups`
  (`:17468`), `discard_groups` (`:17500`), the real bodies of `push_groups` / `pop_groups`
  (`:2490`, `:2662`) and `push_captures` / `pop_captures` (`:2513`, `:2685`), `same_span` and
  friends (`:11634-11654`), `dealloc_groups` (`:18261`) as whatever disposal our pooling needs.
- **Match surface**: `pattern_new_match` (`:20738`) in full, `copy_groups` (`:20621`),
  `match_get_group_by_index` (`:18847`), the span/start/end/captures getters (`:18880-19180`),
  `match_lastindex` / `match_lastgroup` (`:20430`, `:20442`), `state_get_group` (`:20818`).
  Public: `Match.Groups`, `GroupCollection`, `Group.Success`/`Name`/`Index`/`Length`/`Value`,
  `Group.Captures` as the full capture list (`CaptureCollection` - upstream keeps every capture,
  not just the last, which is a headline mrab feature), `Match.LastGroupNumber` /
  `LastGroupName`. The `(Index, Length)` conversion stays only in these accessors (DECISIONS
  2026-08-31); an unmatched group's public shape is decided against what the ported tests
  assert, not invented.
- Zero-width group edge cases: a group that matched empty is a `(pos, pos)` span, distinct from
  an unmatched group - `same_span_as_group` (`:11639`) is where upstream distinguishes them.

## Verification

- **Un-skip** `needs:alternation`, `groups`, `named-groups` and `captures`, reading each skip's
  prose first; stragglers retag with prose.
- **Oracle wave**: generated alternations and nested/named groups over random subjects, comparing
  the full group surface - every group's span and *all* captures, not just group 0 (the S14
  recorder already writes them). Include branch-priority probes (which alternative wins) and
  empty-alternative patterns. Zero divergences; negative control.
- Prove a test red by breaking `restore_groups` on backtrack (rule 8) - group state that is not
  restored is the classic silent engine bug and must be pinned from day one.

## Done when

- [ ] The four tags delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green over the full group surface, counts quoted.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: group spans not restored on
      backtracking out of a branch, capture lists shared by reference between saved and live
      state, an unmatched group reported as an empty match or vice versa), commit.
