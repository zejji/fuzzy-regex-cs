# State

**S57b is CHECKPOINTED after sitting 1 (2026-09-20). Nothing is in progress. Take S57b again.**
Sitting 1 built the instruments and the evidence; **no row is judged yet**. The twenty red gate rows
are captured in `tools/probes/s57b-gate-rows.jsonl` and replay at **diverge 20 of 20** through
`run-oracle.ps1 -Rows`. Per-row table and next steps: `notes/S57b-sittings.md`, "Sitting 1".

**The finding that carries the slice.** Dating each row at every engine commit since `b678aeb` (by
replaying the rows, not re-running a wave - see DECISIONS) splits the twenty: **rows 3, 4, 5, 6 and
15 are regressions introduced by `30e0178` (S52d)**, the other fifteen are inherited. All five are
reversed `partial-sliced` rows with a non-zero `pos` where **upstream answers a match too**, so
`reversed-partial-runs-out-at-the-slice-start` cannot cover them: its predicate needs
`row.Expected is NoMatchOutcome`. S52d settled *whether* a partial is reported there; these are
about *what span*. It did **not** drop the `PartialSide` guard: `RanOutOnTheLeft` carries it.

**Sitting 2, in order:** put the empty-span hypothesis to upstream, fix those five test-first; write
the other fifteen verdicts into `ExpectedDivergences.cs` in one batch; minimise rows 10 and 11
(upstream's `IndexError` in `get_firstset`) into a drafted ledger entry; re-run the gate at `-Count
6000` on three seeds plus `-Generator fuzzy,interactions -Seeds 99991,57057`.

**Phase 6 does not close until that wave is green.** S57 stays checkpointed behind it, holding only
its items 10 and 11. Phase 7 regresses against 878 uncovered lines, 257 members, 0 wholly-unentered
opcode arms, and carries S56's caveats and queued `engine-topup-01..09` windows
(`docs/plan/mutation/2026-09-20-engine.md`).
**Unfixed and unscheduled:** ledger entries 17 (in part), 18, 19, 20, 21 - three pieces of work, and
a slice for any of them amends ROADMAP and the spec, so it is the owner's call. **Phase 7:**
`tools/run-controls.py` needs a `suite` mode before S50's C, S50b's four, S52d's A and S53b's B can
be registered. **Benchmarks here** need both Stryker scripts in `.claude/worktrees/stryker` stopped.
**Owner (S73):** `docs/demo/` reference layouts, and publishing. **Maintenance:** four comments cite
`_regex.c:20535-20537`, now `:20555-20558`; `check-ratchet.ps1:94` writes the upstream-commit line
wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`.
