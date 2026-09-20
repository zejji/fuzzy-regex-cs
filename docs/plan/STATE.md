# State

**S75 is CHECKPOINTED after sitting 1 (2026-09-20, branch `phase9-demo`, demo worktree).** The two
commits before it, `e3f47e2` and `7c2b294`, were the owner's edits to the spec; sitting 1 is the
first code.

**What landed:** item 1 in part. Neighbouring errors of one kind are one mark with one letter, where
they were a mark per character. Deletions that stack in one place are one gap carrying a count
(`data-count`, drawn as CSS `content`, so copying the subject still copies the subject), and the
marker row sits under a taller line that only opens on a result with markers. Measured in a real
browser at 1366x768 and 390x844: the letter's top was 2.8 px above the bottom of the highlight's
border and is now 2.2 px clear of it, with 10.2 px to the line below. Measuring found two faults no
test caught - a counted label that wrapped and drew back through the border, and two labels
overlapping by 6 px. Both fixed and pinned. Evidence, re-run commands and the engine table:
`docs/plan/slices/notes/S75-sittings.md`.

**Sitting 2 picks up:** the rest of item 1 - the legend under the subject, the note on
hover/focus/tap naming the exact position, the unbounded-budget line under the pattern, and the
letter-by-letter alignment view - then items 2 to 5 untouched. `markers` in `demo.ts` is already the
condition the legend needs. Generalise the note mechanism out of `flags.ts` first: item 2's six
heading notes want the same thing, and the spec asks for one help mechanism rather than three. No
reference screenshots are committed yet, because they are meant to show the legend and the
alignment view.

**Ratchet GREEN**, 6,480 tests passing, baseline 6,372; 309 web tests, typecheck clean.

**Take S57b next on the main line, not S57** - S57 is blocked by its own first paragraph. Phase 6's
exit gate is RED: `run-oracle.ps1 -Count 6000` diverges 3 (seed 7), 3 (4242), 14 (20260920), twenty
distinct rows, and the pre-S60 tree gives 3, 3, 15 on the same command. Rows and recreate commands:
`notes/S57-sittings.md`, "Sitting 3". Phase 6 does not close until that wave is green. S57 then
holds only items 10 and 11, the bookkeeping and the Phase 7 handover.

**Five ledger entries are inherited and unfixed, and no slice schedules them:** 17 in part, 18, 19,
20, 21. Three pieces of work; slices for them amend ROADMAP and the spec, so it is the owner's call.

**Waiting on the owner, both from S73:** the `docs/demo/` reference layouts, and publishing (push
`phase9-demo`, Pages > Source = GitHub Actions).
