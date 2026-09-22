---
slice: S82
phase: 6
title: Branch reset, option 3 - the unnamed group numbered before a reused name (ledger entry 17)
delivers: []
---

# S82 - The last inherited bug that is a numbering rule

Ledger entry 17, upstream issue 425. S50 fixed the shape the issue reports using the maintainer's
own **option 2** - `Info.OpenGroup` skips a number a reused name has already claimed in the branch
being parsed. Two orderings were parked then, and a third has since been found. The owner's decision
of 2026-09-22 is to implement **option 3** rather than wait for upstream, who has not chosen between
options 2 and 3 since 2021-09-28.

## What is broken, measured on regex 2026.9.10, 2026-09-22

Upstream skips a number only for a NAMED group. The break is therefore exactly this: an **unnamed**
group numbered before a reused name claims that number. Both then write to one slot and the earlier
one is unreachable through any API - not by name, not by number, not through `captures`.

| Pattern | Subject | Upstream `m.groups()` | Group 1's captures |
|---|---|---|---|
| `(?\|(?P<bug>xxx)(!)\|(!)(?P<bug>BUG))` | `!BUG` | `('BUG', None)` | `['!', 'BUG']` |
| `(?\|(?P<n>a)(b)\|(c)(?P<n>d))` | `cd` | `('d', None)` | `['c', 'd']` |
| `(?\|(?P<n>a)(b)(c)\|(x)(?P<n>y)(z))` | `xyz` | `('y', 'z', None)` | `['x', 'y']` |

The third is new here; the ledger records only the first two. The slice adds it to the entry.

**What already works and must keep working.** Two names in one branch are correct in either order,
because the name itself drives the skip:

| Pattern | Subject | Upstream `m.groups()` |
|---|---|---|
| `(?\|(?P<n>a)(b)\|(?P<m>c)(?P<n>d))` | `cd` | `('d', 'c')` |
| `(?\|(?P<n>a)(b)\|(?P<n>c)(?P<m>d))` | `cd` | `('c', 'd')` |

## Why option 3, and what it costs

Option 3 is "skip group numbers that are used anywhere in the branch". Worked through by hand on
2026-09-22, it fixes all three broken rows and changes none of the working ones. It also leaves
every row of upstream's `test_branch_reset` alone - the row that could have moved is
`(?|(?<a>a)(?<b>b)|(?<b>c)(d))(e)` over `"cde"`, where `b` takes 2, so the numbers used in the
branch are `{2}` and the unnamed `(d)` still takes 1.

**That arithmetic is a prediction, not a measurement.** The slice's first job is to turn it into a
run: upstream's 16 branch-reset assertions, the 613-row compile-parity corpus and the full ported
suite, before and after.

The mechanism is the cost. Option 3 needs the branch's later named groups known before its earlier
unnamed ones are numbered, and this parser is single-pass, so it needs a **source-level pre-scan of
the branch**. A pre-scan that mis-reads the source mis-numbers silently, which is the worst failure
mode this slice can produce, so it is the thing to test hardest:

- a name inside a character class, `[(?<a>x)]`, is not a definition
- a `(?#...)` comment containing something that looks like one
- an escaped paren, `\(?<a>`, and a literal `\\` before it
- a nested branch reset inside the branch
- `(?(name)yes|no)` conditionals and `\g<name>` references, which USE a name but do not define one
- `(?P<a>...)`, `(?<a>...)` and `(?'a'...)`, all three spellings
- a name defined later in the branch that is NOT already in `GroupIndex` - it claims a fresh
  number and must not be pre-reserved

## Scope

1. The pre-scan, in `Info`/`ParseFunctions` beside the existing `BranchGroupNumbers` scoping
   (`Info.cs:125`, `OpenGroup` at `:130`), with a failing test per bullet above written first.
2. `OpenGroup` consults the pre-scanned set for unnamed groups as well as named ones.
3. The two parked orderings and the third shape become passing tests in
   `tests/FuzzyRegex.Tests/Gaps/UpstreamIssues/InheritedIssueTests.cs`, beside
   `Branch_reset_numbers_two_groups_in_the_same_branch_differently`.
4. `docs/DIVERGENCES.md` gains the row: this port numbers these patterns differently from upstream
   on purpose, because upstream's numbering makes a matched group unreachable. `COMPARISON.md` and
   `PORTMAP.md` follow the shape S50 used for option 2.
5. Ledger entry 17 is rewritten: status FIXED HERE, the third shape added, the option 2 vs option 3
   history kept, and the report draft updated to propose option 3 with these three rows as evidence.
6. A negative control, registered in `tools/controls.json`: revert the pre-scan and show the three
   rows going red by name. It is a `suite` control - no wave reaches this, because the generators do
   not build branch resets with reused names.

## Done when

- The three broken rows answer with every group reachable, and the two working rows are unchanged.
- Upstream's `test_branch_reset`, all 16 assertions, green.
- The compile-parity corpus green, or every changed row explained and re-recorded as a deliberate
  divergence with its reason.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds.
- The negative control fires: it reddens exactly the three named tests, and nothing else.
- Ledger entry 17 and `docs/DIVERGENCES.md` say what landed and why option 3 over option 2.

## Notes

- This needs no quiet machine: it is a correctness slice with no timing in its gate.
- With entry 17 fixed, entry 18 (S61 item 7) is the only inherited bug left, and phase 6's
  bookkeeping closes with it.
