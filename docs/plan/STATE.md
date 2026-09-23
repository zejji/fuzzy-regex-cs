# Current state

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Items 2, 3 and 10 and the `SameCharIgn` fast path
are landed and reviewed. Item 3 (`try_match`'s arms) has no time gate yet: it waits for the
orchestrator's quiet run, whose filters are in the notes' "23:10" section. Next items: 6, 8-9,
11-14, 16 and 17.

**S89 is in flight** (`docs/plan/slices/S89-unjudged-bestmatch-rows.md`): the unjudged
`(?b)`/`(?e)`/`(*SKIP)` oracle rows.

S61 and S88 are done (closing notes in `docs/plan/slices/done/`).

## Next

1. The orchestrator's quiet-machine benchmark baseline (read `CountStringMegabyte` first: 1.10x on
   the gate, the row closest to the floor), then item 3's time gate.
2. S60b item 6.

## Findings that need a slice

1. BESTMATCH over a full-folded backreference and the other unjudged `(?b)`/`(?e)`/`(*SKIP)` rows
   are S89's (details in the S85 notes and `git show 1fa48a9:docs/plan/STATE.md`).
2. Ledger 33's residuals loop to the 1 GB limit, as upstream does (`SHORTCUT:`s).
3. Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1)
   with one deletion. Not investigated (ledger 33).
4. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).
5. `SearchStartZeroWidth` has no timeout poll. `\b\B` over 2^21 `a`s with a 1 ms timeout returns
   after about 120 ms (S60b item 3 review; base behaves the same).

## Hand-offs

- Ledger entry 18 is S86's. Oracle rows 3752, 5185 (seed 20260923) and 4957 (seed 99): S87
  classified the first two; 4957 is in S89.
