# State

**S80 is DONE.** `docs/GUIDE.md` (299 lines, no `FLAGS.md` split needed) covers all eight
required sections, linked from README's "Where the docs are". `tools/check-doc-examples.ps1`
and `tests/FuzzyRegex.Tests/Docs/GuideSamples.cs` pin its 7 samples. `demo/web/tests/copy.test.ts`
carries the `docs/GUIDE.md` guard row. `UserDocumentationCompletenessTests.cs` gates every public
member in `PublicAPI.Unshipped.txt` and every `RegexFlags.InlineFlags` letter against `README.md`
+ `docs/GUIDE.md`, proven to fail on a deleted member and a deleted flag letter, then reverted.
Blind review raised 4 prose defects in the guide (invented named-list syntax, a false
static-overload claim, a false exception claim, a backwards parameter description); all
reproduced and fixed; no second pass needed (prose-only fixes to already-reviewed content).
Ratchet green, full suite 6517/6517, demo copy suite 82/82.

**Next slice is S57d** (`docs/plan/slices/S57d-hitend-and-the-partial-that-was-denied.md`),
entry 21. Do not take it from this worktree while another session works it on `main`.

**S57c's `fuzzy-anchored` generator found an unjudged divergence** at seed 1234567 -
`(?b)(?r)\m(?:.fo){e<=2}` over `'x fx'` - that is S57e, spec amendment 36, no slice file yet.
`fuzzy-anchored` stays off `run-oracle.ps1`'s default generator list until S57e judges it.

**Phase 6 gate items are green, Phase 6 is NOT closed.** Remaining: S57d (entry 21), S61 item 7
(entry 18); entry 17 is the owner's decision, not a slice.
