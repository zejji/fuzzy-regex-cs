# Current state

**Two slices are in flight, both at checkpoints and merged here: S60b and S61.** S88 landed on
2026-09-23, after S87.

**S60b** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes in
`docs/plan/slices/notes/S60b-sittings.md`). Items 2 and 10 and the `SameCharIgn` ASCII fast path
are landed and reviewed. **Item 3 is next** (`try_match`'s string arms), mapped in the notes file
under "Item 3, mapped but not started"; start on the code. After it: 6, 8-9, 11-14, 16 and 17.

**S61** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`). Steps A to D are committed and reviewed. Next: the time
gates on a quiet machine (the notes name the keep/revert rule per step), then
`EnumerateMatches(ReadOnlySpan<char>)` gated on a measured gain, then the benchmark baseline.

**S88**: an unbounded greedy repeat outside any fuzzy section stops at a fuzzy iteration that
only deleted, where upstream loops to MemoryError (ledger 33). Closing notes:
`docs/plan/slices/done/S88-fuzzy-backreference-repeat-stack.md`.

Green: suite 6747/6747, ratchet GREEN. Oracle GREEN at seeds 7, 4242 and 20260923.

## Findings that need a slice

1. BESTMATCH over a full-folded backreference: `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch 'fxf', and
   `fuzzy-overhang` row 67 at seed 20260923. S89 (`docs/plan/slices/S89-unjudged-bestmatch-rows.md`)
   holds this and the other unjudged `(?b)`/`(?e)`/`(*SKIP)` rows. Details in the S85 notes.
2. Ledger 33's residuals loop to the 1 GB limit, as upstream does: `(?:(?:(?:x){d<=1})+y){e<=5}`
   over 'y', and a body with a group, `(?:(?(1)c|z)|()(?:x){d<=1})+d` over 'cd' (`SHORTCUT:`s).
3. Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1)
   with one deletion. Not investigated (ledger 33).

## Hand-offs

- Ledger entry 18 is S86's (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).
