---
slice: S84
phase: 7
title: A full-folded backreference that ends half-way through a folding tries a fuzzy edit
delivers: []
---

# S84 - The other half of S83

S83's blind review found this and S83 recorded it without fixing it (its sitting notes, "Out of
scope, noted"; `docs/plan/STATE.md`). No known bug ships (the owner's rule of 2026-09-12), and the
NuGet listing follows tonight's run, so it is fixed now, ahead of the optimisation slices.

## The defect

Under full case folding, a backreference whose group text matches only part of a subject
character's folding - group `s` against `ß`, which folds to `ss` - reaches the leftovers check with
the folding part-used, fails it, and backtracks without trying a fuzzy edit. S83's
`Matcher.FoldingIsPartUsed` covers the opposite case (a folding NOT used at all), so it leaves this
one alone.

Verified 2026-09-22 against `regex` 2026.9.10:

```
regex.search(r'(s)(?:\1){e<=1}', 'sß', regex.I | regex.V1)  -> None
regex.search(r'(s)(?:\1){e<=1}', 'sß', regex.I)             -> (0, 2), counts (1, 0, 0)
regex.search(r's(?:s){e<=1}',    'sß', regex.I | regex.V1)  -> (0, 2), counts (1, 0, 0)
```

The port answers None too. A literal `s` in the same place finds the match, so the backreference
arm is the odd one out: a match within the budget exists and neither engine finds it.

## Scope

1. **Tests first, each seen red**, beside S83's `FullFoldFuzzyDeletionTests.cs` (or a sibling
   file): `(s)(?:\1){e<=1}` over `sß`; `(s)(?:\1){d<=1}` over `sß` -> (0,1), one deletion; the
   reversed arm (`(?r)`); a longer group (e.g. `(as)(?:\1){e<=1}` over `asaß`); and pins that S83's
   tests and the ligature controls stay as they are. Confirm each expected value by counting edits
   and against the literal-pattern form and V0; do not copy them from this file unchecked.
2. **The fix** in the `REF_GROUP_FLD` and `REF_GROUP_FLD_REV` arms of `Matcher.cs` (find them by
   name; S83 changed their subject side): when the group text runs out with a folding part-used,
   the remaining folded characters must be offered to the fuzzy machinery - as the literal
   `STRING_FLD` arm does - rather than failing the path. Compare with how the literal arm reaches
   the same state and handles it, and keep the two consistent. If upstream's C for the two arms
   differs in a way that explains it, cite it.
3. **Ablation and ledger**: extend `PatternObject.ChargeUntouchedFoldings`' pattern (or add a
   sibling flag) so the oracle can show the fix is the whole divergence; new LEDGER entry,
   `docs/DIVERGENCES.md`, `docs/COMPARISON.md` beside S83's section, `ExpectedDivergences.cs`
   entries; oracle at three seeds with the backreference and case-folding generators. The "upstream
   is wrong" call gets a blind review first, given the repros and the edit counts, not this file's
   verdict. Draft the upstream report in `docs/plan/upstream-reports/`; file nothing.
4. **Investigate, and fix or record with evidence**: S83's notes also saw upstream's BESTMATCH
   return counts (1, 0, 1) for `(?b)(?fi)(ßa)(?:\1){s<=1,i<=1,d<=1}` over `ßasa`, where one
   deletion may be the fewest edits. First establish what BESTMATCH promises (upstream README) and
   whether a one-edit match of that span or any span exists; test it with a literal in place of the
   backreference. If it is this slice's defect, the fix covers it; if it is a separate engine bug,
   write it up in the sitting notes with its minimal repro and add a slice file for it; if
   BESTMATCH does not promise the minimum here, say so with the README line and pin it.

## Done when

The tests are green and each was seen red without the fix; ratchet green; oracle green at three
seeds with any new divergences pinned; ledger, DIVERGENCES, COMPARISON and the upstream draft
written; STATE.md no longer lists the defect as open; the slice moved to `done/`.
