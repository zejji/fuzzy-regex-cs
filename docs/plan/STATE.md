# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** S02, upstream tests for lines 1-1007 (2026-08-29).

**Current slice:** none. Next is `docs/plan/slices/S03-port-tests-unicode-classes.md`.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke `port-slice`.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Literal `\uXXXX` text does not survive the authoring toolchain** - it silently decodes to the
  real character before reaching disk. S03 ports `test_properties` and is Unicode-dense, so this
  will bite. Build backslashes from `chr(92)` when scripting, prefer literal UTF-8 or `\xNN`, and
  byte-scan the result for stray control characters afterwards.
- **Reuse S02's 22 capability tags** (listed in its closing notes) before coining any new one; the
  vocabulary also reserves `named-lists` and `version-flags`, still unused.
- **A tag is a scheduling contract.** Tag the capability least likely to land first, and never
  un-skip by tag alone - read the prose, since a test may need two capabilities.
- **Replacement templates are upstream's language** (`\1`, `\g<name>`, `\n`), not `Regex`'s `$1`;
  **`Split` returns `string?[]`** with `null` for a group that did not participate. Both in
  DECISIONS.md.
- **Machine-check expected values against the local `regex` oracle.** These tests never execute, so
  reading them against the Python is the only gate; that sweep found the one wrong value in S02.
  Delegate porting in parallel if you like, but build centrally - the four S02 agents each verified
  in isolation and collectively missed 7 analyzer errors only a whole-project build shows.
