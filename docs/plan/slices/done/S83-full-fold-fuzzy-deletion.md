---
slice: S83
phase: 7
title: A fuzzy deletion inside a full-case-folded string no longer charges an extra edit
delivers: []
---

# S83 - A deleted letter next to a foldable pair

Found 2026-09-22 by `ManyInputsBenchmarks`' answer check and minimised the same evening. This is a
correctness fix that jumps the Phase 7 queue: every fuzzy-phrase timing is meaningless until it
lands, and no known bug ships (DECISIONS, the owner's rule of 2026-09-12).

## The defect

Under full case folding - which IgnoreCase means by default here, because Version 1 is the default
(`docs/DIVERGENCES.md:25`) - `_fix_full_casefold` splits a literal into chunks, and a chunk that
holds a letter pair one character can fold to (fi, ff, st, ss) becomes a `STRING_FLD` item. Inside
that item's fuzzy loop, when a pattern letter fails against the next subject character, that
character's folding has already been loaded (`foldedLen` 1, `foldedPos` 0). If a deletion then
finishes the item, the "leftovers" code after the loop treats that loaded but untouched folding as a
half-matched character: it charges an extra insertion or substitution, or backtracks. A one-deletion
match costs two edits or fails.

Forward: `src/FuzzyRegex/Engine/Matcher.cs:8463` (`while (foldedPos < foldedLen)`) and `:8497`
(`if (foldedPos < foldedLen) goto backtrack`). Reversed: `:8829` and `:8863` (`foldedPos > 0`).
Upstream: `upstream/src/_regex.c:14856` and `:14874` (`RE_OP_STRING_FLD`) and the mirror in
`RE_OP_STRING_FLD_REV` (from `:14882`, leftovers near `:14962`). Line numbers as of `6a3d761`;
re-find them by the quoted code.

**Upstream has the same bug.** Verified 2026-09-22 against `regex` 2026.9.10:

```
regex.search(r'(?:fi){d<=1}', 'fe', regex.I)             -> (0, 1), counts (0, 0, 1)
regex.search(r'(?:fi){d<=1}', 'fe', regex.I | regex.F)   -> None
regex.search(r'(?:fi){d<=1}', 'fe', regex.I | regex.V1)  -> None
regex.search(r'(?:copper field studio){e<=2}', 'COPPER FILD SUDIO HARBOUR CANVAS FALCON 1499452310', regex.I | regex.V1) -> None   (V0: (0, 17), two deletions)
```

Deleting E and T from "copper field studio" gives "copper fild sudio": two edits, within `{e<=2}`.
A "no match" where a match within the budget exists is wrong whatever upstream says. The second
symptom is the same defect: `(?:a fie){e<=2}` IgnoreCase over `x a fe` must give (1,6) with one
insertion and one deletion (upstream V0), where the port gives (2,6) - the earlier start is skipped
because the one-deletion path was charged two. README (`upstream/README.rst:590`): default fuzzy
search returns "the first match that meets the given constraints".

Not caused by S60b item 2: it reproduces identically at `95318dd`. Not a known family:
`docs/DIVERGENCES.md:72` and LEDGER entry 6 are the chunk remap in `_fix_full_casefold`, a
different defect.

## Scope

1. **Tests first, each seen red before the fix** (`tests/FuzzyRegex.Tests/Gaps/Engine/`, a new
   `FullFoldFuzzyDeletionTests.cs`). IgnoreCase with the default version unless stated:
   1. `(?:fi){d<=1}` over `fe` -> (0,1), counts (0,0,1).
   2. `(?fi)(?:fie){d<=1}` over `fe` -> (0,2).
   3. `(?fi)(?:sta){d<=1}` over `sa` -> (0,2).
   4. `(?rfi)(?:fi){d<=1}` over `ei` -> (1,2) - the reversed arm.
   5. `(?:a fie){e<=2}` over `x a fe` -> (1,6), one insertion and one deletion.
   6. `(?b)(?:afie){e<=2}` over ` afe` -> counts (0,0,1); the same under `(?e)`.
   7. The two benchmark rows: `(?:copper field studio){e<=2}` over
      `COPPER FILD SUDIO HARBOUR CANVAS FALCON 1499452310` -> (0,17); and over
      `FALCON FALCON FALCON COPPER FELD STUDIO 59286606` -> (20,39).
   8. Pins the fix must not move: `(?fi)(?:fi){e<=1}` over `ﬁe` -> (0,1), counts (0,0,0);
      `(?fi)(?:sst){e<=1}` over `ßﬆ` -> (0,2), one insertion.
   Before writing each expected value, confirm it by counting edits and against upstream under
   V0 (which never builds a `STRING_FLD` item); do not copy them from this file unchecked.
2. **The fix**: apply the leftovers charge and the backtrack only when a folding is
   part-consumed - `foldedPos > 0 && foldedPos < foldedLen` - at all four sites. Tried in a scratch
   copy the evening it was found: all 106 disagreeing benchmark rows then agree with upstream V0 on
   the span, and the ligature and ß controls are unchanged. The suite was NOT run on that patch.
   Check whether `(?fi)(?:fi){d<=2}` over `fe` (today an empty match at (2,2) in both engines;
   (0,1) after the fix) is right, and pin it.
3. **Every other fold-and-fuzzy site**: grep the matcher for the same leftovers shape (the
   `STRING_FLD` arms are not the only place a folding is loaded before a fuzzy step - check the
   full-fold `CHARACTER`/`SET` arms, and any `FuzzyMatchStringFld`-style helper) and state in the
   sitting notes, per site, whether it has the defect.
4. **The ledger**: this is an upstream bug the port now deliberately fixes, so it is a pinned,
   permanent divergence. New LEDGER entry, `docs/DIVERGENCES.md` entry, and
   `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` entries for any wave row that now differs
   (run `tools/run-oracle.ps1` at three seeds with the fuzzy and case-folding generators; minimise
   every new diverging row into a test). **The "upstream is wrong" call gets a blind review before
   it enters the ledger** (memory: upstream-bug claims need research and blind review) - give the
   reviewer the repros and the edit count, not this file's verdict.
5. **The upstream report**: draft it in `docs/plan/upstream-reports/` for the owner to approve.
   Do not file anything.
6. **Real-data check**: run the port over the `usage-corpus` records for all three phrases and
   compare with upstream under `regex.I` (V0) - expect 0 disagreeing rows on span; record the
   count. Note that upstream's V1 answers are the buggy ones, so a V1 comparison will now disagree.

## Also record (for S63)

The benchmark answer check compared this port (V1 default) with Python `regex` under V0. Any
Python-side timing for the v1.0 gate must state its flags, and a fuzzy row timed on different
versions is timing different work. Add a line to S63's slice file saying so.

## Done when

- [x] The tests above are green and each was seen red without the fix.
- [x] Ratchet green.
- [x] Oracle green at three seeds with the new divergences pinned.
- [x] Ledger, DIVERGENCES and the upstream draft written.
- [x] The corpus check recorded in the sitting notes.
- [x] Slice moved to `done/`.

Sitting notes: `docs/plan/slices/notes/S83-sittings.md`.

## Closing notes (2026-09-22)

**What landed.** `Matcher.FoldingIsPartUsed` decides whether a folding still has leftovers to
charge: forward, `0 < foldedPos < foldedLen`; reversed, the mirror. It replaces upstream's test at
six sites: the `STRING_FLD` and `STRING_FLD_REV` leftovers loops and final backtracks, and the
subject side of `REF_GROUP_FLD` and `REF_GROUP_FLD_REV`. The slice named four sites; the audit
found the backreference pair had the same defect (`(fi)(?:\1){d<=1}` over 'fife'). A folding that
was part-used is still charged, so `(?fi)(?:sst){e<=1}` over 'ßﬆ' keeps its insertion.
`PatternObject.ChargeUntouchedFoldings` restores upstream's rule, and the oracle entry
`full-fold-fuzzy-deletion` keys on it (`OracleComparer.RunWithoutTheFoldFix`), after S57c's
anchor-pin precedent. Ledger entry 28, a DIVERGENCES row, a COMPARISON section and a draft report
(`docs/plan/upstream-reports/entry-28-full-fold-fuzzy-deletion.md`, not filed). S63's slice file
now says a Python timing must state its flags and version.

`(?fi)(?:fi){d<=2}` over 'fe' is (0, 1) after the fix, one deletion, where both engines gave (2, 2)
with two. That is V0's answer and the first match within the budget, so it is pinned.

**Surprises.** Wave row 6250 (seed 7) shows upstream contradicting itself: a match under `{d<=1}`
and none under the looser `{s<=1,i<=1,d<=1}`. The blind review found a second shared defect, the
opposite case: a full-folded backreference that ends half-way through a folding backtracks without
trying an edit (`(s)(?:\1){e<=1}` over 'sß' is None in both engines). It is out of scope and has no
slice yet; STATE.md raises it for the owner.

**For the next slice.** A row the fix moves can also cross another deliberate divergence. Seed
31337 row 6123 does: once the fix lets the match reach a `\B` at the end of the text, the S57d
boundary rule decides the answer. The ablation claims such a row first, which is correct, but judge
it by removing the other construct, as the sitting notes do.

**Review.** Three blind passes with one brief.
- Pass 1 raised one finding, reproduced and fixed: the new SHIPPED divergence had no COMPARISON
  heading, so `Every_SHIPPED_divergence_is_named_in_the_comparison_page` failed. Its aside, the
  backreference defect above, was reproduced and deferred.
- Pass 2, over the pass-1 fixes, raised three findings, all reproduced and fixed. LEDGER and the
  draft had swapped the roles of `_regex.c:14856` (the leftovers loop) and `:14874` (the backtrack
  check), and the probe's docstring misdescribed its output. The same swap in the oracle entry's
  Reason text was fixed with them.
- Pass 3, over that delta: "No defects found."

The independent verifier (fresh Opus) re-ran all six judged claims from the committed files: the
upstream V1 and V0 values, the port's values, the seven wave rows against V0, the three-seed
oracle, the ablation on the 11 recorded rows, and the corpus counts. All six came back CONFIRMED.

**Controls**, final re-runs against the committed code and generator.

> Control A, the entry's predicate: in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`,
> entry `full-fold-fuzzy-deletion`, change
> ```
>             Applies: static (row, ours) => OnlyTheFoldFixExplainsIt(row, ours)
> ```
> to
> ```
>             Applies: static (row, ours) =>
>                 OnlyTheFoldFixExplainsIt(row, ours) || row.Pattern.Contains("fi", StringComparison.Ordinal)
> ```
> Run: `pwsh -File tools/run-oracle.ps1 -SkipRecord` over the default wave recorded at seed 31337
> (6680 rows). Result: the oracle tests give 1 failed, 29 passed; the failure is
> `A_row_the_fold_fix_does_not_explain_is_not_accounted_for`. The control is a unit test over a
> fixed row, so the seed does not change it.

> Control B, the ablation: in `src/FuzzyRegex/Engine/PatternObject.cs`, change
> `    internal bool ChargeUntouchedFoldings;` to `    internal bool ChargeUntouchedFoldings = true;`.
> Run: `pwsh -File tools/run-oracle.ps1 -Seeds "7,4242,20260922,31337"`, default generators, 6680
> rows per seed. Result: no row is EXPECTED as `full-fold-fuzzy-deletion` at any seed, and agree
> rises from the fixed run's 6637, 6637, 6657 and 6647 to 6639, 6641, 6658 and 6650. That is 2, 4, 1
> and 3 rows, every row the entry claims. Diverge is 0 throughout; the run is RED only through the
> meta-tests, which require every entry's example rows to still diverge. Seed 31337 is the unused
> seed.
