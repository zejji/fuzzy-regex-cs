---
slice: S82
phase: 6
title: Branch reset, option 3 - one numbering rule instead of two (ledger entry 17)
delivers: []
---

# S82 - The last inherited bug that is a numbering rule

Ledger entry 17, upstream issue 425. S50 fixed the shape the issue reports with the maintainer's
**option 2** - `Info.OpenGroup` skips a number a reused name has already claimed in the branch being
parsed. That left the mirror-image shape broken, so **this port answers one bug family by two rules**,
and which rule applies depends only on the order the groups are written in. The owner's decision of
2026-09-22 is to implement **option 3** and make it one rule:

> In a branch reset, a group never takes a number another group in the same branch will use.

The owner decided this knowing upstream has a passing test for the current answer (below). It is a
deliberate divergence, in the same family as S50's, and it is pinned as one.

## Everything below was measured on 2026-09-22, not predicted

A spike is committed on branch `spike/branch-reset-option3` (`d4b50da`, worktree
`.claude/worktrees/br-option3`). **It is evidence, never for merge** - its pre-scan is crude and
does not handle character classes, comments or escapes. Read it, then write the real one.

### The mechanism is a pre-seed, and nothing else

`Info.OpenGroup` (`Info.cs:130`) already tests `BranchGroupNumbers` **before** the name check, so it
skips for unnamed groups too. Option 3 is therefore only this: at each branch start in
`ParseFunctions.ParseCommon` (`:1187`), pre-seed `BranchGroupNumbers` with the numbers that names
appearing **later in that branch** already own. `OpenGroup` needs no change.

### What it fixes

| Pattern | Subject | Before (upstream and this port) | After |
|---|---|---|---|
| `(?\|(?P<bug>xxx)(!)\|(!)(?P<bug>BUG))` | `!BUG` | `('BUG', None)` | `('BUG', '!')` |
| `(?\|(?P<n>a)(b)\|(c)(?P<n>d))` | `cd` | `('d', None)` | `('d', 'c')` |
| `(?\|(?P<n>a)(b)(c)\|(x)(?P<n>y)(z))` | `xyz` | `('y','z',None)` | `('y','x','z')` |

The third shape is new here; the ledger records only the first two, and the slice adds it.

### What it must not change, and did not

Two names in one branch are already correct in either order, and every other row of upstream's
`test_branch_reset` is unaffected: `(?|(?P<n>a)(b)|(?P<m>c)(?P<n>d))` over `cd` stays `('d','c')`,
`(?|(?P<n>a)(b)|(?P<n>c)(?P<m>d))` stays `('c','d')`, `(?|(?<a>a)(?<b>b)|(?<b>c)(d))(e)` over `cde`
stays `('d','c','e')`, `(a)(?|(b)|(b))(d)` stays `('a','b','d')`.

### The whole blast radius: 6538 of 6540, two failures

1. `Compiles_to_upstreams_bytecode(#614 '(?|(?<a>a)(?<b>b)|(c)(?<a>d))(e)')` - the one compile-parity
   row with the shape. `groupCount` (3) and `groupIndex` (`{a:1, b:2}`) do **not** move; only the two
   `GROUP` opcodes' numbers, which become the same shape row #613 already has.
2. `Branch_reset_duplicate_name_group_reports_both_of_the_second_alternatives_captures`
   (`tests/FuzzyRegex.Tests/Ported/BranchReset/BranchResetTests.cs:234`) - a **ported upstream test**,
   `RegexTests.test_branch_reset#22-23`.

Nothing else in the suite moves. If a third test reddens, something is wrong with the pre-scan - stop
and find it rather than updating the expectation.

## Two corrections this slice owes the record

- **Upstream has codified the current answer.** `upstream/regex/tests/test_regex.py:1655-1662`, under
  `# Hg issue 87: Allow duplicate names of groups`, asserts
  `regex.match(r"(?|(?<a>a)(?<b>b)|(c)(?<a>d))(e)", "cde").groups() == ("d", None, "e")` and
  `.capturesdict() == {"a": ["c","d"], "b": []}`. Ledger entry 17 presents this as an option the
  maintainer never chose. That is true of the *rule*, not of this *row*: there is a passing test.
