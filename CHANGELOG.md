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
- Phase 4: **every non-fuzzy construct upstream has.** Lookahead and lookbehind, including the
  variable-length lookbehinds upstream allows and .NET's own engine does not; conditionals whose
  condition is a lookaround as well as a group number; the `(*PRUNE)` and `(*SKIP)` backtracking
  verbs; recursion and group calls (`(?R)`, `(?&name)`, `(?P>name)`, `(?(DEFINE)...)`); partial
  matching, both kinds - a subject that runs out and a slice that narrows; and POSIX
  leftmost-longest (`(?p)`). Branch reset, named lists, possessive quantifiers, inline and version
  flags, comments and `(*FAIL)` turned out to have been finished by Phases 2 and 3: the phase
  checkpoint removed their `[Skip]` attributes and 92 tests passed unchanged, which is now the rule
  every phase close follows. Overall parity: **90.7%**, with 28 feature areas at 100%. What is left
  is Phase 5's, and it is the reason this port exists: fuzzy matching, `BESTMATCH` and
  `ENHANCEMATCH`.
- Phase 5: **fuzzy matching - the reason this port exists.** `{e<=2}` and the whole constraint
  grammar: per-error-kind budgets (`{i<=1,d<=2,s<=1}`), weighted cost equations
  (`{2i+1d+1s<4}`), and the `{...:test}` form that restricts which characters an error may fall on.
  Insertions, deletions and substitutions against single characters, literal strings,
  backreferences and case-folded text, inside every repeat form and nested to any depth; the error
  counts and the change positions a match reports; and the two ranking modes, `ENHANCEMATCH`
  (`(?e)`) and `BESTMATCH` (`(?b)`). Overall parity: **100% of the ported suite, with nothing
  skipped** - 5,869 tests, every `needs:` tag on the board delivered.

  Two deliberate divergences, both the same owner decision and both recorded in the
  intentional-divergence allowlist that 1.0's **Divergences from mrab-regex** section will be drawn
  from: **`ENHANCEMATCH` and `BESTMATCH` rank by COST where upstream ranks by error count.** That is
  upstream's open issue 470; with a unit cost equation the two rules agree, so the difference is
  visible only where a pattern weights its error kinds differently. `BESTMATCH` needed more than a
  tie-break to get there - an error-count budget cannot reach a cheaper match that spends the same
  number of errors - so it walks the slice twice, the first walk bounded by cost.

  One port defect fixed that no test could have caught, because upstream faults on every input that
  would show it: a POSIX fuzzy match reported the errors it had spent as zero, since the
  save/restore of the best match carried neither the counts nor the change list.

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

Phase 4's three were found the same way, two of them by widening the oracle rather than by adding a
feature.

- A reversed partial match at the left edge of a narrowed slice was missed, because the six
  `try_match_STRING*` arms bound themselves by the slice where this port's `STRING` opcodes bound
  themselves by the text. Only reachable with `(?r)` and `pos > 0`, which is why nothing before the
  sliced-partial generator saw it.
- `$` under MULTILINE read a bound that a `(*SKIP)` had moved, so it could be true one character
  before an ordinary letter. Every assertion now reads the real end of the text. Upstream has the
  same defect and still answers the other way, which is one of the pinned divergences.
- Full case folding sliced the unfolded characters with offsets taken from the folded text, which
  crashed on `(?r)^İﬁ` under `IGNORECASE|FULLCASE` and, worse, silently answered "no match" for
  `ﬁaﬁ` against `fiafi`. Two expansions in one run were needed, which is why one had always worked.

Phase 5's four were all found by the oracle, three of them by widening it rather than by adding a
feature, and none of them by a ported test.

- A `(*SKIP)` moved the slice the engine searches, and nothing put it back: a scanner carried the
  moved bound from one match of a scan into the next, so a later match could be found in a view of
  the subject that excluded it. Upstream has this defect too and keeps it.
- The same bound, carried the other way: a `partial` request runs a non-partial pass and then a
  partial one, and a `(*SKIP)` in the first left the second searching a narrowed slice, which
  skipped start positions below it. Both slice bounds are now restored between the passes. Upstream
  restores only the text position, in both directions.
- The width check that decides whether a partial retry happens at all counted UTF-16 code units
  where it needed characters, so one astral character read as two: the non-partial pass ran and
  succeeded where it should have been skipped, and the partial answer upstream gives was never
  reached. A comment had argued the mismatch was harmless because over-counting can only fail to
  take a shortcut - true of a plain match, and false of a partial one, where what the shortcut
  suppresses is the fallback.
- A POSIX fuzzy match reported the errors it had spent as zero, because saving and restoring the
  best match so far carried neither the fuzzy counts nor the list of changes. Upstream carries
  neither correctly either; this is fixed here rather than reproduced.

[Unreleased]: https://github.com/zejji/fuzzy-regex-cs/commits/main
