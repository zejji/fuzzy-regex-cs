<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 1.6%** (32 of 1966 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 0 | 12 | 0 | 0.0% |
| Api | 6 | 5 | 1 | 0 | 83.3% |
| Atomic | 1 | 0 | 1 | 0 | 0.0% |
| Basics | 3 | 0 | 3 | 0 | 0.0% |
| Boundaries | 41 | 0 | 41 | 0 | 0.0% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| Captures | 8 | 0 | 8 | 0 | 0.0% |
| CaseFolding | 69 | 0 | 69 | 0 | 0.0% |
| CharacterClasses | 52 | 0 | 52 | 0 | 0.0% |
| Escapes | 103 | 2 | 101 | 0 | 1.9% |
| FindAll | 27 | 0 | 27 | 0 | 0.0% |
| Flags | 20 | 4 | 16 | 0 | 20.0% |
| Format | 11 | 0 | 11 | 0 | 0.0% |
| FullMatch | 12 | 0 | 12 | 0 | 0.0% |
| Fuzzy | 112 | 0 | 112 | 0 | 0.0% |
| Grapheme | 5 | 0 | 5 | 0 | 0.0% |
| Groups | 65 | 0 | 65 | 0 | 0.0% |
| Lookaround | 38 | 0 | 38 | 0 | 0.0% |
| NamedLists | 10 | 0 | 10 | 0 | 0.0% |
| Overlapped | 10 | 0 | 10 | 0 | 0.0% |
| PartialMatching | 18 | 0 | 18 | 0 | 0.0% |
| Possessive | 16 | 0 | 16 | 0 | 0.0% |
| Quantifiers | 53 | 0 | 53 | 0 | 0.0% |
| Recursion | 32 | 0 | 32 | 0 | 0.0% |
| Regressions | 464 | 14 | 450 | 0 | 3.0% |
| Reverse | 37 | 0 | 37 | 0 | 0.0% |
| Splitting | 24 | 0 | 24 | 0 | 0.0% |
| Substitution | 88 | 7 | 81 | 0 | 8.0% |
| UnicodeProperties | 70 | 0 | 70 | 0 | 0.0% |
| Various | 524 | 0 | 524 | 0 | 0.0% |
| ZeroWidth | 14 | 0 | 14 | 0 | 0.0% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 1918 | 1759 | 159 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `fuzzy-syntax` | 184 |
| `quantifiers` | 179 |
| `ignore-case` | 144 |
| `escapes` | 134 |
| `character-classes` | 114 |
| `unicode-properties` | 95 |
| `anchors` | 87 |
| `substitution` | 86 |
| `partial` | 82 |
| `case-folding` | 69 |
| `parse-errors` | 63 |
| `conditionals` | 58 |
| `named-lists` | 50 |
| `fuzzy-matching` | 48 |
| `right-to-left` | 47 |
| `basic-matching` | 42 |
| `backrefs` | 40 |
| `find-all` | 37 |
| `backtracking-verbs` | 34 |
| `groups` | 34 |
| `recursion` | 33 |
| `lookaround` | 32 |
| `inline-flags` | 29 |
| `fuzzy-counts` | 28 |
| `ascii-flag` | 28 |
| `set-operations` | 24 |
| `splitting` | 23 |
| `named-groups` | 23 |
| `branch-reset` | 21 |
| `alternation` | 18 |
| `word-flag` | 18 |
| `fuzzy-bestmatch` | 18 |
| `line-boundaries` | 17 |
| `fuzzy-budget` | 17 |
| `define-groups` | 15 |
| `lookbehind` | 13 |
| `captures` | 12 |
| `version-flags` | 11 |
| `format` | 11 |
| `possessive` | 8 |
| `fuzzy-changes` | 8 |
| `grapheme` | 8 |
| `posix-matching` | 8 |
| `atomic` | 7 |
| `full-match` | 7 |
| `fuzzy-enhancematch` | 6 |
| `keep-marker` | 6 |
| `overlapped` | 4 |
| `comments` | 4 |
| `fuzzy-insertion` | 3 |
| `fuzzy-deletion` | 3 |
| `fuzzy-substitution` | 3 |

