# Current state

**No slice in flight.** S87 landed on 2026-09-23. An undone fuzzy section now takes its error
total with it, so a BESTMATCH search no longer loops for ever and ENHANCEMATCH keeps improving
(ledger 32, a defect upstream has too). Seed 20260923's rows 3752 and 5185 are classified; row 67 of
`fuzzy-overhang` is not explained by the fix (finding 1 below). The blind review passed after one
provenance fix. Closing notes: `docs/plan/slices/done/S87-bestmatch-stale-total-errors.md`.

Green: ratchet GREEN, oracle GREEN at seeds 7, 4242, 20260922 and 20260923.

## New findings for the owner: need slices

1. **BESTMATCH over a full-folded backreference.** `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch 'fxf'
   is None upstream and (0, 3) I[1] here, upstream's answer for the literal form. No ablation
   explains it. S87's fix does not explain row 67 of `fuzzy-overhang` at seed 20260923 either
   (`(?b)(?fi)(f)(?:\d+a00(?:\1)){e<=3}`). Both keep that generator off the default wave. Details:
   `docs/plan/slices/notes/S85-sittings.md`.
2. `(?i)(x)(?:(?:\1){d<=2})+$` over 'xy' exhausts the port's 1 GB backtracking stack; upstream V1
   returns None. Found by S84's blind review; `docs/plan/slices/notes/S84-sittings.md`.

## Next: S60b, a checkpoint, not a landing

`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes in
`docs/plan/slices/notes/S60b-sittings.md`. Its next sitting, in this order:

1. **Triage the benchmarks, after 22:00**, when the owner is off the machine; it may revert item 2.
2. **Re-run the four negative controls against the committed code**, plus one fresh seed each.
3. **The blind review**, which has not run on S60b's code at all.
4. Only then its remaining items: 3, 6, 8-14, 16 and 17, one sitting each. Narrowing 3 (no
   prefilter for a pattern holding `(*SKIP)`) rests on an argument, not a measurement.
