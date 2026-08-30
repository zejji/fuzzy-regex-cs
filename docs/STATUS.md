<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 0.3%** (5 of 1966 ported upstream tests passing).

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
| Escapes | 103 | 0 | 103 | 0 | 0.0% |
| FindAll | 27 | 0 | 27 | 0 | 0.0% |
| Flags | 20 | 0 | 20 | 0 | 0.0% |
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
| Regressions | 464 | 0 | 464 | 0 | 0.0% |
| Reverse | 37 | 0 | 37 | 0 | 0.0% |
| Splitting | 24 | 0 | 24 | 0 | 0.0% |
| Substitution | 88 | 0 | 88 | 0 | 0.0% |
| UnicodeProperties | 70 | 0 | 70 | 0 | 0.0% |
| Various | 524 | 0 | 524 | 0 | 0.0% |
| ZeroWidth | 14 | 0 | 14 | 0 | 0.0% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 1698 | 488 | 1210 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `character-classes` | 474 |
| `quantifiers` | 460 |
| `anchors` | 261 |
| `unicode-properties` | 156 |
| `substitution` | 148 |
| `ignore-case` | 144 |
| `escapes` | 135 |
| `case-folding` | 100 |
| `conditionals` | 85 |
| `partial` | 82 |
| `alternation` | 76 |
| `backrefs` | 75 |
| `parse-errors` | 72 |
| `lookaround` | 63 |
| `fuzzy-syntax` | 57 |
| `fuzzy-matching` | 48 |
| `right-to-left` | 47 |
| `basic-matching` | 40 |
| `recursion` | 40 |
| `lookbehind` | 40 |
| `named-lists` | 39 |
| `find-all` | 37 |
| `backtracking-verbs` | 36 |
| `groups` | 34 |
| `inline-flags` | 29 |
| `fuzzy-counts` | 28 |
| `branch-reset` | 28 |
| `ascii-flag` | 28 |
| `set-operations` | 24 |
| `named-groups` | 23 |
| `splitting` | 23 |
| `word-flag` | 18 |
| `fuzzy-bestmatch` | 18 |
| `line-boundaries` | 17 |
| `fuzzy-budget` | 17 |
| `atomic` | 16 |
| `define-groups` | 15 |
| `captures` | 12 |
| `comments` | 12 |
| `escape-function` | 12 |
| `version-flags` | 11 |
| `named-characters` | 11 |
| `format` | 11 |
| `grapheme` | 11 |
| `posix-matching` | 8 |
| `fuzzy-changes` | 8 |
| `possessive` | 8 |
| `full-match` | 7 |
| `fuzzy-enhancematch` | 6 |
| `keep-marker` | 6 |
| `overlapped` | 4 |
| `fuzzy-deletion` | 3 |
| `fuzzy-insertion` | 3 |
| `fuzzy-substitution` | 3 |
| `unicode-tables` | 2 |

