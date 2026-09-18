---
slice: S65
phase: 8
title: COMPARISON.md checked against every SHIPPED divergence, and two convention tests that keep it so
delivers: []
---

# S65 - One source for "how this differs", enforced

`docs/COMPARISON.md` exists (737 lines, 2026-09-18) and is the single page the research chose as
the port's highest-value artefact: two tables (Python `regex` to here, `System.Text.RegularExpressions`
to here) plus the fuzzy syntax with runnable examples and expected output. S72's help panels are
generated from its section headings, so after this slice the headings are frozen: renaming one is
a spec amendment.

Runs in the `docs` worktree. `src/`, `DIVERGENCES.md`, `PORTMAP.md` read-only.

## Scope

- **Row-by-row check**: every row in `docs/DIVERGENCES.md` marked SHIPPED is named in
  `COMPARISON.md` (by its heading or an explicit anchor) with the behaviour stated the same way;
  every COMPARISON claim traces to a SHIPPED row, a spec section or a pinned test. Fix the prose
  where they disagree, in COMPARISON only.
- **Examples run**: every code example in COMPARISON.md with an expected output becomes a test in
  `tests/FuzzyRegex.Tests/Docs/ComparisonSamples.cs` asserting that output (same pattern as S64's
  README samples; share the helper).
- **Convention test 1**: every public type and member in `src/FuzzyRegex` has an XML doc comment
  (`<summary>` at least). Use the compiler: `GenerateDocumentationFile` is on, so CS1591 warnings
  are the oracle; the test asserts the build emits none, or reads the generated `.xml` and compares
  to the public surface in `PublicAPI.Shipped.txt`. Measured non-vacuity floor, as every scan test
  in this repo carries (S52b rule).
- **Convention test 2**: every SHIPPED row of DIVERGENCES.md is named in COMPARISON.md. Parse the
  table, look up each row's identifier in COMPARISON; fails naming the missing row.
- **Heading freeze**: list COMPARISON.md's `##`/`###` headings in the closing notes as the contract
  S72 generates from.

## Verification

- Both convention tests fail when one row is removed from COMPARISON or one `<summary>` is deleted
  (prove once each, revert).
- Ratchet green; the sample tests are counted in the ratchet total.

## Done when

- COMPARISON.md and DIVERGENCES.md agree row for row, examples are pinned, both convention tests are
  in and non-vacuous, headings listed for S72.
