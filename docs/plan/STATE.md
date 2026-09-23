# Current state

**S87 in flight, checkpoint.** Fix, pins, ledger 32, DIVERGENCES and COMPARISON rows are
committed and green. Left: the blind review (including the "upstream hangs" call), then move the
slice to `done/` with its Review paragraph.

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
