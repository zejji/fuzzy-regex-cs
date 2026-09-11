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

- [x] Tag delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green; controls recorded in full.
- [x] PORTMAP: the three opcodes (both arms), `push_groups`, `pop_groups`, the guard list; the
      `state_init_2` and `reset_guards` rows updated to say only the fuzzy hole remains.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: captures after a call showing the
      callee's values; infinite recursion on `(?R)` with an empty body not guarded; .NET stack
      recursion), commit.

## Closing notes (2026-09-11)

**What landed.** Six matcher arms, each at upstream's switch position: `CALL_REF` (`:12106`
forward, `:15367` backtrack), `GROUP_CALL` (`:13394`, `:16354`) and `GROUP_RETURN` (`:13474`,
`:16375`). Plus `push_groups`/`pop_groups` (`:2490`, `:2662`) as `Matcher.PushGroups`/`PopGroups`,
which completes both that pair's six call sites and `push_repeats`/`pop_repeats`'s remaining six.
`ByteStack.PushNode`/`PopNode` now carry upstream's `NULL` node pointer as index `-1`, which is what
`CALL_REF` pushes and `GROUP_RETURN` reads to decide whether the group was called or merely reached.

**Counts.** 41 `[Skip("needs:recursion …")]` attributes removed across four files - `RecursiveTests`
13, `RegressionsDefineTests` 15, `RegressionsRecursionTests` 12, `RegressionsGroupTests` 1. The
slice file named only three of those four; `RegressionsDefineTests` carries the tag too, because
every test in it calls into a `(?(DEFINE)...)` group with `(?&name)`. 59 tests went red, exactly as
predicted bar the one vacuous pass the slice called out. Suite 5731 -> 5737 tests (six gap tests
added), passing 5396 -> 5462, skipped 275, failing 0. No `needs:recursion` skip remains.

**The five that needed more than the opcodes.** 54 of the 59 went green on the matcher work alone.
The last five were all `captures()` on a group whose `current` a call had restored, and the fault
was not in the engine: `Match.GroupAt` handed out an empty capture list for any group with
`Current < 0`. Upstream keeps the two apart - `match_get_group_by_index` (`:18847`) consults
`current`, `match_get_captures_by_index` (`:19137`) walks `count` and never looks at it - and
`GROUP_RETURN` is the first construct that can make them disagree, so the bug was latent until this
slice. Verified against upstream before changing anything:
`regex.match(r'(?&routine)(?(DEFINE)(?<routine>.))', 'a')` gives `group('routine') is None` and
`captures('routine') == ['a']`. One-line fix, in `Match.cs` rather than in the engine.

**The group-call guard list is not ported, and never will be.** The slice said "allocate what this
slice reads". Nothing reads it: `grep -n group_call_guard_list upstream/src/_regex.c` (2026-09-11)
gives six sites - the declaration, the allocation and `memset`, `reset_guards`, the per-attempt
clear at `:11800`, and two `re_dealloc` calls - and not one read. It guards nothing in upstream as
shipped, so it is now a row in PORTMAP's "deliberately not ported" table with the grep and a
re-check instruction for the next `sync-upstream`. This also killed the slice's second suggested
negative control ("the guard list never cleared"): a mutation to write-only state cannot diverge, so
the control was replaced by one on the repeat-guard clearing `GROUP_CALL` actually does (`:13421`).

**Oracle.** New `recursion` generator, on the default list. All seven call syntaxes, balanced-bracket
and palindrome shapes, calls in repeats, lookarounds in called groups, backreferences to a group the
call set, and `(?(DEFINE)...)` groups reached only through a call. Green: 600/600 at seed 7, 600/600
at seed 4242, and the full sixteen-generator default wave 6400/6400 at seed 20260911.

Two shapes are excluded on purpose. A call inside a **lookbehind** is not drawn - see the divergence
below; it would colour the whole wave with one known upstream defect. And `\w(?R)?` was dropped after
measurement: it is safe as written and raises `MemoryError` from upstream the moment `(?r)` is
prefixed, because reversing turns a trailing recursive call into a leading one that never advances.
Every shape in the table was checked in both directions for that.

