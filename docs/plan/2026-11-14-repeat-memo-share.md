# Is the repeat memo worth optimising? (2026-11-14)

Point-in-time record for phase 11 item 12. The checklist lives in `STATE.md`.

**Answer: no. No change.** The 2026-11-04 profile put about 3% of `(?:(\w+) jumps over){e<=2}`
over 44 chars in the repeat memo (`Matcher.RepeatMemoHit`, its `HashSet<RepeatMemoKey>` and
`RepeatData.ClearMemo`), on main b9c7e502. Since then D138-D145, D135, D29 and D154 (item 8, whose
required-literal screen answers a near miss without the engine) have landed, so only the hit rows
still run the memo. On those rows, on current main, the memo takes about 0.1% of the CPU time, far
under the 2% bar for doing anything. Why the earlier trace read ~3% was not re-derived; the question
here is today's share.

## How it was measured

Main 441c8d8d, Release, the item 8 harness re-pointed at this worktree
(`.scratch/p11-item12/grouped.cs`). One WPR CPU trace (`FuzzyRegexWprStart`/`Stop`) holding three
processes, each a renamed copy of the harness running one row for 1 s of warm-up, 6 s, then 15
measuring batches. The trace was read with `.scratch/wpr/etl-top.cs`; the full exclusive and
inclusive lists are kept beside the harness (`g_*.full.txt`). A peer session shared the machine, so
times are not reported; sample shares within one process do not depend on that.

The `g_hit` process also ran `nogroup e<=2 hit`, because the row filter matches by substring.
That row can run the memo too (a pattern testing no group still keys by position, count and edits).

## Results (inclusive share of the process's samples)

| Process (rows) | Samples | `RepeatMemoHit` | `HashSet.Add` | `RepeatMemoKey.GetHashCode` | `ClearMemo` |
|---|---|---|---|---|---|
| `g_hit` (`group e<=2 hit`, `nogroup e<=2 hit`) | 21,133 | 16 (0.08%) | 14 | 8 | 1 exclusive, not in the top 400 inclusive |
| `g_two` (`two groups e<=2`) | 14,590 | 19 (0.13%) | 15 | 2 exclusive | not in the top 400 |
| `g_lazy` (`lazy group e<=2`) | 15,534 | 20 (0.13%) | 17 | not in the top 400 | not in the top 400 |

`RepeatMemoHit` is a real call, not inlined (it has its own frames), so its inclusive count covers
building the key, hashing it and the set's `Add`. `SpanHelpers.ClearWithoutReferences`, which a
`HashSet.Clear` would reach, is 0.2-0.3% exclusive in all three processes and is shared with the
match state's own clearing (`MatchState.Init`), so even counted whole the memo stays under 0.5%.

Where the time does go, exclusive: `BasicMatchCore` 22.7-24.3%, `AdvanceEndGreedyRepeat` 13.6-14.9%
(`AdvanceEndLazyRepeat` 12.8% for the lazy row), `RetryFuzzyMatchString` 7.5-9.3%,
`FuzzyMatchString` 6.6-8.0%, `FuzzyIterationStands` 5.6-5.7%, `GuardList.Guard` 4.0-4.7%.

## Reading

A cheaper key, a pooled set or a different table could save at most the memo's own ~0.1%, which
no timing harness on this machine can resolve. The grouped hit rows' cost is the fuzzy engine
itself (the repeat-end bookkeeping and the fuzzy string retries), which is a different item if it
is ever pursued.
