# State

**S57c is IN FLIGHT, checkpointed green after sitting 1 (2026-09-21).** The slice file is still in
`docs/plan/slices/`; read it and `docs/plan/slices/notes/S57c-sittings.md` before anything else. The
code, the tests, the probes and the oracle entry all landed and are green; the documentation and the
review are what remain.

**Sitting 2's list, in order:** the `fuzzy-insertion-at-a-pinned-anchor` row in
`docs/DIVERGENCES.md`; the new symbols in `docs/PORTMAP.md`; ledger entries 19 and 20 marked fixed
in `docs/plan/upstream-reports/LEDGER.md`; decide the Control A second seed (see below); the blind
review with a filled-in brief; the independent verifier over the four judged rows; then the closing
notes, the `git mv` to `done/` and the five "Done when" boxes.

**Control A does not fire on the wave, and that is a finding.** With the one-step-on narrowing
removed, the ported suite goes 4 red of 6504 but `-Generator fuzzy,interactions -Count 6000 -Seeds
7` stays GREEN with the identical 36 expected rows. Sitting 2 either widens a generator to reach the
shape or records the zero at a second, unused seed. The full recipe and numbers are in the sitting
notes.

**Measured green on this commit:** ported suite 6504/6504, `OracleTests` 27/27, the default wave at
seeds 7, 4242 and 20260921, `-Count 6000` at the same three (diverge 0/0/0). Ratchet GREEN, base
6396, after `-AcceptRemovals` for the two `InheritedIssueTests` this slice renamed. If sitting 2
changes engine code, all of that is re-run before the commit.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** Five inherited ledger entries
are still reproduced here. **S57d** is entry 21, **S61 item 7** is 18; entry 17 is the owner's
decision, not a slice. S57c and S57d sort ahead of S60b.

**Maintenance:** stale citations in `gate-divergence-doors.py:137` (now `record-oracle.py:1338`) and
in four comments citing `_regex.c:20535-20537` (now `:20555-20558`); `check-ratchet.ps1:94` writes
the upstream-commit line wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`;
`run-controls.py` needs a `suite` mode; `_leak_free_fuzzy` starves a reversed row whose lookahead
reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`,
then Pages > Source = GitHub Actions). **Environment:** a stale vite dev server (PID 27600, port
5179) holds `lightningcss.win32-x64-msvc.node`, so `npm ci` fails with EPERM in `demo/web`; repair
with `npm --prefix demo/web install --no-audit --no-fund`.
