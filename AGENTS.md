# FuzzyRegex - working notes for agents

A .NET 10 port of [mrab-regex](https://github.com/mrabarnett/mrab-regex), the Python `regex`
module. The headline feature is fuzzy (approximate) matching with per-error-type budgets, which
no .NET library provides.

**If you are here to do port work, invoke the `port-slice` skill first.** It is the workflow;
this file is only the map.

## Orientation, in order

| File | What it tells you |
|---|---|
| `docs/plan/STATE.md` | current slice, blockers, next action. Read this first. |
| `docs/plan/ROADMAP.md` | the phases |
| `docs/plan/slices/` | the pending work queue - the lowest-numbered file is next |
| `docs/STATUS.md` | **generated** parity board: what passes, what waits on what |
| `docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md` | the design, and why |
| `docs/plan/DECISIONS.md` | dated one-liners, append-only |
| `docs/PORTMAP.md` | upstream symbol to C# type - what makes syncs mechanical |

Do not read the whole spec at the start of a slice. Read STATE.md, the roadmap, your slice file
and the status board; reach for the spec when a decision needs its reasoning.

## Layout

```
src/FuzzyRegex/Parsing/     port of upstream/regex/_regex_core.py
src/FuzzyRegex/Engine/      port of upstream/src/_regex.c
src/FuzzyRegex/Unicode/     generated tables - never hand-edit
src/FuzzyRegex/             public API (port of upstream/regex/_main.py)
src/FuzzyRegex.UnicodeGenerator/   port of upstream/tools/build_regex_unicode.py
tests/FuzzyRegex.Tests/Ported/     translated upstream tests - counts towards parity
tests/FuzzyRegex.Tests/Gaps/       our own tests - does not count towards parity
tests/FuzzyRegex.OracleTests/      differential harness against Python regex
bench/FuzzyRegex.Benchmarks/       BenchmarkDotNet
tools/                      PortTools.psm1, check-ratchet.ps1, run-slices.ps1
upstream/                   read-only submodule, pinned to the commit we track
```

## Commands

```powershell
dotnet tool restore; dotnet husky install     # fresh clone, once: formatting + git hooks
dotnet csharpier format .                     # CI fails on unformatted code
dotnet build                                  # analyzers are errors; there is no warning tier
dotnet test tests/FuzzyRegex.Tests            # the suite
tools/check-ratchet.ps1                       # suite + parity ratchet + regenerate STATUS.md
tools/check-ratchet.ps1 -UpdateBaseline       # only once it is GREEN
tools/run-tool-tests.ps1                      # Pester tests for the tooling above
tools/run-slices.ps1 -DryRun                  # what the autonomous driver would do next
```

`global.json` sets `test.runner` to Microsoft.Testing.Platform, which TUnit needs on the .NET 10
SDK. Without it `dotnet test` fails with a VSTest message.

Everything under `tools/` needs **PowerShell 7** (`pwsh`); the Windows-bundled 5.1 cannot parse
`PortTools.psm1`.

The hooks `dotnet husky install` writes are formatting on `pre-commit` and
`tools/check-ratchet.ps1` on `pre-push`. Tests deliberately do not run on commit: mid-slice
commits are meant to contain failing tests, and the driver has just run the suite anyway.

## House rules

- **Faithful port.** `Parsing/` and `Engine/` mirror upstream's structure, names and control
  flow, so an upstream diff maps onto our files mechanically. Do not tidy, do not restructure,
  do not "improve" a signature. Optimisation is Phase 7 and it is benchmark-driven.
- **Test-first, always.** The test exists and fails before the code exists. Ported tests are
  written skipped in Phase 1 and un-skipped by the slice that delivers their capability.
- **Never weaken a test to get green.** If a ported test looks wrong, prove it by running the
  Python `regex` module and quoting the output, then record the finding in DECISIONS.md.
- **Never edit generated files by hand:** `docs/STATUS.md`, `tests/parity-baseline.json`,
  `src/FuzzyRegex/Unicode/`.
- **Never disable an analyzer to get a build.** `.editorconfig` already relaxes, for
  `Parsing/` and `Engine/` only, the rules a faithful port must break (method length,
  complexity, nesting, magic numbers, commented-out upstream reference lines). Correctness and
  security rules are errors everywhere and stay that way.
- **UTF-16, not codepoints.** Public indices and lengths are UTF-16 code units, matching .NET
  `Regex`. Upstream indexes by codepoint. Any test whose data contains a non-BMP character needs
  its indices recomputed - verified against Python, never reasoned about.
- **Verification discipline is in `docs/VERIFICATION.md`.** Read it before any review, fix or
  engine slice; it is short and it is the single source. The three rules that catch people out:
  a review finding is a hypothesis until *you* reproduce it (four in five do not survive); one
  review pass per **unreviewed change**, not per slice; and passing the ported suite is evidence
  of parity, not proof of it.
- **Stop rather than guess.** Write the blocker into STATE.md, commit, stop.
- **No `Co-Authored-By` trailer in commit messages.** This repository does not use one.

## Licensing

Apache-2.0, derived from mrab-regex which is `Apache-2.0 AND CNRI-Python`. Any new source file
that is a port of upstream code inherits that provenance; see NOTICE. Do not copy code from any
other regex implementation into this repository.
