# Operating the port

How to start, pause and resume the autonomous port. Written for the project owner; agents follow
the `port-slice` skill, not this file.

**Phase 0 is complete (2026-08-29).** Everything below is live: the driver, the skills, the
ratchet and the budget gate all exist and have been exercised. The "starting the port" section
is kept as the record of how it was bootstrapped.

## One-time prerequisites

- .NET 10 SDK (version pinned in `global.json`).
- **PowerShell 7 (`pwsh`)**, not the Windows-bundled PowerShell 5.1. Every script under
  `tools/` runs on it, and `PortTools.psm1` uses null-conditional and ternary syntax that
  5.1 cannot even parse - `Import-Module ./tools/PortTools.psm1` under 5.1 fails at
  `PortTools.psm1:387`. The `pre-push` hook calls `check-ratchet.ps1` through `pwsh`, so
  without it every push fails. `winget install Microsoft.PowerShell`.
- Python 3.12+ with `pip install regex` (the differential oracle).
  - No C compiler is needed locally today. The PyPI release and the pinned upstream commit
    differ only in version strings, so they behave identically, and CI builds the oracle from
    `upstream/` for the exact-match check. If a future sync pins a commit that genuinely differs
    from a published release, you will need MSVC Build Tools with the C++ workload to run
    `pip install ./upstream` on Windows; the `sync-upstream` skill covers it.
- `git submodule update --init` after cloning.
- `dotnet tool restore` then `dotnet husky install` after cloning, which pins CSharpier
  and Husky.Net locally and writes the `pre-commit` (formatting) and `pre-push`
  (`check-ratchet.ps1`) hooks. Neither hook is a gate - CI is - but they catch drift early.

## Everyday commands

```powershell
tools/check-ratchet.ps1                  # suite + parity ratchet + regenerate docs/STATUS.md
tools/check-ratchet.ps1 -UpdateBaseline  # record the new passing set, once it is GREEN
tools/check-ratchet.ps1 -AcceptRemovals  # a baselined test was renamed or deliberately deleted
tools/run-tool-tests.ps1                 # Pester tests for the tooling itself
tools/run-slices.ps1 -DryRun             # what the driver would do next, without spending anything
dotnet csharpier format .                # formatting; CI fails the build on any drift
```

The ratchet goes red when a baselined test disappears from the run, which is what a rename looks
like from the outside. `-AcceptRemovals` is the sanctioned way through, and it lists every test
it let go. Never hand-edit `tests/parity-baseline.json`.

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

Budget is configured in `docs/plan/budget.json`; the driver reads it before every slice, so
edits take effect immediately without stopping it.

Two different jobs in that file, and it matters which you reach for:

- **Slice caps per day and week are the rationing.** They are exact - the driver counts its own
  sessions in `docs/plan/slice-log.jsonl`. Turn these down when you need more allowance for
  other work.
- **Token caps are a circuit breaker**, set well above typical usage so they only catch a
  runaway. They count every Claude Code session on the machine, not just the port's.

There is no live "percentage of plan used" check. Phase 0 established that nothing local reports
it: the stats cache lags by weeks, a rate-limit record only appears after a request has already
been refused, and the claude.ai usage page needs a browser the unattended driver has not got.
The driver does back off while a recorded rate-limit reset is still in the future.

After Phase 1, recalibrate: `docs/plan/slice-log.jsonl` will hold what each slice actually cost.

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
6. **Review with proof:** the blind reviewer must hand over a reproduction - the command and its
   output, or a failing test. Prose rationale, style nits and speculative rewrites are rejected
   unread. One pass per *unreviewed change*, which is not the same as one pass per slice: if
   fixing the findings adds public API or touches tooling, that delta has not been reviewed and
   gets its own pass. Critique loops (reviewer opines, code changes, reviewer opines again) are
   banned; repairing against a red ratchet or an oracle divergence is not a critique loop and is
   capped at two rounds. Rules and evidence: `docs/VERIFICATION.md`; full reasoning: design spec
   section 8 and amendment 9.
7. **Phase-boundary human gates:** you re-plan with real data roughly every 1-3 weeks; the
   machine never runs months unattended.
8. **Budget gate before every slice:** the project cannot eat allowance reserved for other work.
9. **Oracle differential testing, from the first executing slice:** an independent ground truth
   (Python `regex` itself) catches what ported tests structurally cannot. Passing upstream's own
   suite is evidence of parity, not proof - see amendment 10. The harness is stood up at the start
   of Phase 3 and run locally in every engine slice; the scheduled CI job stays off the merge
   path.
10. **Upstream pinned:** the port targets a fixed SHA; upstream churn cannot destabilise it.

## Files that matter when you glance at the project

| File | What it tells you |
|---|---|
| `docs/STATUS.md` (generated) | parity % per feature area - the real progress bar |
| `docs/plan/STATE.md` | current slice, blockers, next action |
| `docs/plan/slices/` vs `slices/done/` | remaining vs completed work |
| `docs/plan/DECISIONS.md` | dated one-line decision log |
| `git log --oneline` | one commit per slice, readable history |
