# S89 sittings

Upstream is `regex` 2026.9.10. "Port before" is main at 0ab00eb; S89 changes no library behaviour,
so "port now" is the same answer and the column records what explains it. Spans are UTF-16 for the
port and codepoints for upstream where the subject is astral.

## Findings, three answers each

| Finding | Row | Upstream | Port before = now | Verdict |
|---|---|---|---|---|
| 1 | `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch `fxf` | None | (0, 3), i:1 | Ledger 30 plus ledger 12. Both ablations together give None. Row 10 of `full-fold-backreference-retry`. |
| 1 | `fuzzy-overhang` row 67, seed 20260923 | (0, 9) (0, 2, 1) i:5,8 d:9 | (0, 9) (1, 1, 0) s:8 i:5 | Ledger 29 plus ledger 12. Both ablations together give upstream's answer. Row 5 of `full-fold-backreference-leftovers`. |
| 3 | `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` over `oelFin becf` | None | (0, 11), seven insertions | Ledger 12 alone. Row 29 of `bestmatch-loses-a-candidate`. |
| 3 | `(?b)(?r)(?:\L<phrases>){e<=3}` over `znz` | None | (0, 3), three insertions | Ledger 12 alone. Row 30. Minimised to `(?b)(?:){e<=3}`, row 31. |
| 4 | `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf `<>` | `<><>😀` | `<><>` | Ledger 12 alone. Row 32. |
| 5 | no row; generator | - | - | Not needed: the five `fuzzy-*` generators already put `(?b)` on ~30% of rows and `(?e)` on ~30%. See below. |
| 6 | row 4957, seed 99 | (0, 0) partial | (0, 9) partial | Port right. Row 15 of `partial-retry-reversed-slice`; `tools/probes/s89-partial-retry-reversed-row.py`. |

S87's fix (ledger 32, `KeepStaleErrorTotal`) explains none of these; setting it moved no row.

Setting the five ablation flags (`SkipRetriedFoldSteps`, `SkipGroupFoldLeftovers`,
`SkipLeftoverTakeBack`, `ChargeUntouchedFoldings`, `DoubleCountTrailingInsertions`) by reflection,
alone, in every pair and all together, `fxf` gives None only when both `SkipRetriedFoldSteps` and
`DoubleCountTrailingInsertions` are set (or all five); any other combination gives (0, 3).

"Ledger 12 alone" means: set `PatternObject.DoubleCountTrailingInsertions` (new, oracle-only) and the
port answers upstream's value exactly; upstream's own flagless answer is the port's answer.

Why the reference form differs from the literal `(?:f)` on finding 1 (the slice asked for this from
`_regex.c`): the literal `STRING_FLD` arm steps past the folded character after a fuzzy edit
(:14801). A retried edit in `REF_GROUP_FLD` goes through `retry_fuzzy_match_group_fld`, whose
success path does `goto advance` (:17302-17308) and re-enters the arm at :14060, skipping the
post-edit steps at :14145-14149. So the retried insertion is charged but compares the same
character again, which turns the one-insertion fit into a substitution plus a trailing insertion.
Under BESTMATCH the doubled guard (:15516) then refuses that insertion, and nothing is left. A
VERBOSE build of upstream traced exactly this path.

## Finding 5

S60b's reviewer kept no generator. Counting `(?b)` and `(?e)` in the rows the existing generators
draw (`pwsh -File tools/run-oracle.ps1 -Generator
'fuzzy,fuzzy-anchored,fuzzy-literal,fuzzy-alternation,fuzzy-overhang' -Count 2000 -Seeds
20260923,7,4242`, 10,000 rows a seed): `(?b)` 3028, 2863, 2940 and `(?e)` 2952, 2922, 2919. A new
generator would duplicate that. After S89 the wave reads:

    20260923  agree 9544  expected 454  diverge 2   (rows 3874, 9010)
    7         agree 9523  expected 477  diverge 0
    4242      agree 9533  expected 466  diverge 1   (row 8938)

Before S89 it also diverged on rows 3821 (finding 4) and 8067 (row 67). The three left share one
shape - a `partial=True` match over a full-folded ligature, where upstream answers a partial match
with one substitution and the port a complete one - and no ablation flag, alone, paired or all
together, reproduces upstream. Only row 3874 has `(?e)`, and it gives the same answer without it.
Row 3874 minimises to a plain literal: `(?fi)(?:ﬆxba){d<=1}` over `STba` is None upstream, and the
port answers (0, 4) with a deletion at 2 (`FullMatch`, `MatchAtStart`, `Match` and partial agree).
Handed to S90 (`docs/plan/slices/S90-partial-fold-rows.md`), with
`tools/probes/s89-partial-fold-rows.py`.

## Sitting 1 (2026-09-23)

A duplicate driver ran two sittings in this worktree at the same time and twice stashed and
re-applied the work (23:06, 23:16), then appended a wrong PARKED note to STATE.md. The work was
restored from the rescue stash by SHA and finished interactively.
