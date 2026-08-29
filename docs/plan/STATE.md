# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** S01, public API surface stub (2026-08-29). Six files under `src/FuzzyRegex/`,
signatures and XML docs only, every member throwing. 11 gap tests. Ratchet green at 32 passing.

**Current slice:** none in flight. Next is `docs/plan/slices/S01b-formatting-and-git-hooks.md`
(CSharpier plus a pre-commit hook), then S02.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke the `port-slice`
skill.

**Blockers:** none.

**Worth knowing before the next slice:**

- **The root namespace is now `Fuzzy.Text.RegularExpressions`, not `FuzzyRegex`.** A type cannot
  be named after its own namespace and stay reachable - `using FuzzyRegex;` gave CS0118 on
  `new FuzzyRegex(...)`. Measured, not reasoned. Directories, project files, assembly names and
  the NuGet id are unchanged; only C# namespaces moved, tests and benchmarks included. Design
  spec amendment 8 has the detail.
- **Ported tests need no using directive** to name `FuzzyRegex`, `Match` or `Group`: the
  enclosing namespace finds them.
- **`Match` means .NET's `Match`** - search anywhere, i.e. upstream's `search`. Upstream's
  anchored `match` is `MatchAtStart`. Static conveniences take `(input, pattern, options)`,
  `Regex`'s order, not upstream's. `pos`/`endpos` are `beginning`/`length`. Full list in the S01
  closing notes, which S02 should read before translating anything.
- **`.editorconfig` carries a temporary block** suppressing MA0025, S2325 and IDE0060 for
  `src/FuzzyRegex/*.cs`. Phase 2 deletes it when the members gain bodies.
- A namespace change makes the ratchet go RED on the old test ids. `-AcceptRemovals` is the
  documented way through; it is not a licence to use it when tests genuinely disappear.
