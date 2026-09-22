# S83 sitting notes

## Sitting 1, 2026-09-22

### Per-site audit (scope item 3)

Every place `Matcher.cs` loads a full case folding, and whether the leftovers defect is there.
Line numbers are as of the S83 commit.

| Site | Defect? | Now |
|---|---|---|
| `STRING_FLD` leftovers loop and final backtrack (:8497, :8531) | Yes | `FoldingIsPartUsed(..., 1)` |
| `STRING_FLD_REV` leftovers loop and final backtrack (:8864, :8898) | Yes, mirrored: a reversed walk leaves `foldedPos == foldedLen` on an untouched folding | `FoldingIsPartUsed(..., -1)` |
| `REF_GROUP_FLD` final check (:8215) | Yes (upstream :14154): `(?i)(fi)(?:\1){d<=1}` over 'fife' failed | subject side through the helper; the group side (`gfoldedPos < gfoldedLen`) unchanged |
| `REF_GROUP_FLD_REV` final check (:8089) | Yes (upstream :14255): `(?ri)(?:\1){d<=1}(fi)` over 'eifi' failed | the same, `gfoldedPos > 0` unchanged |
| `IsStringTestPartial` (:3046, folds at :3103 and :3133) | No: not fuzzy, it only asks whether the text ran out mid-item | untouched |
| `FoldedCharAt` and the fuzzy-ext check (:4575-4601) | No: reads one folded character for a match test, keeps no leftovers | untouched |
| Full-fold `CHARACTER` and `SET` arms | Not applicable: no other arm calls `FullCaseFold`; a literal with a multi-character folding is always compiled to `STRING_FLD` | none |

### Oracle

Default wave, three seeds, fix in place: 7 rows diverged (seed 7 rows 6250 and 6587; seed 4242
rows 6047, 6106, 6392 and 6443; seed 20260922 row 6080). With the fix reverted, all 7 agree with
upstream, so the fix moved every one. Each was judged against upstream V0 with the pattern's
ligatures spelled out (`.scratch` probe, reproduced by `tools/probes/s83-full-fold-fuzzy-deletion.py`
for the minimal cases):

- 6587 (seed 7), 6047, 6106, 6443 (seed 4242), 6080 (seed 20260922): the port equals V0 exactly.
- 6392 (seed 4242): the port's (0, 2) deletes f, f and o (cost 3 of 3) and then matches `[^a-f][a-f]`
  with the sharp s and the F: valid. V0 finds a different valid match, (0, 4), through one
  substitution of the whole sharp s, which full folding cannot express.
- 6250 (seed 7): upstream contradicts itself, matching under `{d<=1}` and not under the looser
  `{s<=1,i<=1,d<=1}`. The port gives (0, 8) with counts (1, 1, 1) in UTF-16 units.

Keyed on an ablation (`OracleComparer.RunWithoutTheFoldFix`), after S57c's precedent. Final run:
GREEN at all three seeds, all 7 rows EXPECTED as `full-fold-fuzzy-deletion`; seed 7 agree 6637,
expected 37; seed 4242 agree 6637, expected 38; seed 20260922 agree 6657, expected 19.

The new control `A_row_the_fold_fix_does_not_explain_is_not_accounted_for` was seen red by
widening the entry's predicate to `OnlyTheFoldFixExplainsIt(row, ours) ||
row.Pattern.Contains("fi", StringComparison.Ordinal)`: 1 failed, 29 passed. Reverted.

An unused seed, 31337, adds three rows, all EXPECTED as `full-fold-fuzzy-deletion` and all judged
the same way (`.scratch` probe, regex 2026.9.10):

- 6200 (`split`) and 6573 (reversed `search`): the port equals V0 exactly. 6573's span is (0, 8)
  in UTF-16 units against V0's (0, 7) in code points, one deletion at 3 in both.
- 6123 (partial `fullmatch` of `(?e)(?fi)(?:ßa.[^a-f][a-f]\B){d<=2:\s}` over 'sSxd'): the port
  answers a partial (0, 4) and V0 answers None. Two things stack here. Without the `\B`, the port
  and V0 agree on (0, 4) with two deletions; that is S83's fix. The `\B` then fails at the end of the
  text, where this port answers a partial and upstream does not: the deliberate S57d divergence
  `boundary-at-the-end-of-the-text` (`a\B` over 'a' with `match(partial=True)` is None upstream and a
  partial here, fix or no fix). The ablation claims the row first because, without the fix, the
  port never reaches the `\B`.

### Real-data check (scope item 6)

`dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- usage-corpus .scratch/corpus`,
then each of the three phrases as `(?:<phrase>){e<=2}` IgnoreCase over all 100,000 records (300,000
searches), first match's span compared:

- against upstream `regex.I` (V0): **0 disagreements**; the port matched 5,107.
- against upstream `regex.I | regex.V1`: 106 (81 for "copper field studio", 25 for "violet stone
  archive"), the defect's own count, as expected.

### Out of scope, noted

**A second, shared defect, found by the blind review and reproduced (`.scratch` probe, regex
2026.9.10, 2026-09-22): a full-folded backreference that ends half-way through a folding
backtracks without trying a fuzzy edit.** `(s)(?:\1){e<=1}` over 'sß' under `regex.I | regex.V1` is
None in upstream and in this port, where `s(?:s){e<=1}` over the same subject finds (0, 2) with one
substitution and `(s)(?:\1){d<=1}` has the match (0, 1) with one deletion (V0 finds it). The group
text `s` matches half of the sharp s's `ss`, the leftover check fails, and the path is abandoned.
It is the opposite case to S83's (a folding that WAS partly used), so S83's fix leaves it as it
was. It needs its own slice and ledger entry under the no-known-bugs rule.

Upstream's `(?b)` on `(?fi)(ßa)(?:\1){s<=1,i<=1,d<=1}` over 'ßasa' returns (1, 0, 1), which is not
the fewest edits. Both engines agree, it predates S83, and it may be a separate BESTMATCH weakness.
