---
slice: S69
phase: 8
title: Upstream reports filed under the owner's approval, then the 1.0 release - after both gates
delivers: []
---

# S69 - Last slice before 1.0

Two things the plan reserves for the very end. First, filing on mrab-regex: nothing is filed upstream until absolutely everything else in the plan is done (owner decision, 2026-09-12), each
ledger entry re-verified against the then-current upstream release and its text approved by the
owner before `gh` touches anything. Second, the release itself, which is a repeat of S66's
rehearsal with the real version, the tag and the nuget.org push.

**Blocked until**: the Phase 6 exit gate (mutation testing reviewed, S56 and S57 done), the Phase 7
performance gate (S63), S64-S68 done, and S71 live so the README link works. Opus. Runs on `main`,
not in a worktree.

## Scope

- **Ledger pass**: for every entry in `docs/plan/upstream-reports/LEDGER.md`, reproduce against the
  current upstream release (bump the submodule if a newer release exists, via the `sync-upstream`
  skill first); drop entries upstream has fixed, with the fixing commit recorded; draft the report
  text for the rest in `docs/plan/upstream-reports/drafts/`, one file per report, with the minimal
  reproduction in Python.
- **Owner approval**: stop and present the drafts. Nothing is filed until each is approved
  verbatim; approved ones are filed with `gh issue create` on the upstream repository and the issue
  URL recorded in the ledger.
- **Release**: follow `docs/plan/RELEASE.md` (S66) exactly: version `1.0.0`, tag, GitHub release
  with notes generated from DIVERGENCES.md SHIPPED rows and the ledger, nuget.org push with the
  owner's key, post-publish checks (package page renders the README, symbols resolve, the demo link
  works, the consumer AOT sample builds against the published package).
- **Roadmap close**: Phase 8 closing paragraph in ROADMAP.md, STATE.md rewritten for post-1.0
  (Phase 9 v2 and v3, the parked candidates).

## Verification

- Every filed issue URL resolves and matches its approved draft; the published package installs
  in a clean project and the README sample runs; the tag's `src/` matches the packed assembly's
  Source Link commit.

## Done when

- 1.0.0 is on nuget.org, the GitHub release exists, the ledger is reconciled, and the owner has
  signed off the filed reports.
