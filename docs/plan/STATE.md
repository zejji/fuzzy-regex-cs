# State

**S57b is CHECKPOINTED after sitting 2 (2026-09-20). Nothing is in progress. Take S57b again.**
Sitting 2 judged **fourteen** of the twenty red gate rows. Seed 4242 is now GREEN, seed 7 is at 2
diverging and seed 20260920 at 4. Per-row table and evidence: `notes/S57b-sittings.md`.

**Landed this sitting.** Rows 3, 4, 5, 6, 15 (the S52d regressions) under the new
`reversed-partial-answers-the-cut-subject`, with six permanent tests and a new recorded second fact
`cutSubjectOutcome`. Rows 16, 17 under `turkic-default-folding-without-spans`; 18, 20 under
`bestmatch-loses-a-candidate`; 9, 12, 13 under `search-start-partial`; 14 under
`partial-retry-reversed-slice`; 19 under a new `enhancematch-loses-a-candidate` plus ledger entry 25.
Row 107758 was re-judged off `turkic-default-folding` by the isolating control it demands.

**Sitting 3, in order.** (1) Judge the four still red: seed 20260920 rows **72433** (upstream spends
one substitution where this port spends two, same span and groups, no ranking flag) and **74399**
(`finditer-overlapped`, upstream 4 matches to this port's 3), and seed 7 rows **75921** (upstream's
counts disagree with its own change-list lengths; candidate
`fuzzy-changes-of-the-wrong-kind-for-their-own-counts`) and **76160** (a `(*SKIP)` split, not
Turkic - the U+01F0 swap leaves it standing). (2) Minimise rows 10 and 11, seed 20260920 rows 81232
and 87091, where upstream raises `IndexError: tuple index out of range` while COMPILING, into a
drafted ledger entry; they cannot be oracle pins. (3) Re-record and re-run the gate at `-Count 6000`
on three seeds plus `-Generator fuzzy,interactions -Seeds 99991,57057`, **re-measure the cut-subject
door figures** (831 named / 777 SAME / 54 DIFFERENT were measured before the re-record, and the new
entry's Reason quotes them), then ratchet, blind review, verifier, and close.

**Phase 6 does not close until that wave is green.** S57 stays checkpointed behind it, holding only
its items 10 and 11. Phase 7 regresses against 878 uncovered lines, 257 members, 0 wholly-unentered
opcode arms, and carries S56's caveats and queued `engine-topup-01..09` windows
(`docs/plan/mutation/2026-09-20-engine.md`).
**Unfixed and unscheduled:** ledger entries 17 (in part), 18, 19, 20, 21, 25 - three pieces of work,
and a slice for any of them amends ROADMAP and the spec, so it is the owner's call. **Phase 7:**
`tools/run-controls.py` needs a `suite` mode before S50's C, S50b's four, S52d's A and S53b's B can
be registered. **Benchmarks here** need both Stryker scripts in `.claude/worktrees/stryker` stopped.
**Owner (S73):** `docs/demo/` reference layouts, and publishing. **Maintenance:** four comments cite
`_regex.c:20535-20537`, now `:20555-20558`; `check-ratchet.ps1:94` writes the upstream-commit line
wrongly with no submodule; MAIN has a stray `.github/workflows/pages.yml`.