- **The ledger over-states the severity.** It says the earlier group's text is "unreachable through
  any API - not by name, not by number, not through `captures`". Measured on all three shapes, that
  is false: `m.captures(1)` returns `['!','BUG']`, `['c','d']` and `['x','y']`. Every matched text is
  recoverable. What is lost is which group each capture came from, and `groups()` reporting `None`
  for a slot whose group matched. Rewrite the entry to claim only that.

## Scope

1. The real pre-scan, test-first, one failing test per hazard before any of it works. The spike
   handles none of these and the slice must:
   - a name inside a character class, `[(?<a>x)]`, is not a definition
   - a `(?#...)` comment containing something that looks like one
   - an escaped paren, `\(?<a>`, and a literal `\\` immediately before one
   - a nested branch reset inside the branch
   - `(?(name)yes|no)` conditionals and `\g<name>` references, which use a name without defining one
   - all three spellings: `(?P<a>...)`, `(?<a>...)`, `(?'a'...)`
   - lookarounds `(?<=...)` and `(?<!...)`, which start `(?<` and are not names
   - a name defined later in the branch that is NOT yet in `GroupIndex`: it claims a fresh number and
     must not be pre-reserved
   - `IgnorePatternWhitespace`, where whitespace and `#` comments inside the branch are skipped
2. The three shapes become passing tests in `InheritedIssueTests.cs`, beside
   `Branch_reset_numbers_two_groups_in_the_same_branch_differently`.
3. The two failing tests above are **inverted and pinned**, each carrying upstream's answer, this
   port's answer and the reason - the shape S50 used for its two.
4. Corpus row #614 re-recorded, with the reason beside it.
5. `docs/DIVERGENCES.md`, `COMPARISON.md`, `PORTMAP.md`: the rule in one sentence, and why this port
   does not answer what upstream's own test asserts.
6. Ledger entry 17 rewritten: status FIXED HERE, the third shape added, both corrections above made,
   and the report draft proposing option 3 with these rows as evidence.
7. A negative control in `tools/controls.json`, `"signal": "suite"` (added 2026-09-22): revert the
   pre-seed and show exactly the three named tests going red. No wave reaches this - the generators
   do not build branch resets with reused names.

## Done when

- [x] The three shapes answer with every group reachable; the two working shapes unchanged.
- [x] Upstream's `test_branch_reset` green except the one row deliberately inverted.
- [x] `pwsh -File tools/check-ratchet.ps1` GREEN (6563 passing, 6455 distinct ids, baseline updated).
- [x] `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds ("no row diverged from
  upstream, at all 3 seeds").
- [x] `python tools/run-controls.py --ids S82-A` reports FIRED. It reddens 15 tests, not three: the
  20 pre-scan tests did not exist when this file was written, and 12 of them plus the corpus row,
  the doc sample and the inverted ported row all rest on the pre-seed.
- [x] Ledger entry 17 and `docs/DIVERGENCES.md` carry the rule, the divergence and both corrections.

## Notes

- No quiet machine needed: correctness only, no timing in the gate.
- With entry 17 fixed, entry 18 (S61 item 7) is the only inherited bug left, and phase 6's
  bookkeeping closes with it.

## Closing notes (2026-09-22)

### What landed

`ParseFunctions.ReserveBranchGroupNumbers`, with the helpers `ScanOpenParen`, `ReserveName`,
`SkipComment`, `SkipSet` and `SetScratch`: a source-level pre-scan of one branch-reset branch, run
from `ParseCommon` before each branch is parsed, on a scratch `Source` and a scratch `Info` so it
cannot touch real parse state. It walks the branch to its `|` or its closing `)`, and for every
named-group definition it finds whose name already owns a number, it adds that number to
`Info.BranchGroupNumbers`. `Info.OpenGroup` already skipped that set - S50 wrote the skip - so
nothing in the numbering code changed. That is the whole of option 3: a group never takes a number
another group in the same branch will use, whichever order the two appear in.

The outer set is saved and restored around the branch reset, so a nested one cannot leak its
reservations outward.

Also:

- 20 cases in `Gaps/Parsing/BranchResetPreScanTests.cs`, one per hazard, each carrying upstream's
  measured answer in a remark.
- Three shapes in `Gaps/UpstreamIssues/InheritedIssueTests.cs`; the third, a name defined in the
  middle of the branch, is new to the record.
- `Ported/BranchReset/BranchResetTests` #22-23 inverted and pinned, and corpus row #614 pinned
  through `_pinnedDivergentPatterns`, which replaced `_turkicDivergentPatterns` so a pin carries its
  own reason into the failure message.
- Docs: the DIVERGENCES row and the COMPARISON section rewritten for option 3, a PORTMAP structural
  row for the pre-scan, ledger entry 17 rewritten with both corrections.
- Control `S82-A`, and `S50-C` re-measured under option 3.

### Surprising

- `(?'name'...)` is not a spelling either engine supports, though scope item 1 asked for all three.
  Upstream raises `error: unknown extension at position 2`, and `parse_name` reads to `>` or `)`
  only. The pre-scan must not treat `'` as a name delimiter; a test pins the error.
