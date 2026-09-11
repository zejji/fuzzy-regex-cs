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

- [ ] Both tags delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green; controls recorded with snippet, generator, rows and two seeds.
- [ ] `docs/PORTMAP.md` rows for `LOOKAROUND`, `END_LOOKAROUND` (both arms each) and any helper
      they pulled in.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: state restored on success but not on
      backtrack; a lookbehind at position 0 or a lookahead at the end reading past the slice;
      `(?!)` - the empty negative lookahead - not failing), commit.
