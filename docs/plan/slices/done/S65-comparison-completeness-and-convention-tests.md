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

- [x] COMPARISON.md and DIVERGENCES.md agree row for row, examples are pinned, both convention tests are
  in and non-vacuous, headings listed for S72.

## Closing notes (2026-09-18)

**What landed.** `docs/COMPARISON.md` gained 8 lines of prose (the branch-reset UNNAMED-group-first
case, next to the existing NAMED-group-first row, both citing the same DIVERGENCES.md row).
`tests/FuzzyRegex.Tests/Docs/ComparisonSamples.cs` pins every runnable example in COMPARISON.md's
"Fuzzy syntax in one page" and "Behaviour that differs and why" sections (27 tests). Two convention
tests: `Conventions/ComparisonCoversDivergencesTests.cs` (DIVERGENCES.md's SHIPPED rows all named in
COMPARISON.md) and `Conventions/PublicApiDocumentationTests.cs` (every public type/member in
`src/FuzzyRegex` carries an XML `<summary>` or `<inheritdoc/>`, read from the generated
`FuzzyRegex.xml` against the assembly's real reflected surface).

**Negative controls, both proved by breaking and reverting:**
- `PublicApiDocumentationTests`: deleted the `<summary>` line from `FuzzyRegex.Match`'s doc comment
  (kept its `<param>`/`<returns>` tags, so CS1591 stays silent - the whole point of reading the XML
  rather than trusting the compiler). Failed: `M:Fuzzy.Text.RegularExpressions.FuzzyRegex.Match: has
  a doc entry with no <summary> and no <inheritdoc/>`. Reverted; ratchet green.
- `ComparisonCoversDivergencesTests`: removed the `### Inherited upstream bugs are fixed here`
  heading. Failed with that heading reported missing. Reverted; ratchet green.
- `ComparisonCoversDivergencesTests`: removed the `### There is no \`findall\`` heading while
  leaving its cross-reference intact (`docs/COMPARISON.md:33`, `See "**There is no \`findall\`.**"
  below.`). Failed with that heading reported missing. Reverted; ratchet green.

**Review.** Two blind passes, both fresh Opus subagents with no prior context, both dispatched as
blocking calls in this same turn.

Pass 1 (full diff) raised four findings. Two survived reproduction and were fixed:
- The DIVERGENCES-row scan (`SplitRowRespectingCodeSpans`) originally split every table row on a
  naive `line.Split('|')`. One real row - "Inherited upstream bugs are fixed here" - quotes
  `` `I\|F` `` in its Why column; the literal `|` inside that code span desynchronised every cell
  after it, so the row's Status column landed in `cells[5]` instead of `cells[4]` and the row was
  silently dropped (reproduced: this exact row, missing from the 28-heading list the naive split
  produced). Fixed by tracking backtick state and only splitting outside a code span; the real
  DIVERGENCES.md count is 34 SHIPPED rows, all now found. This is the bug the sabotage-and-revert
  step above exists to prove stays caught.
- The COMPARISON-side check compared each heading against the WHOLE file's raw text, not against
  COMPARISON's own heading lines. COMPARISON.md's cross-reference tables quote a heading's text
  inside an ordinary body cell (`See "**There is no \`findall\`.**" below.`), so deleting the actual
  section while leaving the cross-reference behind still passed (reproduced: exact scenario above).
  Fixed by extracting only `##`/`###` lines (`ComparisonHeadingLines`) and matching against those.

Two findings did not survive reproduction against the real code and were left as documented,
`SHORTCUT`-marked ceilings rather than fixed: explicit interface implementations
(`MatchCollections.cs:30,219,227,270`) are correctly excluded from `PublicApiDocumentationTests`'s
scan, matching CS1591's own convention that they need no doc comment; and a public event, nested
type, or generic method would be mismapped by `Prefix`/`CheckGroup` (a false VIOLATION, not a
silent pass), but none of the three exists in `FuzzyRegex`'s public surface today, so the gap is
speculative rather than live.

Pass 2 (scoped to the delta the fixes made, per the rule that unreviewed changes get their own
pass) found the two new non-vacuity floors too loose relative to the measured counts (34 rows vs a
`>20` floor, 46 heading lines vs no floor at all in the first cut) and a fenced-code-block edge case
in `ComparisonHeadingLines` that, like the two above, does not occur in the real file today. The
floors were tightened to `>33` and `>45`; the fence case was `SHORTCUT`-marked rather than fixed,
for the same reason. No third pass: both rounds repaired against a concrete, reproduced defect
(ground truth), not a critique cycle.

**Headings frozen for S72** (`docs/COMPARISON.md`'s `##`/`###` lines, in order):

Table A: Python `regex` to FuzzyRegex; Table B: `System.Text.RegularExpressions` to FuzzyRegex;
Fuzzy syntax in one page; `{e<=n}`: allow up to `n` errors of any kind; `{s,i,d,e}`: separate
budgets per kind of error; Cost forms: `{Ni+Md<n}` weights errors instead of just counting them;
`{0<e<5}`: two-sided form, at least one error and fewer than five; `{e<=n:[set]}`: constrain which
characters an edit may touch; `FuzzyRegexOptions.BestMatch` / `(?b)`: rank by the best fuzzy match,
not the first; `FuzzyRegexOptions.EnhanceMatch` / `(?e)`: tighten a match after it is found;
`\L<name>`: fuzzy matching against a named list of words; Behaviour that differs and why; Version 1
is the default; `(?V0)` really means version 0, where upstream's own algorithm would leave version
1's `FULLCASE` on; The "unterminated character set" parse error names `FuzzyRegexOptions.Version0`
and the `\[` escape; No `concurrent` parameter; A `Match` may be read from any thread, where
`System.Text.RegularExpressions.Match` may not; A `CancellationToken` on every input-dependent
method; A per-call `timeout` on every input-dependent method; `Match` means upstream's `search`;
upstream's anchored `match` is `MatchAtStart`; `Regex`-shaped surface; **Indices are UTF-16 code
units**, not codepoints; Upstream's `pos`/`endpos` are reshaped to `beginning`/`length`; `Split`
spells "no limit" as `maxSplits = -1`; `Replace`'s `count` is inverted the same way; There is no
`findall`; `Match.Groups` is an `IReadOnlyDictionary<string, Group>` as well as a list; A lazy walk
times each STEP, where `Matches` times the whole scan; `Split` returns `string?[]` and puts `null`
where a capturing group did not take part; Replacement templates speak upstream's language;
Exception mapping; `Match.allcaptures`, `allspans`, `groupdict` and `capturesdict` are not on the
public surface, and will not be; The "unused keyword argument" message interpolates the name as
written; Not ported at all; `(?e)` and `(?b)` rank candidates by fuzzy COST; The Turkic `I`
pairings are not applied by default; The search prefilters are not ported; The Unicode data is
version 17.0.0; A digit's decimal value is derived from upstream's own tables; `\N{...}` named
sequences are not carried; `(?L)` works only when casing is not requested; CPython's 4300-digit cap
on `int(str)` is not ported; A group call that would re-enter the same group at the same text
position fails that PATH; A branch-reset branch skips a group number a reused name has already
claimed; Reversed partial matches run out of text at the slice start; Inherited upstream bugs are
fixed here.

**Ratchet:** GREEN, 6307/6307 (6199 distinct ids), baseline 6199 (unchanged - this slice added
tests, not ported behaviour, so the baseline count was already current from S64).
