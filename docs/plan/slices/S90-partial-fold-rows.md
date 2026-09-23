---
slice: S90
phase: 7
title: Three partial rows over a full-folded ligature are judged, then fixed or pinned
delivers: []
---

# S90 - Upstream answers a partial match where this port answers a complete one

S89 ran the five `fuzzy-*` generators at 2000 rows each, seeds 20260923, 7 and 4242:

```
pwsh -File tools/run-oracle.ps1 -Generator 'fuzzy,fuzzy-anchored,fuzzy-literal,fuzzy-alternation,fuzzy-overhang' -Count 2000 -Seeds 20260923,7,4242
```

After S89, three rows diverge, all with the same shape. Each is a `match` asked with
`partial=True` over a subject holding a full-folded ligature. Upstream answers a PARTIAL match
with one substitution. This port answers a COMPLETE match over the same span, with deletions.
None of the port's ablation flags (`SkipRetriedFoldSteps`, `SkipGroupFoldLeftovers`,
`SkipLeftoverTakeBack`, `ChargeUntouchedFoldings`, `DoubleCountTrailingInsertions`), alone, in
pairs or all together, reproduces upstream's answer. The default 300-row wave does not draw them.

| Seed, row | Pattern | Subject | Upstream (codepoints) | Port (UTF-16) |
|---|---|---|---|---|
| 20260923, 3874 (`fuzzy-anchored`) | `(?e)(?fi)\m(ﬆx)(?:(?:\1)(?:𝟮.){s<=1}){1i+1d+1s<=1}` | `ﬆxST𝟮a` | (0, 6) partial, s:4 | (0, 7), d:4 |
| 20260923, 9010 (`fuzzy-overhang`) | `(?fi)(st)(?:[^a](?:a(?:\1)){2i+1d+1s<=2}){e<=2}` | `stat` | (0, 4) partial, s:2 | (0, 4), d:2,4 |
| 4242, 8938 (`fuzzy-overhang`) | `(?fi)(?r)(?:\A(?:(?:\1)a){e<=2,s<=1}[^a]){e<=3,1i+1d+2s<=3:[a-f]}(f)` | `ﬀAf` | (0, 3) partial, s:2 | (0, 3), s:1 d:2 |

What S89 measured upstream (regex 2026.9.10, 2026-09-23,
`python tools/probes/s89-partial-fold-rows.py`):

- Rows 9010 and 8938: without `partial=True` upstream answers None. Replacing `(?:\1)` with the
  group's text, or dropping full folding (`(?i)`), gives this port's answer exactly. So upstream
  loses a complete match only in the full-folded reference form.
- Row 3874: the literal form (`(?:ﬆx)` in place of `(?:\1)`) and the form without `(?e)` give
  upstream's same partial answer and None without `partial`. The smallest literal relative,
  `(?fi)(?:ﬆxba){d<=1}` over `STba`, is None upstream, while `(?fi)(?:stxba){d<=1}` matches with
  one deletion at 2. This port answers (0, 4) with a deletion at 2 to BOTH, so it diverges from
  upstream on a plain literal: upstream's `STRING_FLD` arm cannot delete the character after a
  pattern-side ligature, and this port's can. (S89's first probe of the port got this wrong through
  a script bug; the blind review caught it.)

## Scope

1. Reproduce the three rows and the literal relatives. Say from `_regex.c` why a full-folded
   reference loses the complete match on rows 9010 and 8938 (the `REF_GROUP_FLD` arms and their
   retry, `retry_fuzzy_match_group_fld`), and why the literal `STRING_FLD` arm cannot delete the
   character after a ligature (`fuzzy_match_string_fld`).
2. Decide who is right per row, by the upstream-bug rule (docs, isolating probe, blind review).
   Start with the literal `(?fi)(?:ﬆxba){d<=1}` over `STba`: it is the smallest divergence and
   has no reference, partial flag or `(?e)` in it.
3. Tests first, each seen red. The smallest fix; a shared bug gets a ledger entry and a draft in
   `docs/plan/upstream-reports/`, nothing filed.
4. Pin what stays as ablation-keyed or judged rows. Run the wave above at its three seeds, and the
   default oracle at seeds 7, 4242 and 20260923.
