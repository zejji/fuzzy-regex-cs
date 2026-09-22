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

- [x] All ten judged, with the mechanism and the verifier's CONFIRMED lines.
- [x] Each fixed or pinned, with a permanent test and a `DIVERGENCES.md` row where it is a pin.
- [x] The gate green at three seeds; blind review; commit.

## Closing notes (2026-09-22)

**All ten rows are judged and pinned, and none of them is a port bug.** Six joined entries that
already existed - rows 73420 and 76118 under `posix-fuzzy-contradicts-its-own-flagless-answer`,
74554 under `partial-retry-carried-slice-forward`, 103000 under `search-start-partial`, 77887 under
`enhancematch-loses-a-candidate`, and 74222 under a new third arm of
`fuzzy-changes-leaked-from-an-abandoned-attempt`. Three are the Turkic default-folding family. One,
row 97332, is a new divergence with its own entry, test, `DIVERGENCES.md` row, `COMPARISON.md`
section and ledger draft.

**Row 97332 is the find.** Upstream reports a partial `fullmatch` of `(\S??)\.` over `'.a'`
although the pattern matches at most two characters and every two-character match ends in a full
stop, so no longer subject can complete it. PCRE2 10.47 refuses that partial on every subject
upstream grants it over, while its partial machinery stays reachable on the same pattern. That is
the opposite of S49's finding for upstream issue 367, and the difference is worth carrying forward:
S49's rows all put a LOOKAROUND at the truncation point, where a longer subject genuinely could
change the verdict, and this row has no assertion there at all. **A 367-family row has to be judged
on whether a completion can exist, not by citing S49.**

**Two methodological findings, both from the blind review, both worth the next sitting's attention.**

1. **An ablation that restores agreement does not name the mechanism on its own.** Row 76118's
   comment said POSIX was the flag and that `(?e)` and `(?r)` were not. Dropping either of those
   also restores agreement - but WITHOUT upstream answering anything different, so they change this
   port's answer, not upstream's. Only POSIX moves upstream. Ask which engine the ablation moved,
   not just whether the pair agreed afterwards.
2. **"Inert" has to be measured, not inferred from the shape.** Row 74554's outer `{1<=e<=2}`
   section looked like scaffolding around the verb and is not: delete it and the two engines agree.
   The `\K` and the inner `{i<=1}` section really are inert.

**The ablation batch is in the tree, so it can be re-run.** `tools/probes/s57f-ablation-rows.jsonl`
holds all 26 ablations; `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57f-ablation-rows.jsonl`
prints `agree 16  unsupported 0  expected 7  timeout 0  resource 0  diverge 3  of 26 rows` and then
RED, because rows 4, 12 and 13 are meant to keep diverging - those are the inert ablations. RED is
the correct verdict for that file, not a regression. The row-by-row table is in
`docs/plan/slices/notes/S57f-sittings.md`.

**No negative control was run.** This slice added no generator and no engine code - every change is
a pin, a test, a probe or prose - so there is nothing for a control to measure.

**Verification.** `pwsh -File tools/run-oracle.ps1 -Count 6000` GREEN at all three seeds
(7: `agree 131503 expected 504 diverge 0`; 4242: `agree 131464 expected 547 diverge 0`; 20260922
green). `pwsh -File tools/run-oracle.ps1` GREEN at all three seeds. `pwsh -File
tools/check-ratchet.ps1` GREEN, 6540 tests passing. The ten rows tally `expected 10 diverge 0`.

**Independent verifier** (spec amendment 16 limb (d), one pass over the whole batch of ten): a fresh
Opus subagent re-ran every probe and every upstream claim the ten verdicts quote, restricted to
Python and PCRE2 so it could not collide with the running gate. All three probes reproduced their
docstrings line for line. Two claims came back DIFFERENT - row 74554's "deleting the verb agrees
with `(*PRUNE)`" (it agrees with `(*SKIP)`, and answers a complete match) and row 74222's
"the anchored re-ask equals this port's list" (it does on the matches that differ, not on the first
one). Both comments were corrected against a re-run. Every port-side claim came back COULD NOT RUN,
by design: the verifier was barred from `dotnet` while the 6000-row gate held the build.

**Review.** Three blind passes, one reviewer, the same brief each time. Nine findings raised, nine
reproduced, nine fixed, none rejected - an unusually high survival rate for this repo, and all nine
were wrong claims in prose rather than wrong code. Pass 1 (six findings): the two ablation claims
the verifier had also caught in different words, upstream's `'abcz'` cost read as one substitution
where it is two insertions, "the other five matches agree" where upstream's second match is charged
an extra insertion, ledger entry 27 inserted into the middle of entry 26, and a pin id in the notes
that does not exist. Pass 2 (three findings, over the fixes plus the new
`tools/probes/s57f-posix-flag-ablations.py`, which the first pass never saw): a probe list that
still said three, two comments citing an ablation batch the notes did not actually record, and a
probe reconstructing row 73420's template as expanded characters rather than the recorded escapes.
Pass 3, over the new `s57f-ablation-rows.jsonl` and the citations: **No defects found.**
