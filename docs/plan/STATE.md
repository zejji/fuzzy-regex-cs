# Current state

**No slice in flight.** S83 landed on 2026-09-22: a fuzzy deletion that finishes a full-case-folded
string or backreference now costs one edit (`Matcher.FoldingIsPartUsed`, ledger entry 28, oracle
entry `full-fold-fuzzy-deletion`). Closing notes: `docs/plan/slices/done/S83-full-fold-fuzzy-deletion.md`.

Green: suite 6586/6586, ratchet GREEN, oracle GREEN at its three default seeds. Real data: 0 span
differences from upstream V0 over the 300,000 usage-corpus phrase searches.

## Still pending from before S83

**S60b is a checkpoint, not a landing** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`,
notes in `docs/plan/slices/notes/S60b-sittings.md`). Its next sitting, in this order:

1. **Triage the benchmarks, after 22:00**, when the owner is off the machine. Item 2's measurement
   is outstanding, and the triage may revert item 2.
2. **Re-run the four negative controls against the committed code**, plus one fresh seed each.
3. **The blind review**, which has not run on S60b's code at all.
4. Only then its remaining items: 3, 6, 8-14, 16 and 17, one sitting each.

Narrowing 3 - no prefilter for a pattern holding a `(*SKIP)` - rests on an argument, not a
measurement; the S60b notes state the experiment that would settle it.

## New finding for the owner: needs a slice

S83's blind review found a second shared defect, reproduced: a full-folded backreference that ends
half-way through a folding backtracks without trying a fuzzy edit. `(s)(?:\1){e<=1}` over 'sß' is
None in upstream V1 and here; `s(?:s){e<=1}` finds (0, 2). Details in
`docs/plan/slices/notes/S83-sittings.md`. No slice file exists for it yet.
