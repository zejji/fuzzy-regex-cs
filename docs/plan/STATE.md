# State

**Tree is clean and S57d is DONE** (2026-09-22, four sittings). A word or grapheme boundary judged at
the end of the available text now makes a partial match under `partial: true`, on PCRE2's soft model
with both of S50's narrowings: ledger entry 21, upstream issue 589. The closing notes in
`docs/plan/slices/done/S57d-hitend-and-the-partial-that-was-denied.md` carry both control recipes and
the review record. S57c is done too: the anchor pin ships, fixing inherited upstream issues 563 and
564 (ledger entries 19 and 20).

**Next is S57e**, which S57c created. The `fuzzy-anchored` generator is off the default list in
`tools/run-oracle.ps1`, which is what every gate runs, because it is red at seed 1234567 on a row
where `(?b)(?r)` moves the recorded insertion position. It stays on `record-oracle.py`'s `GENERATORS`,
so `-Generator fuzzy-anchored` still works. S57e judges the row under amendment 16 and deletes the
hold-out paragraph in each tool. Design spec amendment 36 and the ROADMAP paragraph record it.

**Measured green on this commit:** ported suite 6528/6528, ratchet GREEN, the default oracle wave
GREEN at seeds 7, 4242 and 20260921, and the `-Count 6000` gate GREEN at the same three seeds -
126,080 rows a seed, 0 diverging.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** With entries 19, 20 and 21 fixed,
one inherited ledger entry is still reproduced: **S61 item 7** is entry 18. Entry 17 is the owner's
decision rather than a slice. S57e sorts ahead of S60b.

**Maintenance:** three files cite the POSIX `fuzzy_changes` guard as `record-oracle.py:1019`, now
`:1338` (`gate-divergence-doors.py:134`, `upstream-posix-fuzzy-changes-crash.py:33`, `LEDGER.md:1212`);
`check-ratchet.ps1:105` writes the upstream-commit line wrongly when there is no submodule;
`run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a reversed row whose lookahead
reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`, then
Pages > Source = GitHub Actions). **Environment:** if `npm ci` fails with EPERM in `demo/web`, a vite
dev server holds `lightningcss.win32-x64-msvc.node`; repair with
`npm --prefix demo/web install --no-audit --no-fund`.
