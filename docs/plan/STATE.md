# Current state

**No slice in flight.** S85 landed on 2026-09-23: a fuzzy full-folded match can now stop part-way
into a folding, each folded character it gives back costing one deletion, in the literal and
backreference arms alike (ledger 31). Closing notes:
`docs/plan/slices/done/S85-full-fold-deletion-at-a-folding-boundary.md`.

Green: suite 6623/6623, ratchet GREEN, oracle GREEN at seeds 7 and 4242. Seed 20260923 is RED on
two rows older than S85 (3752, a `(?b)\b\K` split that times out; 5185, a `(*SKIP)` partial
slice), which the orchestrator is triaging separately.

## New findings for the owner: need slices

1. **BESTMATCH over a full-folded backreference.** `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch 'fxf'
   is None upstream and (0, 3) I[1] here, upstream's answer for the literal form. No ablation
   explains it. It keeps the `fuzzy-overhang` generator off the default wave. Details:
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
