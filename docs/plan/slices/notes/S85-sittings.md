# S85 sitting notes

## Sitting 1, 2026-09-23

### The mechanism (scope item 1)

All four full-fold arms compare the pattern against the subject one folded character at a time.
`STRING_FLD` moves `text_pos` on only when it has used up the current subject folding
(`upstream/src/_regex.c:14834`, `:14847`). When the pattern runs out part-way through a folding, the
leftovers loop at `:14856` (reversed `:14962`) asks for a fuzzy edit until the folding is used up.

The only edit it can get with deletions alone is `RE_FUZZY_DEL` in `next_fuzzy_match_string_fld`
(`:10590`). That moves the pattern position (`new_string_pos += step`) and nothing else. In the
leftovers the pattern is already used up, so the deletion deletes nothing: it charges one edit and
leaves `folded_pos` where it was. Two results follow.

- `(?:sss){d<=1}` over 'ßß': 'ss' matches the first ß, the third s matches the first half of the
  second ß, and the leftovers loop spends the one deletion without moving. The match at 0 fails, and
  the search finds (1, 2) instead. The port gave the same.
- With free deletions (`{0d+1s+1i<=1:[x]}`) the loop never ends: upstream raises MemoryError and the
  port overflowed its backtracking stack.

Why 'ß' matches and 'ßx' does not: over 'ß' the third s meets the end of the slice, where
`folded_len` is 0 (`:14824`). The mismatch goes to the fuzzy arm, the deletion skips the s, and the
loop ends with nothing left. Over 'ßx' the deletion happens the same way, but the leftovers loop then
sees 'x' as a folding with `folded_pos 0 < folded_len 1` and charges it. That is S83's untouched
folding, which the port already answered as (0, 1) since S83. It is not part of this defect.

`REF_GROUP_FLD` (`:14060`) has no leftovers loop. It backtracks at `:14154` when either folding is
half-used. S84 added the loop to the port's backreference arms and refused a deletion there. S85
replaces that refusal with the same take-back.

### The fix

In the leftovers loop, a deletion takes back the last comparison into the half-used folding
(`NewFoldedPos -= step`). Repeated, it walks the folding back to its start, so the item ends before
the character and each comparison it gave back costs one deletion. That makes 'ßß' match at 0 with
D[1], and ends the free-deletion loop, because each take-back moves.

A guard refuses the take-back when an insertion or substitution was made in the same folding, that
is, when any `FuzzyChange` the item recorded at `state.TextPos` is not a deletion (the review's
finding below narrowed it from every trailing change there). Without it, the port gave
answers with a substitution and a deletion at the same position. A 3000-row differential at seed 85
showed the guard changes 8 rows, all of them backreference rows. `A_folding_an_edit_was_made_in_is_not_taken_back`
pins it, with expected values from the literal forms cut at the boundary.

Differential, 3000 rows, seed 85: all 84 port-upstream differences are explained by the fold-fix
ablations. The S85 fix changes 19 rows: the free-deletion MemoryError rows, and new, cheaper or
earlier matches such as `(?:xfff){i<=1,d<=2}` over 'ﬀﬃfi' at (0, 1) where upstream gives (1, 3).

### The generator (scope item 5)

`fuzzy-overhang` in `tools/record-oracle.py`: `fuzzy` with a full-folded backreference whose group
(from `FUZZY_OVERHANG_GROUPS`) ends part-way into a subject folding from `FUZZY_OVERHANG_CHARACTERS`.
It adds no random draws to the other generators. At 300 rows it gives 61, 50 and 47 rows accounted
for by the S83, S84 and S85 entries at seeds 7, 4242 and 20260923.

Six rows at first matched no entry. Five need both S84 repairs off at once, and one also needs
S83's. The `full-fold-backreference-retry` entry now accepts those combinations.

### New finding: BESTMATCH and ENHANCEMATCH over a full-folded backreference

