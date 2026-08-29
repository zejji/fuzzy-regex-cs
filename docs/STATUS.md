<!--
  GENERATED FILE - do not edit by hand.
  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.
  Regenerate: tools/check-ratchet.ps1
-->

# FuzzyRegex parity status

Parity against upstream commit `1760a20647f1c2ddcc025128407fe6f7edb905a1`.

**Overall parity: 0.0%** (0 of 432 ported upstream tests passing).

## Ported upstream tests, by feature area

| Area | Tests | Passing | Skipped | Failing | Parity |
|---|---:|---:|---:|---:|---:|
| Anchors | 11 | 0 | 11 | 0 | 0.0% |
| Api | 6 | 0 | 6 | 0 | 0.0% |
| Basics | 2 | 0 | 2 | 0 | 0.0% |
| CaseFolding | 52 | 0 | 52 | 0 | 0.0% |
| CharacterClasses | 11 | 0 | 11 | 0 | 0.0% |
| Escapes | 103 | 0 | 103 | 0 | 0.0% |
| FindAll | 23 | 0 | 23 | 0 | 0.0% |
| Flags | 12 | 0 | 12 | 0 | 0.0% |
| Groups | 63 | 0 | 63 | 0 | 0.0% |
| Lookaround | 13 | 0 | 13 | 0 | 0.0% |
| Quantifiers | 44 | 0 | 44 | 0 | 0.0% |
| Splitting | 20 | 0 | 20 | 0 | 0.0% |
| Substitution | 72 | 0 | 72 | 0 | 0.0% |

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
| `escapes` | 96 |
| `quantifiers` | 58 |
| `substitution` | 53 |
| `case-folding` | 36 |
| `find-all` | 23 |
| `parse-errors` | 22 |
| `splitting` | 15 |
| `groups` | 15 |
| `anchors` | 14 |
| `conditionals` | 13 |
| `ignore-case` | 13 |
| `lookaround` | 13 |
| `character-classes` | 12 |
| `named-groups` | 12 |
| `pattern-properties` | 9 |
| `inline-flags` | 8 |
| `backrefs` | 7 |
| `escape-function` | 4 |
| `alternation` | 3 |
| `right-to-left` | 2 |
| `basic-matching` | 2 |
| `fuzzy-matching` | 2 |