- A conditional `(?(name)yes|no)` needs its inner `(` counted by the scan's own depth, or the
  branch's `|` is read as the end of the branch.
- `m.captures(1)` recovers both texts under upstream's numbering, so the ledger's first draft
  overstated the severity. The corrected claim is that `m.group(2)` is unreachable, not that a
  capture is lost.
- The blast radius was 15 tests, not the three the spec predicted, because the 20 pre-scan tests did
  not exist when it was written.

### For the next slice

- The pre-scan reads the whitespace setting the branch starts with, so an `(?x)` that appears
  mid-branch does not change how the rest of that branch is scanned. A `SHORTCUT:` in the source
  names the ceiling; both engines reject a mid-branch version switch, which is what keeps the
  scratch reader in step.
- `_pinnedDivergentPatterns` in `CompileParityTests.cs` is where a future deliberate corpus
  divergence goes.

### Review

One blind pass, briefed from `docs/VERIFICATION.md`, over the whole diff. It returned **"No defects
found."** after building, running the suite (6563/6563), running the probe, and checking each hazard
itself: a differential of 129 generated rows against the installed `regex` module (decoys,
definitions and malformed patterns, including verbose comments containing `[ | )`, V0-vs-V1 set
grammar, `\Q...\E`, `\N{...}` in a class, and names owned by an outer group), a cost check to
42,001-character patterns finding no measurable pre-scan overhead, a line-by-line check of every
`Upstream:` remark against the probe's live output, and an independent application of the S82-A
mutation showing it changes exactly the 15 listed cases and nothing else. No findings raised, so
none reproduced and none fixed, and no second pass was needed: nothing changed after the review.

The independent verifier ran once over the 16 judged numbers this slice pins, from the committed
files, and reported all 16 CONFIRMED - the six upstream answers, upstream's own asserts at
`test_regex.py:1653-1662`, the port's answers, the ratchet line, and corpus row #614, where it
found `groupCount` 3 and `groupIndex {a: 1, b: 2}` identical and exactly two ints different, the
operands of the `Group` instruction that opens `(c)`. It confirmed no repository file changed during
its run.

### Controls

Both are suite-signal controls, so neither has a seed; they are judged by exact match against a
recorded list of failing test names, and both figures below are from a final run against the
committed code (`python tools/run-controls.py --ids S82-A,S50-C`).

> Control A, `425-option-3-pre-seed-removed` (`S82-A`): in
> `src/FuzzyRegex/Parsing/ParseFunctions.cs`, method `ReserveName`, change
> `            info.BranchGroupNumbers.Add(group);` to `            _ = group;`.
> Result: 15 tests red of 6563, FIRED. Re-run: `python tools/run-controls.py --ids S82-A`.

The mutation sits in `ReserveName` rather than at the two `ReserveBranchGroupNumbers` call sites:
deleting the calls leaves the method unreferenced, and `IDE0052` then fails the build, which the
runner reports as a fired control against an empty list.

> `S50-C`, `425-reverted`, re-measured under option 3: 16 red of 6563, FIRED - the 15 above plus
> `Branch_reset_numbers_two_groups_in_the_same_branch_differently`, S50's own shape, which the
> pre-seed alone does not carry.
