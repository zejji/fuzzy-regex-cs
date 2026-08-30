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

- [ ] Corpus rows within scope pass; no row fails.
- [ ] `docs/PORTMAP.md` updated for every symbol.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a negative private group alias that
      never gets fixed up, `is_open_group` applied under the wrong version, `defined_groups`
      keyed by alias rather than final number), commit.
