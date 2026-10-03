# D27 and D30: fuzzy repeats whose empty iterations only spend edits

Decided 2026-09-29. Outcome: no engine change. F-A's "needed" rule (merged at fa42f231) already
ends both loops; this note records why that rule is the minimal one, and the tests that pin it.

## The two defects

- D27: `(?:(?:(?:b|(?P=g))*(?P<g>)){0,2}(?=(?P<g>b))$){e<=1}`, search over `'abb'`.
- D30: `(?:(?:(?(g))|(?=.(?P<g>b)))*(?P=g)$){d<=1}` over `'xb'`.

Upstream (`regex` 2026.9.10) gives TimeoutError on D27 and MemoryError on D30. In both, an
iteration of the inner repeat reads no text and leaves every tested group as it was, but makes one
fuzzy edit. Upstream counts any edit as progress (`upstream/src/_regex.c`:12550-12553 with :10487),
so the repeat goes round again, and the edit is undone on backtracking, so the budget never runs
out.

## Where each stands

| Build | D27 | D30 |
|---|---|---|
| main before F-A (7cecdb35) | times out | times out |
| main 9abaa593 | (1, 3), one insertion, 118 ms (first call) | None, 1 ms |
| D17 tip 4798964f | as main | as main |

So D27 did not need D1 as such: it needed F-A's rule (D2's fix), which merged together with D1. D30 was found on the
D17 branch before that branch merged F-A; on main it never reproduced.

## The rule, from principles

Take an iteration that ends where it began and changes no tested group. Leaving the repeat instead
reaches the same position with the same groups and fewer edits. A fuzzy limit is an upper bound, so
anything that can follow the edited path can follow the unedited one, with one exception: a
section minimum (`{1<=e<=2}` and the like), which fewer edits can fail to meet. The cost equation
of `(?e)` and `(?b)` only grows with edits. So such an iteration can change a later outcome only
when something needs its edits, and repeating it otherwise only spends budget.

That is what `Matcher.EmptyIterationAdmitted` (src/FuzzyRegex/Engine/Matcher.cs:5005) admits: an
edited empty iteration stands only if (a) the repeat is still at or below its minimum count, (b) its
edits raise a count an open section has an unmet minimum for (`RaisesUnmetMinimum`), or (c) it
changed a tested group. Each admission raises a count toward a finite bound or a tested span, so a
run of them ends. The rule the brief proposed ("progress only while it could still change a later
outcome, bounded by the section limits") is this rule: (b) is exactly "while a minimum is unmet",
and past the limits the edit is not even possible. Dropping any of (a) to (c) would lose an answer
FuzzyNeededEmptyIterationTests pins (a required iteration deleted once, a section minimum, a
deletion that sets a tested group), and no further stop is needed.

It keeps what it must:

- Ledger 33 (F-A's needed rule): unchanged; this is that rule.
- F6's 23-shape semantics: an error-free empty iteration still follows upstream's rule, a tested
  group's change is progress (`FuzzyIterationStands`, Matcher.cs:3271, the `else` branch), and (c)
  keeps that for an edited one.
- Termination does not depend on the repeat memo: a pattern with three tested groups turns the memo
  off (PatternObject.cs:909), and the D27 and D30 variants with three tested groups still answer at
  once. A tested group that cycles between spans is D17, a separate rule.

## Evidence

A grid of 5,144 rows (both families; `{d<=1}`, `{e<=1}`, `{i<=1}`, `{s<=1}`, `{e}`,
`{1<=e<=1}`, `{1<=e<=2}`, `{1<=d<=2}`, `{e<=2}`, `{i<=1,d<=1}`; greedy, lazy, `+`, `{2,}`, nested
repeats, three tested groups, inner edits, nested sections; plain, `(?r)`, `(?b)`, `(?e)`; search
and fullmatch) on main: no timeout, no exception, slowest row 9 ms after the first.

- Rows upstream answers (4,301): 4,049 agree; the other 252 are all classified EXPECTED by the
  oracle's own comparer (`run-oracle.ps1 -Rows`): 204 "fuzzy insertion before a failing
  lookaround", 48 "fuzzy exact item offered as a deletion".
- Rows upstream loops on (843): `(?P=g)` in D27's loop only ever reads an empty g, and `(?(g))` in
  D30's has no item to edit, so removing that alternative cannot change the answer. For 462 rows
  that control terminates upstream: 459 equal the port's answer to the looping pattern, and the 3
  others are one row repeated, where upstream's control spends a deletion nothing needs, the
  divergence F-A already pins.

## Tests

`FuzzyNeededEmptyIterationTests.An_empty_iteration_that_only_spends_an_edit_does_not_loop` (27
rows) and `The_d27_match_keeps_the_captures_upstream_gives_from_the_next_position`. With the rule
switched off (`UpstreamEmptyIterations` forced on), 23 of the 27 rows time out at 5 s; the four
that pass are the `(?r)` rows and the two D30 rows upstream answers, kept as controls.
