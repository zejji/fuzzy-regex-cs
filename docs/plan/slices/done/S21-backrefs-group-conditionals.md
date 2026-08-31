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

- [x] `backrefs` and `define-groups` delivered or stragglers retagged; the `conditionals`
      boundary note above reflected in the remaining skips' prose; counts in closing notes.
- [x] Oracle wave green; counts quoted.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: an unmatched group's backreference
      matching empty, a reference inside a repeat reading the pre-iteration span, GROUP_EXISTS
      consulting the group's *final* state instead of its current one), commit.

## Closing notes (2026-08-31)

**What landed.** Two cases in `Matcher.BasicMatch`'s dispatch switch and nothing else in the
engine: `REF_GROUP` (`:14004`) and `GROUP_EXISTS` (`:13442`). The parser, `NodeCompiler` and
`Optimiser` already built both nodes, so this slice never touched `Parsing/`. Neither case pushes
to the backtracking stack, so neither has a backtrack arm - `REF_GROUP`'s upstream backtrack rows
(`:17269-17276`) are nothing but `retry_fuzzy_match_string`, which is Phase 5's, and `GROUP_EXISTS`
only picks an exit.

Parity **39.0% to 42.2%** (767 to 830 of 1966). Suite 4416 passing of 5552, 0 failing, 1136
skipped. `backrefs` and `define-groups` are both off the capability board; `conditionals` remains
at 15, exactly as the slice predicted.

**Tag counts.** 64 tests came off a skip. 59 failed on the two seams as expected; 5 passed
immediately, all of them rows tagged `backrefs` that hold no backreference at all - `[\1]`,
`\141`, `[\41]` and `^\w+=(\\[\000-\277]|[^\n\\])*` are octal escapes, so they never compile a
`REF_GROUP`. Over-tagged when they were ported, not evidence of anything.

Retagged rather than un-skipped:

- **All 15 `define-groups` tests to `needs:recursion`.** Every one of them *calls* into the DEFINE
  with `(?&name)`, which is `CALL_GROUP` and Phase 4's. The slice expected some to be deliverable;
  on reading the file, none is. The behaviour this slice *does* deliver - the DEFINE body being
  stepped over in place, and consuming nothing - has no ported test at all, so it is pinned by two
  new gap tests instead.
- **17 `conditionals` tests in `LookbehindTests` to `needs:lookaround`.** They are group-existence
  conditionals *inside* a lookbehind; after this slice the only missing piece is the lookbehind, so
  the tag now names it. The 8 attributes that stay on `needs:conditionals` are the
  lookaround-condition form `(?(?=...)yes|no)`, upstream's separate `CONDITIONAL` opcode.
- **6 tests to `needs:find-all`**, all blocked only by `FuzzyRegex.Matches` (S25).
- **1 test to `needs:lookaround`**: `(.)(?(1)(?!))`. The forced fail is not an optimised-away
  `FAILURE` node - upstream compiles that pattern to
  `[30, 1, 1, 1, 2, 0, 20, 32, 1, 35, 0, 1, 20, 20, 1]`, where 35 is `LOOKAROUND` over an empty
  body (probed against regex 2026.7.19, 2026-08-31). Not a guess; the bytecode says so.

**Ten new gap tests** in `Gaps/Engine/BackrefAndConditionalTests.cs`, each with its upstream answer
probed and quoted. The two that earn their place hardest: `(?(1)a|b)(b)` on `bb` matches, because
group 1 has not matched *yet* when the conditional is reached, and `(?:(a)x|a)(?(1)y|z)` on `az`
takes the false branch, because the first alternative's capture has to be backtracked away first.

**Oracle.** New `backrefs` generator, added to `run-oracle.ps1`'s default list. Full run over all
eight generators at 1500 rows each: **agree 12000, unsupported 0, diverge 0**.

**Negative controls** - all four against `-Generator backrefs -Count 600 -Seed 1`, run with
`-SkipRecord` against one recorded wave. Three fired; two did not, and *why* is the useful part.

> Control A, `unmatched-ref-matches-empty`: in `Matcher.cs`, the `RefGroup` case, replace
> `goto backtrack;` in the `refGroup.Current < 0` block with `node = node.Next1.Node!; break;`.
> Wave: `backrefs`, 600 rows, seed 1. Result: 593 agree, **7 diverge**.

