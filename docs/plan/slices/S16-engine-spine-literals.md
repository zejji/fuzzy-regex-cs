---
slice: S16
phase: 3
title: Engine spine - state, backtracking, search loop, literals and plain anchors
delivers: [basic-matching, full-match]
---

# S16 - Engine spine: state, backtracking, search loop, literals and plain anchors

The first slice in which a pattern matches a string. It is deliberately the smallest opcode set
that exercises the whole spine - match state, the backtracking stacks, the dispatch loop, the
search-position advance, timeout, and the public entry points - so the novelty budget is spent on
the loop, not on constructs. Budget two driver sessions mentally; the two-failure park rule is
the backstop.

**Structural decision, made here and recorded in DECISIONS**: mirror upstream's shape - an
explicit byte-stack backtracking machine inside one big dispatch switch - rather than
"improving" it into recursion or a strategy table. Upstream diffs must keep mapping onto our
files mechanically (AGENTS.md), and `basic_match` is where most future upstream fixes land.

## Scope

All line references are `upstream/src/_regex.c`.

- **The stacks**: `ByteStack_*` (`:2285-2433`) and the typed push/pop/drop helpers
  (`:2434-2815`), including `push_groups`/`pop_groups` and friends as far as the spine needs them
  (the group ones get real content in S18). Pool the backing arrays (`ArrayPool`, design spec
  section 4). If S15 did not land the first field-exposing struct, this slice does: handle
  S1104/MA0008/CA1051 per the `.editorconfig` comment.
- **State**: `RE_State` init/fini - `state_init` (`:18598`), `state_init_2` (`:18275`),
  `state_fini` (`:18662`), `init_match` (`:3404`), `clear_groups` (`:3369`), `reset_guards`
  (`:3383`). Drop the bytes/buffer branches (`get_string` `:18217` - this port is `char`-based)
  and the GIL/lock machinery (`acquire_state_lock` `:20847` - state is per-call here, immutability
  of the pattern object is the thread-safety story, and the `initonly` reflection test from S07
  already pins it). `check_compatible` (`:18573`) drops with bytes.
- **Timeout**: `check_timed_out` (`:2253`), wired to `MatchTimeout` with `decode_timeout`'s
  semantics (`:21056`); a gap test that a pathological pattern actually times out arrives with
  quantifiers (S19), but the plumbing and an `InfiniteMatchTimeout` test land now.
- **The match drivers**: `do_match` (`:18121`), `do_match_2` (`:18099`), `do_exact_match`
  (`:18064`); `do_simple_fuzzy_match`, `do_enhanced_fuzzy_match`, `do_best_fuzzy_match`
  (`:18027`, `:17862`, `:17584`) exist as seam stubs throwing `needs:fuzzy-matching`.
- **`basic_match`** (`:11714-17403`): the outer shape - the quick anchor checks at the top
  (`:11742-11752`), the search-position advance and retry loop, `start_match`, the main dispatch
  switch and the `backtrack:` switch - with only this slice's opcodes real. **Deferred to Phase
  7** (DECISIONS 2026-08-31): `locate_required_string` (`:11082`), the `string_search` /
  `fast_string_search` family (`:5231-6918`), and the test-node fast path (`try_match`,
  `:6919-7686`). All are semantically transparent prefilters; search tries the pattern at each
  position. If a later slice's un-skipped test times out because of this, that is the named
  contingency in S19 and S26.
- **Every out-of-scope opcode case throws a seam exception naming its capability tag** (the S07
  rule: port the control flow, throw at the leaf). `basic_match` has 62 references to
  `RE_STATUS_FUZZY`; the fuzzy hooks it needs before Phase 5 are stubs that throw, never silent
  no-ops - an unexpected reach must be a skip or an `unsupported` oracle row, not a wrong match.
