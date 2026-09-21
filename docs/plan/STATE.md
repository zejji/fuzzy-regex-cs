# State

**S57c is DONE (2026-09-21).** Ledger entries 19 and 20 (upstream issues 563/564) fixed:
`Optimiser.FindAnchorGuards` / `PatternObject.AnchorGuards` / `Matcher.AnchorIsPinned`, compile-time
route, with the one-step-on narrowing S50 had proved necessary. Both inherited pins in
`InheritedIssueTests.cs` inverted. Ratchet GREEN (6509/6509, baseline 6401), default wave and
`-Count 6000` wave GREEN at three seeds, blind review clean, independent verifier confirmed all
nine judged-row claims. Record: `docs/plan/slices/done/S57c-*.md`,
`docs/plan/slices/notes/S57c-sittings.md`. S50's own test-row figure (51, 52, 54, 56) did not
survive re-measurement - actually 51 and 56, corrected in `Matcher.cs` and `ExpectedDivergences.cs`.

**Next slice is S57d** (`docs/plan/slices/S57d-hitend-and-the-partial-that-was-denied.md`), entry 21.

**S57c's new `fuzzy-anchored` generator found an unjudged divergence** at seed 1234567 -
`(?b)(?r)\m(?:.fo){e<=2}` over `'x fx'`, same span and errors, different insertion position. Not
S57c's rule (under `(?r)` the leading `\m` is not at the head of the reversed graph, so its anchor
guards are empty). That is **S57e**, spec amendment 36, no slice file yet. `fuzzy-anchored` stays
off `run-oracle.ps1`'s default generator list until S57e judges it.

**Phase 6's four gate items are green, Phase 6 is NOT closed.** Remaining: **S57d** is entry 21,
**S61 item 7** is entry 18; entry 17 is the owner's decision, not a slice.

**Maintenance:** stale citations in `gate-divergence-doors.py:137` (now `record-oracle.py:1338`)
and four `_regex.c:20535-20537` comments (now `:20555-20558`); `check-ratchet.ps1:94` writes the
upstream-commit line wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`;
`run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a reversed row whose lookahead
reads past the match end (S57b sitting 4).

**Owner (S73):** `docs/demo/` reference layouts; publish (push `phase9-demo`, Pages > Source =
GitHub Actions). **Environment:** stale vite dev server (PID 27600, port 5179) holds
`lightningcss.win32-x64-msvc.node`; repair `demo/web` EPERM with `npm --prefix demo/web install
--no-audit --no-fund`.
