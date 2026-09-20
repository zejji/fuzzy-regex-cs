# State

**S75 is CHECKPOINTED after sitting 2 (2026-09-20, branch `phase9-demo`, demo worktree).** The
sitting stopped on the allowance, not on a blocker.

**What landed in sitting 2:** items 1 and 2 complete. A `(?)` note on all six input headings
(`HeadingHelp.vue`, `lib/help-notes.ts`), the line under the pattern when a fuzzy budget has no
bound (`lib/budget.ts`), the legend under the subject, the note naming where one marked run was
spent, and the alignment view - the selected match a cell per character, each labelled with its
position and kind (`lib/alignment.ts`). Sitting 1 delivered the marker row itself.

**Sitting 3 picks up at item 3**, whose engine numbers are already measured and quoted in
`notes/S75-sittings.md`: write the example, then items 4 and 5, then the blind review and the
verifier over BOTH sittings, then the reference screenshots. Two things for the owner, both
recorded in the notes: the spec's named-lists wording describes a box that parses one list per
line, and the alignment view has no pattern row because `fuzzy_changes` carries no pattern-side
information.

**Green at the checkpoint:** 363 web tests, `vue-tsc` clean, the copy linter included. No C# changed
in this sitting, so the ratchet stands where sitting 1 left it: GREEN, 6,480 tests, baseline 6,372.

**Take S57b next on the main line, not S57** - S57 is blocked by its own first paragraph. Phase 6's
exit gate is RED: `run-oracle.ps1 -Count 6000` diverges 3 (seed 7), 3 (4242), 14 (20260920), twenty
distinct rows, and the pre-S60 tree gives 3, 3, 15 on the same command. Rows and recreate commands:
`notes/S57-sittings.md`, "Sitting 3". Phase 6 does not close until that wave is green. S57 then
holds only items 10 and 11, the bookkeeping and the Phase 7 handover.

**Five ledger entries are inherited and unfixed, and no slice schedules them:** 17 in part, 18, 19,
20, 21. Three pieces of work; slices for them amend ROADMAP and the spec, so it is the owner's call.

**Waiting on the owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push
`phase9-demo`, Pages > Source = GitHub Actions).
