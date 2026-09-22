# State

**S57e is done** (2026-09-22). The row `(?b)(?r)\m(?:.fo){e<=2}` over `'x fx'` is judged to ledger
entry 12, the doubled `END_FUZZY` backtrack guard, and pinned rather than fixed. It is the first row
of that family measured from BOTH sides: delete the term from an upstream build and upstream moves to
this port's answer, restore it here and this port moves to upstream's
(`tools/probes/s57e-double-count-moves-the-insertion.py`). `fuzzy-anchored` is back on the default
generator list with both hold-out paragraphs gone; it drew three more rows of the same family at once
and the slice's own negative control drew a fourth. The pin's second arm holds this port's judged
answer by exact string, because "cheaper than upstream" also describes a port that lost count of its
errors. Four blind passes, six findings, all six reproduced and fixed, the fourth clean.

**Measured green on this commit:** ported suite 6531/6531, ratchet GREEN, the `fuzzy-anchored`
2000-row wave GREEN at seeds 1234567, 7, 4242 and 271828, the default wave GREEN at its three seeds.
The `-Count 6000` gate is GREEN at seeds 7 and 4242 and RED at 20260922 on ten rows S57e neither
caused nor fixed. They are **S57f**, which sorts ahead of S60b and is the next slice.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** One inherited ledger entry is
still reproduced: **S61 item 7** is entry 18. Entry 17 is the owner's decision rather than a slice.

**Environment:** a wedged VBCSCompiler (PID 39948) holds
`src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json`. Release builds run with
`$env:IntermediateOutputPath = 'obj/Release/net10.0-<slice>/'`. Killing it is not authorised.

**Maintenance:** `check-ratchet.ps1:105` writes the upstream-commit line wrongly when there is no submodule;
`run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a reversed row whose lookahead
reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`,
then Pages > Source = GitHub Actions).
