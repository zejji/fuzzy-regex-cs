# Current state

**Two slices are in flight, both at checkpoints: S60b and S61.**

**S61** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`, sitting 5). Steps A to C passed their time gates on the
2026-09-23 quiet run. Step D is kept under the owner's option (c): the span overloads of `IsMatch`
and `Count` copy into a pooled buffer (1fa48a9, reviewed). The span walk,
`EnumerateMatches(ReadOnlySpan<char>)` returning a `ValueMatchEnumerator`, landed in 05019f0 and
takes the 100 KB walk from 3,017,968 B to 16 B.

**S60b** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes in
`docs/plan/slices/notes/S60b-sittings.md`). Items 2 and 10 and the `SameCharIgn` fast path are
landed and reviewed. **Item 3 is next** (`try_match`'s string arms, mapped in the notes).

Green on `slice/s61`: suite 6768/6768, ratchet GREEN, oracle GREEN at three seeds, AOT GREEN.

## Next

S61, all waiting on the orchestrator's quiet-machine run:

1. `*SpanOverload*` against 8dd746e. If `SpanMegabyte` is still above 1.13x, take only the
   span-overload part of step D back out.
2. `*WorkloadBenchmarks.EnumerateMatches*ToEndDense*`: the time half of the span walk's gate.
3. Update the benchmark baseline, write the closing notes, move the slice to `done/`.

## Findings that need a slice

The six listed in 1fa48a9's STATE.md are unchanged: BESTMATCH over a full-folded backreference
(S85 notes), the `(?i)(x)(?:(?:\1){d<=2})+$` stack exhaustion (S84 notes), reverse BESTMATCH
fullmatch (seed 20260923 row 1611), `fuzzy-anchored` row 5821, S60b's 18 `(?b)`/`(?e)` rows, and
oracle row 4957 at seed 99. Read them with `git show 1fa48a9:docs/plan/STATE.md`.

## Hand-offs

- Ledger entry 18 is S86's (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).
