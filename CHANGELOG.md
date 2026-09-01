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
- Phase 3: **the matching engine**. Patterns match. In order of arrival: the differential oracle
  harness, which runs identical inputs through Python `regex` and through this port and diffs the
  results row by row; `re_compile`, turning the code list into the node graph; the engine spine
  (`basic_match`) with the `Match`, `Group` and `Capture` accessors; character classes and the V1
  set operators; alternation and capture groups; greedy and lazy quantifiers with the repeat
  position guards; word, default-word and grapheme boundaries and the `\K` marker;
  backreferences and group-existence conditionals; case-insensitive and full-casefold matching, for
  every `_IGN` and `_FLD` opcode; reverse matching (`(?r)`), for every `_REV` opcode; substitution
  (`Replace`, `ReplaceFormat`, `Match.Result` and the two template languages); and iteration
  (`Matches`, `Count`, `NextMatch`, `Split` and overlapped matching). Overall parity against
  upstream's own suite: **71.8%**, with fifteen feature areas at 100%. What is left is Phase 4's and
  Phase 5's: lookaround, recursion, branch reset, named lists, POSIX, partial and fuzzy matching.

### Fixed

The first two were found by the differential oracle, and neither was visible to the ported test
suite.

- A zero-width `CHARACTER` node hoisted in front of a leading anchor was stepped by one, leaving the
  anchor testing the wrong position, so nothing with a leading anchor could match.
- The scoped ASCII flag was not read at match time, so `(?a:\d)` matched non-ASCII digits such as
  U+FF19 FULLWIDTH DIGIT NINE.
- **A single-character repeat was quadratic in the length of the subject.** Upstream converts
  between a character count and a position by multiplying, because a CPython `str` is a fixed-width
  codepoint array; a .NET `string` is UTF-16, so this port converted by walking the subject, once
  per repeat position. `.*?cd` over 60,002 characters took 69 seconds where upstream takes 0.0003.
  Now two paths - upstream's arithmetic restored for a subject with no surrogate pair, and a sampled
  position table for the rest - and two permanent complexity guards over 240,000-code-unit subjects.

[Unreleased]: https://github.com/zejji/fuzzy-regex-cs/commits/main
