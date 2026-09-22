---
slice: S85
phase: 7
title: A fuzzy deletion that would end a match half-way through a folding
delivers: []
---

# S85 - Deletions at a folding boundary

S84 found this while testing its scope item 1 and moved the case here, because the cause is not in
the backreference arms S84 fixed and it affects the literal arm too.

## The defect

Under full case folding a subject `ß` folds to `ss`. When a pattern with only deletions allowed
needs just one of those two `s` characters, the match cannot stop half-way through the `ß`, and a
deletion is the only way out. Fuzzy edits are tried only on a mismatch, and at the end of the
group or literal there is none, so the engine backtracks.

Verified 2026-09-22 against `regex` 2026.9.10, `regex.I | regex.F | regex.V1` (V0 with `regex.F`
gives the same answers):

```
(?:sss){d<=1}      over 'ß'     -> (0, 1), counts (0, 0, 1)
(?:sss){d<=1}      over 'ßx'    -> None
(?:sss){d<=1}      over 'ßß'    -> (1, 2), counts (0, 0, 1)
(s)(?:s){d<=1}     over 'sß'    -> (0, 1), counts (0, 0, 1)
(s)(?:\1){d<=1}    over 'sß'    -> None
(s)(?:\1){d<=1}    over 'sßs'   -> (2, 3), counts (0, 0, 1)
(as)(?:\1){d<=1}   over 'asaß'  -> None
(?r)(?:\1){d<=1}(s) over 'ßs'   -> None
```

The first three rows show the literal arm: appending a character after a one-edit match makes the
match disappear, and `ßß` reports the second `ß` when the first gives the same one-deletion match.
The last four show the backreference arm failing where the literal (row four) succeeds. The port
gives upstream's answer on every row but one (S84's scratch probe, 2026-09-22): over `'ßx'` it
already gives (0, 1) with one deletion, the answer the `'ß'` row suggests is right, where upstream
gives None. Nothing pins that difference and the oracle has no row for it, so explaining it is part
of scope item 1. The rest is an inherited bug, and the owner's rule of 2026-09-12 says it is fixed
before 1.0.

The S84 slice spec quoted `(s)(?:\1){d<=1}` over `sß` as `(0,1)` under V0. That was V0's simple
folding, where `ß` does not fold to `ss`; it is not evidence for the full-folding answer.

A second deletion defect sits in the literal arm's leftovers loop (`_regex.c:14856`). A deletion
there deletes nothing and leaves the folding as it was, so when deletions are free the loop never
ends: `regex.search(r'(?:sss){0d+1s+1i<=1:[x]}', 'ßß', regex.I | regex.V1)` raises MemoryError,
and the port throws "backtracking stack exceeded its 1GB limit" (both measured 2026-09-22). S84's
blind review found it. S84 fixed its own backreference leftovers loops: `NextFuzzyMatchGroupFld`
offers a deletion only while the group has a folded character left, which also covers the retry
path (a check inside the loop alone did not: `(s)(?:\1){0d+1s<=1}x` over `sßy` still looped). It
left the literal arms to this slice.

## Scope

1. **Establish the mechanism** in `upstream/src/_regex.c` before writing code: why `'ß'` succeeds
   and `'ßx'` fails for `(?:sss){d<=1}`, citing the lines. Check `STRING_FLD`, `STRING_FLD_REV`,
   `REF_GROUP_FLD` and `REF_GROUP_FLD_REV`, and the end-of-text path that lets the `'ß'` row match.
2. **Tests first, each seen red**, beside `FullFoldBackreferenceLeftoversTests.cs`: every row
   above, forward and reversed, with each expected value confirmed by counting edits. Pin S83's and
   S84's tests unchanged.
3. **The fix**, consistent across the four arms, including the endless loop in `STRING_FLD` and
   `STRING_FLD_REV` (S84's guard, moved to `NextFuzzyMatchStringFld` and tested on the retry path
too, is the obvious candidate), with an ablation flag in `PatternObject` in the
   style of `SkipGroupFoldLeftovers`.
4. **Ledger, DIVERGENCES, COMPARISON, `ExpectedDivergences.cs`, upstream draft** as S84 did; oracle
   at three seeds; blind review of the "upstream is wrong" call. File nothing.
5. **Widen the generator.** No wave row reaches S84's leftovers defect, and its retry defect
   appears on at most four rows of 6680 per seed (none at seed 31337). Teach the backreference or
   fuzzy generator to build a fuzzy full-folded backreference whose group ends inside a subject
   folding, so S84's and this slice's controls fire on generated rows.

## Done when

- [ ] Tests green, each seen red without the fix.
- [ ] Ratchet green; oracle green at three seeds with any new divergences pinned.
- [ ] Ledger, DIVERGENCES, COMPARISON and the upstream draft written.
- [ ] STATE.md no longer lists the defect; slice moved to `done/`.
