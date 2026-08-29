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

- [x] `dotnet tool restore` then `dotnet husky install` gives a working `pre-commit` and
      `pre-push` in a fresh clone.
- [x] The whole tree is CSharpier-clean, in its own commit, and `dotnet build` is still clean
      with zero warnings.
- [x] A deliberately mis-formatted staged file is fixed by the hook; a deliberately failing test
      does **not** block a commit but does block a push.
- [x] CI fails on unformatted code.
- [x] `tools/check-ratchet.ps1` GREEN. The formatting pass must not change a single test id, so
      the baseline must not move - if it does, something is wrong.
- [x] Slice file moved to `done/` with closing notes; STATE.md rewritten; DECISIONS.md gains the
      pre-commit-formatting-only decision and the Parsing/Engine call.

---

## Closing notes (2026-08-29)

**Landed.** CSharpier 1.3.0 and Husky.Net 0.9.1 pinned in `.config/dotnet-tools.json`; `pre-commit`
(formatting) and `pre-push` (`check-ratchet.ps1`) hooks; a `Formatting must be clean` step in
`ci.yml`; the whole tree formatted in its own commit; bootstrap lines in `AGENTS.md` and
`OPERATIONS.md`.

**The two things the slice said to prove, and what proving them showed.**

1. *CSharpier's output must survive the analyzers.* It does. `dotnet build --configuration Release
   --no-incremental` after formatting the whole tree: **0 Warning(s), 0 Error(s)**. Nothing had to
   be suppressed.
2. *What CSharpier does to `Parsing/` and `Engine/`.* Neither directory exists yet - S01 wrote only
   the root API stub - so there was no real ported file to look at. Rather than defer, the shapes
   those ports will take were probed directly: a dispatch dictionary, an opcode-name table, a
   `case`-per-opcode switch, an if/elif chain, and upstream reference lines in comments. CSharpier
   reorders nothing and touches no comment; it only rewraps. **Decision: format them like
   everything else, no ignore entry.** `// csharpier-ignore` is the escape hatch for any specific
   table whose grid must survive, and it was verified to hold.

**Surprises.**

- The .NET 10 SDK's `dotnet new tool-manifest` writes `dotnet-tools.json` at the repository root,
  not `.config/dotnet-tools.json`. Both resolve; it was moved to `.config/`.
- CSharpier 1.3.0 formats MSBuild XML as well as C#. The entire effect on this repo is deleting the
  blank lines just inside `<Project>` in seven project files. Accepted rather than ignored.
- No `.csharpierrc` and no `.csharpierignore` were needed. CSharpier reads `indent_style`,
  `indent_size` and `max_line_length` straight from `.editorconfig` (proven: a 112-character line
  wraps at the default `printWidth` of 100 and survives once `max_line_length = 120` is present),
  and it already skips `obj`, `.git`, `*.g.cs` and everything in `.gitignore`. `upstream/` holds no
  C# at all.
- The hooks are plain shell rather than `task-runner.json` tasks: pre-push is one `pwsh` line, and
  pre-commit needs a `git add` after formatting, which the task runner cannot express.
  `task-runner.json` is committed with an empty task list purely because `dotnet husky install`
  recreates it when it is missing.

**For the next slice.**

- Run `dotnet tool restore` then `dotnet husky install` once per clone. `.husky/_/` is gitignored by
  Husky itself, so without `install` the hooks are inert.
- The pre-commit hook **refuses** a commit if a staged `.cs` file also carries unstaged edits,
  because the `git add` it does after formatting would otherwise sweep them in. Stage fully, or run
  `dotnet csharpier format .` first. The slice driver stages everything, so it never meets this.
- S02-S05 add ~1,544 test methods. Write them formatted (`dotnet csharpier format .` before
  committing) or the hook will do it and the diff will be noisier than it needs to be.
- The ratchet baseline did not move: 35 tests, 34 passing, GREEN both before and after the
  formatting pass, exactly as the done-criteria required.

**Review pass.** Two findings, both reproduced before anything changed, both real.

1. `.husky/pre-commit` word-split a filename containing a space when listing partially staged
   files, so the message named the wrong file - the one thing that message exists to do.
   `src/FuzzyRegex/file one.cs` printed as two lines. Quoted, and re-verified against the same
   repro.
2. The `pre-push` hook calls `pwsh`, which an out-of-the-box Windows 11 does not have, and no file
   in the repo said PowerShell 7 was needed. Deliberately **not** fixed with a fallback to
   PowerShell 5.1: 5.1 cannot run this tooling at all - `Import-Module ./tools/PortTools.psm1`
   under 5.1 dies parsing line 387, which uses null-conditional and ternary syntax. The
   requirement predates this slice and was undocumented; the hook is just what made it bite. Now a
   prerequisite in `OPERATIONS.md` and `AGENTS.md`.

If a fresh clone ever hits that the confusing way, a `#Requires -Version 7.0` at the top of
`check-ratchet.ps1` would turn the parse error into a clear message. Left out here as a change to
a tool file outside this slice's scope.