The sixth row, seed 20260923 row 67, `(?b)(?fi)(f)(?:\d+a00(?:\1)){e<=3}` fullmatch over
'f767ax00ﬂ', matches no ablation. Minimal form, measured 2026-09-23 on regex 2026.9.10:

```
(?b)(?fi)(f)(?:(?:\1)){e<=3}  fullmatch 'fxf'   upstream None
(?e)(?fi)(f)(?:(?:\1)){e<=3}  fullmatch 'fxf'   upstream (0, 3) S[1] I[2]
(?fi)(f)(?:(?:\1)){e<=2}      fullmatch 'fxf'   upstream (0, 3) S[1] I[2]
(?b)(?fi)(f)(?:f){e<=3}       fullmatch 'fxf'   upstream (0, 3) I[1]
```

BESTMATCH finds no match where plain matching finds one. The port at 95318dd (before S83), and the
live port with every fold-fix flag set, give (0, 3) S[1] I[2]. The live port gives (0, 3) I[1],
the literal form's answer. The difference needs a full-folded backreference and a cap set through
`max_errors` (`do_best_fuzzy_match` `:17736`, the enhanced loop `:17978`). A `{e<=2}` constraint
does not show it. So the ablations reproduce upstream without a cap and miss something upstream
does with one. The mechanism is not yet found. It is older than S83. On the two rows checked (row 67 and 'fxf') the
shipped answer agrees with the literal form. `fuzzy-overhang` stays off the default wave
until a slice explains it. Probes: `.scratch/s85min*.py` in this sitting, not kept.

### Entry order

Two older entries claimed S85's rows for the wrong reason, so `full-fold-leftover-take-back` sits
before both.

- `full-fold-fuzzy-deletion` (S83): its ablation undoes S85 too, because the take-back runs inside
  the loop whose condition is S83's `FoldingIsPartUsed`.
- `fuzzy-changes-leaked-from-an-abandoned-attempt` (S47): its control asks upstream
  `match(pos=start, endpos=end)`, and the `endpos` cut removes the folding. Row 81 of
  `fuzzy-overhang` at seed 20260923, `(?fi)(f)(?:b(?:\1)){e<=3,1i+1d+2s<=3}` over 'fSﬄ': upstream
  search and `match(s, 0)` give S[1] D[1], `match(s, 0, 2)` gives S[1] D[2], which is the port's
  answer. Nothing leaked. The row is now an S85 example row, so a reorder turns the staleness check
  red.

One of the example rows first written for S85, `(?fi)(s)(?:\1){s<=1,d<=1}` over 'sﬀ', is not
S85's. Only S83's ablation reproduces upstream's None on it, so it was dropped from S85's rows.
It stays in `A_folding_an_edit_was_made_in_is_not_taken_back` as a guard case.

### Oracle

Default wave, three seeds: 0 diverge at 7 and 4242. At 20260923 two rows diverge, row 3752 (a
`(?b)\b\K` split that times out) and row 5185 (a `(*SKIP)` partial slice). Both are present at
8dd746e, before S85, and the orchestrator is triaging them apart from this slice (its message of
01:37). They also account for the `The_lazy_walks_answer_exactly_what_the_eager_ones_do` failure.

`fuzzy-overhang`, seeds 7, 4242, 20260923 and 31337: diverge 0, 0, 1 (row 67, the BESTMATCH
finding above) and 0. Rows accounted for by S85's entry: 10, 6, 9 and 9.

### Blind review, pass 1

Three findings; one real.

1. Reproduced and fixed. A lookaround's edit at the item's starting position stopped the take-back:
   `(?fi)(s)(?=(?:x){s<=1})(?:\1){d<=1}` over 'sß' gave None where the literal form gives (0, 1)
   S[1] D[1]. The guard scanned every trailing change at `text_pos`, and the lookahead's
   substitution is recorded there before the item starts. The fix records the change count when a
   full-folded item begins (`foldChangesStart`, pushed below each fuzzy frame of the four arms, and
   restored by the retries) and scans only from there.
   `An_edit_made_before_the_item_does_not_stop_a_take_back` pins four cases, all four seen red.
