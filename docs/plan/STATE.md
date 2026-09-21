# State

**S57b is CHECKPOINTED after sitting 3 (2026-09-21). Nothing is in progress. Take S57b again.**
Sitting 3 judged the last six of the twenty red rows, re-recorded the gate, and judged nine of the
eleven rows the new day's sample drew. Per-row evidence: `notes/S57b-sittings.md`.

**The gate at `-Count 6000`, re-recorded 2026-09-21.** Seed 7 GREEN, 0 of 126,080. Seed 4242 GREEN,
0 of 126,080. Seed 20260921 RED, 2 of 126,080 once the new pins landed, down from 11. The third
default seed is the RUN DATE (`run-oracle.ps1:256`), so any sitting that re-records draws a sample
nobody has judged; none of the eleven was a regression. That structural problem is one question for
the owner, with three ways out, written up in the notes.

**Sitting 4, in order.** (0) **The blind review and the amendment-16 verifier did not report** - both
were dispatched, the allowance hook forced this checkpoint, both were stopped. The filled briefs were
kept (`notes/S57b-review-brief.md`, `notes/S57b-verifier-brief.md`); re-dispatch them over the last
two commits. Until a pass comes back clean the pins are unreviewed. (1) Row **72790** may be a defect
on THIS side, so judge it before pinning anything: a leak here is an engine fix with a failing test
first. (2) Row **74947** needs its own entry and gap test. (3) Re-record, gate on three seeds plus
`-Generator fuzzy,interactions -Seeds 99991,57057`, ratchet, review, verifier, close. The cut-subject
door figures sitting 2 left owed were re-measured (853 named, 795 SAME, 58 DIFFERENT).

**Phase 6 does not close until that wave is green.** S57 stays checkpointed behind it, holding only
its items 10 and 11. Phase 7 regresses against 878 uncovered lines, 257 members, 0 wholly-unentered
opcode arms, and carries S56's caveats and queued `engine-topup-01..09` windows
(`docs/plan/mutation/2026-09-20-engine.md`). **Unfixed and unscheduled:** ledger entries 17 (in
part), 18, 19, 20, 21, 25 - three pieces of work, and a slice for any amends ROADMAP and the spec, so
it is the owner's call. **Phase 7:** `tools/run-controls.py` needs a `suite` mode before
S50's C, S50b's four, S52d's A and S53b's B can be registered. **Benchmarks here** need both Stryker
scripts in `.claude/worktrees/stryker` stopped. **Owner (S73):** `docs/demo/` reference layouts, and
publishing. **Maintenance:** four comments cite `_regex.c:20535-20537`, now `:20555-20558`;
`check-ratchet.ps1:94` writes the upstream-commit line wrongly with no submodule; MAIN has a stray
`.github/workflows/pages.yml`.
