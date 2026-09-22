---
slice: S57f
phase: 6
title: The ten 6000-row gate rows the seed of 2026-09-22 drew
delivers: []
---

# S57f - ten gate rows from a new date seed

The 6000-row gate's third seed is today's date, so every day it asks about six thousand rows a
generator that nobody has asked about before. On 2026-09-22 it drew ten divergences, in four
generators, and none of them is the family S57e was judging. S57e measured that rather than assumed
it: running the same gate at the same seed with S57e's own generator change reverted - the
pre-S57e list, `fuzzy-anchored` removed - leaves exactly these ten rows and no others.

```
pwsh -File tools/run-oracle.ps1 -Count 6000 -Seeds 20260922 -Generator literals,literal-dot,anchors,classes,groups,quantifiers,boundaries,backrefs,case-folding,reverse,substitution,iteration,interactions,lookaround,conditionals,recursion,partial,partial-sliced,posix,verbs,fuzzy,timeout
agree 125520  unsupported 0  expected 488  timeout 3  resource 59  diverge 10  of 126080 rows
```

The rows, from `TestResults/oracle/report-20260922.txt`:

| Row | Generator | Operation | What differs |
| --- | --- | --- | --- |
| 73420 | `interactions` | `sub` | upstream replaces nothing, this port replaces once |
| 74120 | `interactions` | `split` | two parts against one, on a Turkic-folding subject |
| 74222 | `interactions` | `finditer` | five matches each, one deletion pair recorded two positions later |
| 74554 | `interactions` | `match` | different partial span, a deletion against a substitution |
| 76118 | `interactions` | `finditer` | upstream charges one substitution, this port charges none |
| 76484 | `interactions` | `sub` | two replacements against none, Turkic folding again |
| 77887 | `interactions` | `finditer-overlapped` | upstream returns six matches, this port five |
| 97332 | `partial` | `fullmatch` | upstream answers a partial, this port answers none |
| 103000 | `partial-sliced` | `search` | different partial span |
| 116428 | `verbs` | `sub` | one replacement each, different text |

Several look like one mechanism seen twice - the two Turkic `sub` rows, the two partial-span rows -
so judge them in groups rather than one at a time. S52's lesson applies: run the ablation before
reasoning from a row's shape.

## Scope

1. **Judge all ten to amendment 16's standard**, in groups where they share a mechanism, with the
   probe or ablation that names the cause, the blind review and one independent verifier over the
   batch.
2. **Fix the port or pin each divergence**, with a permanent minimised test carrying its
   provenance and a `docs/DIVERGENCES.md` row for each deliberate difference.
3. **Nothing else.** A row a later date seed draws belongs to whatever finds it.

## Verification

- `pwsh -File tools/run-oracle.ps1 -Count 6000` GREEN at three seeds.
- `pwsh -File tools/run-oracle.ps1` GREEN at three seeds.
- `pwsh -File tools/check-ratchet.ps1` GREEN.

## Done when

- [ ] All ten judged, with the mechanism and the verifier's CONFIRMED lines.
- [ ] Each fixed or pinned, with a permanent test and a `DIVERGENCES.md` row where it is a pin.
- [ ] The gate green at three seeds; blind review; commit.
