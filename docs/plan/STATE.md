# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** S01b, CSharpier formatting and Husky.Net git hooks (2026-08-29).

**Current slice:** none. Next is `docs/plan/slices/S02-port-tests-core.md`.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke `port-slice`.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Run `dotnet tool restore` then `dotnet husky install` once per clone** - `.husky/_/` is
  gitignored, so without it the hooks are inert. **PowerShell 7 is required**, and now says so in
  OPERATIONS.md: 5.1 cannot parse `tools/PortTools.psm1`.
- **Write S02-S05 tests formatted** (`dotnet csharpier format .` before committing) or the hook
  does it and the diff is noisier. CI fails on unformatted code. `.editorconfig` is the only
  formatting config: no `.csharpierrc`, no `.csharpierignore`, deliberately.
- **pre-commit formats only; pre-push runs the ratchet.** A failing test does not block a commit
  (the point of TDD) but does block a push. pre-commit refuses a commit when a staged `.cs` file
  also has unstaged edits, since the `git add` after formatting would sweep them in.
- **Read `docs/VERIFICATION.md` before any review or fix.** One pass per *unreviewed change*;
  reviewers hand over a reproduction, never prose; reproduce every finding yourself first.
- **Root namespace is `Fuzzy.Text.RegularExpressions`.** `Match` means .NET's `Match` (search
  anywhere); upstream's anchored `match` is `MatchAtStart`; statics take `(input, pattern, options)`.
- **`.editorconfig` suppresses** MA0025, S2325, IDE0060 for `src/FuzzyRegex/*.cs`; Phase 2 deletes it.
