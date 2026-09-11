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

- [x] Tag delivered or stragglers retagged.
- [x] Oracle wave green; controls recorded in full.
- [x] PORTMAP: `CONDITIONAL`, `END_CONDITIONAL`, `push_repeats`, `pop_repeats`; the S16 row's
      "not ported yet" text updated to say which four call sites are now ported.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: repeat state not popped on the
      backtrack path; a condition that consumes text), commit.

## Closing notes (2026-09-11)

**What landed.** The four `CONDITIONAL`/`END_CONDITIONAL` cases in `Matcher.BasicMatch`, forward and
backtrack, plus the repeat stack they need: `PushRepeats`/`PopRepeats`/`PushRepeatData`/
`PopRepeatData` in `Matcher.cs` and `GuardList.PushTo`/`PopFrom` (upstream `push_guard_data`/
`pop_guard_data`). 15 `needs:conditionals` tests un-skipped, all passing; the tag is gone from the
board. Overall parity 80.6% to 81.3%, `Regressions` 51.5% to 54.7%. Suite 5724 total, 5357 passing,
367 skipped, 0 failing. Baseline `passingCount` 5234 to 5249, and the diff is exactly those 15 test
names and nothing else.

**Nothing new was needed on the compiler or the state side**, exactly as the slice predicted.
`NodeCompiler.BuildConditional` was already there from S13, and `CONDITIONAL` reuses S27's
`LookaroundStateData` unchanged - so this slice is four dispatch cases and six stack helpers, and no
new type. `BuildConditional` wires `testNode.Next2` to the end node when the pattern has no
`|no` branch, so the 'false' exit is always a real node and the no-else form needs no special case.

**Two things S29 and S30 inherit.** `push_repeats` pushes `bstack.count` onto the `pstack` through
`push_bstack` and `pop_repeats` pops it, and that coupling is ported verbatim - it is what S29's
verbs unwind against. Six of the ten `push_repeats`/`pop_repeats` call sites are still unported and
all six are S30's, inside `GROUP_CALL` and `GROUP_RETURN`; PORTMAP's new row lists them by line.
`push_groups`/`pop_groups` is still entirely S30's.

**The repeat stack is nearly invisible to a differential wave, and that is structural.** This is the
finding worth carrying, because S30 reuses the same mechanism and will be tempted to trust a green
wave. A repeat's own state is already saved and restored by its own backtrack entries along the
*same* path the conditional is unwound on, so `pop_repeats` is mostly restoring what the repeat
machinery would have restored anyway. Only an *outer* repeat, mid-iteration, whose count is non-zero
when the `CONDITIONAL` is entered, can show the difference - and then only if a pop path runs and
the count changes the answer. Two attempts to widen the generator into that corner both failed to
move the number (see Control C below). Do not read a green conditionals wave as evidence that the
repeat stack is right; it is evidence that the branch selection is right.

**A trap in the control runner, found the hard way.** `tools/run-controls.py` caches its waves in
`.scratch/control-waves/<generator>-<count>-<seed>.jsonl` and reuses them. Widen a generator and
re-run a control and you will measure the *old* generator with a straight face - the first widening
here reported byte-identical figures until the cache was deleted. Delete the cached wave for a
generator you have just changed, every time.

