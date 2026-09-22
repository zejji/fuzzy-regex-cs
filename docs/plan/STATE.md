# State

**S57f is in flight** (checkpoint, 2026-09-22). It judges the ten rows the `-Count 6000` gate draws
at seed 20260922 - `interactions` 7, `partial` 1, `partial-sliced` 1, `verbs` 1 - which S57e neither
caused nor fixed. Sitting 1 recovered all ten from disk and judged one.

**All ten rows are now in `docs/plan/slices/notes/S57f-sittings.md`**, pattern, subject, flags and
both engines' answers. They had to be re-recorded: `TestResults/` is gitignored and the default wave
had already overwritten `report-20260922.txt`. Re-run the consumer over the wave still on disk with
`tools/run-oracle.ps1 -SkipRecord` rather than paying for the 126080-row recording again.

**Row 97332 is judged: the port is right.** `(\S??)\.` fullmatch on `'.a'` with `partial=True` -
upstream answers a partial match (0,2), the port answers no match. No string beginning `.a` can be
fullmatched by a pattern two characters wide whose two-character matches all end in `.`, so
`upstream/README.rst:270` and its own `\d{4}` on `'a'` example make `None` the documented answer.
Upstream also contradicts itself one character further along: `'.ab'` is no match, `'abab'` against
`(\S??)ab` is a partial. The pin - a test, a `docs/DIVERGENCES.md` row and an upstream-report draft -
lands with the rest of the batch, so the independent verifier sees all ten at once (amendment 34).

**Rows 76484, 116428 and 74120 are `turkic-default-folding`.** Upstream's `[A-Z]` and its
`[\p{ASCII}&&\p{L}]` reach U+0130 and U+0131 under IGNORECASE and this port's do not. All three are confirmed on
both sides through `-Rows`: swap the Turkic letter for one whose fold the port shares and the engines
agree, remove IGNORECASE and they agree, and only the row as drawn diverges. All three are span-less
operations - two `sub` and a `split` - so each wants a row in `_turkicWithoutSpansRows`, the way
S52's ninth sitting did it.

**Next**: judge the remaining six. Of those only 73420 and 76118 carry IGNORECASE and neither
subject holds a Turkic character, so expect the family to stop at three.

**Measured green at S57e's commit:** ported suite 6531/6531, ratchet GREEN, the default wave GREEN at
its three seeds. This checkpoint changes documentation only - no file under `src/` or `tests/` is
touched, so the parity board cannot have moved.

**The ratchet is GREEN here, and `$env:IntermediateOutputPath` is what reddens it.** Two runs on
this tree differing in that one variable: with it set to `obj/s57f-release/` the test run was killed
at the 1200 s timeout; with it unset the same suite finished in 34 s, 6531/6531, and left
`docs/STATUS.md` and the stamp byte-identical. **Set that variable only for a Release build that the
wedged compiler would otherwise block, never for the ratchet.**

**S80 is done and merged into main (2026-09-22).** `docs/GUIDE.md` (299 lines) covers all
eight required sections and is linked from README's "Where the docs are".
`tools/check-doc-examples.ps1` and `tests/FuzzyRegex.Tests/Docs/GuideSamples.cs` pin its 7
samples; `demo/web/tests/copy.test.ts` carries the `docs/GUIDE.md` guard row.
`UserDocumentationCompletenessTests.cs` gates every public member in
`PublicAPI.Unshipped.txt` and every `RegexFlags.InlineFlags` letter against `README.md` plus
`docs/GUIDE.md`, proven to fail on a deleted member and on a deleted flag letter.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** One inherited ledger entry is
still reproduced: **S61 item 7** is entry 18. Entry 17 is the owner's decision rather than a slice.

**Environment:** a wedged VBCSCompiler (PID 39948) holds
`src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json`. Release builds run with
`$env:IntermediateOutputPath = 'obj/Release/net10.0-<slice>/'`. Killing it is not authorised.

**Maintenance still open:** `run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a
reversed row whose lookahead reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`,
then Pages > Source = GitHub Actions).
