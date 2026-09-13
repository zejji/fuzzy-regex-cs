# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S40d is next, then S41, S42, S43.
Launch: `pwsh -File tools/launch-slice.ps1 s40d`.

**S40c is closed, and it found no group-call defect at all.** `do_exact_match`'s width early-out
counted UTF-16 code units where `min_width` counts characters, so one astral character read as two.
That early-out is guarded by `partial_side == RE_PARTIAL_NONE` and `DoMatch` falls back to the
partial pass only when the non-partial one FAILS - so failing to fire it did not "do the work and
fail later", it let the non-partial pass SUCCEED and suppressed the retry. Now `CountBetween`.

**Both of S40a's readings were wrong.** The ASCII rows were right all along; the astral rows were
ours. Upstream's extra partial is `min_width` counting a group CALL at the callee's width even inside
a zero-width lookaround, showing through the two-pass structure - so it depends on how much text is
AVAILABLE, not on the call. Predicted then confirmed nine for nine on 2026.7.19 and 2026.9.10:
`tools/probes/upstream-min-width-partial-retry.py`.

**The gate is down from nine rows to two:** `tools/run-oracle.ps1 -Count 6000` gives 0+1+1 at seeds
7, 4242, 20260913, and **both survivors are S40d's** (reversed carried slice: row 117071 at 4242,
row 116388 at 20260913). Seed 7 is fully green.

**Two of S40c's seven rows were never its family** and are now judged into existing entries: row
98191 is `bounded-lazy-repeat-partial`, row 74396 is `group-call-loses-the-match` (keyed as a judged
row, because that entry's sub predicate needs upstream to have replaced NOTHING). One example row
left that entry - seed 99991 row 1624, the `split` shape - because the fix made it agree; the family
is unaffected and the closing notes prove it by padding the subject.

**Unjudged, and not S40c's:** seed 31 has three divergences on `partial,partial-sliced,interactions`
at 6000 rows (rows 1075, 6943, 16545), present before and after. The earlier note about seed 31 named
`partial,verbs`; different generator set, overlap unchecked.

**Environmental, cost a turn:** a wedged `VBCSCompiler` made `dotnet build` hang forever with no
output. `tools/find-lock-holder.ps1`'s advice applies - stop the compiler server, it restarts on
demand. Also: rewriting a `.cs` file from Python normalises its line endings and reds IDE0055; run
`dotnet csharpier format .` after.

**Where the port stands:** ratchet GREEN, 5827 tests, 5772 passing, parity **97.2%**, **29** areas at
100%. 55 skipped, all `(?e)`/`(?b)` - S41's and S42's scope.

**Still open for the owner:** the design spec's amendment 20 (the Phase 5 re-plan; ROADMAP carries
the repo half); `slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main`
trails local and needs a push.
