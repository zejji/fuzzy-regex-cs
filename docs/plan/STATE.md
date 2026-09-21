# State

**S57 is DONE (2026-09-21, four sittings). Nothing is in progress. Take S57c next.** It delivered the
coverage backstop, the gate walked, the CHANGELOG entry and `docs/plan/PHASE-7-HANDOVER.md`.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** The gate's other test is the
owner's rule that no known bug ships, and five inherited ledger entries are still reproduced here.
S57 scheduled them rather than re-tabling them (design spec amendment 35): **S57c** is entries 19
and 20, **S57d** is 21, **S61 item 7** is 18. Phase 6's closing bookkeeping lands with S61. S57c
and S57d sort ahead of S60b, so correctness runs before the next optimisation slice.

**Ledger entry 17 is the OWNER'S DECISION, and the only thing S57 needs from you.** Its two
remaining branch-reset orderings can only be fixed by choosing between two options upstream has left
unchosen since 2021, so a fix here would invent semantics upstream may contradict. It sits as "owner
decision pending with the evidence", which the gate allows. Entries 25 and 26 landed after the gate
table, both upstream-only and pinned, so neither joins the fix list.

**The gate numbers, 2026-09-21.** `-Count 6000` at seeds 7, 4242 and 20260921: 0 of 126,080 rows
each, one seed at a time; the extra wave green at both of its. Ratchet GREEN, 6503 tests, base 6395.

**Maintenance:** stale citations in `gate-divergence-doors.py:137` (now `record-oracle.py:1338`) and
in four comments citing `_regex.c:20535-20537` (now `:20555-20558`); `check-ratchet.ps1:94` writes
the upstream-commit line wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`;
`run-controls.py` needs a `suite` mode before seven controls register; `_leak_free_fuzzy` starves a
reversed row whose lookahead reads past the match end (S57b sitting 4).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`, then
Pages > Source = GitHub Actions). **Environment:** a stale vite dev server (PID 27600, port 5179) holds
`lightningcss.win32-x64-msvc.node`, so `npm ci` fails with EPERM in `demo/web`; repair with
`npm --prefix demo/web install --no-audit --no-fund`. Benchmarks need both Stryker scripts stopped.
