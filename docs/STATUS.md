<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 0.0%** (0 of 1451 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 0 | 12 | 0 | 0.0% |
| Api | 6 | 0 | 6 | 0 | 0.0% |
| Atomic | 1 | 0 | 1 | 0 | 0.0% |
| Basics | 3 | 0 | 3 | 0 | 0.0% |
| Boundaries | 40 | 0 | 40 | 0 | 0.0% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| Captures | 8 | 0 | 8 | 0 | 0.0% |
| CaseFolding | 69 | 0 | 69 | 0 | 0.0% |
| CharacterClasses | 52 | 0 | 52 | 0 | 0.0% |
| Escapes | 103 | 0 | 103 | 0 | 0.0% |
| FindAll | 27 | 0 | 27 | 0 | 0.0% |
| Flags | 20 | 0 | 20 | 0 | 0.0% |
| Format | 5 | 0 | 5 | 0 | 0.0% |
| FullMatch | 12 | 0 | 12 | 0 | 0.0% |
| Fuzzy | 72 | 0 | 72 | 0 | 0.0% |
| Grapheme | 5 | 0 | 5 | 0 | 0.0% |
| Groups | 65 | 0 | 65 | 0 | 0.0% |
| Lookaround | 38 | 0 | 38 | 0 | 0.0% |
| NamedLists | 10 | 0 | 10 | 0 | 0.0% |
| Overlapped | 10 | 0 | 10 | 0 | 0.0% |
| PartialMatching | 18 | 0 | 18 | 0 | 0.0% |
| Possessive | 16 | 0 | 16 | 0 | 0.0% |
| Quantifiers | 53 | 0 | 53 | 0 | 0.0% |
| Recursion | 32 | 0 | 32 | 0 | 0.0% |
| Reverse | 37 | 0 | 37 | 0 | 0.0% |
| Splitting | 24 | 0 | 24 | 0 | 0.0% |
| Substitution | 88 | 0 | 88 | 0 | 0.0% |
| UnicodeProperties | 70 | 0 | 70 | 0 | 0.0% |
| Various | 524 | 0 | 524 | 0 | 0.0% |
| ZeroWidth | 10 | 0 | 10 | 0 | 0.0% |

## Our own tests (gap tests and conventions)

These are not upstream tests, so they do not count towards parity.

| Area | Tests | Passing | Skipped | Failing |
|---|---:|---:|---:|---:|
| Conventions | 21 | 21 | 0 | 0 |
| Gaps | 13 | 13 | 0 | 0 |

## Tests waiting on a capability

What the next slice should deliver, biggest win first.

| Capability | Tests it would enable |
|---|---:|
| `quantifiers` | 156 |
| `ignore-case` | 140 |
| `escapes` | 131 |
| `character-classes` | 99 |
| `anchors` | 86 |
| `unicode-properties` | 74 |
| `substitution` | 69 |
| `parse-errors` | 68 |
| `case-folding` | 53 |
| `right-to-left` | 46 |
| `find-all` | 36 |
| `backrefs` | 32 |
| `basic-matching` | 32 |
| `conditionals` | 30 |
| `groups` | 30 |
| `fuzzy-matching` | 29 |
| `inline-flags` | 27 |
| `lookaround` | 26 |
| `named-groups` | 22 |
| `recursion` | 22 |
| `branch-reset` | 21 |
| `splitting` | 20 |
| `partial` | 18 |
| `word-flag` | 18 |
| `alternation` | 16 |
| `line-boundaries` | 15 |
| `fuzzy-budget` | 13 |
| `set-operations` | 13 |
| `version-flags` | 11 |
| `named-lists` | 10 |
| `pattern-properties` | 9 |
| `captures` | 8 |
| `possessive` | 8 |
| `fuzzy-syntax` | 7 |
| `full-match` | 6 |
| `lookbehind` | 5 |
| `fuzzy-counts` | 5 |
| `fuzzy-bestmatch` | 5 |
| `fuzzy-enhancematch` | 5 |
| `format` | 5 |
| `grapheme` | 5 |
| `overlapped` | 4 |
| `escape-function` | 4 |
| `fuzzy-deletion` | 3 |
| `fuzzy-insertion` | 3 |
| `fuzzy-substitution` | 3 |
| `comments` | 1 |
| `atomic` | 1 |
| `fuzzy-changes` | 1 |

