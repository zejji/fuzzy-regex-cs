---
slice: S08
phase: 2
title: Quantifiers, alternation and zero-width assertions
delivers: []
---

# S08 - Quantifiers, alternation and zero-width assertions

## Scope

`src/FuzzyRegex/Parsing/`, mirroring `upstream/regex/_regex_core.py`.

- **Quantifiers**: `parse_quantifier`, `parse_limited_quantifier`, `is_above_limit`,
  `apply_quantifier`, `_QUANTIFIERS`, `parse_count` (`:558-660`, `:846`); `GreedyRepeat`,
  `LazyRepeat`, `PossessiveRepeat` (`:2938-3060`, `:3146`) including `get_required_string` and
  `max_width`. `apply_constraint` and `parse_fuzzy` stay `NotImplementedException` until S13.
- **Alternation**: `Branch` complete (`:2134-2526`): `_flatten_branches`,
  `_split_common_prefix`, `_split_common_suffix`, `_can_split`, `_can_split_rev`,
  `_merge_common_prefixes`, `_reduce_to_set` **with the sorted set order S06 recorded**,
  `_flush_char_prefix`, `_flush_set_members`, `_is_full_case`, `_is_folded`, `_add_precheck`.
  `_is_folded` calls `_regex.get_all_cases`; until S09 lands the tables it may throw
  `NotImplementedException` when `case_flags` is non-zero. `make_sequence` (`:1935`).
- **Zero-width**: `ZeroWidthBase` (`:2011`) and every subclass that has no parsing logic of its
  own: `Boundary`, `DefaultBoundary`, `DefaultEndOfWord`, `DefaultStartOfWord`, `EndOfLine`,
  `EndOfLineU`, `EndOfString`, `EndOfStringLine`, `EndOfStringLineU`, `EndOfWord`,
  `StartOfLine`, `StartOfLineU`, `StartOfString`, `StartOfWord`, `SearchAnchor`, `Keep`
  (`:2130`, `:2744-2780`, `:3142`, `:3498`, `:3987-4007`). Parser side: `^` and `$` in
  `parse_sequence`, and `\A \Z \b \B \m \M \G \K` in `parse_escape`.

## Verification

Corpus rows using quantifiers, `|`, anchors and word boundaries now pass. `Branch.optimise` is
the densest logic in the whole parser and the corpus is the only thing that can check it; expect
the prefix and suffix splitting to be where the bytes first disagree. When they do, print both
integer lists side by side and find the first differing opcode before touching code.

Two Python-isms to watch in `Branch`: `dict` insertion order is load-bearing in
`_merge_common_prefixes` (`order` and `prefixed`), and `RegexBase.__eq__` structural equality is
load-bearing in `_flatten_branches` and `_split_common_*`. A `Dictionary<,>` keeps insertion
order only while nothing is removed; where upstream removes, use an explicit ordered structure.

## Done when

- [ ] Corpus rows within scope pass; no row fails.
- [ ] `docs/PORTMAP.md` updated for every symbol.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: `dict` order, `set` order, structural
      equality, `UNLIMITED` versus `None` for an unbounded `max_count`), commit.
