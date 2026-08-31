<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 18.8%** (369 of 1966 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 0 | 12 | 0 | 0.0% |
| Api | 6 | 5 | 1 | 0 | 83.3% |
| Atomic | 1 | 0 | 1 | 0 | 0.0% |
| Basics | 3 | 2 | 1 | 0 | 66.7% |
| Boundaries | 41 | 0 | 41 | 0 | 0.0% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| Captures | 8 | 0 | 8 | 0 | 0.0% |
| CaseFolding | 69 | 1 | 68 | 0 | 1.4% |
| CharacterClasses | 52 | 5 | 47 | 0 | 9.6% |
| Escapes | 103 | 103 | 0 | 0 | 100.0% |
| FindAll | 27 | 0 | 27 | 0 | 0.0% |
| Flags | 20 | 4 | 16 | 0 | 20.0% |
| Format | 11 | 0 | 11 | 0 | 0.0% |
| FullMatch | 12 | 6 | 6 | 0 | 50.0% |
| Fuzzy | 112 | 7 | 105 | 0 | 6.3% |
| Grapheme | 5 | 0 | 5 | 0 | 0.0% |
| Groups | 65 | 0 | 65 | 0 | 0.0% |
| Lookaround | 38 | 0 | 38 | 0 | 0.0% |
| NamedLists | 10 | 0 | 10 | 0 | 0.0% |
| Overlapped | 10 | 0 | 10 | 0 | 0.0% |
| PartialMatching | 18 | 0 | 18 | 0 | 0.0% |
| Possessive | 16 | 0 | 16 | 0 | 0.0% |
| Quantifiers | 53 | 0 | 53 | 0 | 0.0% |
| Recursion | 32 | 0 | 32 | 0 | 0.0% |
| Regressions | 464 | 45 | 419 | 0 | 9.7% |
| Reverse | 37 | 0 | 37 | 0 | 0.0% |
| Splitting | 24 | 0 | 24 | 0 | 0.0% |
| Substitution | 88 | 7 | 81 | 0 | 8.0% |
| UnicodeProperties | 70 | 49 | 21 | 0 | 70.0% |
| Various | 524 | 135 | 389 | 0 | 25.8% |
| ZeroWidth | 14 | 0 | 14 | 0 | 0.0% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 3529 | 3527 | 2 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `quantifiers` | 211 |
| `ignore-case` | 154 |
| `find-all` | 132 |
| `substitution` | 98 |
| `fuzzy-matching` | 98 |
| `anchors` | 87 |
| `partial` | 82 |
| `case-folding` | 69 |
| `conditionals` | 58 |
| `groups` | 57 |
| `right-to-left` | 47 |
| `backrefs` | 40 |
| `backtracking-verbs` | 34 |
| `recursion` | 33 |
| `lookaround` | 32 |
| `inline-flags` | 29 |
| `fuzzy-counts` | 28 |
| `named-groups` | 23 |
| `splitting` | 23 |
| `branch-reset` | 21 |
| `named-lists` | 20 |
| `alternation` | 19 |
| `word-flag` | 18 |
| `fuzzy-bestmatch` | 18 |
| `line-boundaries` | 17 |
| `lookbehind` | 17 |
| `fuzzy-budget` | 17 |
| `define-groups` | 15 |
| `captures` | 12 |
| `version-flags` | 11 |
| `format` | 11 |
| `fuzzy-changes` | 8 |
| `grapheme` | 8 |
| `posix-matching` | 8 |
| `possessive` | 8 |
| `atomic` | 7 |
| `fuzzy-enhancematch` | 6 |
| `keep-marker` | 6 |
| `overlapped` | 4 |
| `comments` | 4 |
| `fuzzy-deletion` | 3 |
| `fuzzy-insertion` | 3 |
| `fuzzy-substitution` | 3 |

