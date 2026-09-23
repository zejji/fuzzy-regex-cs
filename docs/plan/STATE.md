# Current state

**S61 is done** (`docs/plan/slices/done/S61-per-match-allocation.md`). The quiet-machine gates of
2026-09-23 kept every step: a pattern keeps one state between calls, a lazy walk holds one state,
the span overloads copy into a pooled buffer, and `EnumerateMatches(ReadOnlySpan<char>)` returns a
`ValueMatchEnumerator`.

**S88 is done**: an unbounded greedy repeat outside any fuzzy section stops at a fuzzy iteration
that only deleted, where upstream loops to MemoryError (ledger 33). Closing notes:
`docs/plan/slices/done/S88-fuzzy-backreference-repeat-stack.md`.

**S89 is in flight** (`docs/plan/slices/S89-unjudged-bestmatch-rows.md`): the unjudged
`(?b)`/`(?e)`/`(*SKIP)` oracle rows.

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Items 2 and 10 and the `SameCharIgn` fast path are
landed and reviewed. **Item 3 is next** (`try_match`'s string arms, mapped in the notes); start on
the code. After it: 6, 8-9, 11-14, 16 and 17.

## Next

1. The orchestrator re-records the full benchmark baseline in process on merged main and commits
   it as maintenance. Read `CountStringMegabyte` first: 1.10x on the gate, the row closest to the
   floor.
2. S60b item 3.

## Findings that need a slice

1. BESTMATCH over a full-folded backreference: `(?b)(?fi)(f)(?:(?:)){e<=3}` fullmatch 'fxf', and
   `fuzzy-overhang` row 67 at seed 20260923. S89 holds this and the other unjudged
   `(?b)`/`(?e)`/`(*SKIP)` rows (reverse BESTMATCH fullmatch at seed 20260923 row 1611,
   `fuzzy-anchored` row 5821, S60b's 18 `(?b)`/`(?e)` rows, oracle row 4957 at seed 99). Details in
   the S85 notes and `git show 1fa48a9:docs/plan/STATE.md`.
2. Ledger 33's residuals loop to the 1 GB limit, as upstream does: `(?:(?:(?:x){d<=1})+y){e<=5}`
   over 'y', and a body with a group, `(?:(?(1)c|z)|()(?:x){d<=1})+d` over 'cd' (`SHORTCUT:`s).
3. Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1)
   with one deletion. Not investigated (ledger 33).
4. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).

## Hand-offs

- Ledger entry 18 is S86's (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).
- Oracle rows 3752, 5185 (seed 20260923) and 4957 (seed 99): S87 classified the first two; 4957
  is in S89.
