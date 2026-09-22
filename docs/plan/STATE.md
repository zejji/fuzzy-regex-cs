# Current state

**No slice in flight.** S84 landed on 2026-09-22: a full-folded fuzzy backreference now charges the
rest of a half-used folding as an edit, and a retried edit steps past what it used up (ledger 29
and 30). Closing notes: `docs/plan/slices/done/S84-full-fold-backreference-mid-folding.md`.

Green: suite 6601/6601, ratchet GREEN, oracle GREEN at seeds 7, 4242, 20260922 and 31337.

## Next: S85

`docs/plan/slices/S85-full-fold-deletion-at-a-folding-boundary.md`. With only deletions allowed, a
match that would end half-way through a folding is lost, in the literal arm as well as the
backreference arms, and the literal arm loops for ever on free deletions. Upstream shares both.
S85 also widens the generator, which reaches S84's defects on at most four rows per seed.

## New finding for the owner: needs a slice

`(?i)(x)(?:(?:\1){d<=2})+$` over 'xy' exhausts the port's 1 GB backtracking stack, at HEAD too;
upstream V1 returns None (it raises MemoryError without IgnoreCase). S84's blind review found it;
details in `docs/plan/slices/notes/S84-sittings.md`. No slice file yet.

## Still pending from before S83

**S60b is a checkpoint, not a landing** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`,
notes in `docs/plan/slices/notes/S60b-sittings.md`). Its next sitting, in this order:

1. **Triage the benchmarks, after 22:00**, when the owner is off the machine; it may revert item 2.
2. **Re-run the four negative controls against the committed code**, plus one fresh seed each.
3. **The blind review**, which has not run on S60b's code at all.
4. Only then its remaining items: 3, 6, 8-14, 16 and 17, one sitting each.

Narrowing 3 (no prefilter for a pattern holding `(*SKIP)`) rests on an argument, not a measurement.
