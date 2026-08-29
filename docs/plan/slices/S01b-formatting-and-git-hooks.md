---
slice: S01b
phase: 1
title: CSharpier formatting and a pre-commit hook
delivers: []
---

# S01b - CSharpier formatting and a pre-commit hook

## Why this exists, and why now

Requested by the project owner, 2026-08-29. Formatting drifts when several sessions and several
models write code into the same repository, and drift shows up as noise in every future diff -
including the upstream-sync diffs this port depends on being readable.

It is numbered `S01b` rather than `S06` so the driver picks it up **before** S02. That is the
whole point of the timing: S02-S05 add roughly 1,544 test methods across dozens of files.
Formatting them as they are written costs nothing; reformatting them afterwards is a
several-thousand-line commit that buries the ratchet's own signal.

## Decision: what the hooks run

Asked by the owner, decided here.

- **pre-commit: formatting only.** CSharpier over the staged files, nothing else.
- **pre-push: `tools/check-ratchet.ps1`.** This is the natural place for the suite.
- **CI stays the real gate.** Three OS legs plus the ratchet, exactly as now.

**Tests do not run on pre-commit.** Four reasons, in order of how much they matter here:

1. `tools/run-slices.ps1` commits automatically at the end of every slice, and the slice has just
   run `check-ratchet.ps1`, which runs the whole suite. A test-running pre-commit hook would run
   it a second time, seconds later, for no new information - doubling the test time of every
   unattended slice and eating the token and wall-clock budget the gate in `budget.json` exists
   to protect.
2. A hook people wait on is a hook people bypass. `--no-verify` is one flag, and the suite is
   heading for ~1,544 tests plus an oracle. A pre-commit hook has to stay in the low seconds to
   survive.
3. A local hook is not a gate and cannot be made into one: it is absent in a fresh clone until
   someone runs `dotnet husky install`, and it is one flag away from being skipped. Treating it
   as a safety net invites relying on it. The ratchet in CI is the thing that actually cannot be
   bypassed.
4. Commits inside a slice are work-in-progress by design. Failing tests mid-slice are the normal
   state during TDD - that is the point of writing the failing test first. A hook that refuses
   those commits fights the workflow the whole repository is built around.

Pre-push is different: it is the last moment before work leaves the machine, it happens far less
often, and waiting there is proportionate.

## Scope

- `dotnet new tool-manifest`, then pin **CSharpier** and **Husky.Net** as local tools in
  `.config/dotnet-tools.json`. Neither exists in the repo yet - checked 2026-08-29.
- `dotnet husky install`, then two hooks:
  - `pre-commit` - CSharpier over staged C# files.
  - `pre-push` - `tools/check-ratchet.ps1`.
- A `.csharpierrc` (or the equivalent current config file) matching the `.editorconfig` settings
  CSharpier honours: 4-space indent, 120-column print width, LF endings.
- A `.csharpierignore` covering `upstream/`, `obj/`, `bin/`, `TestResults/` and any generated
  Unicode tables.
- One formatting pass over the existing tree, as its **own commit**, separate from the tooling
  commit, so the tooling commit stays reviewable.
- A CI check that formatting is clean (`csharpier check` or current equivalent), so the hook
  being bypassable stops mattering.
- A line in `AGENTS.md` and in `docs/plan/OPERATIONS.md` telling a fresh clone to run
  `dotnet tool restore && dotnet husky install`.

## Two things to prove before wiring anything up

Both are real risks, not hypotheticals, and both are cheap to check. If either fails, stop and
write it into STATE.md rather than forcing it through.

1. **CSharpier's output must survive this repo's analyzers.** `Directory.Build.props` sets
   `EnforceCodeStyleInBuild=true` and `TreatWarningsAsErrors=true`, and `.editorconfig` carries
   style rules at `warning`. CSharpier is deliberately opinionated and ignores most
   `.editorconfig` style keys. If its output trips IDE0055 or any other style rule, the hook
   would cheerfully produce commits that cannot build. **Run CSharpier over the whole tree and
   then `dotnet build` before committing any hook.**
2. **Decide what CSharpier does to `src/FuzzyRegex/Parsing` and `src/FuzzyRegex/Engine`.** Those
   are hand-ports whose shape is meant to track `_regex_core.py` and `_regex.c` line for line, so
   that an upstream diff maps onto them by eye - the same reason `.editorconfig` relaxes method
   length and complexity rules there and nowhere else. A formatter that reflows them works
   against that. Look at what it actually does to a real ported file before choosing; add them to
   `.csharpierignore` if the reflow hurts. Record the decision in DECISIONS.md either way. This
   is the one genuinely contentious call in the slice.

Verify the CLI surface against current CSharpier and Husky.Net documentation rather than from
memory - both have renamed commands across versions.

## Done when

- [ ] `dotnet tool restore` then `dotnet husky install` gives a working `pre-commit` and
      `pre-push` in a fresh clone.
- [ ] The whole tree is CSharpier-clean, in its own commit, and `dotnet build` is still clean
      with zero warnings.
- [ ] A deliberately mis-formatted staged file is fixed by the hook; a deliberately failing test
      does **not** block a commit but does block a push.
- [ ] CI fails on unformatted code.
- [ ] `tools/check-ratchet.ps1` GREEN. The formatting pass must not change a single test id, so
      the baseline must not move - if it does, something is wrong.
- [ ] Slice file moved to `done/` with closing notes; STATE.md rewritten; DECISIONS.md gains the
      pre-commit-formatting-only decision and the Parsing/Engine call.
