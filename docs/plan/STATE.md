# State

**S57b is DONE (2026-09-21, six sittings). Nothing is in progress. Take S57 next**, now unblocked
and holding only its items 10 and 11: Phase 6's bookkeeping, the measured rate, the CHANGELOG entry
and the Phase 7 handover. Sitting 6 judged the extra wave's last ten rows into four existing
`ExpectedDivergences` entries; detail in `notes/S57b-sittings.md`.

**Phase 6's exit gate is GREEN, 2026-09-21.** `run-oracle.ps1 -Count 6000` at seeds 7, 4242 and
20260921: 0 of 126,080 rows each. The extra wave, `-Count 6000 -Generator fuzzy,interactions
-Seeds 99991,57057`: GREEN at both. Run one seed at a time; three together exceed the blocking cap.
Ratchet GREEN, 6503 tests, baseline 6395. The third seed is the RUN DATE (`run-oracle.ps1:256`), so
re-recording on a new day draws a fresh sample and the gate can be red tomorrow on rows nobody has
seen - that is the gate working, and S57b's closing notes say how to judge such a batch in one pass.

**Phase 7** regresses against 878 uncovered lines, 257 members, 0 wholly-unentered opcode arms, and
carries S56's caveats and the queued `engine-topup-01..09` windows
(`docs/plan/mutation/2026-09-20-engine.md`). `tools/run-controls.py` needs a `suite` mode before
S50's C, S50b's four, S52d's A and S53b's B can be registered. Benchmarks here need both Stryker
scripts in `.claude/worktrees/stryker` stopped. **Unfixed and unscheduled ledger entries:** 17 (in
part), 18, 19, 20, 21, 25; 17 to 21 are three pieces of work whose slices amend ROADMAP and the
spec, and that is the owner's call.

**Maintenance:** stale citations in `gate-divergence-doors.py:137` (now `record-oracle.py:1338`)
and in four comments citing `_regex.c:20535-20537` (now `:20555-20558`); `check-ratchet.ps1:94`
writes the upstream-commit line wrongly with no submodule; MAIN has a stray
`.github/workflows/pages.yml`; `_leak_free_fuzzy` answers a starved question on a reversed row
whose lookahead reads past the match end (S57b sitting 4; fails safe, wants its own slice).

**Owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push `phase9-demo`,
then Pages > Source = GitHub Actions). **Environment:** a stale vite dev server (PID 27600, port
5179) holds `lightningcss.win32-x64-msvc.node`, so `npm ci` fails with EPERM in `demo/web`;
`npm --prefix demo/web install --no-audit --no-fund` repairs it, then `build-demo-web.ps1
-SkipInstall` and `run-wasm-smoke.ps1 -SkipWebBuild`.
