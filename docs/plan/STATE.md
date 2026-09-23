# Current state

**S61 is done** (`docs/plan/slices/done/S61-per-match-allocation.md`). The quiet-machine gates of
2026-09-23 kept every step: a pattern keeps one state between calls, a lazy walk holds one state,
the span overloads copy into a pooled buffer, and `EnumerateMatches(ReadOnlySpan<char>)` returns a
`ValueMatchEnumerator`.

**S88 is done**: an unbounded greedy repeat outside any fuzzy section stops at a fuzzy iteration
that only deleted, where upstream loops to MemoryError (ledger 33). Closing notes:
`docs/plan/slices/done/S88-fuzzy-backreference-repeat-stack.md`.

**S89 is done**: it judged the old findings 1 and 3-6. The BESTMATCH rows are ledger 12
(upstream's doubled insertion guard), alone or with ledgers 29 and 30; row 4957 at seed 99 is
port-right. `fuzzy-overhang` is on the default wave. Closing notes:
`docs/plan/slices/done/S89-unjudged-bestmatch-rows.md`.

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Items 2 and 10 and the `SameCharIgn` fast path are
landed and reviewed; item 3 (`try_match`'s string arms) is in progress on `slice/s60b`. After it:
6, 8-9, 11-14, 16 and 17.

## Queue

1. The orchestrator re-records the full benchmark baseline in process on merged main and commits
   it as maintenance. Read `CountStringMegabyte` first: 1.10x on the gate, the row closest to the
   floor.
2. S60b item 3 and onwards.
3. **S90**: three partial rows over a full-folded ligature, drawn by the 2000-row `fuzzy-*` wave and
   explained by no ablation (`docs/plan/slices/S90-partial-fold-rows.md`).
4. **S86**: ledger entry 18 (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).

## Findings that need a slice

1. Ledger 33's residuals loop to the 1 GB limit, as upstream does: `(?:(?:(?:x){d<=1})+y){e<=5}`
   over 'y', and a body with a group, `(?:(?(1)c|z)|()(?:x){d<=1})+d` over 'cd' (`SHORTCUT:`s).
2. Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1)
   with one deletion. Not investigated (ledger 33).
3. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).

## Hand-offs

- Ledger entry 18 is S86's (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).