**Review.** One blind pass over the whole working-tree diff (Opus subagent, `docs/VERIFICATION.md`
brief, hunt list: state pushed but not popped on any of the four paths, the wrong branch for a
negative condition, a condition that consumes text leaving `text_pos` or the slice unrestored,
`PopFrom` reading a different byte count than `PushTo` wrote, divergence from upstream in which path
calls `pop_repeats`, generator rows upstream rejects). **Two findings raised, two reproduced, two
fixed; none in the engine** - the reviewer checked the four cases line by line against `_regex.c` and
ran 4,100 rows of its own adversarial waves with zero divergences. Both findings were in the new
generator: (1) the comment justifying `CONDITIONAL_REPEAT_QUANTIFIERS` quoted figures from an earlier
form of control S28-C, so re-running the committed control gave 2 and 8 where the comment said 1 and
5; (2) `cond-then-ref` emitted its backreference as a bare `\N`, which runs into a following digit -
`\1` then `0` is read as group 10 - so upstream rejected the row instead of exercising the cell. Now
`\g<N>`, verified against regex 2026.7.19 on 2026-09-11: `regex.match(r'(a)\g<1>0', 'aa0')` matches
and `regex.match(r'(a)\10', 'aa0')` raises "invalid group reference at position 6".
**No second pass was run**, judged on what changed: the fixes touch one emitted token and two
comments in a file the reviewer had already read and had itself reported on - no public API, no
engine, no new tooling behaviour - and both were re-verified by a fresh 600-row green wave and a
full re-run of all five controls rather than by opinion.

**Controls.** All five live in `tools/controls.json` and re-run with
`python tools/run-controls.py --slices S28` (or `--check` to resolve the sites without building).
Generator `conditionals`, 600 rows, seeds 7 and 20260912, every figure re-taken against the code and
the generator actually being committed, after the review's fixes.

> **Control A**, `a condition that holds takes the no-branch`: in `Matcher.cs`, the advance
> `case Opcode.EndConditional: // End of a conditional subpattern (:12356).`, positive arm, change
> `// Go to the 'true' branch.` / `node = node.Next1.Node!;` to
> `// Go to the 'true' branch.` / `node = endCondNode.Next2.Node!;`. Wave: `conditionals`, 600 rows,
> seed 7. Result: 571 agree, **29 diverge**. Re-run at seed 20260912: **42 diverge**.
>
> **Control B**, `a negative condition that holds takes the yes-branch`: same case, negative arm,
> change `// Go to the 'false' branch.` / `node = endCondNode.Next2.Node!;` to
> `// Go to the 'false' branch.` / `node = node.Next1.Node!;`. Seed 7: 583 agree, **17 diverge**.
> Seed 20260912: **32 diverge**.
>
> **Control C**, `push_repeats saves the wrong repeat count`: in `Matcher.PushRepeatData`, change
> `        stack.PushSize(repeatData.Count);` to `        stack.PushSize(repeatData.Count + 1);`.
> Seed 7: 598 agree, **2 diverge**. Seed 20260912: **8 diverge**. **A thin margin, and deliberately
> recorded as one.** Two widenings were tried and neither moved it: raising `repeat-of-cond` from
> weight 12 to 24 with a forced repeating quantifier (4 and 5 became 1 and 5, measured with the
> earlier `stack.PushSize(0)` mutation), and then changing the mutation itself from saving 0 to
> saving a wrong value so that a zero count would also bite (2 and 8). The reason is the structural
> one above, not a weighting one.
>
> **Control D**, `a condition that fails takes the wrong branch`: in `Matcher.cs`, the backtrack
> `case Opcode.Conditional: // Conditional subpattern (:15378).`, change
> `                    node = condNode.Match ? condNode.Next2.Node! : condNode.TrueNode!;` to
> `                    node = condNode.Match ? condNode.TrueNode! : condNode.Next2.Node!;`.
> Seed 7: 554 agree, **46 diverge**. Seed 20260912: **53 diverge**.
>
> **Control E**, `the repeat guard lists are not restored`: in `GuardList.PopFrom`, change
> `        Count = (int)count;` / `        return stack.PopBlock(MemoryMarshal.AsBytes(_spans.AsSpan(0, Count)));`
> to save `Count`, pop the block, then put the saved value back. Seed 7: **0 diverge**.
> Seed 20260912: **1 diverge**. **This control does not fire and must not be read as evidence.** It
> is kept in `controls.json` rather than deleted so the negative result is re-runnable: the guard
> lists are the half of `RE_RepeatData` that nothing else restores, and this wave cannot see them.
> A slice that needs that half covered - S30 - has to build a generator that can, not reuse this one.
