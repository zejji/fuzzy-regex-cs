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

- [x] The tests are green and each was seen red without the fix.
- [x] Ratchet green; oracle green at three seeds with any new divergences pinned.
- [x] Ledger, DIVERGENCES, COMPARISON and the upstream draft written.
- [x] STATE.md no longer lists the defect as open; the slice moved to `done/`.

## Closing notes

Sitting notes: `docs/plan/slices/notes/S84-sittings.md`.

**What landed.** Two fixes in `REF_GROUP_FLD` and `REF_GROUP_FLD_REV`. When the group runs out with
a subject folding part-used, the rest is offered to `FuzzyMatchGroupFld`, as the literal arm does
(ledger 29, flag `SkipGroupFoldLeftovers`). A retried edit re-entering the arm first steps past a
folding the edit used up, as the loop body does after a first try (ledger 30, flag
`SkipRetriedFoldSteps`). A deletion guard in `NextFuzzyMatchGroupFld` stops a deletion once the
group's folding has nothing left, so free deletions cannot loop for ever. Tests:
`Gaps/Engine/FullFoldBackreferenceLeftoversTests.cs`, 15 cases, each seen red. Upstream draft: `docs/plan/upstream-reports/entry-29-30-full-fold-backreference.md`, not
filed. Probe: `tools/probes/s84-full-fold-backreference.py`.

**Surprising.** The spec's defect moved no oracle row; the retry defect, which it did not name,
explains all six. S83's BESTMATCH case (scope item 4) was the retry defect. Scope item 1's
deletion-only case is a different defect shared with the literal arm, so it is S85, not this slice;
the notes give the evidence.

**For the next slice.** The generators never reach the leftovers defect and reach the retry defect
on at most four rows per seed; S85 is asked to widen them.

**Controls**, final re-runs against the committed code.

> Control B, the retry ablation on the oracle: in `src/FuzzyRegex/Engine/PatternObject.cs`, change
> `    internal bool SkipRetriedFoldSteps;` to `    internal bool SkipRetriedFoldSteps = true;`.
> Run: `pwsh -File tools/run-oracle.ps1 -Seeds 7,4242,20260922,31337`, default generators, 6680
> rows per seed. Result: no row is EXPECTED as `full-fold-backreference-retry` at any seed. Agree
> rises from 6634, 6636, 6656 and 6647 to 6637, 6637, 6657 and 6647; seed 7 row 6250 falls back to
> `full-fold-fuzzy-deletion` (S83's entry), so 4, 1, 1 and 0 rows moved, every row the entry claims.
> Diverge is 0 throughout. The run still ends RED, on purpose: the harness self-test
> `A_row_the_fold_fix_does_not_explain_is_not_accounted_for(full-fold-backreference-retry)` fails
> because the entry's example row now agrees with upstream. Seed 31337 is the unused seed, and the control does not fire there: the
> generators reach the retry defect on no row at that seed.

> Control L, the leftovers ablation on the unit tests: change
> `    internal bool SkipGroupFoldLeftovers;` to `    internal bool SkipGroupFoldLeftovers = true;`.
> Run: `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/FullFoldBackreferenceLeftoversTests/*"`.
> Result: 7 of 15 fail (the leftovers cases). It has no oracle form: no wave row at any of the
> four seeds reaches the defect.

> Control R, the retry ablation on the unit tests: `SkipRetriedFoldSteps = true` as in Control B,
> same command. Result: 2 of 15 fail, `A_retried_insertion_steps_past_the_inserted_character` and
> `A_retried_deletion_steps_past_the_deleted_group_character`.

> Control A, the retry entry's predicate: in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`,
> entry `full-fold-backreference-retry`, change the end of its `Applies` from
> ```
>                     OracleComparer.RunWithoutTheRetriedFoldSteps(row, withoutTheFoldFix: true)
>                 )
>         ),
> ```
> to
> ```
>                     OracleComparer.RunWithoutTheRetriedFoldSteps(row, withoutTheFoldFix: true)
>                 )
>                 || row.Pattern.Contains(@"\1", StringComparison.Ordinal)
>         ),
> ```
> Run: `pwsh -File tools/run-oracle.ps1 -SkipRecord` over the default wave recorded at seed 31337
> (6680 rows). Result: the oracle tests give 3 failed, 29 passed; the failures are the three cases
> of `A_row_the_fold_fix_does_not_explain_is_not_accounted_for`, because every full-fold entry's
> example rows now fall to the widened entry first or through it. The control runs over fixed
> example rows, so the seed does not change it.

Controls B, L and R above were re-run after the last code change (the deletion guard); the numbers
above are those runs. The six deletion-guard cases fail without the guard, which has no flag.

**Review.** Three blind passes, per `docs/VERIFICATION.md`. Pass 1 raised two findings; both
reproduced, one was real (the leftovers loop never ended when deletions were free) and was fixed;
the other was upstream's own literal behaviour. Pass 2, over the fix, raised two findings, both
reproduced and both real: the fix missed the retry path, and a deletion of nothing counted
towards a `1<=d` minimum. The guard moved into `NextFuzzyMatchGroupFld`, with five new test cases
seen red. Pass 3, over that delta, found no code defect and one false claim in the S85 file, which
was corrected; it also found a pre-existing stack exhaustion outside this slice, recorded in
STATE.md. The independent verifier, a fresh agent, re-ran the judged claims from the working
tree: the upstream values in the ledger and draft, the six rows against their written-out form,
the oracle at three seeds, the suite, and Control B. All five came back CONFIRMED.
