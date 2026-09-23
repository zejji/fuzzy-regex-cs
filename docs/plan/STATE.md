# Current state

**Two slices are in flight, both at checkpoints and merged here: S60b and S61.** S89 landed on
2026-09-23.

**S60b** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes in
`docs/plan/slices/notes/S60b-sittings.md`). Items 2 and 10 and the `SameCharIgn` ASCII fast path
are landed and reviewed. **Item 3 is next** (`try_match`'s string arms; its map is in the notes
file). After it: 6, 8-9, 11-14, 16 and 17, one sitting each, or deferred in OPTIMISATION-NOTES.md.

**S61** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`). Steps A to D are committed and reviewed. Next: the time
gates on a quiet machine, then `EnumerateMatches(ReadOnlySpan<char>)` if a gain is measured, then
the benchmark baseline and closing notes.

**S89** judged STATE's old findings 1 and 3-6. The BESTMATCH rows are ledger 12 (upstream's doubled
insertion guard), alone or with ledgers 29 and 30; row 4957 at seed 99 is port-right.
`fuzzy-overhang` is on the default wave. Closing notes:
`docs/plan/slices/done/S89-unjudged-bestmatch-rows.md`.

Green: suite 6744/6744, ratchet GREEN, oracle GREEN at seeds 7, 4242 and 20260923.

## Queue

- **S88**: a repeated fuzzy backreference with deletions exhausts the port's 1 GB backtracking
  stack (`docs/plan/slices/S88-fuzzy-backreference-repeat-stack.md`).
- **S90**: three partial rows over a full-folded ligature, drawn by the 2000-row `fuzzy-*` wave and
  explained by no ablation (`docs/plan/slices/S90-partial-fold-rows.md`).
- **S86**: ledger entry 18 (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).
