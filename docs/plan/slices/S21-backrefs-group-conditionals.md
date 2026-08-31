---
slice: S21
phase: 3
title: Backreferences and group-existence conditionals
delivers: [backrefs, define-groups]
---

# S21 - Backreferences and group-existence conditionals

Needs S18 (a backreference reads a group span). Forward, case-sensitive variants only; the
`_IGN`/`_FLD` backreference variants are S22 and `_REV` is S23.

**`conditionals` straddles the phase boundary - do not chase the other half.** `(?(1)yes|no)`
and `(?(name)yes|no)` are `GROUP_EXISTS` and belong here. `(?(?=test)yes|no)` is the
`CONDITIONAL` opcode (`:12213`), which is lookaround and is Phase 4's. The `needs:conditionals`
tag will therefore still be on the status board when this slice is done and the phase closes -
that is correct, not unfinished work. Un-skip only the GROUP_EXISTS-shaped tests; leave the
lookaround-shaped ones on their tag with prose updated to say they wait for Phase 4.

## Scope

All line references are `upstream/src/_regex.c`.

- **Main-switch cases**: `REF_GROUP` (`:14004`), `GROUP_EXISTS` (`:13442`); the shared
  backreference backtrack block (`:17269-17276`, the case-sensitive rows).
- The comparison walks the captured span against the subject with `same_char` (`:2838`) -
  codepoint by codepoint over UTF-16, the S16 stepping.
- **Semantics to pin against the oracle, not assume**: a backreference to an unmatched group
  fails (it does not match empty); a backreference to a group that matched empty matches empty;
  `GROUP_EXISTS` tests *matched so far in this attempt*, not "exists in the pattern";
  `(?(DEFINE)...)` - upstream compiles DEFINE as a `GROUP_EXISTS` whose condition never holds, so
  its body never executes in place, which is why `define-groups` (15 tests) is deliverable here
  even though *calling* into a DEFINE'd group is recursion and stays on `needs:recursion` for
  Phase 4. Read each `define-groups` skip's prose: un-skip the ones that only need the body to be
  skipped over, retag the ones that call.

## Verification

- **Un-skip** `needs:backrefs` (40) and the `define-groups` and `conditionals` tests within
  scope as above; stragglers retag with prose.
- **Oracle wave**: backreferences to matched, unmatched and empty-matched groups, numbered and
  named forms, references inside repeats (the span the reference reads changes per iteration),
  `(?(n)y|n)` with and without `|no`, over random subjects. Zero divergences; negative control.

## Done when

- [ ] `backrefs` and `define-groups` delivered or stragglers retagged; the `conditionals`
      boundary note above reflected in the remaining skips' prose; counts in closing notes.
- [ ] Oracle wave green; counts quoted.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: an unmatched group's backreference
      matching empty, a reference inside a repeat reading the pre-iteration span, GROUP_EXISTS
      consulting the group's *final* state instead of its current one), commit.
