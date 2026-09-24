# Current state

**S60b is in flight** (slice file and `docs/plan/slices/notes/S60b-sittings.md`). Items 2, 3, 9,
10, 14 and 17 and the `SameCharIgn` fast path are landed and reviewed; item 6 is closed as never
ported. Items 3 and 17 wait for the orchestrator's quiet run (filters in the notes' "23:10" and
"2026-09-24, 00:50" sections). Open: 8, 11-13, 16, and the three `long-alternation` rows below.

**S89 is in flight**: the unjudged `(?b)`/`(?e)`/`(*SKIP)` oracle rows.
## Next
1. The orchestrator's quiet-machine benchmark baseline (read `CountStringMegabyte` first: 1.10x on
   the gate, the row closest to the floor), then the time gates for items 3 and 17.
2. S60b: judge the three `long-alternation` oracle rows (notes, "02:45" section) so the generator
   joins the default list; then items 16, 11, 12, 13, 8. `(?i)` named lists are not indexed.
   Item 9 still owes an AOT run (`tools/run-aot-tests.ps1` stalled on a starved machine).

## Findings that need a slice
1. BESTMATCH over a full-folded backreference and the other unjudged `(?b)`/`(?e)`/`(*SKIP)` rows
   are S89's (details in the S85 notes and `git show 1fa48a9:docs/plan/STATE.md`).
2. Ledger 33's residuals loop to the 1 GB limit, as upstream does (`SHORTCUT:`s). Upstream
   `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1).
3. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).
4. `SearchStartZeroWidth` has no timeout poll: `\b\B` over 2^21 `a`s with a 1 ms timeout returns
   after about 120 ms (S60b item 3 review; base behaves the same).
5. S22's three upstream self-inconsistencies (S60b item 6) need a draft in `upstream-reports/`.
6. Upstream `finditer(r'a*?(*SKIP)', 'aaab')` gives (0, 0), (4, 4); `(a*?)(*SKIP)` gives every
   position. Possibly an upstream bug the port shares; not yet researched (S60b item 17 notes).
7. **Bug, base too:** `(?i).{0,2}x` over `"😀x😀😀x"` sliced at beginning 1, length 1 throws
   `IndexOutOfRangeException` (`MatchState.CharAt`); at beginning 4 or 6 it never ends.
## Hand-offs
- Ledger entry 18 is S86's. Oracle rows 3752, 5185 (seed 20260923) and 4957 (seed 99): S87
  classified the first two; 4957 is in S89.
