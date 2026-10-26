# Changelog

All notable changes to FuzzyRegex are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Before 1.0 this file tracks **phases, not slices**. Per-slice detail already lives in the git log,
in `docs/plan/slices/done/` and in the generated `docs/STATUS.md`; repeating it here would only give
it somewhere to rot. An entry is written when a phase closes, which is already a human checkpoint,
and the release entry is written in phase 8. A **Divergences from mrab-regex** section is owed at
1.0, sourced from the intentional-divergence allowlist: anyone arriving from the Python `regex`
module needs to know where this port deliberately behaves differently. From 1.0 on, entries are
per release.

## [Unreleased]

## [1.1.0] - 2026-10-26

### Changed

- **Tracks mrab-regex 2026.9.29** (commit `0e8cb05`): the Unicode data is 18.0.0, up from
  17.0.0. New scripts (Jurchen, Seal), blocks, joining groups, case pairs and character names
  work in patterns, and so does U+1DF95, the first character outside the BMP whose case folding
  lands inside it (`(?i)ß` finds it). `\X` follows Unicode 18.0.0's simplified Indic conjunct
  rule, which upstream's `\X` does not yet (see COMPARISON). Upstream's other change in that
  release, C type names, has nothing to port.
- **Case-insensitive lists of literals are found two to three times faster**: `(?i)cat|dog`,
  and `\L<list>` with IgnoreCase, now skip ahead with a vectorised search, including lists
  with a `k` or an `s`, whose Kelvin sign and long s are handled near where they occur.
- **Matching is 10-25% faster across the benchmark suite**, most on counting many matches
  (`Count` over a megabyte about 21% faster), repeats and short subjects; no benchmark is slower.
  The engine's main matching method is split, so its repeat handling, case-folded strings and
  failed-start handling run in their own smaller methods, and the JIT clears fewer stack slots
  on each call.

### Fixed

- **Partial matching with a capturing lookahead inside a repeat that a later conditional
  reads** is polynomial, not exponential up to the fork budget, and answers the exact
  partial where it used to answer a partial at the attempt's start that no text completes:
  `(?:(?=(?:a|(b))*y)a)+(?(1)c|d)` over 'a' * 10 + 'b' is a partial at (11, 11), not (0, 11).
- **A conditional in a repeat whose pattern ends in a backreference** no longer allocates per
  call: `(\w)(?:(?(?=\w)\w+|\s))*\1$` over 1,000 characters allocated about 105 MB a call, now
  24 bytes.
- **Partial matching with a negative lookaround whose fuzzy body can delete its way to the empty
  string** decides the lookaround at the end of the text, where the body matches whatever
  follows: `(x)(?!(?:x){d<=1})` over 'xx' is a partial at the edge, (2, 2), as `(x)(?!x?)` is,
  not (1, 2), which no text completes. The same holds for a condition's test, a reversed
  lookbehind and full case folding (`(?fi)(ß)(?!(?:\1){d<=1})` over 'ßß').

## [1.0.0] - 2026-10-20

The first release. FuzzyRegex is a .NET port of mrab-regex, the Python `regex` module, tracking
upstream 2026.9.19: every construct upstream's parser accepts, fuzzy matching with per-kind error
budgets and weighted costs, and an API shaped after `System.Text.RegularExpressions.Regex`. All
1,967 ported upstream tests pass with none skipped. The phases below record how it was built; the
GitHub release notes for v1.0.0 list every deliberate difference from upstream, the upstream bugs
this port does not share, the performance gate and the known limitations.

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
- Phase 6: **the bug sweep, and the promises a library has to keep beyond matching correctly.**
  Per-call `MatchTimeout` and `CancellationToken`, polled on the matcher's own loop rather than
  between matches; a compiled pattern that is immutable and safe to share across threads, as
  upstream and .NET `Regex` both promise; and the API surface completed against upstream's, so
  nothing upstream exposes is missing here. Upstream's own test suite runs with **nothing skipped**,
  and the differential oracle is green at three seeds over 126,080 rows a seed.

  Phase 6 closed with the three inherited bugs it had scheduled all fixed: a word-start anchor
  before a fuzzy section (`docs/plan/upstream-reports/LEDGER.md` entries 19 and 20), a partial
  `fullmatch` that denied a prefix whose completion exists (entry 21), and a repeated capture group
  whose backtracking cost grew with the repetitions (entry 18).
