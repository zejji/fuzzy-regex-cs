# Current state

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Item 10, the fuzzy literal filter, landed on
2026-09-23 for a literal, an alternation and a named list.

1cb7cc6 added an ASCII fast path to `Matcher.SameCharIgn` (orchestrator item 1). Its blind
review found no defects, and a re-measure off the filter's workloads shows about 9% on a fuzzy
and 17% on a plain IGNORECASE search. Notes file, "2026-09-23, 03:35".

Green: suite 6650/6650, ratchet GREEN. Oracle GREEN at seeds 7 and 4242; seed 20260923 is RED on
rows 3752 and 5185 only, which diverge on the base commit 8dd746e too.

## Next, in this order

1. Item 2's benchmark triage, still deferred. Do not build on its numbers.
2. The remaining items: 3, 6, 8-9, 11-14, 16 and 17, one sitting each.

## Findings that need a slice

- Reverse BESTMATCH fullmatch: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` over 'oelFin becf' is None
  upstream, (0, 11) here (`fuzzy-literal` seed 20260923, row 1611). Likely the same class:
  `(?b)(?r)(?:\L<phrases>){e<=3}`, phrases `['', 'amber lantern']`, fullmatch 'znz', not re-run.
- `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf diverges (`fuzzy-anchored` seed 20260923, row 5821).
- S60b's reviewer saw 18 `(?b)`/`(?e)` divergences in 14,000 rows; generator not kept, re-derive.
- Oracle rows 3752 (interactions split) and 5185 (partial-sliced search) at seed 20260923.
- S83: `(s)(?:\1){e<=1}` over 'sß' is None both sides; `s(?:s){e<=1}` finds (0, 2). See S83 notes.
