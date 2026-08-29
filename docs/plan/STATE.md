# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** S01, public API surface stub (2026-08-29), then a research-grounded revision of
the review and verification discipline (spec amendments 9 and 10).

**Current slice:** none. Next is `docs/plan/slices/S01b-formatting-and-git-hooks.md`, then S02.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke `port-slice`.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Review discipline changed - read `docs/VERIFICATION.md` first.** It is short and it is the
  single source. Headlines: one pass per *unreviewed change*, not per slice; reviewers hand over a
  reproduction, never prose; reproduce every finding yourself before acting (four in five do not
  survive); critique loops banned, but repair against a red ratchet or oracle divergence is not one
  and gets two rounds.
- **The differential oracle moved to the start of Phase 3**, before the first VM slice, and runs
  locally in every engine slice after that. Passing the ported suite is evidence of parity, not
  proof (amendment 10). Phase 6 keeps oracle *hardening* only.
- **Root namespace is `Fuzzy.Text.RegularExpressions`** - a type cannot be named after its own
  namespace and stay reachable. Directories, assembly names and NuGet id unchanged. Ported tests
  need no using directive for `FuzzyRegex`, `Match` or `Group`.
- **`Match` means .NET's `Match`** (search anywhere); upstream's anchored `match` is `MatchAtStart`;
  statics take `(input, pattern, options)`. Full list in the S01 closing notes.
- **`.editorconfig` temporarily suppresses** MA0025, S2325, IDE0060 for `src/FuzzyRegex/*.cs`;
  Phase 2 deletes that block.
