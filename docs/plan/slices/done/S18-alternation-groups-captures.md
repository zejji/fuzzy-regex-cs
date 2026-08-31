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

- [x] The four tags delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green over the full group surface, counts quoted.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: group spans not restored on
      backtracking out of a branch, capture lists shared by reference between saved and live
      state, an unmatched group reported as an empty match or vice versa), commit.

## Closing notes (2026-08-31)

**What landed.** `BRANCH`, `START_GROUP` and `END_GROUP` in both switches; `GroupSpan`/`GroupData`,
`save_capture`/`unsave_capture`/`clear_groups`, `same_span`/`same_span_as_group`; `copy_groups` and
the whole `Match` group surface - `Groups[n]`, `Groups[name]`, `Group.Success`/`Name`/`Index`/
`Length`/`Value`, `Group.Captures` as the full capture list, and `LastGroupNumber`/`LastGroupName`
with the `do_match` loop behind them. Parity **18.8% to 21.8%** (369 to 429 of 1966); 3917 to 3987
passing, 0 failing. `Groups` 0% to 30.8%, `Various` 25.8% to 31.7%, `Api` and `Basics` to 100%.

**Tags.** 61 `[Skip]` attributes removed across 22 files, and **all four tags are now at zero** -
no `needs:alternation`, `needs:groups`, `needs:named-groups` or `needs:captures` remains anywhere.
Of those, 28 test methods were re-skipped with prose naming the construct they still wait on (24 by
one pass, 4 found by a second run), and **one fan-out method was split**:
`VariousNamedGroupTests.Search_returns_the_expected_group_values` kept its 3 rows that now pass and
its 5 backreference rows moved to a new `needs:backrefs` method, with the upstream row numbers
divided `#11,17,201` and `#6,12,14,18,202`. Every other fan-out method failed in *all* its rows, so
nothing else needed splitting. `needs:quantifiers` went 72 to 96 attributes and is now worth 253
tests - it is by a wide margin the biggest remaining win, which is S19.

**Oracle.** New `groups` generator: nested and named groups round two-, three- and empty-alternative
branches, over ASCII, mixed and astral subjects, no construct beyond S18. `agree 7500
unsupported 0 diverge 0` over all five generators at 1500 rows each. **Three negative controls
fired**: dropping `unsave_capture` gave 9 divergences, reversing branch priority 79, and ordering
`lastindex` by group number instead of by which group closed last gave 195.

**`lastindex`/`lastgroup` are now in the wave.** The first blind pass noted the recorder did not
record them, so the oracle was blind to two members this slice ships. Closing that was in scope -
the slice asks for the *full* group surface - and it is what the third negative control above
exercises. `MatchOutcome` gained `LastIndex`/`LastGroup`, and
`A_wrong_lastindex_alone_is_reported_as_a_divergence` proves the comparer notices a difference in
those two fields alone.

**`push_pointer` is decided.** `ByteStack.PushNode`/`PopNode` push an **index into
`PatternObject.NodeList`** - upstream's own `node_list` - not a reference. 8 bytes either way, so
the byte layout still matches upstream's, which a second parallel stack of nodes would have broken.
`Node.Index` is assigned once at the end of `PatternObject.Compile`, after the optimiser has pruned
the list and after the required-string node has been added to it.

**Two things this slice's scope got wrong, narrowed with reasons.** `save_captures` /
`restore_groups` / `discard_groups` (`:17403`, `:17468`, `:17500`) are **not** exercised by a
repeated capture group: their only callers are `do_best_fuzzy_match` and
`do_enhanced_fuzzy_match`, which are Phase 5. A repeat uses `push_captures`/`pop_captures`, and
*those* are only called from `ATOMIC`, `CONDITIONAL`, `LOOKAROUND`, `GROUP_CALL` and
`GROUP_RETURN` - all Phase 4. So none of the six was ported: each would have been engine code with
no caller and therefore no test. `PORTMAP.md` records the call sites so the next slice does not
re-derive this.

**Analyzers.** Four findings, all fixed rather than suppressed, nothing disapplied: MA0008 got
`[StructLayout(LayoutKind.Auto)]` on the two new structs (the `FuzzyCounts.cs` precedent), IDE0032
turned `LastGroupNumber` into an auto-property, IDE0017 moved the group allocation ahead of the
object initialiser, and MA0051 was answered by splitting `pattern_new_match` out of `FuzzyRegex.Run`
into `NewMatch` - which is a separate function upstream too, so the rule improved the fidelity.

**Review.** One blind pass over the whole diff (hunting: spans not restored on backtracking out of a
branch, capture lists shared by reference, an unmatched group reported as empty or vice versa, a
stale `Node.Index`, `(start,end)` vs `(Index,Length)` slips, `lastindex` from the wrong field,
`IndexOutOfRange`/`NullReference` on a legal pattern, and a generator emitting patterns the port
cannot compile). **Findings raised: 0. Reproduced: 0. Fixed: 0.** The reviewer additionally swept
6000 group-only rows, 6470 rows over shapes the generator omits, and all 1547 flag-free corpus
patterns against seven subjects - zero divergences, and no exception other than a `needs:` seam or
`FuzzyRegexParseException`. Its one non-defect observation - that the oracle could not see
`lastindex`/`lastgroup` - was acted on, and **a second blind pass ran over that delta alone** (rule
4): also **0 findings**, with the comparison proved live by mutating one field of one recorded row.

**For S19.** The `advance:` label is back and `BRANCH`'s backtrack case jumps to it, so a repeat
case can too. `reset_guards` is still the only thing the `FAILURE` backtrack case and `init_match`
leave unported, and both call sites name it. The three `needs:quantifiers` methods that a repeat
alone will *not* unblock are tagged for their real slice instead: one `needs:lookaround`, one
`needs:atomic`, one `needs:recursion`.
