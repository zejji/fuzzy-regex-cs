---
slice: S48b
phase: 6
title: The two fuzzy-reporting bugs S47 and S46 left open - the change list restored as a block wherever the counts are, and POSIX with (?e) losing an error
delivers: []
---

# S48b - Ledger 11's mechanisms C and D, and ledger 9's port-side count bug

Two known bugs are still open in this port after S48, and the owner's rule (2026-09-12, spec
amendment 16) is that none is carried to Phase 7. Both were found and characterised by earlier
slices and deferred because the fix was "a slice of its own"; this is that slice. Found by the
DIVERGENCES completeness audit on 2026-09-14, which had to invent a slice id for them; this file is
the real one.

## 1. Ledger 11, mechanisms C and D (inherited, both engines wrong)

`Match.FuzzyCounts` and `Match.FuzzyChanges` are two views of the same errors and must agree. S47
fixed mechanisms A and B. C and D remain because the fix is not local: the counts are saved and
restored as a block at eight `PushFuzzyCounts` and eleven `PopFuzzyCounts` sites in `Matcher.cs`,
while the change list is unwound one item at a time, so any abandoned sub-attempt that is not
backtracked through leaves the list ahead of the counts. Until this slice, `FuzzyCounts` is tallied
from the list only on a partial match and read from the counter otherwise (S47's holding rule).

- **Design first, test second, code third.** For each of the nineteen sites decide "restore"
  (truncate the change list to the length saved beside the counts) or "merge" (leave it, as
  `END_FUZZY`'s forward arm needs) from upstream's `_regex.c` semantics at the corresponding site,
  with the line quoted. Table the nineteen decisions in the notes before writing code.
- **Test-first from the recorded rows**: ledger 11's C and D reproductions
  (`tools/probes/upstream-fuzzy-restart-leak.py`, `(?:[ab][bc](*PRUNE)[wx]){e<=2}` over `qab`, the
  seed 4242 row S47's property found) red, then green with counts and changes agreeing and BOTH
  equal to the edit script the match actually took. The wave property S47 added (counts equal the
  tally of changes) runs at three seeds and 99991 and is what proves the nineteen decisions.
- **Remove S47's holding rule** once the list is trustworthy everywhere; `FuzzyCounts` then has one
  source. Ledger 11 rewritten: all four mechanisms fixed here, upstream's line for each.

## 2. Ledger 9's port-side bug: POSIX with `(?e)` loses an error count

Seed 31337 row 3343, minimised:

```
(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}   POSIX   fullmatch('+ aBA')
upstream: (0, 5) fuzzy_counts (0, 1, 1)     this port: (0, 5) fuzzy_counts (1, 1, 1)
```

Self-refuting on the port alone: without POSIX the port answers `(0, 1, 1)`. It needs POSIX and
`(?e)` together, which is where the port's cost ranking lives. Standing hypothesis, unproven:
`RestoreBestMatch` puts back `FuzzyCounts` and `FuzzyChanges` but not `state.TotalErrors` or
`state.TotalCost`, and the port's ranking reads both where upstream keeps the last run.

- **Prove or refute the hypothesis with the debug build.** S47c's MSVC debug build of upstream and
  its `fprintf` method are available now (`.venvs/regex-debug`, or rebuild per S47c's notes), so the
  ledger's "establishing it needs a debug build, which this project has deliberately not set up" is
  no longer true. Trace upstream's `restore_best_match` on this row and the port's
  `RestoreBestMatch`, `IsBetterFuzzyMatch` and `DoEnhancedFuzzyMatch` side by side.
- **Test-first**: the row red as a permanent test, then green, then the `interactions` wave with
  POSIX at seed 31337 (RED today) and at three seeds plus 99991.
- **Do not widen the fix into the ranking rule.** S41/S42's cost ranking is an owner decision; if
  the bug is in how the ranking reads stale totals, fix the staleness, not the rule.

## Verification

- Ratchet GREEN; the four reproductions red-first then green; wave property and `interactions` with
  POSIX green at the seeds named; ledger 11 and 9 rewritten; `docs/DIVERGENCES.md` inherited-bugs
  row updated from PLANNED (S48b) to SHIPPED in the same commit.
- Blind review (hunt: a "restore" decision at a site upstream merges; a count test that asserts the
  port's old answer; a POSIX fix that changes a non-POSIX row), then the verifier pass re-running the
  probes and the wave property.

## Done when

- [ ] Nineteen site decisions tabled with upstream lines; C and D green; holding rule removed.
- [ ] Ledger 9's port bug proven to the line with the debug build and fixed; seed 31337 green.
- [ ] Ledgers 11 and 9 rewritten; DIVERGENCES row updated; DECISIONS entry.
- [ ] Ratchet GREEN, blind review, verifier, commit.