- Phase 7: **benchmarks and optimisation.** A BenchmarkDotNet suite of workloads, each measured
  against Python `regex` with pyperf on the same machine, with a measured noise floor. A cache of
  the fifteen most recently used patterns for the static methods (`FuzzyRegex.CacheSize`).
  Upstream's search prefilters (the required-string locator and the `search_start` family), ported
  so that none of them changes an answer. Less allocation per match, a lazy `Matches`, and the
  matcher's hot method split so the JIT can optimise it again (rarely used arms moved out: every
  timing harness row 0.74-0.93x of its earlier time). The 1.0 gate on 2026-10-18: of 40 workloads
  paired with Python, 38 are at or below Python's time and 2 are 1.09x, inside the tolerance of
  10% of workloads and 1.25x
  (`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-10-18-S63-gate.md`).
- Phase 8: **documentation, packaging and the demo.** `docs/GUIDE.md` for a reader who knows
  `System.Text.RegularExpressions` and has never used Python `regex`; `docs/COMPARISON.md`, the
  complete member-by-member mapping; package validation and a rehearsed release
  (`docs/plan/RELEASE.md`); and a browser demo that runs the engine in a Web Worker. The 59
  deliberate differences from upstream that change a public member now show in IntelliSense, as a
  `<remarks>` note on that member linked to its section of `docs/COMPARISON.md`; the other 42 are
  the behaviour of a pattern construct and name no member. A convention test keeps the notes in
  step with `docs/DIVERGENCES.md`.
