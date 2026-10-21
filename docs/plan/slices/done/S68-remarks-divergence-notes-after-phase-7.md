---
slice: S68
phase: 8
title: <remarks> divergence notes on every affected public member - after Phase 7, because it edits src/
delivers: []
---

# S68 - The documentation that travels in the package

XML doc comments reach IntelliSense, Copilot and the `.xml` in every consumer's package cache with
no site and no crawler, which the research (2026-09-16) rated the highest-leverage artefact. This
slice adds a `<remarks>` divergence note to every public member whose behaviour differs from
Python `regex` or `System.Text.RegularExpressions`, each one a short restatement of the
COMPARISON.md section it links to, never new prose.

**Blocked until Phase 7 is done** (owner notes, 2026-09-16): this is the one Phase 8 slice that
edits `src/`, and it must not collide with the optimisation stream's edits. Do not start while any
S58-S63 slice is pending. Runs in the `docs` worktree after rebasing onto `main`.

## Scope

- For every SHIPPED row in `docs/DIVERGENCES.md`, find the public members it touches (the row
  names them; `PublicAPI.Shipped.txt` is the list) and add a `<remarks>` paragraph: one or two
  sentences stating the difference and a `<see href>` to the COMPARISON.md anchor. Members already
  carrying such a note are checked, not duplicated.
- The fuzzy-specific members (`FuzzyRegexOptions.BestMatch`, `EnhanceMatch`, the `{e<=n}` family
  documented on `FuzzyRegex` itself, `\L<name>` lists) get the worked example from COMPARISON.md
  in `<example>`, copied verbatim so the S65 sample tests remain the proof.
- A convention test extending S65's: every member named by a SHIPPED row has a `<remarks>`
  containing a COMPARISON.md link. Non-vacuity floor as always.

## Verification

- Build emits no CS1573/CS1574/CS1591; the generated `.xml` contains each link; the new
  convention test fails when one `<remarks>` is removed (prove once, revert).
- Ratchet green, oracle unchanged (no behaviour change is possible from comments, but the ratchet
  runs anyway).

## Done when

- [x] Every SHIPPED divergence is visible from IntelliSense on the member it affects, with a link to the
  one page that explains it.

## Closing notes (2026-10-18)

**What landed.** 101 SHIPPED rows. 59 are attached to the public members they change, each with a
`<remarks>` paragraph that restates the COMPARISON.md section in one or two sentences and links it.
Where a member already carried the note (34 member and row pairs, from S41 to S65), the link was
added to that paragraph and nothing was duplicated. The other 42 rows are behaviour of a pattern
construct (group calls, backtracking verbs, `\K`, conditionals, fuzzy-section internals), visible
only in what a match returns, and their COMPARISON.md sections name no member in their prose; they
are listed with a reason in `DivergenceRemarksTests._noMember`.

Where a row went:

- Rows about every input-dependent method (cancellation token, per-call timeout, UTF-16 indices,
  `beginning`/`length`, thread safety, `concurrent`, not-ported members) go on the `FuzzyRegex`
  type, plus `Match.NextMatch` for the two that name it, rather than on all 35 overloads.
- Rows whose heading names an option or its inline flag go on that `FuzzyRegexOptions` member:
  `BestMatch`/`EnhanceMatch` (6 rows), `IgnoreCase` (5), `Ascii` (5), `FullCase` (5, the
  "full-folded" rows), `RightToLeft` (3), `Word`, `Unicode`, `Version0`, `Version1`.
- The partial-matching rows go on `Match.PartialMatch`, the member that reports one.
- Method rows go on every public overload of the method the row names (`Split`, `Replace`,
  `Matches`, ...), and only on the overloads that have the parameter in question (`count`,
  `maxSplits`).

The fuzzy members carry COMPARISON.md's worked examples in `<example>`: the five syntax sections on
the `FuzzyRegex` type, `BestMatch`, `EnhanceMatch`, and the `\L<name>` example on the constructor
that takes named lists. Links point at `https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#<anchor>`.
The anchor rule (lower case; letters, digits, `_`, `-` and spaces kept; spaces to hyphens) was
checked against the ids GitHub rendered for the published file through the contents API: 115 of
115 published headings agree; the 7 headings newer than the last push follow the same rule.

**Convention test.** `Conventions/DivergenceRemarksTests.cs`, extending S65's
`ComparisonCoversDivergencesTests` (whose row and heading scans it now shares). It fails when a
SHIPPED row is neither attached nor listed as having no member, when an entry names no row, when a
row has no single COMPARISON.md heading, and when any covered doc entry in the generated
`FuzzyRegex.xml` lacks a `<remarks>` link to that row's anchor. Floors: more than 100 rows (measured
101) and more than 120 member entries checked (measured 122). A second test holds each `<example>`
to a C# block of COMPARISON.md, verbatim after trimming indentation (9 blocks).

**Negative control.** Removed the WORD-flag paragraph from `FuzzyRegexOptions.Word`: the test
failed with `F:Fuzzy.Text.RegularExpressions.FuzzyRegexOptions.Word: no <remarks> link to
#the-word-flags-word-boundary-is-unicodes-default-one`. Reverted; green.
