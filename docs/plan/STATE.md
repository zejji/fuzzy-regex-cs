# Current state

**S61 is done** (`docs/plan/slices/done/S61-per-match-allocation.md`). The quiet-machine gates of
2026-09-23 kept every step: a pattern keeps one state between calls, a lazy walk holds one state,
the span overloads copy into a pooled buffer, and `EnumerateMatches(ReadOnlySpan<char>)` returns a
`ValueMatchEnumerator`. Green on `slice/s61`: suite 6768/6768, ratchet GREEN.

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Items 2 and 10 and the `SameCharIgn` fast path are
landed and reviewed. **Item 3 is next** (`try_match`'s string arms, mapped in the notes).

## Next

1. Merge `slice/s61` to main.
2. The orchestrator re-records the full benchmark baseline in process on merged main and commits
   it as maintenance. Read `CountStringMegabyte` first: 1.10x on the gate, the row closest to the
   floor.
3. S60b item 3.

## Findings that need a slice

The six listed in 1fa48a9's STATE.md are unchanged: BESTMATCH over a full-folded backreference
(S85 notes), the `(?i)(x)(?:(?:\1){d<=2})+$` stack exhaustion (S84 notes), reverse BESTMATCH
fullmatch (seed 20260923 row 1611), `fuzzy-anchored` row 5821, S60b's 18 `(?b)`/`(?e)` rows, and
oracle row 4957 at seed 99. Read them with `git show 1fa48a9:docs/plan/STATE.md`. Also: a
non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).

## Hand-offs

- Ledger entry 18 is S86's (`docs/plan/slices/S86-repeated-capture-group-bytes.md`).
- Oracle rows 3752, 5185 (seed 20260923) and 4957 (seed 99) are S87's.