> Control B, `group-exists-swapped-exits`: in `Matcher.cs`, the `GroupExists` case, replace the
> `groupExistsGroup.Current >= 0 ? node.Next1.Node! : node.Next2.Node!` conditional with
> `groupExistsGroup.Current >= 0 ? node.Next2.Node! : node.Next1.Node!`. Wave: `backrefs`, 600
> rows, seed 1. Result: 541 agree, **59 diverge**.

> Control C, `group-exists-ever-matched`: in `Matcher.cs`, the `GroupExists` case, replace
> `groupExistsGroup.Current >= 0` with `groupExistsGroup.Count > 0`. Wave: `backrefs`, 600 rows,
> seed 1. Result: 600 agree, **0 diverge - did not fire**. Not a generator weakness: `UnsaveCapture`
> (`MatchState.cs:624`) decrements `Count` on the same backtrack that restores `Current`, so for a
> plain capture group the two tests are equivalent. They can only part company for a branch-reset
> group, whose private/public index asymmetry is not in scope until that slice.

> Control D, `ref-reads-first-capture`: in `Matcher.cs`, the `RefGroup` case, replace
> `refGroup.Captures[refGroup.Current]` with `refGroup.Captures[0]`. Wave: `backrefs`, 600 rows,
> seed 1. Result: 583 agree, **17 diverge**. It fired on 6 before the `repeated-then-ref` shape was
> added to the generator; the shape exists to widen exactly this control.

> Control E, `ref-steps-by-code-unit`: in `Matcher.cs`, the `RefGroup` case, replace
> `stringPos = state.NextPos(stringPos); state.TextPos = state.NextPos(state.TextPos);` with
> `++stringPos; ++state.TextPos;`. Wave: `backrefs`, 600 rows, seed 1. Result: 600 agree,
> **0 diverge - did not fire**, and the whole ported suite passed too. Also not a generator
> weakness, and the reason is worth carrying forward: both operands of a backreference index the
> *same* string, so a code-unit walk stays in lockstep with a codepoint walk - it tests each
> surrogate half separately and reaches the same end. The `NextPos` form is kept anyway, and the
> code says why: upstream's `++` means one character, every other opcode here spells that
> `NextPos`, and Phase 5's fuzzy retry moves `stringPos` on its own, where the lockstep argument
> stops holding. A future reader who "simplifies" this will not be caught by any test we have.

**Review.** Two blind passes, both dispatched inside the working turn.

The first pass reviewed the whole diff. It raised **5 findings and reproduced all 5**; none was an
engine defect, and it independently ran ~16,000 differential rows (two adversarial hand-built sets
on top of the generated waves) hunting the three faults the slice named, finding nothing.
Findings fixed: (1) `Matcher.Tag` still routed `REF_GROUP`/`GROUP_EXISTS` to tags this slice had
just delivered - the exact fault the rule written into that function after the S17 review exists to
prevent - so `RefGroup` and `GroupExists` came out of it entirely and `RefGroupFld`/`RefGroupIgn`
moved to `ignore-case` beside `StringFld`/`StringIgn`; (5) the prepended forward conditional in the
new generator could open a capture group, which shifted every group number in that row by one and
silently defeated both repeat shapes on 8 rows of 300 - fixed with a `may_capture` flag. Findings
2-4 were quoted measurements in the generator's own comments that did not reproduce.

The second pass reviewed only that delta - the `Tag` switch, the `may_capture` fix and the
corrected figures - because none of it had been reviewed. It confirmed the `Tag` switch is
exhaustive and disjoint from the dispatched opcodes and that `may_capture` works, and raised
**6 further findings, all reproduced, all of them figures in comments**: the compile-parity corpus
is 1,547 patterns and not 1,534 (a number `DECISIONS.md` had already retired), `classes` sits at
23% and not "near 30%", and four counts were measured with `random.Random(1)` where the recorder
seeds with `random.Random(f"{seed}:{name}")` - a different stream, so the figures described a wave
the recorder never produces. All six corrected against the recorder's own seeding.

Both reviewers were right about everything they reported, which is not the usual hit rate. The
common thread in 9 of the 11 findings is the same mistake: quoting a measurement without re-taking
it after the thing being measured changed.

**Two hazards for the next slice.** A review subagent runs in this working tree. The first one left
`upstream/regex/__pycache__` behind (dirtying the submodule) and a stale `parity-baseline.json`
recorded at 4309 while its own temporary probe file was present. Both were caught by reading
`git status --porcelain` line by line before committing, which is why that step is in the skill.
Brief a reviewer to clean up after itself, and re-run `-UpdateBaseline` after any review that ran
the ratchet.
