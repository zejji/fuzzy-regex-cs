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

- [x] Corpus rows within scope pass; no row fails.
- [x] `docs/PORTMAP.md` updated for every symbol.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: `dict` order, `set` order, structural
      equality, `UNLIMITED` versus `None` for an unbounded `max_count`), commit.

## Closing notes

**What landed.** Quantifiers, alternation and the zero-width position nodes, as scoped.
`Parsing/Nodes.cs` gains `Branch` and every one of its `optimise` helpers, the three repeat
classes and the sixteen `ZeroWidthBase` subclasses; `ParseFunctions.cs` gains `apply_quantifier`,
`parse_quantifier`, `is_above_limit`, `parse_limited_quantifier`, `parse_count`, the `^`/`$` and
`?*+{` branches of `parse_sequence` and the positional escapes. Compile-parity rows went from 436
to **693 of 1547** compiles and 16 to **23 of 50** errors, with nothing failing; the ratchet
reports **786 passing tests** (was 514 at the baseline this slice started from). `quantifiers`
fell from 460 waiting tests to 179, `anchors` from 261 to 87, and `alternation` left the board
entirely.

**Two claims this slice had written down before their evidence arrived**, both now settled against
the real Python module (`regex` 2026.7.19, 2026-08-30):

1. `.???` really is `multiple repeat` at position 3 - `ERR error multiple repeat at position 3
   pos= 3`. But the ported test asserted the message `"multiple repeat at position 3"`, which is
   Python's `str(error)` and not this port's shape, and it was red. Split into a message assertion
   and an `Offset` assertion, which is stricter than upstream's own `assertRaisesRegex` against
   the bare string `"multiple repeat"`.
2. `max_width` saturation is indistinguishable from upstream's unbounded `int`, confirmed by
   bytecode rather than by argument. Getting that bytecode needed a new technique: upstream's C
   compiler is O(repeat count), so `regex.compile('a{4294967294}')` runs for about twenty minutes
   and dies with `MemoryError`, while everything before it is O(1). Capturing the arguments to
   `regex._regex.compile` and *not* calling through returns in milliseconds. The dead session's
   script called through, which is the whole reason it looked like a hang.

**Surprises.**

- **`CompiledPattern.ReqOffset` was an `int` and truncated.** Writing the test for claim 2 found
  it: an offset can be as large as `UNLIMITED - 1` = 4294967294, so `(?:a{65535}){0,65535}a`'s
  offset of 4294836225 came out as -131071. Now a `long` end to end. No corpus row has an offset
  above `int.MaxValue`, which is why 693 rows passed with the bug in place - worth remembering
  before trusting a green corpus about a boundary the corpus never reaches.
- **A comment can fail the build.** Sonar S125 read a comment line ending in a semicolon as
  commented-out code. The first un-skip of this slice was red for that reason alone.
- Both patterns in the width test end in the same character they repeat (`...a` rather than
  `...b`): a different trailing character makes the first set two members wide, which needs
  `SetUnion` and so waits for S10.

**Review.** One blind pass over the whole diff, briefed to hunt `dict` insertion order, `set`
order, structural equality and `UNLIMITED` versus `None`, and to hand over reproductions rather
than opinions. **Findings raised: 0. Reproduced: 0. Fixed: 0. No second pass was needed**, because
no code changed after the review - the three code fixes this slice made (the `long` offset, the
split parse-error assertion, the S125 comment) all landed before it was dispatched. The pass was
differential rather than by eye: about 34,000 (pattern, flags) pairs compiled on both sides, 19,000
of them reaching real bytecode, comparing `code`, `req_offset`, `req_chars`, `req_flags`,
`group_count` and the resolved flags - hand-built waves over every construct in scope, plus six
seeds of grammar fuzzing at depth 3, plus ~150 maximal-count shapes that independently confirm the
three expectations in `RepeatWidthOverflowTests.cs`. A negative control (a wrong opcode injected
into three rows) proved the comparator detects differences. It cleared the four hunt items by
reading as well: every ordering leak that reaches the bytecode is still behind a `needs:` seam,
the node equalities match upstream's `_key`, `MaxCount` stays nullable everywhere, and
`PySlice`'s clamping is unreachable with a negative start because both `_can_split` guards
short-circuit first.

**One out-of-scope difference the reviewer noticed and this slice deliberately did not fix**:
`(?V0:x(?V1:y))` raises `KeyError: regex.V0|V1` upstream (reproduced 2026-08-30) but
`ArgumentOutOfRangeException` here, from `RegexFlags.cs:180`, which S07 wrote and this diff does
not touch. Both are non-`error` exceptions, so no corpus row can see it. It belongs to whichever
slice surfaces the version flags.
