<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 0.0%** (0 of 731 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 12 | 0 | 12 | 0 | 0.0% |
| Api | 6 | 0 | 6 | 0 | 0.0% |
| Atomic | 1 | 0 | 1 | 0 | 0.0% |
| Basics | 2 | 0 | 2 | 0 | 0.0% |
| Boundaries | 40 | 0 | 40 | 0 | 0.0% |
| BranchReset | 21 | 0 | 21 | 0 | 0.0% |
| CaseFolding | 52 | 0 | 52 | 0 | 0.0% |
| CharacterClasses | 52 | 0 | 52 | 0 | 0.0% |
| Escapes | 103 | 0 | 103 | 0 | 0.0% |
| FindAll | 23 | 0 | 23 | 0 | 0.0% |
| Flags | 20 | 0 | 20 | 0 | 0.0% |
| Grapheme | 5 | 0 | 5 | 0 | 0.0% |
| Groups | 63 | 0 | 63 | 0 | 0.0% |
| Lookaround | 38 | 0 | 38 | 0 | 0.0% |
| Overlapped | 10 | 0 | 10 | 0 | 0.0% |
| Possessive | 16 | 0 | 16 | 0 | 0.0% |
| Quantifiers | 47 | 0 | 47 | 0 | 0.0% |
| Reverse | 37 | 0 | 37 | 0 | 0.0% |
| Splitting | 21 | 0 | 21 | 0 | 0.0% |
| Substitution | 82 | 0 | 82 | 0 | 0.0% |
| UnicodeProperties | 70 | 0 | 70 | 0 | 0.0% |
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
| `escapes` | 114 |
| `unicode-properties` | 69 |
| `quantifiers` | 69 |
| `substitution` | 63 |
| `case-folding` | 36 |
| `find-all` | 32 |
| `conditionals` | 30 |
| `right-to-left` | 30 |
| `anchors` | 27 |
| `character-classes` | 22 |
| `parse-errors` | 22 |
| `branch-reset` | 21 |
| `lookaround` | 21 |
| `word-flag` | 18 |
| `splitting` | 17 |
| `line-boundaries` | 15 |
| `ignore-case` | 15 |
| `groups` | 15 |
| `inline-flags` | 14 |
| `set-operations` | 13 |
| `named-groups` | 12 |
| `version-flags` | 11 |
| `pattern-properties` | 9 |
| `possessive` | 8 |
| `backrefs` | 7 |
| `grapheme` | 5 |
| `overlapped` | 4 |
| `escape-function` | 4 |
| `alternation` | 3 |
| `fuzzy-matching` | 2 |
| `basic-matching` | 2 |
| `atomic` | 1 |

