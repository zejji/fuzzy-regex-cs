# State

**S57e is a CHECKPOINT, not done** (2026-09-22). The allowance window ran out with one step left:
send the third blind pass over the three fixes the second pass produced (PORTMAP's stale "twenty
lines earlier", the wrong position comment above
`FuzzyBestMatchTests.Bestmatch_keeps_the_folded_match_its_own_flagless_run_finds`, and the wave box
ticked while the gate is red at the date seed). If it comes back clean, `git mv` the slice file to
`docs/plan/slices/done/` and the slice is closed - everything else is landed and recorded in its
closing notes.

**What landed.** The row `(?b)(?r)\m(?:.fo){e<=2}` over `'x fx'` is judged to ledger entry 12, the
doubled `END_FUZZY` backtrack guard, and is the first row of that family measured from BOTH sides:
delete the term from an upstream build and upstream moves to this port's answer, restore it here and
this port moves to upstream's (`tools/probes/s57e-double-count-moves-the-insertion.py`). Pinned, not
fixed. `fuzzy-anchored` is back on the default generator list and both hold-out paragraphs are gone;
it immediately drew three more rows of the same family, and the slice's own negative control at seed
8675309 drew a fourth. The pin's second arm now holds this port's judged answer by exact string,
because "cheaper than upstream" also describes a port that lost count of its errors. The independent
verifier confirmed every claim but one: entry 12's merge citation is `:12475-12513`, not
`:12473-12484`, corrected in all nine files that carried it.

**Measured green on this commit:** ported suite 6531/6531, oracle harness 29/29, ratchet GREEN, the
`fuzzy-anchored` 2000-row wave GREEN at seeds 1234567, 7, 4242 and 271828, the default wave GREEN at
its three seeds. The `-Count 6000` gate is GREEN at seeds 7 and 4242 and RED at 20260922 on ten rows
this slice neither caused nor fixed: they are **S57f**, already written up in
`docs/plan/slices/S57f-the-ten-rows-the-date-seed-drew.md`, and an ablation with the pre-S57e
generator list draws them without `fuzzy-anchored`.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** One inherited ledger entry is
still reproduced: **S61 item 7** is entry 18. Entry 17 is the owner's decision rather than a slice.
S57f sorts ahead of S60b.

**Environment:** a wedged VBCSCompiler (PID 39948) holds
`src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json`; every Release build in this sitting
ran with `$env:IntermediateOutputPath = 'obj/Release/net10.0-s57e/'`. The owner has not authorised
killing it.

**Maintenance:** three files cite the POSIX `fuzzy_changes` guard as `record-oracle.py:1019`, now
`:1338` (`gate-divergence-doors.py:134`, `upstream-posix-fuzzy-changes-crash.py:33`, `LEDGER.md:1212`);
`check-ratchet.ps1:105` writes the upstream-commit line wrongly when there is no submodule;
`run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a reversed row whose lookahead
reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`, then
Pages > Source = GitHub Actions).
