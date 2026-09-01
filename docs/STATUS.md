<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 59.3%** (1165 of 1966 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 11 | 1 | 0 | 91.7% |
| Api | 6 | 6 | 0 | 0 | 100.0% |
| Atomic | 1 | 1 | 0 | 0 | 100.0% |
| Basics | 3 | 3 | 0 | 0 | 100.0% |
| Boundaries | 41 | 24 | 17 | 0 | 58.5% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| Captures | 8 | 7 | 1 | 0 | 87.5% |
| CaseFolding | 69 | 67 | 2 | 0 | 97.1% |
| CharacterClasses | 52 | 13 | 39 | 0 | 25.0% |
| Escapes | 103 | 103 | 0 | 0 | 100.0% |
| FindAll | 27 | 0 | 27 | 0 | 0.0% |
| Flags | 20 | 4 | 16 | 0 | 20.0% |
| Format | 11 | 11 | 0 | 0 | 100.0% |
| FullMatch | 12 | 12 | 0 | 0 | 100.0% |
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
| Regressions | 464 | 129 | 335 | 0 | 27.8% |
| Reverse | 37 | 4 | 33 | 0 | 10.8% |
| Splitting | 24 | 0 | 24 | 0 | 0.0% |
| Substitution | 88 | 84 | 4 | 0 | 95.5% |
| UnicodeProperties | 70 | 52 | 18 | 0 | 74.3% |
| Various | 524 | 500 | 24 | 0 | 95.4% |
| ZeroWidth | 14 | 1 | 13 | 0 | 7.1% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 3716 | 3716 | 0 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `find-all` | 195 |
| `fuzzy-matching` | 98 |
| `partial` | 82 |
| `lookaround` | 61 |
| `recursion` | 60 |
| `splitting` | 38 |
| `backtracking-verbs` | 34 |
| `inline-flags` | 29 |
| `fuzzy-counts` | 28 |
| `branch-reset` | 21 |
| `named-lists` | 20 |
| `fuzzy-bestmatch` | 18 |
| `lookbehind` | 17 |
| `fuzzy-budget` | 17 |
| `conditionals` | 15 |
| `overlapped` | 14 |
| `version-flags` | 11 |
| `posix-matching` | 8 |
| `possessive` | 8 |
| `fuzzy-changes` | 8 |
| `fuzzy-enhancematch` | 6 |
| `comments` | 4 |
| `fuzzy-insertion` | 3 |
| `fuzzy-deletion` | 3 |
| `fuzzy-substitution` | 3 |

