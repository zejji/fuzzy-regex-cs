---
slice: S30
phase: 4
title: Recursion and group calls - (?R), (?1), (?&name), and the group stack
delivers: [recursion]
---

# S30 - Recursion and group calls

Needs S27 (18 of the 60 tests carry a lookaround) and S28 (`push_repeats`/`pop_repeats`). The
largest structural addition in the phase after lookaround, and the one with the most
backtrack-path state.

## Scope

All line references are `upstream/src/_regex.c` unless marked.

- **Three opcodes, both halves each**: `GROUP_CALL` forward (`:13394`) and backtrack (`:16354`);
  `GROUP_RETURN` forward (`:13474`) and backtrack (`:16375`); `CALL_REF` forward (`:12106`) and
  backtrack (`:15367`). Compiler side exists: `NodeCompiler.BuildGroupCall` and `BuildCallRef`
  (upstream `build_GROUP_CALL`, `:24757`).
- **`push_groups` (`:2490`) and `pop_groups` (`:2662`)** - all six call sites are these opcodes
  (PORTMAP S16 row). Plus the remaining six `push_repeats`/`pop_repeats` sites.
- **The group-call guard list**: `state->group_call_guard_list` (field `:513`), allocated in
  `state_init_2` (`:18275`; ours `MatchState.cs:327` deliberately leaves the hole), reset in
  `reset_guards` (`:3398`) and cleared per search attempt at `:11800` when
  `pattern->pattern_call_ref >= 0`. Allocate what this slice reads, per the convention the
  comment records.
- **Recursion depth**: upstream recurses on its own stacks, not the C stack, so a deep `(?R)`
  costs heap. Match that: no .NET recursion in the matcher, or a pathological pattern turns into
  a `StackOverflowException` that cannot be caught. `LazyAndRepeatedGroupRecursionTests` (already
  green) are the stack-depth tests for repeats; add the same shape for `(?R)`.
- **Upstream issue 614**: a group called from inside a lookbehind is built forward. Port
  faithfully; a gap test pins current behaviour for Phase 6.

## Verification

- **Un-skip** `needs:recursion` (60 tests across `RecursiveTests.cs`,
  `RegressionsRecursionTests.cs`, `RegressionsGroupTests.cs`; 19 of the 41 attributes are
  multi-line `[Skip(`). One fan-out case,
  `Recursive_numbered_group_reference_checks_balanced_xml_like_tags(foo<foo/>, False)`, already
  passes vacuously - the subject fails before the call is reached - so seeing one green before
  any code is written is expected there and nowhere else.
- **Oracle generator `recursion`**: `(?R)`, `(?0)`, `(?1)`, `(?-1)`, `(?+1)`, `(?&name)`,
  `(?P>name)`; balanced-bracket and palindrome shapes; a call inside a repeat; a call inside a
  lookaround and a lookaround inside a called group; a backreference to a group that was set
  inside a call (upstream restores the caller's captures on return - check what the reference
  sees); under `(?r)`. Zero divergences. Negative controls: `pop_groups` skipped on the backtrack
  path; the guard list never cleared (a second search on the same state stops early).
- **Add `recursion` to the default oracle list.**

## Done when

- [ ] Tag delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green; controls recorded in full.
- [ ] PORTMAP: the three opcodes (both arms), `push_groups`, `pop_groups`, the guard list; the
      `state_init_2` and `reset_guards` rows updated to say only the fuzzy hole remains.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: captures after a call showing the
      callee's values; infinite recursion on `(?R)` with an empty body not guarded; .NET stack
      recursion), commit.
