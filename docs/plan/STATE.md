# Current state

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Landed so far: item 2 (`search_start`), item 10
(the fuzzy literal filter) and the `SameCharIgn` ASCII fast path, each reviewed.

Item 2 is triaged and kept: on a quiet machine RedactDigits ran 1.72x faster and every other row
stayed flat (OPTIMISATION-NOTES.md). Finishing its review found and fixed one defect in 8fed3de:
a reverse zero-width scan answered below a slice start that splits a surrogate pair.

Green: suite 6651/6651, ratchet GREEN. Oracle GREEN at seeds 7 and 4242; seed 20260923 is RED on
rows 3752 and 5185 only, which diverge on the base commit 8dd746e too.

## Next, in this order

1. **One blind pass over 8fed3de's fix** (`Matcher.SearchStartZeroWidth`, eight lines). The brief
   and hunt list are in the notes file, "2026-09-23, 05:15", Review. It needs a session that can
   dispatch a reviewer.
2. The remaining items: 3, 6, 8-9, 11-14, 16 and 17, one sitting each, or deferred with a row in
   OPTIMISATION-NOTES.md. None is started.

## Findings that need a slice

- Reverse BESTMATCH fullmatch: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` over 'oelFin becf' is None
  upstream, (0, 11) here (`fuzzy-literal` seed 20260923, row 1611). Likely the same class:
  `(?b)(?r)(?:\L<phrases>){e<=3}`, phrases `['', 'amber lantern']`, fullmatch 'znz', not re-run.
- `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf diverges (`fuzzy-anchored` seed 20260923, row 5821).
- S60b's reviewer saw 18 `(?b)`/`(?e)` divergences in 14,000 rows; generator not kept, re-derive.
- Oracle rows 3752 (interactions split) and 5185 (partial-sliced search) at seed 20260923.
- S83: `(s)(?:\1){e<=1}` over 'sß' is None both sides; `s(?:s){e<=1}` finds (0, 2). See S83 notes.
