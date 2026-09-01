<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 71.8%** (1412 of 1966 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 12 | 0 | 0 | 100.0% |
| Api | 6 | 6 | 0 | 0 | 100.0% |
| Atomic | 1 | 1 | 0 | 0 | 100.0% |
| Basics | 3 | 3 | 0 | 0 | 100.0% |
| Boundaries | 41 | 41 | 0 | 0 | 100.0% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| Captures | 8 | 7 | 1 | 0 | 87.5% |
| CaseFolding | 69 | 67 | 2 | 0 | 97.1% |
| CharacterClasses | 52 | 52 | 0 | 0 | 100.0% |
| Escapes | 103 | 103 | 0 | 0 | 100.0% |
| FindAll | 27 | 27 | 0 | 0 | 100.0% |
| Flags | 20 | 4 | 16 | 0 | 20.0% |
| Format | 11 | 11 | 0 | 0 | 100.0% |
| FullMatch | 12 | 12 | 0 | 0 | 100.0% |
| Fuzzy | 112 | 7 | 105 | 0 | 6.3% |
| Grapheme | 5 | 5 | 0 | 0 | 100.0% |
| Groups | 65 | 65 | 0 | 0 | 100.0% |
| Lookaround | 38 | 0 | 38 | 0 | 0.0% |
| NamedLists | 10 | 0 | 10 | 0 | 0.0% |
| Overlapped | 10 | 10 | 0 | 0 | 100.0% |
| PartialMatching | 18 | 0 | 18 | 0 | 0.0% |
| Possessive | 16 | 8 | 8 | 0 | 50.0% |
| Quantifiers | 53 | 52 | 1 | 0 | 98.1% |
| Recursion | 32 | 0 | 32 | 0 | 0.0% |
| Regressions | 464 | 200 | 264 | 0 | 43.1% |
| Reverse | 37 | 32 | 5 | 0 | 86.5% |
| Splitting | 24 | 24 | 0 | 0 | 100.0% |
| Substitution | 88 | 84 | 4 | 0 | 95.5% |
| UnicodeProperties | 70 | 70 | 0 | 0 | 100.0% |
| Various | 524 | 500 | 24 | 0 | 95.4% |
| ZeroWidth | 14 | 9 | 5 | 0 | 64.3% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 3729 | 3729 | 0 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `fuzzy-matching` | 98 |
| `partial` | 82 |
| `lookaround` | 61 |
| `recursion` | 60 |
| `backtracking-verbs` | 34 |
| `inline-flags` | 29 |
| `fuzzy-counts` | 28 |
| `branch-reset` | 21 |
| `named-lists` | 20 |
| `fuzzy-bestmatch` | 18 |
| `fuzzy-budget` | 17 |
| `lookbehind` | 17 |
| `conditionals` | 15 |
| `version-flags` | 11 |
| `fuzzy-changes` | 8 |
| `posix-matching` | 8 |
| `possessive` | 8 |
| `fuzzy-enhancematch` | 6 |
| `comments` | 4 |
| `fuzzy-deletion` | 3 |
| `fuzzy-substitution` | 3 |
| `fuzzy-insertion` | 3 |

