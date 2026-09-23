# Current state

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Landed so far: item 2 (`search_start`), item 10
(the fuzzy literal filter) and the `SameCharIgn` ASCII fast path, each reviewed.

Item 2 is triaged, kept and closed: on a quiet machine RedactDigits ran 1.72x faster and every
other row stayed flat (OPTIMISATION-NOTES.md). Its review found one defect, fixed in 8fed3de (a
reverse zero-width scan answered below a slice start that splits a surrogate pair); the fix's own
blind pass found no defects (notes file, "05:35").

S87 landed on 2026-09-23. An undone fuzzy section now takes its error total with it, so a
BESTMATCH search no longer loops for ever and ENHANCEMATCH keeps improving (ledger 32, a defect
upstream has too). Seed 20260923's rows 3752 and 5185 are classified; row 67 of `fuzzy-overhang` is
not explained by the fix (finding 1 below). Closing notes:
`docs/plan/slices/done/S87-bestmatch-stale-total-errors.md`.

Green: suite 6700/6700, ratchet GREEN. Oracle GREEN at seeds 7 and 4242. Seed 20260923 has not
been re-run since S60b's generators and S87's fix met; before the merge, S87 was GREEN there and
S60b was RED only on rows 3752 and 5185, which S87 classifies.

## Next

**S60b item 3 is next** (`try_match`'s string arms). Its map is in the notes file, "Item 3, mapped
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
