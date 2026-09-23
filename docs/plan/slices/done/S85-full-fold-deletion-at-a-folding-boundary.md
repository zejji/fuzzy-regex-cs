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

- [x] Tests green, each seen red without the fix (two are pins by design; see the closing notes).
- [x] Ratchet green; oracle green at three seeds with any new divergences pinned (seed 20260923
  counted green on the orchestrator's ruling: its only rows, 3752 and 5185, predate S85).
- [x] Ledger, DIVERGENCES, COMPARISON and the upstream draft written.
- [x] STATE.md no longer lists the defect; slice moved to `done/`.

## Closing notes

Sitting notes: `docs/plan/slices/notes/S85-sittings.md`, which hold the mechanism, the entry
order, the oracle figures, the review and the controls with their snippets.

**What landed.** In a full-folded item's leftovers, a fuzzy deletion now takes back the last
comparison into the subject folding (`Matcher.TakeBackFoldedComparison`, ledger 31, flag
`SkipLeftoverTakeBack`), in `NextFuzzyMatchStringFld` and `NextFuzzyMatchGroupFld` alike, so an
item can end before a half-used character and the free-deletion loop ends. It replaces S84's
deletion refusal in the backreference arms. A guard refuses the take-back after an insertion or
substitution the item made in the same folding; the item's starting change count
(`FoldChangesStart`, carried on each fuzzy frame) keeps an earlier lookaround's edit out of it.
Tests: `Gaps/Engine/FullFoldDeletionAtFoldingBoundaryTests.cs`, 24 cases. With the take-back off
19 fail, among them the 4 lookaround cases, which were also seen red before their own fix. With the
guard loosened 3 more fail. The other two pass with the fix off and are pins by design: the
reversed retry row, and the insertion that is still preferred to a take-back. Upstream draft:
`docs/plan/upstream-reports/entry-31-full-fold-leftover-deletion.md`, not filed. Probe:
`tools/probes/s85-leftover-take-back.py`. Generator: `fuzzy-overhang`.

**Surprising.** Upstream's 'ß' row matches only because the folding ends the slice; 'ßx' is S83's
defect, not this one. Two older oracle entries claimed S85's rows for the wrong reason: S83's,
whose ablation undoes this fix too, and S47's leaked-changes entry, whose `endpos` control cuts off
the folding. The new generator found a BESTMATCH difference no ablation explains, older than S83.
A fuzzy change's position does not say which item made it: a lookaround's edit lands on the same
position as the item that follows it.

**For the next slice.** `fuzzy-overhang` joins the default wave once a slice explains that row
(STATE.md, finding 1). The guard has no generated row yet.

**Review.** Two blind passes. Pass 1 raised three findings; one reproduced and was fixed (a
lookaround's substitution at the item's start stopped the take-back, both arms, both directions),
and two were expected states of an unfinished slice (the ratchet before `-AcceptRemovals`, STATE.md
drafted ahead of the move). Pass 2 covered only that fix and returned "No defects found", with
6000 randomised backreference-against-literal pairs agreeing. The independent verifier re-ran the
judged values, and all 8 claims came back CONFIRMED on regex 2026.9.10.

**Controls**: the snippets, commands and generator are in the sitting notes, "Controls". The final
runs used the committed code, `fuzzy-overhang` at 300 rows per seed, and seeds 7, 4242, 20260923
and 31337 (31337 unused elsewhere). T: agree 247, 253, 261 and 271 against live 237, 247, 252 and
262, RED at every seed. Tu: 19 of 24 fail. G: no oracle row changes. Gu: 3 of 24 fail. B:
retry rows fall from 6, 7, 3 and 3 to 1, 0, 0 and 0. L: leftovers rows fall to 0 at every seed.

**Oracle.** Default wave green at 7 (diverge 0 of 6680) and 4242 (0 of 6680). At 20260923 only
rows 3752 and 5185 diverge. Both are older than S85, so the orchestrator counts that seed as green.