**The one divergence, and it is upstream's.** Upstream matches a lookbehind containing a `(?&name)`
call only when that lookbehind is the entire pattern; put any other node in the sequence and upstream
fails where this port matches. Minimised to
`regex.compile(r'(?(DEFINE)(?<a>a))(?<=(?&a))c').match('ac', pos=1)` - `None` upstream, `(1, 2)`
here. Two internal inconsistencies say the defect is upstream's: its own `search` finds `(2, 3)` on
`r'(?(DEFINE)(?<ab>ab))(?<=(?&ab))c'` / `'abcd'` while `match(pos=2)` on the same compiled pattern
returns `None`; and with the tail made optional (`c?`, `c*`) upstream matches the very `c` it refuses
for `c`, `c+`, `[c]` and `(?:c)`. Ruled out: our parser (this port's bytecode for the pattern is
upstream's code for code, modulo one SET_UNION member's character order that the corpus recorder
sorts - and upstream's own dump carries the reversed copy of the called group, so the direction did
reach the bytecode), and the required-string prefilter (switching upstream's off leaves it `None`).
It sits next to upstream issue 614 for Phase 6's sweep. **Nothing drafted or filed upstream** - that
needs the owner's approval first. Pinned in `Gaps/Engine/GroupCallTests.cs`.

**Deliberate corner-cut.** Left recursion - `(?R)?b` on `'b'`, `(?<x>(?&x)?a)` on `'aaa'` - is
unguarded in both engines: upstream raises `MemoryError`, this port throws
`InvalidOperationException("… backtracking stack exceeded its 1GB limit")` from `ByteStack.Grow`. So
it is bounded and matches upstream's shape, but asserting it costs a 1GB allocation on every ratchet
run against a suite that finishes in eight seconds, so it is documented in `GroupCallTests.cs` and
not tested. Ceiling and upgrade path are in the comment there.

**Review.** One blind pass over the whole diff, dispatched and read inside the turn. Two findings
raised, two reproduced, two fixed - both comment-only. (1) The divergence comment claimed a zero-width
`$` after the lookbehind also fails upstream; it does not. My original probe passed `\$` through a
shell-quoted `python -c`, which reached the compiler as an escaped literal `$` and so tested a
character the subject never held. The sentence is replaced by the `c?`/`c*` versus `c`/`c+` evidence
above, which is stronger and is measured. (2) `RecursiveTests`'s class remark still said its tests
"now carry `needs:recursion`" after this slice removed every one. The reviewer also ran 5,040
differential rows of its own - 1,368 hand-built adversarial ones plus 3,000 more from the new
generator - all agreeing. **No second pass**: both fixes are comments, in files the first pass read,
and neither touches public API or tooling.

### Negative controls, re-run against the committed code and generator

Both are in `tools/controls.json`; re-run with `python tools/run-controls.py --slices S30`.

> **Control S30-A, `pop-groups restores nothing`**: in `Matcher.cs`, `PopGroups`, change
> `            state.Groups[g].Current = (int)current;` to `            _ = current;` (the stack
> still pops the same bytes, so only the restoration is lost). Wave: `recursion`, 600 rows.
> Result at seed 7: 578 agree, **22 diverge**. Re-run at seed 20260901: **20 diverge**.
>
> **Control S30-B, `group call does not clear the repeat guards`**: in `Matcher.cs`, the forward
> `case Opcode.GroupCall:` arm, delete
> ```
>                     foreach (RepeatData groupCallRepeat in state.Repeats)
>                     {
>                         groupCallRepeat.BodyGuardList.Reset();
>                         groupCallRepeat.TailGuardList.Reset();
>                     }
> ```
> and the blank line after it. Wave: `recursion`, **2400 rows**. Result at seed 7: 2390 agree,
> **10 diverge**. Seed 20260901: **5 diverge**. Seed 4242: **8 diverge**.

S30-B is the interesting one, and its history is the finding. At 600 rows it gave 0 at seed 7 and 4
at seed 20260901 - it fired at one seed and not another, which the port-slice skill says is a finding
about the generator rather than a tick. Two widenings were tried. The first (four shapes with a
repeat *inside* a called group) moved it to 5 and 2, firing at both. The second (four more) made it
*worse*, 1 and 0, by diluting the draw - so it was reverted, and that is the honest result: the
guard-clearing path is genuinely rare, about one row in three hundred, and the fix is rows rather
than shapes. At 2400 it fires at all three seeds. Anyone widening this generator must delete
`.scratch/control-waves/` and re-measure both controls; the numbers above are from a run made after
the last change to code and generator alike.
