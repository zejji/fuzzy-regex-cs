# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** S03, upstream tests for lines 1008-1740 (2026-08-29).

**Current slice:** none. Next is `docs/plan/slices/S04-port-tests-various-fuzzy.md`.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke `port-slice`.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Non-BMP data has not appeared yet.** Lines 1-1740 contain no code point above U+FFFF, measured
  both slices. S04 opens at `test_various`, so expect the first real surrogate work there - and
  measure the range before planning around it either way.
- **Put these two rules in the porting brief**, both broken by delegated agents in S03 and both
  invisible to the build: a `#n` provenance counts *every* `self.assert` in source order, including
  `sys.version_info` branches the port does not use; and a `(?V0)`/`(?V1)` pattern is never folded
  into an unflagged sibling that happens to expect the same value.
- **Reuse the 32-tag vocabulary** before coining (see S03's closing notes and `docs/STATUS.md`).
  `version-flags` is now in use; nothing is held in reserve.
- **Build centrally.** Agents verify their own files in isolation and miss whole-project analyzer
  errors every time - 7 in S02, 21 in S03. Private fields are `_camelCase`; agents default to
  PascalCase and the build is the only thing that says so.
- **Machine-check every expected value against the local `regex` oracle**, with
  `PYTHONDONTWRITEBYTECODE=1` and `PYTHONIOENCODING=utf-8` set. These tests never execute, so
  reading them against Python is the only gate.
- **Re-derive a reviewer's coverage count rather than trusting it.** S03's reviewer reported
  `test_set: 42/42 accounted` and had missed an off-by-one in that very method.
