# Current state

**Two slices are in flight, both at checkpoints and merged here: S60b and S61.** S87 landed on
2026-09-23.

**S60b** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes in
`docs/plan/slices/notes/S60b-sittings.md`). Landed so far: item 2 (`search_start`), item 10 (the
fuzzy literal filter) and the `SameCharIgn` ASCII fast path, each reviewed. Item 2 is triaged, kept
and closed: on a quiet machine RedactDigits ran 1.72x faster and every other row stayed flat
(OPTIMISATION-NOTES.md). Its review found one defect, fixed in 8fed3de (a reverse zero-width scan
answered below a slice start that splits a surrogate pair); the fix's own blind pass found no
defects (notes file, "05:35").

**S61** (`docs/plan/slices/S61-per-match-allocation.md`, notes in
`docs/plan/slices/notes/S61-sittings.md`). Steps A to D, the allocation gate and the wave-scale
reset test are committed and blind-reviewed; AOT GREEN on the branch. Its time gates are open.

**S87**: an undone fuzzy section now takes its error total with it, so a BESTMATCH search no longer
loops for ever and ENHANCEMATCH keeps improving (ledger 32, a defect upstream has too). Seed
20260923's rows 3752 and 5185 are classified; row 67 of `fuzzy-overhang` is not explained by the
fix (finding 1 below). Closing notes: `docs/plan/slices/done/S87-bestmatch-stale-total-errors.md`.

Green: suite 6739/6739, ratchet GREEN. Oracle GREEN at seeds 7 and 4242. Seed 20260923 has not
been re-run since S60b, S61 and S87 met; before the merge, S87 was GREEN there and S60b was RED
only on rows 3752 and 5185, which S87 classifies.

## Next

S61:

1. **Time gates.** Measured on a quiet machine, now that S61 is merged. The S61 notes' "How the
   orchestrator decides each open time gate" names the filters and the keep/revert rule per step.
   Record the verdicts in the notes. A failed gate means the next sitting takes that step back out.
2. **`EnumerateMatches(ReadOnlySpan<char>)` returning a `ValueMatchEnumerator`**, signed off
   2026-09-22, gated on a measured gain. Not started. New public API: `update-public-api.ps1`, the
   documents the slice file lists, its own blind pass.
3. Update the benchmark baseline from the quiet-machine run, closing notes, move the slice to
   `done/`.

S60b: **item 3 is next** (`try_match`'s string arms). Its map is in the notes file, "Item 3, mapped
but not started"; start on the code, not on orientation. After it: 6, 8-9, 11-14, 16 and 17, one
sitting each, or deferred with a row in OPTIMISATION-NOTES.md. None is started.

## Findings that need a slice

1. **BESTMATCH over a full-folded backreference.** `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch 'fxf'
   is None upstream and (0, 3) I[1] here, upstream's answer for the literal form. No ablation
   explains it. S87's fix does not explain row 67 of `fuzzy-overhang` at seed 20260923 either
   (`(?b)(?fi)(f)(?:\d+a00(?:\1)){e<=3}`). Both keep that generator off the default wave. Details:
   `docs/plan/slices/notes/S85-sittings.md`.
2. `(?i)(x)(?:(?:\1){d<=2})+$` over 'xy' exhausts the port's 1 GB backtracking stack; upstream V1
   returns None. Found by S84's blind review; `docs/plan/slices/notes/S84-sittings.md`.
3. Reverse BESTMATCH fullmatch: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` over 'oelFin becf' is None
   upstream, (0, 11) here (`fuzzy-literal` seed 20260923, row 1611). Likely the same class:
   `(?b)(?r)(?:\L<phrases>){e<=3}`, phrases `['', 'amber lantern']`, fullmatch 'znz', not re-run.
   Found before S87's fix, not re-checked against it.
4. `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf diverges (`fuzzy-anchored` seed 20260923, row 5821).
   Found before S87's fix, not re-checked against it.
5. S60b's reviewer saw 18 `(?b)`/`(?e)` divergences in 14,000 rows; generator not kept, re-derive.
6. Oracle row 4957 at seed 99 (partial, `(*SKIP)`, like row 5185), found by S61 and reproducing
   at 8dd746e, before S61. Handed to S87, whose closing notes do not name it; not re-checked since.

## Hand-offs

- Ledger entry 18 is S86's (planned in 50d0aec, `docs/plan/slices/S86-repeated-capture-group-bytes.md`).
