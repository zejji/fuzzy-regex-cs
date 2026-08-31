<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 42.2%** (830 of 1966 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 5 | 7 | 0 | 41.7% |
| Api | 6 | 6 | 0 | 0 | 100.0% |
| Atomic | 1 | 1 | 0 | 0 | 100.0% |
| Basics | 3 | 3 | 0 | 0 | 100.0% |
| Boundaries | 41 | 24 | 17 | 0 | 58.5% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| Captures | 8 | 7 | 1 | 0 | 87.5% |
| CaseFolding | 69 | 1 | 68 | 0 | 1.4% |
| CharacterClasses | 52 | 10 | 42 | 0 | 19.2% |
| Escapes | 103 | 103 | 0 | 0 | 100.0% |
| FindAll | 27 | 0 | 27 | 0 | 0.0% |
| Flags | 20 | 4 | 16 | 0 | 20.0% |
| Format | 11 | 0 | 11 | 0 | 0.0% |
| FullMatch | 12 | 6 | 6 | 0 | 50.0% |
| Fuzzy | 112 | 7 | 105 | 0 | 6.3% |
| Grapheme | 5 | 2 | 3 | 0 | 40.0% |
| Groups | 65 | 64 | 1 | 0 | 98.5% |
| Lookaround | 38 | 0 | 38 | 0 | 0.0% |
| NamedLists | 10 | 0 | 10 | 0 | 0.0% |
| Overlapped | 10 | 0 | 10 | 0 | 0.0% |
| PartialMatching | 18 | 0 | 18 | 0 | 0.0% |
| Possessive | 16 | 8 | 8 | 0 | 50.0% |
| Quantifiers | 53 | 52 | 1 | 0 | 98.1% |
| Recursion | 32 | 0 | 32 | 0 | 0.0% |
| Regressions | 464 | 96 | 368 | 0 | 20.7% |
| Reverse | 37 | 0 | 37 | 0 | 0.0% |
| Splitting | 24 | 0 | 24 | 0 | 0.0% |
| Substitution | 88 | 7 | 81 | 0 | 8.0% |
| UnicodeProperties | 70 | 49 | 21 | 0 | 70.0% |
| Various | 524 | 375 | 149 | 0 | 71.6% |
| ZeroWidth | 14 | 0 | 14 | 0 | 0.0% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 3565 | 3565 | 0 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `find-all` | 170 |
| `ignore-case` | 154 |
| `substitution` | 104 |
| `fuzzy-matching` | 98 |
| `partial` | 82 |
| `case-folding` | 69 |
| `lookaround` | 58 |
| `recursion` | 50 |
| `right-to-left` | 48 |
| `splitting` | 35 |
| `backtracking-verbs` | 34 |
| `inline-flags` | 29 |
| `fuzzy-counts` | 28 |
| `branch-reset` | 21 |
| `named-lists` | 20 |
| `fuzzy-bestmatch` | 18 |
| `fuzzy-budget` | 17 |
| `lookbehind` | 17 |
| `conditionals` | 15 |
| `format` | 11 |
| `version-flags` | 11 |
| `fuzzy-changes` | 8 |
| `posix-matching` | 8 |
| `possessive` | 8 |
| `fuzzy-enhancematch` | 6 |
| `overlapped` | 4 |
| `comments` | 4 |
| `fuzzy-insertion` | 3 |
| `fuzzy-substitution` | 3 |
| `fuzzy-deletion` | 3 |

