---
slice: S28
phase: 4
title: Conditional with a lookaround condition, and the repeat stack it needs
delivers: [conditionals]
---

# S28 - Conditional with a lookaround condition

Needs S27. Small on the board (15 tests) and its own slice because it brings in the
`push_repeats`/`pop_repeats` family, which S30's group calls then reuse - landing it here means
the recursion slice ports one new mechanism instead of two.

## Scope

- **`CONDITIONAL`** forward (`upstream/src/_regex.c:12213`) and backtrack (`:15378`);
  **`END_CONDITIONAL`** forward (`:12356`) and backtrack (`:15460`). This is `(?(?=...)yes|no)`,
  `(?(?<=...)yes|no)` and their negative forms. The group-existence form `(?(1)yes|no)` is the
  separate `GROUP_EXISTS` opcode and is already S21's; do not touch it.
- **`push_repeats` (`:2570`) and `pop_repeats` (`:2744`)**, unported until now (PORTMAP's S16 row
  lists all ten call sites; the four inside `CONDITIONAL`/`END_CONDITIONAL` are this slice's, the
  six inside `GROUP_CALL`/`GROUP_RETURN` are S30's). Note `:2591`: `push_repeats` also pushes
  `bstack.count` onto `pstack`, and `pop_repeats` (`:2765`) pops it - that is the coupling S29's
  verbs depend on, so port it exactly.
- **Compiler side is done**: `NodeCompiler.BuildConditional` exists (upstream
  `build_CONDITIONAL`, `:24547`). `LookAroundConditional` (`_regex_core.py:3218`) is parsed and
  compiled since S13; upstream issue 611 lives in its `is_empty()` - a boolean-precedence bug
  that desyncs the group count. Port faithfully and recognise it if a test trips it.

## Verification

- **Un-skip** `needs:conditionals` (15 tests, all in `RegressionsConditionalTests.cs`, all with
  multi-line `[Skip(` attributes, so a one-line grep for `Skip("needs:conditionals` misses them).
- **Oracle generator `conditionals`**: the four condition forms round literal, class and group
  bodies; with and without `|no`; the yes-branch and no-branch each empty in turn; nested inside a
  repeat so the condition is re-evaluated per iteration; under `(?r)`. Zero divergences.
  Negative controls: the no-branch taken when the condition holds; `pop_repeats` skipped on the
  backtrack path.
- **Add `conditionals` to the default oracle list.**

## Done when

- [ ] Tag delivered or stragglers retagged.
- [ ] Oracle wave green; controls recorded in full.
- [ ] PORTMAP: `CONDITIONAL`, `END_CONDITIONAL`, `push_repeats`, `pop_repeats`; the S16 row's
      "not ported yet" text updated to say which four call sites are now ported.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: repeat state not popped on the
      backtrack path; a condition that consumes text), commit.
