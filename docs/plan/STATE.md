# State

**S80 is IN PROGRESS, checkpoint committed (allowance hook, 2026-09-21).** `docs/GUIDE.md`
written (295 lines, no `FLAGS.md` split needed), linked from README's "Where the docs are",
`tools/check-doc-examples.ps1` and `tests/FuzzyRegex.Tests/Docs/GuideSamples.cs` pin its 7
samples - both green. `demo/web/tests/copy-sources.ts`'s `DOC_SOURCES` now includes
`docs/GUIDE.md`; three copy-rule violations (`says-what-it-is-not`) found and fixed in the
guide's prose; `demo/web` test suite green (81/81).

**Remaining for S80, in order:**
1. Add a `['docs/GUIDE.md', 45, "An error budget lives inside the pattern, next to the part of
   it that may be wrong"]` guard row to `demo/web/tests/copy.test.ts`'s `it.each` block (measured
   paragraph count 2026-09-21: 57; floor set below that, not at it).
2. Write the new completeness convention test (sibling to `PublicApiDocumentationTests.cs`):
   every member name in `src/FuzzyRegex/PublicAPI.Unshipped.txt` (NOT `.Shipped.txt`, which is
   pre-1.0 and has zero members - using it would be vacuous) and every key of
   `RegexFlags.InlineFlags` must be named in `README.md` + `docs/GUIDE.md`. Prove it fails on a
   deleted reference, then revert.
3. Run `tools/check-ratchet.ps1`, blind review per `docs/VERIFICATION.md`, closing notes (record
   the Shipped→Unshipped substitution and the 295-line/no-split measurement), move the slice file
   to `docs/plan/slices/done/`, commit.

**Next slice after S80 is S57d** (`docs/plan/slices/S57d-hitend-and-the-partial-that-was-denied.md`),
entry 21. Do not take it from this worktree while another session works it on `main`.

**S57c's `fuzzy-anchored` generator found an unjudged divergence** at seed 1234567 -
`(?b)(?r)\m(?:.fo){e<=2}` over `'x fx'` - that is S57e, spec amendment 36, no slice file yet.
`fuzzy-anchored` stays off `run-oracle.ps1`'s default generator list until S57e judges it.

**Phase 6 gate items are green, Phase 6 is NOT closed.** Remaining: S57d (entry 21), S61 item 7
(entry 18); entry 17 is the owner's decision, not a slice.
