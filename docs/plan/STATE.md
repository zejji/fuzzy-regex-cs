# State

**S57b is CHECKPOINTED after sitting 5 (2026-09-21). Nothing is in progress. Take S57b again.**
Sitting 5 pinned the last two gate rows. Row 72790 is a new family,
`reversed-body-change-lands-where-the-lookahead-reached`, with two gap tests and drafted ledger
entry 26. Row 74947 went into the existing `bestmatch-loses-a-partial` as its tenth row, with a gap
test and a paragraph in ledger entry 13 - sitting 3's proposed `reversed-skip-missing-match` family
was wrong, and `notes/S57b-sittings.md`, "Sitting 5", says why.

**The default gate is GREEN at all three seeds** (`-Count 6000`, 2026-09-21): seeds 7, 4242 and
20260921, 0 of 126,080 rows each. Run one seed at a time; three together exceed the blocking cap.

**The extra wave is RED and its ten rows are the next sitting's batch.**
`pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions -Seeds 99991,57057`:
four rows at 99991 (683 `fullmatch`, 9204 `sub`, 9720 `split`, 9951 `finditer-overlapped`) and six
at 57057. Reports and waves are under `TestResults/oracle/`. Judge them in one pass with
`python tools/probes/gate-divergence-doors.py <seed>`, as sitting 3 did for the gate, then close the
slice. Note the gate's third seed is the RUN DATE (`run-oracle.ps1:256`), so a sitting that
re-records on a new day draws a fresh unjudged sample; the three ways out are at the end of the
sitting-3 notes and the choice is the owner's.

**Sittings 3, 4 and 5 are reviewed.** One reviewer, the verifier, and a second blind pass over the
fixes. What each pass found and what was fixed is under "Sitting 5 / Review" in the notes. Sitting 6
reviews its own rows only.

**Sitting 6, in order.** (1) The ten rows above, scripted and judged off the output. (2) Re-run the
default gate and the extra wave, ratchet. (3) The blind review and the verifier over sitting 6's own
rows - both briefs are current (`notes/S57b-review-brief.md`, `notes/S57b-verifier-brief.md`) and
need only their scope line changed. Then close.

**Phase 6 does not close until that wave is green.** S57 stays checkpointed behind it, holding only
its items 10 and 11. Phase 7 regresses against 878 uncovered lines, 257 members, 0 wholly-unentered
opcode arms, and carries S56's caveats and queued `engine-topup-01..09` windows
(`docs/plan/mutation/2026-09-20-engine.md`). **Unfixed and unscheduled:** ledger entries 17 (in
part), 18, 19, 20, 21, 25. **Phase 7:** `tools/run-controls.py` needs a `suite` mode before S50's C,
S50b's four, S52d's A and S53b's B can be registered. **Benchmarks here** need both Stryker scripts
in `.claude/worktrees/stryker` stopped. **Owner (S73):** `docs/demo/` reference layouts, and
publishing. **Maintenance:** four comments cite `_regex.c:20535-20537`, now `:20555-20558`;
`check-ratchet.ps1:94` writes the upstream-commit line wrongly with no submodule; MAIN has a stray
`.github/workflows/pages.yml`; `_leak_free_fuzzy` answers a starved question on a reversed row whose
lookahead reads past the match end (sitting 4; it fails safe, and narrowing it wants its own slice).
**S75 DONE (2026-09-21, branch `phase9-demo`, demo worktree).** Four sittings; what each one
measured is in `docs/plan/slices/notes/S75-sittings.md`, and the closing notes are on the slice file
in `docs/plan/slices/done/`.

**What landed:** edit markers in their own row under the subject, with neighbouring errors of one
kind drawn as one mark; a legend, a per-run note and an alignment view; the line that names a fuzzy
budget with no bound; help notes on all six input headings; the `(?:colour){e<=2:[a-z]}` worked
example; snippet identifiers pinned by compiling the snippet the page writes; and the copy linter
extended over `README.md`, six documents and the public XML doc comments.

**Green at the close:** ratchet GREEN, 6,487 tests against baseline 6,379; 427 web tests;
`vue-tsc` clean; 123 Pester tests; 40 documentation examples; demo web build GREEN; WASM smoke
GREEN over 58 published endpoints.

**Take S57b next on the main line, not S57** - S57 is blocked by its own first paragraph. Phase 6's
exit gate is RED: `run-oracle.ps1 -Count 6000` diverges 3 (seed 7), 3 (4242) and 14 (20260920) over
twenty distinct rows, and the pre-S60 tree gives 3, 3 and 15 on the same command. The rows and the
commands that recreate them are in `notes/S57-sittings.md`, "Sitting 3". Phase 6 does not close
until that wave is green. S57 itself holds only items 10 and 11, bookkeeping for the Phase 7
handover.

**Five ledger entries are inherited unfixed and no slice schedules them:** 17 in part, 18, 19, 20,
21. That is three pieces of work; the slices that take them amend ROADMAP and the spec, and it is
the owner's call.

**Waiting on the owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push
`phase9-demo`, then Pages > Source = GitHub Actions).

**One environment note:** a stale vite dev server (PID 27600, port 5179) holds
`lightningcss.win32-x64-msvc.node`, so `npm ci` fails with EPERM in `demo/web` and deletes
`node_modules` on the way. `npm --prefix demo/web install --no-audit --no-fund` repairs it, then
build with `tools/build-demo-web.ps1 -SkipInstall` and smoke with `run-wasm-smoke.ps1 -SkipWebBuild`.
