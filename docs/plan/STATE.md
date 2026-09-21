# State

**S57b is CHECKPOINTED after sitting 4 (2026-09-21). Nothing is in progress. Take S57b again.**
Sitting 4 judged row 72790, the one row sitting 3 thought might be a defect on THIS side. It is
not: this port is right and upstream misplaces the change. The evidence and both killed hypotheses
are in `notes/S57b-sittings.md`, "Sitting 4". No pin was written - see item (1) below for why.

**The gate at `-Count 6000`, as re-recorded on 2026-09-21.** Seed 7 GREEN, 0 of 126,080. Seed 4242
GREEN, 0 of 126,080. Seed 20260921 RED, 2 of 126,080 - rows 72790 and 74947, both unpinned. The
third default seed is the RUN DATE (`run-oracle.ps1:256`), so a sitting that re-records on a new day
draws a sample nobody has judged. That structural problem is one question for the owner, with three
ways out, written up at the end of the sitting-3 notes.

**Sitting 5, in order.** (1) **Write row 72790's pin**: a new family, keyed on the row and on this
port's `Describe()` string, `Reason` carrying the minimised table from the sitting-4 notes, a gap
test with its provenance, and a drafted ledger entry for the upstream defect. It was left unwritten
deliberately: the blind review and the amendment-16 verifier have not reported since sitting 2, so
pinning now would deepen that debt rather than settle it. (2) Row **74947** needs its own entry and
gap test, as sitting 3 left it. (3) Re-record, gate on three seeds plus
`-Generator fuzzy,interactions -Seeds 99991,57057`, ratchet. (4) **One blind review and one verifier
pass over sittings 3, 4 and 5 together** - the filled briefs are kept
(`notes/S57b-review-brief.md`, `notes/S57b-verifier-brief.md`) and need their scope widened to the
commits since 99d9294. Until a pass comes back clean, sitting 3's pins are unreviewed. Then close.

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
`.github/workflows/pages.yml`. **Also noted by sitting 4:** `_leak_free_fuzzy` answers a starved
question on a reversed row whose lookahead reads past the match end; it fails safe, and narrowing it
is a change to a live recorded fact that wants its own slice.
