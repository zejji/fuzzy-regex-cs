---
slice: S49
phase: 6
title: Upstream issue sweep, part one - live re-triage, and a reproduction or a written dismissal for every open issue
delivers: []
---

# S49 - The issue sweep, part one

The oracle is blind to inherited bugs by construction (amendment 13), so the tracker is the
instrument. The 2026-08-31 triage of 79 issues is a stale reading of a moving list; re-triage live.

## Before launch (orchestrator)

`gh` is not in the driver's allowlist and stays out, so an unattended session cannot post. The
orchestrator snapshots the tracker before launch into `docs/plan/upstream-issues/<date>-open.json`
with `gh issue list -R mrabarnett/mrab-regex --state open --limit 500 --json
number,title,body,labels,createdAt,updatedAt,comments`, and the session works from that file.
Comments are included because the maintainer's "looks like a bug" is evidence.

## Scope

- **Re-triage every open issue** into the 2026-08-31 classes (A not a bug; B Python-specific or
  API-shape; C engine or parser bug) with a one-line reason each, in
  `docs/plan/upstream-issues/<date>-triage.md`. Diff against the old triage: closed, new, reclassed.
- **Reproduce every class C issue** against `.venvs/regex-2026.9.10` AND against this port, as a
  probe under `tools/probes/` and a test under `Gaps/`. Known from the old triage: 367 (partial
  with jointly unsatisfiable lookaheads), 425 (branch reset with mixed named and numbered groups),
  470 (done, S42), 551 (infinite loop on a V1 search), 554 (fullmatch `MemoryError` on a long
  string), 563 (`\m` with a fuzzy quantifier at position 0), 564 (loosening `<=1` to `<=2` returns
  fewer matches), 596 (`{e<=0}` 210x slowdown), and 611-614 (fixed upstream, verified by S44).
  Each lands in one of: reproduces on both (inherited, S50 fixes); reproduces upstream only (port
  right, pin and ledger); does not reproduce (dismissed with the run quoted); not a bug.
- **Resource issues (551, 554, 596)** are reproduced under `timeout` and a memory cap, with the
  numbers, not by waiting.
- Tests for inherited issues are written now, failing, and skipped with `needs:issue-<n>` so S50
  un-skips them; the ratchet stays green.

## Verification

- Triage table complete with one reason per issue; a probe and a test per class C issue; a ledger
  entry per inherited issue.

## Done when

- [ ] Every open issue classed and reasoned; every class C issue reproduced or dismissed with output.
- [ ] Failing tests written and tagged for S50; probes committed; ledger updated; nothing filed.
- [ ] Ratchet GREEN, blind review (hunt: a dismissal that reasoned instead of ran; a reproduction
      against the old interpreter), commit.
