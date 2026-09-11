---
slice: S27
phase: 4
title: Lookaround - lookahead and lookbehind, positive and negative
delivers: [lookaround, lookbehind]
---

# S27 - Lookaround: lookahead and lookbehind, positive and negative

The first Phase 4 slice, and the one the rest of the phase leans on: the conditional (S28), the
verbs (S29) and the recursion (S30) tests all carry lookarounds in their patterns - 18 of the 60
recursion tests fail on the lookaround seam before they reach a group call.

## Scope

All line references are `upstream/src/_regex.c` unless marked.

- **The two opcode pairs, both halves each**: `LOOKAROUND` forward (`:13758`) and backtrack
  (`:17109`); `END_LOOKAROUND` forward (`:12918`) and backtrack (`:15650`). Both land in the two
  `default:` arms of `Matcher.BasicMatch` (`Matcher.cs:4567` advance, `:5196` backtrack, as of
  2026-09-11; the `Seam.For` throws at `:856` and `:1936` are helper switches, not the seams). `ATOMIC`/`END_ATOMIC` (S20) are the worked example of the push/pop shape: a
  lookaround is an atomic group that also restores `text_pos` and, when negative, inverts the
  verdict.
- **Lookbehind is a lookaround whose body was compiled reversed.** `build_LOOKAROUND` (`:24900`,
  ours `NodeCompiler.BuildLookaround`) already exists from S15/S16 and marks the body reversed;
  the matcher reads the node's status and steps backwards with the S23 machinery. So there is no
  separate lookbehind opcode - `needs:lookbehind` (17 tests) delivers with the same code, which
  is why both tags are on this slice.
- **What a lookaround saves and restores**: read the forward arm in full before writing anything.
  It pushes the group and repeat state the body may disturb, and the backtrack arm is where those
  come back - the S26 handover's warning applies verbatim: *the backtrack half is what an
  opcode-by-opcode port misses*, and a lookaround that restores captures on success but not on
  the backtrack path passes every simple test.
- **Captures inside a positive lookahead are visible after it** (`(?=(a))\1` is legal upstream);
  inside a negative one they are discarded. Confirm both against the oracle rather than from
  memory, and pin each as a gap test.
- **Upstream issue 614** (`docs/plan/2026-08-31-upstream-issue-triage.md`): `build_GROUP()` does
  not propagate match direction into a group *called* from a lookbehind. That is S30's
  construct, not this slice's, but if a reversed body inside `(?<=...)` steps forward, this is
  the bug to recognise. Port faithfully; Phase 6 fixes it.

## Verification

- **Un-skip** `needs:lookaround` (63 tests across 14 files) and `needs:lookbehind` (17). Read
  every skip's prose first: several name a second capability ("also needs FuzzyRegex.Matches",
  "Replace lands in S24") that has since landed, and two named-list tests were retagged to this
  slice on 2026-09-11. Stragglers retag with prose.
- **Oracle generator `lookaround`**: `(?=...)`, `(?!...)`, `(?<=...)`, `(?<!...)` wrapped round
  literals, classes, quantified atoms and capture groups; nested one inside another; a
  lookaround inside a repeat and a repeat inside a lookaround; a backreference *to* a capture made
  inside a lookahead; variable-length lookbehind (`(?<=a+)`, `(?<=a|bc)`), which upstream allows
  and .NET does not; under `(?r)`, IGNORECASE and MULTILINE; through search, match, fullmatch,
  findall and sub. Zero divergences. Negative controls: the backtrack arm not restoring
  `text_pos`; a negative lookaround keeping its captures; the lookbehind body stepping forward.
  Record all four re-run values per control (`docs/VERIFICATION.md`).
- **Add `lookaround` to `run-oracle.ps1`'s default list**, and re-run the whole default list once
  at the end.

## Done when

- [x] Both tags delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green; controls recorded with snippet, generator, rows and two seeds.
- [x] `docs/PORTMAP.md` rows for `LOOKAROUND`, `END_LOOKAROUND` (both arms each) and any helper
      they pulled in.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: state restored on success but not on
      backtrack; a lookbehind at position 0 or a lookahead at the end reading past the slice;
      `(?!)` - the empty negative lookahead - not failing), commit.

## Closing notes (2026-09-11)

**What landed.** Four cases in `Matcher.BasicMatch`, placed where upstream places them:
`LOOKAROUND` forward (`:13758`) and backtrack (`:17109`), `END_LOOKAROUND` forward (`:12918`) and
backtrack (`:15650`), plus `LookaroundStateData` (upstream `RE_LookaroundStateData`, `:409-414`)
and its field-by-field push/pop beside the other state-data helpers. No compiler or parser change
at all: `BuildLookaround` and `LookAround.Compile` were already complete from S15/S16, so the whole
slice is the matcher. 59 `[Skip]` attributes removed across 17 files, which is **80 tests: 63
`needs:lookaround` and 17 `needs:lookbehind`**, exactly the slice's estimate. No straggler needed
retagging - both tags are gone from the suite. Parity 76.5% to **80.6%**; `Lookaround` and
`Various` are now 100%.

