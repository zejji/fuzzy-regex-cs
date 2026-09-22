# State

**S57f is done (2026-09-22).** The ten rows the `-Count 6000` gate drew at seed 20260922 are judged
and pinned, and none is a port bug. Six joined existing entries, three are the Turkic folding family,
and row 97332 is new: upstream reports a partial `fullmatch` over `'.a'` for `(\S??)\.` that no longer
subject could complete, PCRE2 refuses it, and the port answers no match, permanently.

**Carry forward from its review:** an ablation that restores agreement does not name the mechanism on
its own - ask which engine moved. Row 76118 agrees again if `(?e)` or `(?r)` goes, but neither moves
upstream, so only POSIX does. And "inert" must be measured: 74554's outer `{1<=e<=2}` looked like
scaffolding and is not.

**The ablations are re-runnable:** `tools/probes/s57f-ablation-rows.jsonl`, 26 rows, tallied row by
row in `docs/plan/slices/notes/S57f-sittings.md`. It prints RED on purpose - rows 4, 12 and 13 are
the inert ablations and are meant to keep diverging.

**Measured at this commit:** `run-oracle.ps1 -Count 6000` GREEN at three seeds, the default wave GREEN
at three seeds, ratchet GREEN at 6540 tests. **Next:** the queue's lowest is
`S60b-search-start-and-the-researched-prefilters.md`.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** One inherited ledger entry is
still reproduced: **S61 item 7** is entry 18. Entry 17 is the owner's decision rather than a slice.

**Environment:** a wedged VBCSCompiler (PID 39948, killing it is not authorised) holds
`src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json`, so Release builds run with
`$env:IntermediateOutputPath` set - never for the ratchet, which times out under it.

**Maintenance still open:** `run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a
reversed row whose lookahead reads past the match end (S57b sitting 4). **Owner, both from S73:**
the `docs/demo/` reference layouts, and publishing (push `phase9-demo`, then Pages > GitHub Actions).
