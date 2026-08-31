# Changelog

All notable changes to FuzzyRegex are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Before 1.0 this file tracks **phases, not slices**. Per-slice detail already lives in the git log,
in `docs/plan/slices/done/` and in the generated `docs/STATUS.md`; repeating it here would only give
it somewhere to rot. An entry is written when a phase closes, which is already a human checkpoint,
and the release entry is written in phase 8. A **Divergences from mrab-regex** section is owed at
1.0, sourced from the intentional-divergence allowlist: anyone arriving from the Python `regex`
module needs to know where this port deliberately behaves differently.

## [Unreleased]

### Added

- Phase 0 scaffolding: solution, central package management, analyzer set, CI, the
  `port-slice` / `port-tests` / `sync-upstream` / `benchmark` skills, the slice driver
  (`tools/run-slices.ps1`), the parity ratchet and the generated status board.
- Phase 1: the public API surface as signatures only, then upstream's full test suite ported,
  every test skipped behind a machine-readable `needs:` tag that names the capability it waits on.
- Phase 2: the parser and compiler (`_regex_core.py`), the Unicode tables and case folding, and the
  pattern-level public API. Every construct upstream's parser accepts is parsed and compiled here,
  and all 1,659 rows of the compile-parity corpus pass with none skipped.
- Phase 3 so far: the differential oracle harness, which runs identical inputs through Python
  `regex` and through this port and diffs the results; `re_compile`, turning the code list into the
  node graph; the engine spine (`basic_match`), so patterns match for the first time - literals,
  the dot, anchors and the `Match`, `Group` and `Capture` accessors; and character classes,
  including the V1 set operators.

### Fixed

Both found by the differential oracle, and neither visible to the ported test suite.

- A zero-width `CHARACTER` node hoisted in front of a leading anchor was stepped by one, leaving the
  anchor testing the wrong position, so nothing with a leading anchor could match.
- The scoped ASCII flag was not read at match time, so `(?a:\d)` matched non-ASCII digits such as
  U+FF19 FULLWIDTH DIGIT NINE.

[Unreleased]: https://github.com/zejji/fuzzy-regex-cs/commits/main
