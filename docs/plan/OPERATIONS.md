# Operating the port

How to start, pause and resume the autonomous port. Written for the project owner; agents follow
the `port-slice` skill, not this file. The driver script and skills referenced here are built in
Phase 0; until Phase 0 is complete, use the bootstrap instructions.

## One-time prerequisites

- .NET 10 SDK (version pinned in `global.json`).
- Python 3.12+ with `pip install regex` (the differential oracle; version noted in
  `docs/plan/DECISIONS.md` when first installed).
- `git submodule update --init` after cloning.

## Starting the port (first time)

Phase 0 is interactive, not autonomous - it builds the machinery everything else relies on.
Open a fresh Claude Code session in the repo root and paste:

> Read docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md and docs/plan/OPERATIONS.md.
> Execute Phase 0 exactly as specified: solution scaffolding, CI, the port-slice / port-tests /
> sync-upstream / benchmark skills, tools/run-slices.ps1, budget-gate validation, STATE.md, and
> the Phase 1 slice files. Work test-first where there is logic to test. Stop and ask before any
> deviation from the spec.

Review the Phase 0 result yourself (an hour well spent - everything downstream depends on it).

## Running autonomously (Phase 1 onward)

```powershell
tools/run-slices.ps1            # runs until budget gate or phase boundary stops it
tools/run-slices.ps1 -MaxSlices 3   # or cap explicitly
```

The driver, per slice: budget gate -> fresh `claude -p` process running the `port-slice` skill ->
verifies ratchet green + commit made -> next slice. It stops at: budget exhausted, phase boundary,
`-MaxSlices` reached, or two consecutive slice failures (slice is parked with notes in
`docs/plan/STATE.md`).

Budget is configured in `docs/plan/budget.json` (sessions per day/week, overall-usage threshold).
Adjust it whenever you need more allowance for other work; the driver reads it before every slice.

## Pausing

- **Between slices (normal case):** stop the driver (Ctrl+C) or let its caps stop it. Nothing else
  to do - every completed slice is committed with a green ratchet, so the repo is always in a
  clean, resumable state. Pausing costs nothing; days or weeks may pass.
- **Mid-slice (interrupted session):** fine too. The next session's recovery step handles it (see
  below). Never leave manual edits uncommitted on top of an interrupted slice.

## Resuming

- **Default: new session, always.** Fresh context per slice is deliberate (context degradation is
  the enemy). Run the driver again, or for a single interactive slice open a new session and paste:

  > Invoke the port-slice skill.

  The skill self-orients from STATE.md, the roadmap, the current slice file and the generated
  status - no other briefing needed, regardless of how long the pause was.

- **Recovery after an interrupted slice:** the port-slice skill begins with a recovery check: if
  the working tree is dirty or STATE.md marks a slice in-flight, it either finishes the slice to
  green or resets to the last green commit and restarts the slice. You do not need a special
  prompt.

- **Continue an existing session only when** it stopped mid-conversation waiting for *your*
  answer (a question, a design decision, a review discussion). Use `claude --continue` and answer.
  Never continue yesterday's session to start new work - start fresh.

- **Phase boundaries (human checkpoint, required):** the driver refuses to cross them. Review
  `docs/STATUS.md`, skim recent commits, then open a fresh session and paste:

  > Read docs/plan/STATE.md and the spec. Phase <N> is complete. Author the slice files for
  > phase <N+1> per the roadmap, present them for my review, and wait.

  Approve (or adjust) the slices, then restart the driver.

## Safeguards (why this doesn't descend into a mess)

1. **Parity ratchet in CI and in the driver:** no commit lands if any previously-passing test
   fails or the passing count drops. Regressions cannot accumulate silently.
2. **Atomic slices:** a slice either ends committed-and-green or is rolled back/parked. The repo
   never drifts into a half-done state.
3. **Two-failure park rule:** a slice that fails twice stops the driver and waits for a human (or
   a Fable escalation session) instead of grinding and thrashing.
4. **Derived status:** progress numbers come from the test suite via script, never from agent
   memory, so reporting cannot rot.
5. **Fresh context per slice + small fixed briefing files** (STATE.md <= 30 lines): no long-lived
   degraded context making quiet mistakes.
6. **Review with proof:** the per-slice blind reviewer must demonstrate defects with a failing
   test or reproduction; cosmetic or speculative findings are rejected. One pass, no loops.
7. **Phase-boundary human gates:** you re-plan with real data roughly every 1-3 weeks; the
   machine never runs months unattended.
8. **Budget gate before every slice:** the project cannot eat allowance reserved for other work.
9. **Oracle differential testing:** an independent ground truth (Python regex itself) catches
   what ported tests miss.
10. **Upstream pinned:** the port targets a fixed SHA; upstream churn cannot destabilise it.

## Files that matter when you glance at the project

| File | What it tells you |
|---|---|
| `docs/STATUS.md` (generated) | parity % per feature area - the real progress bar |
| `docs/plan/STATE.md` | current slice, blockers, next action |
| `docs/plan/slices/` vs `slices/done/` | remaining vs completed work |
| `docs/plan/DECISIONS.md` | dated one-line decision log |
| `git log --oneline` | one commit per slice, readable history |
