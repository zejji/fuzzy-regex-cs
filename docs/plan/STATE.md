# State

**Tree is clean and S57d is IN FLIGHT at a checkpoint** (2026-09-21, three sittings, commit
`a1b0ce1`). The hitend model is implemented and green: a word or grapheme boundary judged at the end
of the available text makes a partial match, on PCRE2's soft model with S50's two narrowings.
**The slice is not finished.** Outstanding, in order, with both control recipes:
`docs/plan/slices/notes/S57d-sittings.md`, "Still to do". In short: the `-Count 6000` gate at three
seeds, the final control re-runs, `PORTMAP.md` checked and reported either way, the blind review, and
the verifier over the one judged row (the `(?r)\b$` twin). Then S57e, which S57c created.
S57c is done: the anchor pin ships, fixing inherited upstream issues 563 and 564 (ledger entries
19 and 20).

**S57e exists because of a hold-out.** The `fuzzy-anchored` generator is off the default list in
`tools/run-oracle.ps1`, which is what every gate runs, because it is red at seed 1234567 on a row
where `(?b)(?r)` moves the recorded insertion position. It stays on `record-oracle.py`'s `GENERATORS`,
so `-Generator fuzzy-anchored` still works. S57e judges the row under amendment 16 and deletes the
hold-out paragraph in each tool. Design spec amendment 36 and the ROADMAP paragraph record it.

**Measured green on this commit:** ported suite 6528/6528, ratchet GREEN, the default oracle wave
GREEN at seeds 7, 4242 and 20260921. The `-Count 6000` gate was green at 7 and 4242 before the last
predicate fix and needs re-running at all three.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** With entries 19 and 20 fixed here,
three inherited ledger entries are still reproduced: **S57d** is entry 21, **S61 item 7** is 18, and
entry 17 is the owner's decision rather than a slice. S57d and S57e sort ahead of S60b.

**Maintenance:** three files cite the POSIX `fuzzy_changes` guard as `record-oracle.py:1019`, now
`:1338` (`gate-divergence-doors.py:134`, `upstream-posix-fuzzy-changes-crash.py:33`, `LEDGER.md:1212`);
`check-ratchet.ps1:105` writes the upstream-commit line wrongly when there is no submodule;
`run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a reversed row whose lookahead
reads past the match end (S57b sitting 4). The `pages.yml` item is closed: the one tracked copy
reached `main` through the `phase9-demo` merge, which is S71's deliverable.

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`, then
Pages > Source = GitHub Actions). **Environment:** if `npm ci` fails with EPERM in `demo/web`, a vite
dev server holds `lightningcss.win32-x64-msvc.node`; repair with
`npm --prefix demo/web install --no-audit --no-fund`.