2. Not a defect: the ratchet was red on the two S84 cases this slice moved out of
   `FullFoldBackreferenceLeftoversTests`, a deliberate move that `-AcceptRemovals` records.
3. Not a defect: STATE.md was drafted ahead of the slice move.

### Blind review, pass 2, and the verifier

Pass 2 covered only the `foldChangesStart` delta and returned "No defects found", with 6000
randomised backreference-against-literal pairs agreeing. The independent verifier re-ran, from the
files, every judged value: the probe rows, the ledger and draft figures, the test comments, the
COMPARISON example and the `fuzzy-overhang` counts. All 8 claims came back CONFIRMED on regex
2026.9.10.

### Controls

Run by `.scratch/controls.py` (not kept). Each flips one snippet, runs the command and restores the
file. The figures below are the final runs, against the committed code, after the review's fix;
every oracle figure matched the earlier run.

> Control T, the take-back ablation on the oracle: in `src/FuzzyRegex/Engine/PatternObject.cs`,
> change `    internal bool SkipLeftoverTakeBack;` to
> `    internal bool SkipLeftoverTakeBack = true;`. Run:
> `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-overhang -Seeds 7,4242,20260923,31337`, 300 rows
> per seed. Result: no row is EXPECTED as `full-fold-leftover-take-back` at any seed; agree rises
> from 237, 247, 252 and 262 to 247, 253, 261 and 271, every row the entry claimed. The run ends RED
> on purpose: `A_row_the_fold_fix_does_not_explain_is_not_accounted_for(full-fold-leftover-take-back)`
> and `Every_expected_divergence_still_diverges` fail at every seed. Seed 31337 is the unused seed.

> Control Tu, the same flag on the unit tests. Run:
> `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/FullFoldDeletionAtFoldingBoundaryTests/*"`.
> Result: 19 of 24 fail: the 15 cases written before the review and the 4 lookaround cases.

> Control G, the guard: in `src/FuzzyRegex/Engine/Matcher.cs`, `TakeBackFoldedComparison`, change
> `            if (changes[i].Type != FuzzyValue.Del)` to `            if (changes[i].Type < 0)`.
> Unit command as Control Tu: 3 of 24 fail, the three cases of
> `A_folding_an_edit_was_made_in_is_not_taken_back`. On the oracle (as Control T) it changes no row
> at any of the four seeds: the guard has no generated row yet.

The ablation is not upstream on one path. With `SkipLeftoverTakeBack` set,
`(?i)(?:sss){0d+1s<=1}x` over 'ßßy' runs for 23 s and fails the test (the free-deletion loop on
the retry path), where upstream V1 gives None at once (measured 2026-09-23). A generated row
reaching that path would be reported as a divergence rather than classified, so the gap cannot hide
a defect.

> Control B (S84's), `    internal bool SkipRetriedFoldSteps;` to
> `    internal bool SkipRetriedFoldSteps = true;` in `PatternObject.cs`, oracle as Control T. Result:
> `full-fold-backreference-retry` rows fall from 6, 7, 3 and 3 to 1, 0, 0 and 0; agree 242, 249, 256
> and 265. The one row left at seed 7 is one the entry keys on with the leftovers flag off as well.

> Control L (S84's), `    internal bool SkipGroupFoldLeftovers;` to
> `    internal bool SkipGroupFoldLeftovers = true;`, oracle as Control T. Result:
> `full-fold-backreference-leftovers` rows fall from 31, 27, 28 and 17 to 0 at every seed, and the
> take-back rows go with them (every take-back row of this generator is a backreference row, and the
> take-back runs inside S84's loop); agree 278, 280, 289 and 288. Before this generator S84's
> leftovers defect had no generated row at all.
