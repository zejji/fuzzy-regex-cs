# Current state

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Item 10, the fuzzy literal filter, landed on
2026-09-23 as its own commit (`Engine/FuzzyLiteralFilter.cs`): FuzzyPhraseOneIsMatch 2.424 s to
64.2 ms, FuzzyPhraseThreeSeparatePasses 7.438 s to 164.9 ms, answers unchanged.

Green: suite 6620/6620, ratchet GREEN, AOT GREEN. Oracle GREEN at seeds 7 and 4242; seed 20260923
is RED on rows 3752 and 5185 only, which diverge on the base commit 8dd746e too.

## Next, in this order

1. **Extend the filter to an alternation of literals and a named list** (orchestrator,
   2026-09-23): `(?:copper field studio|violet stone archive|amber lantern works){e<=2}` and the
   `\L<name>` form, the rows FuzzyPhraseThreeAlternation and FuzzyPhraseThreeNamedList. Pieces of
   every branch searched in one pass; reject only when no branch has a piece. Same correctness
   conditions and gate as item 10.
2. Item 2's benchmark triage, still deferred. Do not build on its numbers.
3. The remaining items: 3, 6, 8-9, 11-14, 16 and 17, one sitting each.

## Findings that need a slice

- Reverse BESTMATCH fullmatch: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` over 'oelFin becf' is None
  upstream and (0, 11) here (`fuzzy-literal` seed 20260923, 2000 rows, row 1611).
- `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf diverges (`fuzzy-anchored` seed 20260923, 2000 rows,
  row 5821). Both persist with the filter ablated.
- S60b's reviewer saw 18 divergences in 14,000 rows of its own, all `(?b)`/`(?e)` with a cost
  equation or `(?p)`, identical with the filter nulled. Its generator is not kept; re-derive.
- The oracle rows 3752 (interactions split) and 5185 (partial-sliced search) at seed 20260923.
- S83's finding: `(s)(?:\1){e<=1}` over 'sß' is None upstream and here; `s(?:s){e<=1}` finds
  (0, 2). Details in `docs/plan/slices/notes/S83-sittings.md`.