- **The correctness sweep before 1.0.** After Phase 6 a complete interaction matrix and then a
  battery of searches (fresh oracle seeds, a bed of real and generated patterns, and generators for
  each pair of features such as fuzzy matching with backreferences, or partial matching with
  lookarounds) found further root causes, most of them inherited from upstream in rare
  combinations. Each was fixed test first or settled by a recorded decision; `docs/KNOWN-DEFECTS.md`
  lists them. The stopping rule was two consecutive battery runs with no new root cause. Runs 9
  and 10 met it on 2026-10-18; run 11 then found D109, and 1.0 waited for its fix and a clean run
  after it. Run 12, on the release engine, found no new defect: its one new finding was
  settled by a recorded decision as by design (F108). The largest change was to partial matching, which now works to a
  written contract: a partial promises that more text may complete the match, and where some text
  does, a partial call never answers "no match". For one class of fuzzy pattern, set out in the
  release notes, the partial start is a bound: never later than where a completed text's match
  starts, and sometimes a few characters earlier. Among the last fixes were a repeat guard that could cut a matching path in a partial call's second pass (D90),
  and a search that skipped start positions after a `\K` and a `(*PRUNE)` or `(*SKIP)` (D92).
  A reversed `\X` now stops at a slice start that cuts a grapheme cluster, as a forward one stops
  at the slice end (D108).
  `BestMatch` and `EnhanceMatch` now answer only matches the pattern can make without the flag:
  upstream's error budget could make an atomic group, possessive repeat, lookaround or
  conditional's test commit to a different match (D109, inherited). They refuse a fuzzy pattern
  that holds `(*PRUNE)` or `(*SKIP)`, because the budget would decide where the verb acts and
  holding it only at the end of the match cost 7 to 110 times the work; drop the flag, drop
  the verb or make that section exact. An `EnhanceMatch` refinement no longer cuts the text short,
  so atomic groups and possessive repeats inside it see the whole text (D111, inherited). It reads past the
  match only inside an atomic group, so a scan over adversarial text stays linear (D114). A partial search with a lookahead nested first in a lookahead no longer reports a
  partial that starts later than a real match, or a span the pattern cannot make (D113). A
  `BestMatch` scan where no match is better than the one found no longer repeats a failed search
  after every match, so it runs in linear time where upstream's grows with the square of the text
  (D115; a pattern with a lookbehind keeps upstream's cost, D116).
- **Deep nesting cannot crash the process.** A pattern nested more than 500 levels deep in groups
  and sets is refused with `FuzzyRegexParseException`, and running out of stack while compiling is
  reported as the same exception rather than a stack overflow, which .NET cannot catch and which
  ends the process (D110). Upstream raises Python's `RecursionError` instead.

### Changed

- **`Version1` is the default**, matching upstream's own recommendation; `Version0` is available
  per-pattern for `re`- and .NET-style set syntax and simple case folding. An inline `(?V0)` now
  selects version 0, which upstream's own flag handling does not when its default is version 1.

- **Tracks mrab-regex 2026.9.10** (commit `7dd71c1`), up from 2026.8.12. Three of the four engine
  and parser fixes in that range are ported - upstream issues 611, 612 and 613 - and the fourth,
  issue 614, needed nothing here because this port was already right about it. The remaining
  changelog entries in the range are CPython C-API error propagation and wheel-building, neither of
  which has a counterpart in a managed port; `docs/PORTMAP.md` accounts for every entry.

  The sync also found a **regression in 2026.9.10 itself**: issue 613's retreat clamp loses a
  partial match that 2026.8.12 and PCRE2 10.47 both find. This port keeps the match, and pins it.

  One port defect fixed that no test could have caught, because upstream faults on every input that
  would show it: a POSIX fuzzy match reported the errors it had spent as zero, since the
  save/restore of the best match carried neither the counts nor the change list.

- **Tracks mrab-regex 2026.9.19** (commit `85e568c`), up from 2026.9.10. Its one change, issue 619,
  stops `expand()` crashing after `detach_string()`; this port has no `detach_string` and holds its
  subject for the life of the match, so there was nothing to port, and a test now pins that.
  2026.9.29 (Unicode 18.0.0) follows after 1.0.

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

Phase 6's were found by reading upstream's C and its issue tracker rather than by a wave, because the
oracle is blind to a bug this port reproduces faithfully: comparing the two engines reports agreement.
Each is fixed here even though upstream still has it, and each is recorded in
`docs/plan/upstream-reports/LEDGER.md` with its mechanism.

- **Case folding applied a Turkish locale rule with no locale asked for.** Upstream's table builder
  merges `CaseFolding.txt`'s Turkic-only `T` rows into both default tables, which that file itself
  says to exclude by default. Two consequences: `İ` (U+0130) never reached its full case fold, and
  `(?i)I` matched `ı`. This port now agrees with PCRE2, Perl and .NET instead, and deliberately
  differs from upstream on four of `test_turkic`'s sixteen cells.
- **`BESTMATCH` lost a match that plain fuzzy matching finds**, when the best fit ended in a trailing
  insertion.
- **A fuzzy match reported change positions that contradicted its own change counts.** Four distinct
  doors into the same symptom; three more remain upstream's alone.
- **A self-recursive call around a fuzzy section that can match empty exhausted memory.** A progress
  guard is not enough: progress bounds the depth and not the branching.
- **Branch reset gave two groups in the same branch the same number**, for the shape upstream's own
  issue reports. Two other orderings did too, until the choice between the two options upstream
  has left unchosen was made: in a branch reset, a group never takes a number another group in the
  same branch will use.
- **A reversed partial match reported running out of text at the slice start, or did not, depending
  on which optimisation had run.**

### Divergences from mrab-regex

This port behaves differently from upstream on purpose in 104 places, each one a row of
[`docs/DIVERGENCES.md`](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md)
with its reason, its evidence and, where one exists, how to get upstream's behaviour back. The ones
a caller arriving from Python meets first:

- **Version 1 is the default**, so nested sets and full case folding are on; `(?V0)` or
  `FuzzyRegexOptions.Version0` switches back.
- **`Match` searches**; upstream's anchored `match` is `MatchAtStart`. Positions count UTF-16 code
  units, and "no limit" is `-1` for `Split` and `Replace`.
- **Replacement templates use upstream's `\1`**, where .NET's `Regex` uses `$1`.
- **`EnhanceMatch` and `BestMatch` rank by cost** where upstream ranks by error count, which differs
  only when a pattern weights its error kinds differently.
- **The Turkic `I` pairings are off by default.**
- **Partial matching follows a written contract**, described in `docs/GUIDE.md` under "Reading a
  result".

Most of the remaining rows are upstream defects fixed here and pinned by tests; each has an entry
in `docs/plan/upstream-reports/LEDGER.md`.

[Unreleased]: https://github.com/zejji/fuzzy-regex-cs/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/zejji/fuzzy-regex-cs/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/zejji/fuzzy-regex-cs/releases/tag/v1.0.0
