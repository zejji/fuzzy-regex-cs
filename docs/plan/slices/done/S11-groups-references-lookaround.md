---
slice: S11
phase: 2
title: Backreferences, group calls, conditionals, lookaround, atomic groups and verbs
delivers: []
---

# S11 - Backreferences, group calls, conditionals, lookaround, atomic groups and verbs

## Scope

`src/FuzzyRegex/Parsing/`, mirroring `upstream/regex/_regex_core.py`.

- **Backreferences**: `RefGroup` (`:3449-3498`), `make_ref_group` (`:437`), `parse_group_ref`
  (`:1416`), the numeric-escape backreference branch of `parse_numeric_escape` (`:1339`),
  `(?P=name)`, `\g<name>` and `\g<n>`, `Info.is_open_group` semantics in V0 versus V1, and the
  `REF_GROUP_IGN`/`_FLD` variants under case folding.
- **Group calls and recursion**: `CallGroup`, `CallRef` (`:2526-2581`), `parse_call_group`,
  `parse_rel_call_group`, `parse_call_named_group` (`:1100-1133`): `(?R)`, `(?0)`, `(?1)`,
  `(?+1)`, `(?-1)`, `(?&name)`, `(?P>name)`; `Info.group_calls`, `Info.defined_groups`,
  `_check_group_features` for real (`:4421-4460`), `info.call_refs`, `additional_groups` and the
  `CALL_REF`/`END` wrapping in the pipeline (`_main.py:634-650`), `Group.fix_groups` recording
  `defined_groups`.
- **Conditionals**: `Conditional`, `LookAroundConditional` (`:2655-2744`, `:3218-3303`),
  `parse_conditional`, `parse_lookaround_conditional` (`:1007-1070`): `(?(1)yes|no)`,
  `(?(name)yes|no)`, `(?(?=...)yes|no)`, `(?(DEFINE)...)`.
- **Lookaround and atomic**: `LookAround` (`:3150-3218`), `Atomic` (`:2074-2130`),
  `parse_lookaround`, `parse_atomic` (`:995-1007`, `:1070`): `(?=)`, `(?!)`, `(?<=)`, `(?<!)`,
  `(?>)`.
- **Branch reset**: `parse_common` (`:1082`), `Info.open_group_count`, `private_groups` and the
  nested-named-group aliasing in `Info.open_group`.
- **Comments and verbs**: `parse_comment` (`:978`) for `(?#...)`; `parse_extension` (`:942`)
  for `(*PRUNE)`, `(*SKIP)`, `(*FAIL)`, `(*F)`; `Prune`, `Skip`, `Failure` (`:3366`, `:3987`,
  `:2780`).
- `parse_paren` (`:850-942`) now dispatches every `(?` form except fuzzy constraints, which are
  S13's.

## Verification

Corpus rows with references, recursion, conditionals, lookaround, atomic groups, branch reset,
DEFINE and verbs now pass. `_check_group_features` is the one place the compiler emits extra
copies of groups (`additional_groups`); a row that is right up to the `SUCCESS` opcode and wrong
after it is that code path.

Group numbering under branch reset and nested named groups is the most error-prone bookkeeping
in the parser (`Info.open_group`, `private_groups`, negative aliases fixed up in `fix_groups`).
The corpus catches wrong numbers only when they reach the bytecode or `group_index`; also
un-skip nothing here, because every `groups`/`named-groups`/`branch-reset` test needs a match.

## Done when

- [x] Corpus rows within scope pass; no row fails.
- [x] `docs/PORTMAP.md` updated for every symbol.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a negative private group alias that
      never gets fixed up, `is_open_group` applied under the wrong version, `defined_groups`
      keyed by alias rather than final number), commit.

## Closing notes (2026-08-31)

**What landed.** Every `(?` form except fuzzy constraints, and every `needs:` seam this slice
owned. Nodes: `Atomic`, `CallGroup`, `CallRef`, `Conditional`, `LookAround`,
`LookAroundConditional`, `RefGroup`, `Grapheme`, `GraphemeBoundary`, `Failure`, `Prune`, `Skip`.
Parse functions: `ParseLookaround`, `ParseConditional`, `ParseLookaroundConditional`,
`ParseAtomic`, `ParseCommon`, `ParseCallGroup`, `ParseRelCallGroup`, `ParseCallNamedGroup`,
`MakeRefGroup`, the `Verbs` table, the completed `ParseParen` / `ParseExtension` /
`ParseGroupRef` / `ParseNumericEscape`, the `\R` and `\X` arms of `ParseEscape`, and the real
`CheckGroupFeatures`.