- **Opcodes, main and backtrack cases**: `SUCCESS` (`:15160`), `FAILURE` (`:13130`, `:15681`),
  `CHARACTER` (`:12123`, `:15907`, `:16533`), `STRING` (`:14719`, `:15993`, `:16683`) via
  `same_char` (`:2838`) and `matches_CHARACTER` (`:2912`), `ANY` / `ANY_ALL` / `ANY_U`
  (`:11917`, `:11937`, `:11997`) via `matches_ANY`/`matches_ANY_U` (`:2900`, `:2906`),
  `START_OF_STRING` (`:14681`), `END_OF_STRING` (`:13052`), `START_OF_LINE` / `_U` (`:14643`,
  `:14662`), `END_OF_LINE` / `_U` (`:13014`, `:13033`), `END_OF_STRING_LINE` / `_U` (`:13071`,
  `:13091`), `SEARCH_ANCHOR` (`:14431`), with the line predicates `ascii_at_line_start/end`
  (`:899`, `:919`), `unicode_at_line_start/end` (`:1942`, `:1963`), `ascii_is_line_sep` /
  `unicode_is_line_sep` (`:894`, `:1936`). Word and grapheme boundaries are S20.
- **Codepoint iteration over UTF-16**: `char_at` and position stepping decode surrogate pairs
  inline (design spec section 4). Internal positions and spans are `(start, end)` in UTF-16 code
  units, mirroring upstream's names; conversion to `(Index, Length)` happens in exactly one
  place, the public `Match`/`Group` accessors (DECISIONS 2026-08-31). A gap test with an astral
  subject pins both the internal step (one codepoint, two units) and the public numbers.
- **Public surface**: `pattern_search_or_match` (`:21522`), `get_limits` (`:21627`),
  `limited_range` (`:18794`), `pattern_new_match` (`:20738`) reduced to what group 0 needs.
  `Match` gains fields and returns real `Index`/`Length`/`Value`/`ValueSpan` for the whole match;
  `Groups` and the rest keep throwing their seams until S18. `IsMatch`, `IsMatchAtStart`,
  `IsFullMatch`, `Match`, `MatchAtStart`, `FullMatch` (upstream `search` / `match` / `fullmatch`
  respectively - fullmatch is `match_all` in `state_init_2`) and the static conveniences work for
  in-scope patterns; `partial: true` throws `needs:partial` (Phase 4). `beginning`/`length`
  follow `get_limits`' clamping. `Matches`, `Replace`, `Split`, `Count` stay seamed (S24/S25).

## Verification

- **Un-skip `needs:basic-matching`** (44 tests) and the `full-match` tests whose patterns are
  in scope, reading each skip's prose first; anchor tests that need only this slice's opcodes
  un-skip too, the rest stay on their tags.
- **Oracle wave, the first real one**: the S14 literal and literal-plus-dot generators, plus
  waves for anchors under MULTILINE and for `IsMatchAtStart`/`IsFullMatch` semantics against
  upstream `match`/`fullmatch`. Zero divergences, every remaining `unsupported` row explained by
  a named seam. Quote the counts.
- **Non-BMP**: the astral-subject gap test above, proven against a recorded oracle row.
- Prove one test red without the engine (VERIFICATION rule 8) - e.g. revert the dispatch loop's
  `CHARACTER` case and watch the suite fail.

## Done when

- [ ] `basic-matching` delivered: the tag is gone from `docs/STATUS.md`'s waiting table (tests
      genuinely blocked on something else are retagged with prose saying why, the S07/S13
      precedent); `full-match` delivered or its stragglers retagged.
- [ ] Oracle run green with counts quoted; divergences, if any were found and fixed en route,
      minimised into permanent tests.
- [ ] Span-convention gap test in; conversion exists only in the `Match`/`Group` accessors.
- [ ] Analyzer duty discharged here if S15 handed it over.
- [ ] `docs/PORTMAP.md` updated for every `_regex.c` symbol ported or stubbed; the structural
      decision and the prefilter deferral recorded in DECISIONS (deferral is already there dated
      2026-08-31 - extend it if the shape changed in practice).
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a surrogate pair stepped as two
      positions, a search-position advance that skips the empty match at the end of the subject,
      a backtrack case that pops in a different order than its push, `state.text_pos` written
      where upstream writes a local), commit.
