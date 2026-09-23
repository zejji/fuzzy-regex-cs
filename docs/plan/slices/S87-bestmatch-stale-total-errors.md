---
slice: S87
phase: 7
title: A rejected fuzzy section no longer leaves a stale error total, so BESTMATCH stops hanging
delivers: []
---

# S87 - The BESTMATCH walk that never ends

Found 2026-09-23 by the differential oracle at the date seed 20260923 (generator `interactions`,
0-based data row 3752), triaged the same night. It is a hang shared with upstream, so it is fixed
under the owner's no-known-bugs rule. The slice also classifies a second row from the same seed
that is an already-judged family (item 4).

## The defect

`(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)` over `2y` never returns, in this port and in upstream.
Verified 2026-09-23 against `regex` 2026.9.10 (orchestrator):

```
regex.search(r'(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')      -> (0, 2), fuzzy_counts (2, 0, 0)
regex.search(r'(?e)(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')  -> (0, 2), fuzzy_counts (2, 0, 0)
regex.search(r'(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')  -> no answer in 15 s (killed)
```

A zero-error match `2` at (0, 1) exists, so BESTMATCH has a right answer and must return it.

Cause, as the triage found it by instrumenting a scratch copy (a hypothesis until this slice
reproduces it): `state.TotalErrors` goes stale. The `EndFuzzy` arm (`Matcher.cs` near :6499 at
`8dd746e`) sets `TotalErrors` to the running outer-plus-inner total; when that is over budget the
reject at ~:6522 backtracks without restoring it. The `EndFuzzy` backtrack arm (~:9278-:9393)
subtracts the inner counts without recomputing it either. A later zero-error success through `|2`
then reports two errors with counts (0, 0, 0); `DoBestFuzzyMatch` (~:10767, `runErrors =
state.TotalErrors`) lands in the "equal" branch its own comment calls unreachable, `startPos` stays
at 0, and the walk re-finds the same match for ever. Upstream: `_regex.c:12483-12495` (set and
reject), `:15568` (subtract without recomputing), `:17652` (the walk reads it). Re-find every line
by the quoted code.

## Scope

1. **Reproduce and minimise** in this port; confirm the stale value directly (a debug assertion
   or a probe), not from this file.
2. **Tests first, each seen red** (a timeout is red), in `Gaps/Engine/FuzzyBestMatchTests` or a
   sibling: the minimal form above gives (0, 1) with counts (0, 0, 0) under a short timeout; the
   constrained form `{s<=1,i<=1,d<=1}` with an inner `{s<=1:\W}` and `\n` in place of `y`; the full
   oracle row's `split` under a timeout (row below); and the same patterns under `(?e)` and with no
   flag, pinned to their current answers so the fix cannot move them.
3. **The fix**: restore `TotalErrors` and `TotalCost` in the reject branch and recompute them from
   the counts in the `EndFuzzy` backtrack arm. Consider making the walks derive `runErrors` from
   `state.FuzzyCounts` as the `rankByCost` path already does - and check `DoEnhancedFuzzyMatch`
   (~:10513), which reads the same field. The triage's scratch fix (save and restore in the reject
   branch only) made the minimal cases and the full row finish in about 1 ms.
4. **Classify row 5185** of the same seed (generator `partial-sliced`, recorded prefilter-free):
   `regex.compile(r"(?:[[:digit:]]?(*SKIP)[^\d]|\s)([_])?\1\g<1>\b", regex.I).search("BB__", 0, 2,
   partial=True)` - upstream (0,2) partial, the port (1,2) partial. The triage judged it the known
   `search-start-partial` family's second arm ("reports its own partial elsewhere",
   `ExpectedDivergences.cs` `_searchStartElsewhereRows` ~:562, answers in `_searchStartElsewhereOurs`
   ~:592); upstream's own `match(s, 1, 2, partial=True)` gives the port's answer. Re-check that, then
   add the row and its answer string (from the report's `Describe()` output) and extend
   `tools/probes/upstream-search-start-whole-region-partial.py`. No engine change.
5. **The oracle**: after the fix, row 3752 still differs from upstream (the port's split becomes
   `['', '\r\n', '𝔘𝔘\rAa']`, which upstream's own `search(s, 1)` supports), because upstream's
   answer only comes quickly through its known `(*SKIP)` slice truncation (ledger 5,
   `bestmatch-walk-truncated-by-a-skip`). Classify it under that entry if its predicate admits
   `split`, or extend the predicate with a test. `tools/run-oracle.ps1` GREEN at seeds 7, 4242,
   20260922 and 20260923.
6. **Ledger**: new LEDGER entry (the hang is upstream's too), `docs/DIVERGENCES.md` row, draft
   upstream report in `docs/plan/upstream-reports/`, filed by nobody. The "upstream hangs" call gets a
   blind review first, given the repro and a timing, not this file's verdict.

The full row 3752, flags 16394 (IgnoreCase | Multiline | FullCase), V0:
`regex.compile("(?b)\\b\\K(?:(?:\U0001d7eea(?:[[:alpha:]]+?){s<=1:\\W}){s<=1,i<=1,d<=1}(*SKIP)\\S|\\S)", regex.I|regex.M|regex.F).split("\U0001d7ee\r\n\U0001d518\U0001d518\U0001d518\rAa")`
- upstream `['', '\r\n𝔘𝔘𝔘\rAa']` in ~0.1 ms; the port does not finish.
Re-record: `python tools/record-oracle.py --generator <default list> --count 300 --seed 20260923`.

## Done when

Tests green and each seen red without the fix; ratchet green; oracle green at the four seeds; ledger,
DIVERGENCES and the upstream draft written; slice moved to `done/`.
