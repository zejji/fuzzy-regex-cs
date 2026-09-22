# S84 sitting notes

## Sitting 1, 2026-09-22

### Two defects, not one

The spec named one defect: a full-folded group that ends half-way through a subject folding
backtracks at the final check (upstream `_regex.c:14154`, reversed `:14255`) where the literal
`STRING_FLD` arm offers the rest of the folding to the fuzzy machinery (`:14855`). Fixing that
alone moved no oracle row. Six wave rows still diverged, and all six came from a second defect in
the same arm: `retry_fuzzy_match_group_fld` re-enters at `:14094` and skips the two steps the loop
body takes after an edit (`:14145` subject, `:14148` group), so a retried insertion compares the
inserted character again. It needs no special character: `(?i)(ab)(?:\1){e<=1}` over `abxab` is
None under `I | V1` and matches case-sensitively. Ledger entries 29 (leftovers) and 30 (retry).

### Judging the rows

Every port answer equals upstream's answer with `(?:\1)` replaced by the group's literal text,
which is the cheapest independent check that the port is right and upstream is wrong. Ablation
settles which fix explains each row (`.scratch/s84classify.txt` at the time):

| Seed | Row | Explained by |
|---|---|---|
| 7 | 6208, 6243, 6471 | retry flag alone |
| 7 | 6250 | S83's fold fix and the retry flag together |
| 4242 | 6114 | retry flag alone |
| 20260922 | 6591 | retry flag alone |

Row 6250 was S83's; it moved from `full-fold-fuzzy-deletion` to `full-fold-backreference-retry`,
whose predicate accepts either the retry ablation alone or both ablations together. The leftovers
flag explains no wave row: the generators never build a fuzzy full-folded backreference whose group
ends inside a subject folding. It is covered by unit tests and four minimised oracle rows only.

### S83's BESTMATCH case (scope item 4)

`(?b)(?fi)(ßa)(?:\1){s<=1,i<=1,d<=1}` over `ßasa`: upstream reports counts (1, 0, 1); the literal
form `(?:ßa)` reports (0, 0, 1). The retry defect caused it. The port now gives (0, 0, 1), pinned by
`A_retried_deletion_steps_past_the_deleted_group_character`.

### Moved out of scope: deletion-only at a folding boundary

The spec's scope item 1 asked for `(s)(?:\1){d<=1}` over `sß` giving (0, 1). Both engines give
None, with and without this fix. The spec's (0, 1) came from V0's simple folding, where `ß` does
not fold. The literal arm has the same defect (`(?:sss){d<=1}` over `ß` matches, over `ßx` does
not), so the cause sits outside the arms this slice fixed. Written up as
`docs/plan/slices/S85-full-fold-deletion-at-a-folding-boundary.md` with the probe values.

### Blind review

Three passes.

- **Pass 1** raised two findings, both reproduced.
  - The first was real. With free deletions (`{0d+1s+1i<=1:[x]}`), a deletion in the new leftovers
    loop deletes nothing, so the loop never ended: `(?i)(s)(?:\1){0d+1s+1i<=1:[x]}` over `sß`
    threw "backtracking stack exceeded its 1GB limit". Upstream's literal loop has the same flaw:
    `(?:sss){0d+1s+1i<=1:[x]}` over `ßß` raises MemoryError.
  - The second was not a defect. The port charges two substitutions for `(sb)(?:\1){s<=2}` over
    `sbsß` where upstream finds nothing. Upstream's own literal arm does the same:
    `fullmatch('(?:ßb){s<=2}', 'ßß')` gives counts (2, 0, 0). The comparison the reviewer used,
    `(?:sb)`, has no multi-character folding, so it compiles to a whole-character comparison.
- **Pass 2**, over the fix (a check in the loop that `foldedPos` moved), showed it was incomplete.
  A later item's failure retries the edit through `RetryFuzzyMatchGroupFld`, which offered the
  deletion again: `(?i)(s)(?:\1){0d+1s<=1}x` over `sßy` still looped. The same path counted a
  deletion of nothing towards a `1<=d` minimum. The guard moved to the root: `NextFuzzyMatchGroupFld`
  offers a deletion only while the group has a folded character left. Five new test cases, each
  seen red.
- **Pass 3**, over that delta, found no code defect. It compared the working tree with a copy
  lacking only the check, over 15,000 generated full-fold backreference cases, and the check
  changed only rows that the copy ran out of time or memory on, or where it counted an empty
  deletion. It found one wrong claim in the S85 file, since fixed: the port already gives (0, 1)
  for `(?:sss){d<=1}` over `ßx`, where upstream gives None.

It also found a pre-existing defect outside this slice, reproduced at HEAD:
`(?i)(x)(?:(?:\1){d<=2})+$` over `xy` exhausts the backtracking stack, where upstream V1 returns
None. Upstream raises MemoryError for the same pattern without IgnoreCase, and for `(x)(?:(?:x){d<=2})+$`
under IgnoreCase. Recorded in STATE.md for a slice.

### Oracle

Final run, committed code, `pwsh -File tools/run-oracle.ps1 -Seeds 7,4242,20260922,31337`, default
generators, 6680 rows per seed, `diverge 0` at every seed:

| Seed | agree | expected | retry rows |
|---|---|---|---|
| 7 | 6634 | 40 | 4 |
| 4242 | 6636 | 39 | 1 |
| 20260922 | 6656 | 20 | 1 |
| 31337 | 6647 | 31 | 0 |

Seed 31337 has no retry row: the generators reach this defect on about one row in 6680. That is
a thin margin, recorded rather than fixed here; S85 works in the same arms and can widen the
backreference generator while it holds it.