**Lookbehind really is the same code.** Predicted by the slice file and confirmed: no opcode, no
case, no branch of its own. The direction lives in the *parser* (`LookAround.Compile` passes
`Behind` to `Subpattern.Compile`), so the body arrives already reversed and the S23 machinery steps
it. Worth knowing for S30, whose upstream issue 614 is about that flag not propagating into a
called group.

**One thing the next slice should know.** `CONDITIONAL` (`:12215`) pushes the *same*
`RE_LookaroundStateData` struct, so S28 gets `PushLookaroundStateData`/`PopLookaroundStateData` for
free. Its push is longer than this one's - `push_captures` unconditionally, then `push_repeats`,
which is not ported yet (PORTMAP's `push_int8` row lists its ten call sites, all S28's or S30's).

**A correction to this slice file's own scope note.** It says the four state-saving pushes are
guarded by `HAS_GROUPS`; only `push_captures` is. `push_fuzzy_counts` is Phase 5 and is left out
here exactly as `ATOMIC` leaves it out, so the block has the same shape at both ends.

### Review

One blind pass, Opus, over the whole diff, briefed to hunt the four failure modes in the "Done
when" box plus push/pop stack-shape mismatch, and to report only reproduced findings with the
command and its output. **It returned `NO FINDINGS` after 81 tool calls.** Nothing was raised, so
nothing was reproduced and nothing was fixed, and no second pass was needed: no code changed after
the review, so there is no unreviewed delta. The only edits after it were the final control re-run
(which changes nothing) and these notes.

Two findings did come out of the *controls* rather than the review, and both were real:

- The first version of control C mutated `subargs.Forward` in `NodeCompiler.BuildLookaround` and
  found **0 divergences in 1200 rows**. `args.Forward` turns out to be read only by the repeat
  builders (`NodeCompiler.cs:1424`, `:1432`, `:1488`, `:1495`), so it was never the lookbehind
  direction mechanism at all. Repointed at `Subpattern.Compile(Behind)` in `Parsing/Nodes.cs`,
  which is, it finds 25 and 29. A control that fires at zero is a control that measures nothing.
- The generator as first written left controls A and B at 1-2 divergences of 600. Widened twice
  (see below), which is what the figures in `_generate_lookaround`'s docstring describe.

### Negative controls

All three are in `tools/controls.json` as `S27-A/B/C`; re-run every one with
`python tools/run-controls.py --slices S27`, or check the sites resolve with `--check`. Every
figure below is from the final run, against the code and the generator in this commit.

**Control A, `S27-A` - the backtrack arm drops `text_pos`.** In `Matcher.cs`, the
`case Opcode.Lookaround:` arm of the *backtrack* switch, delete the first of these three lines:

```csharp
                    state.TextPos = lookData.TextPos;
                    state.SliceEnd = lookData.SliceEnd;
                    state.SliceStart = lookData.SliceStart;
```

Wave: `lookaround`, 600 rows, seed 7. Result: 588 agree, **12 diverge**. Re-run at seed 20260911:
**7 diverge**. It also fails 4 tests in the ported suite, one of them this slice's own gap test
`A_negative_lookahead_whose_body_consumed_before_failing_resumes_where_it_started`.

**Control B, `S27-B` - the lookaround never saves its captures.** In `Matcher.cs`, the
`case Opcode.Lookaround:` arm of the *advance* switch, change:

```csharp
                    bool lookHasGroups = (node.Status & NodeStatus.HasGroups) != 0;
```

to `bool lookHasGroups = false;`. Chosen over deleting the `PopCaptures` call because that would
desynchronise the byte stack and crash rather than answer wrongly, which is a different fault.
Wave: `lookaround`, 600 rows, seed 7. Result: 593 agree, **7 diverge**. Re-run at seed 20260911:
**6 diverge**. It also fails 6 tests in the ported suite.

**Control C, `S27-C` - the lookbehind body is compiled forwards.** In `Parsing/Nodes.cs`,
`LookAround.Compile`, change:

```csharp
            .. Subpattern.Compile(Behind),
```

to `.. Subpattern.Compile(false),`. Wave: `lookaround`, 600 rows, seed 7. Result: 575 agree,
**25 diverge**. Re-run at seed 20260911: **29 diverge**. It also fails 91 tests in the ported
suite, most of them compile-parity rows, which is expected: it changes the emitted bytecode.

**The two widenings the controls forced, and what each was worth.** Both are commented at their
tables in `tools/record-oracle.py`, with the same figures.

1. A `sequence` body kind - two or three atoms rather than one. Every other body kind is a single
   atom, and a single atom fails *without having moved*, so the body never leaves `text_pos`
   displaced and control A has nothing to detect. A at seeds 7/20260911: **1 and 2** before,
   **8 and 6** with `sequence` added, **12 and 7** at the final weight.
2. An `alt-with-look` piece - `(?:LOOK atom|atom)`, a branch leading with a lookaround beside a
   fallback that does not. Everything else reaches a lookaround once per attempt, and a construct
   visited once cannot show whether what it undoes is undone properly; this one is given up
   *within the same attempt*, which is the only route into the backtrack arm. B at seeds
   7/20260911: **1 and 1** before, **15 and 3** with it (unstable), **7 and 6** at the final
   weight, which is the weight kept - the swing, not the mean, is what the second seed is for.