Ratchet GREEN: 3,865 tests, 1,692 passing (from 1,447), 0 failing. **1,443 of the corpus's 1,662
rows now pass and none fails**; all 219 that skip are S13's - 127 `fuzzy-syntax`, 62
`substitution`, 30 `named-lists`. No `needs:` seam this slice owned survives.

**Un-skipped nothing**, as the slice said: every `groups`/`named-groups`/`branch-reset` test needs
a matching engine. The evidence is corpus rows and a differential wave.

**The corpus is blind to some of this, so a wave was run.** `.scratch/corpus_coverage.py` measured
the fixture: **zero** rows for a relative group call, 7 for branch reset, 1 for `\R`, 3 for `\X`.
A wave of 3,668 patterns aimed at those gaps (`.scratch/s11_record.py` reusing
`tools/record-compile-corpus.py`'s set-order patches, against a scratch console) ended at **40
divergences, every one in a class already recorded in PORTMAP**: 16 are the UTF-16-versus-codepoint
offset divergence, 18 are the deliberate `NotSupportedException` where upstream's `ValueError`
escapes, 6 are the Unicode 17.0-versus-host-16.0.0 table difference. Nothing else disagreed, byte
for byte.

**Surprises.**

- **`(?-0)` is not `(?-1)`'s neighbour.** The two signs are asymmetric: only the minus arm carries
  the `+ 1`, so `(?-0)` names the *next* group. Pinned.
- **`(?+)` is not a relative call.** `parse_paren` only takes that branch when a digit follows, so
  it falls through to the flags parser and fails as "unknown extension" at a different offset.
- **Nd runs are not all ten codepoints long.** U+1D7CE-U+1D7FF is one 50-long run of five aligned
  blocks, which killed the first attempt at deriving a digit's value from its run.
- **Two analyzer families fired and both were obeyed, not suppressed.** SS008/S2328 (hash over a
  mutable member) was right: the group number is only known after `fix_groups`, so hashing it would
  let a node's hash change inside a set - the three new nodes hash their immutable part instead, as
  `Branch` already did. S3877 (throw from `Equals`/`GetHashCode`) was also right: `CallRef` and
  `GraphemeBoundary` use reference equality rather than reproducing upstream's `AttributeError`,
  because throwing there breaks any collection that touches the node and buys nothing. Both
  decisions are in PORTMAP's "Where we diverge". Nothing was disapplied.
- **One name collision.** Our `(*SKIP)` node is `Parsing.Skip`, which is ambiguous with
  `TUnit.Core.Skip` inside the corpus tests; the one call site is now fully qualified.

**Review.** One blind pass over the whole diff, then a second over the fix delta.

- **First pass: 2 findings raised, 2 reproduced, 2 fixed.** (1) `Conditional.Equals` compared the
  group *text* as well as the resolved number, so `(?(1)…)` and `(?(one)…)` naming one group were
  unequal and `Branch.optimise` failed to hoist them - a real bytecode divergence on
  `(?<one>x)(?:(?(1)a|b)c|(?(one)a|b)d)`, reproduced against the oracle. (2) A group name in
  non-ASCII decimal digits threw `FormatException`: Python's `int()` accepts any Unicode decimal
  digit, so `(?P=١)` is a reference to group 1 upstream. Fixed at the root - one
  `PythonStr.TryParseInt` now serves `parse_name`, `is_open_group` and all three nodes' `fix_groups`
  - and pinned by `UnicodeDigitGroupNameTests`.
- **Second pass over the fix delta (rule 4): 2 findings raised, 2 reproduced, 2 fixed.** The first
  fix took the digit *value* from .NET's `CharUnicodeInfo` while the `isdigit` gate reads upstream's
  17.0.0 tables, so ten Unicode 17.0 codepoints were a digit to one and not to the other and the
  port **threw where upstream compiles**. `PythonStr.DecimalValue` now derives the value from
  upstream's own tables, verified exhaustively against `CharUnicodeInfo`. The second was a stale
  remark asserting an invariant the delta's own test disproves. No third pass: the fixes touched
  only code both passes had now seen, plus tests.
- Both passes also verified the wave and the suite independently and reported matching counts.

**For the next slice.** S12 owns replacement templates and `regex.escape`; S13 owns fuzzy syntax
and named lists, which is every remaining corpus skip. `PatternCompiler.Compile` still hard-codes
`fuzzy = false` in two places - the `_check_group_features` call and the whole-pattern
`call_refs` lookup - and both become real when `Fuzzy` lands.
