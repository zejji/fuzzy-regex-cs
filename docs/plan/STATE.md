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

**Next**: judge the remaining nine, starting with the group whose reversed match begins in the wrong
place (73420, 74554, 103000, 116428).

**Measured green at S57e's commit:** ported suite 6531/6531, ratchet GREEN, the default wave GREEN at
its three seeds. This checkpoint changes documentation only - no file under `src/` or `tests/` is
touched, so the parity board cannot have moved.

**The ratchet on this checkpoint is RED by TIMEOUT, not by a test:** "the test run did not finish
within 1200 s and was killed", after a clean build. Its advice, "a hung test is an engine loop: diff
src/ against HEAD before re-running", does not apply - `git status` is documentation only. The run
was started with `$env:IntermediateOutputPath` redirected, which rebuilds every project from
scratch; whether that is the cause is not established. **Re-run it first thing next sitting**, with
the variable set only if a Release build needs it.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** One inherited ledger entry is
still reproduced: **S61 item 7** is entry 18. Entry 17 is the owner's decision rather than a slice.

**Environment:** a wedged VBCSCompiler (PID 39948) holds
`src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json`. Release builds run with
`$env:IntermediateOutputPath = 'obj/Release/net10.0-<slice>/'`. Killing it is not authorised.

**Maintenance still open:** `run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a
reversed row whose lookahead reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`,
then Pages > Source = GitHub Actions).
