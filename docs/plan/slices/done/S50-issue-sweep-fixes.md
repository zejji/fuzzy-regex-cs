---
slice: S50
phase: 6
title: Upstream issue sweep, part two - fix every issue S49 reproduced in this port
delivers: []
---

# S50 - The issue sweep, part two

Un-skips and turns green every `needs:issue-<n>` test S49 left, one issue at a time, each judged
to amendment 16 before the fix and each fix a deliberate divergence with an entry and a control.

## Scope

- Work S49's inherited list in the order S49 recommends, smallest mechanism first. For each: the
  definition (docs; a PCRE2 or Perl run where the construct exists there; comparable libraries for
  fuzzy semantics), the mechanism in `_regex.c` with line references, the fix, the pinned test, the
  divergence entry, the negative control, the ledger update.
- **Likely members and what is already known**: 563 and 564 are fuzzy-search shapes S39 flagged
  to recognise; 596 is a no-op constraint upstream does not elide, and eliding `{e<=0}` at compile
  time is a parser change with a compile-parity consequence, so record the corpus rows it changes;
  551 and 554 are resource blowups whose fix may be a bound rather than an answer, decided the way
  S47 decides entry 14; 367 and 425 are correctness bugs with clear expected answers in their
  issue threads.
- A fix that would change an answer upstream gets right on any oracle row is wrong; the wave says so.
- Anything S49 reproduced that cannot be fixed inside the slice is parked as a named blocker in
  STATE.md with the evidence.

## Verification

- Every `needs:issue-<n>` tag gone; each fix has a control that fires; waves GREEN at three seeds
  and 99991; the compile-parity corpus GREEN or its changed rows named for 596.

## Done when

- [x] Every reproduced issue fixed test-first or parked with evidence; zero `needs:issue-*` skips.
- [x] Entries, controls, ledger updated; nothing filed.
- [x] Ratchet GREEN, blind review (hunt: a bound that turns a finite pattern into an exception; a
      fix judged from the issue thread alone without a run), commit.

## Closing notes

**One issue fixed, four parked, and two of the four were fixed and then reverted when this slice's
own blind reviews broke them.** Suite 5,968 / 5,968 passing / **0 skipped** - every `needs:issue-*`
tag is gone, which is what the "Done when" box asks for whether an issue was fixed or parked. Oracle
GREEN at seeds 7, 4242, 20260914 and 99991. Ratchet GREEN.

| Issue | Outcome |
|---|---|
| 425 | **FIXED** for the shape it was filed as; two other orderings parked (they need the maintainer's undecided option 3). |
| 563 | **Fixed, then reverted.** Mechanism now established to a line; two fix designs built and reverted. |
| 564 | **Fixed, then reverted** with 563 - S50 proved they are one bug, which the reporter suspected and nobody had shown. |
| 589 | **Fixed, then reverted.** The sound fix is PCRE2's `hitend` model. |
| 554 | **Parked**, with both cheap answers ruled out by measurement. |

**This is a thin slice for the work in it, and the honest reason is that three of the four fixes were
wrong and the reviews caught them.** What the slice actually produces is the diagnosis: three issues
whose mechanism is now pinned to a line of upstream with a reproducible probe, two failed fix designs
written down so the next attempt does not repeat them, and a test file that asserts the inherited
answers plus every row the failed attempts broke.

### 425 is the maintainer's option 2, and it is the one fix that shipped

`ParseCommon` rolls `GroupCount` back for each branch, and a name already seen in an earlier branch
resolves to its old number WITHOUT moving the counter - so the next group in the same branch is
handed that number a second time, two groups write to one slot and the later write wins. On
`(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))` over 'BUG!' upstream answers `bug='!'`, and `(?P<bug>BUG)`'s
text is then unreachable through any API.

`Info.OpenGroup` now skips a number a reused name has already claimed in the branch being parsed -
the maintainer's own option 2. **An over-strong first attempt advanced `GroupCount` to the reused
name's number instead of skipping it, and reddened upstream's own `test_branch_reset#16-17` plus
compile-parity corpus row 613.** Skipping leaves both green. **No compile-parity corpus row changes.**

Two orderings are parked, where the UNNAMED group comes first - `(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))`
and `(?|(?P<n>a)(b)|(c)(?P<n>d))`. There the name's number is already fixed by an earlier branch, so
only option 3's lookahead over the whole branch would help, and upstream has not chosen between
options 2 and 3.

### 563 and 564 are ONE bug, and that is the slice's main finding

`_regex.c`:10214, `permit_insertion = !search || text_pos != search_anchor`, under upstream's own
comment "Permit insertion except initially when searching (it's better just to start searching one
character later)". `search_anchor` is set once per matching operation (`init_match`, :3410) and never
per candidate start position, so **the rule fires at exactly one of the positions a scan visits.**
The isolating probe is the same subject and the same winning span answered two ways purely by where
the search was told to begin:

```
>>> a = regex.compile(r'\m(?:Y){i}\M')
>>> a.search(' XY', 0)      # span (1, 3) 'XY'
>>> a.search(' XY', 1)      # None
```

S49's ledger said this was "not established to a line" and suspected the word-boundary check had no
preceding character to compare against. Wrong on both counts. **And 564 is the same bug reached
through `BESTMATCH`'s re-anchoring:** the 563 change turned it green with no code of its own, and
reverting 563 took it straight back. Re-run it all with
`python tools/probes/issue-563-anchor-rule.py`.

**Why a fix is justified at all, which the next attempt will need:** it is a generalisation of
upstream's own behaviour, not a new rule. `^` and `\A` already escape the prohibition, because
`basic_match` turns a start-anchored pattern into an anchored match - so upstream KEEPS a match
beginning with an inserted character for `^(?:abc){i<=1}` over 'xabc' and loses it for the identical
`(?m)^` pattern. Upstream also already allows a runaway leading insertion at every position except
the anchor: `\m(?:Y){i}\M` over `' q XY YX'` is `['q XY', 'YX']` upstream.

**The rule that survived both reviews, and the design that did not.** Lift the prohibition only when
an assertion held at the anchor AND fails one character on. That narrowing is not optional - a first
version that lifted it whenever any assertion had held reddened upstream's own `test_fuzzy` rows 51,
52, 54 and 56. What does not work is holding the answer in a bare `MatchState` field, because the
backtracking engine never saves or restores it, and it is wrong in BOTH directions:

- **Under-clearing**: an assertion that held only on a path the engine abandoned still pinned the
  anchor, so `(?:\bq|)(?:abc){i<=1}` and `(?!\bz)(?:abc){i<=1}` over 'xabc' answered `'xabc'`.
- Clearing it in the `Branch` and failed-lookaround backtrack arms fixed those and **left the
  repeats**: `(?:\bq)*`, `(?:\bq)?`, `(?:\bq){0,3}`, `(?:\bq)*+` and `(?>(?:\bq)*)` all still
  answered `'xabc'`; ten of twelve probed shapes diverged.
- **Over-clearing**, by those same clears: they also discard a pin set before and outside the
  construct, so an inert `(?:z|)` or `(?!q)` after the `\m` threw the fix away -
  `\m(?:z|)(?:Y){i}\M` over 'XY' went back to no match.

So the pin must be part of the backtracking state, or be replaced by a compile-time "every path to
this fuzzy item passes a position assertion" analysis plus the same dynamic one-step-on test. Either
is a slice.

### 589 was fixed and reverted, and the revert is the finding

The attempt made all seven word and grapheme boundary predicates answer `PARTIAL` at the right-hand
truncation point. Then the blind review showed that **returning `PARTIAL` from a predicate ENDS the
match, and the engine had not finished backtracking**:

```
search(r'(\.+?)\1\b', '..',   partial=True)  -> group 1 was (0, 1); upstream and HEAD give (0, 2)
search(r'(\.+?)\1\b', '....', partial=True)  -> group 1 was (0, 2); upstream and HEAD give (0, 3)
```

The lazy repeat's first try reached the boundary, escalated, and returned before the repeat could
grow - so a partial this port previously got exactly right came back with a truncated capture group.
**And the `ExpectedDivergences` entry written for the fix hid it**, because its predicate compared
overall spans and never looked inside a group. The sound fix is PCRE2's `hitend`: let matching run to
completion and turn only a FINAL failure into a partial.

### 554 is parked, and the two cheap answers were ruled out by measurement

`(?:ab)*` reaches n=4,000,000 and fails at 6,000,000 where `(ab)*` fails at 4,000,000, so capturing
roughly doubles the per-repetition cost; atomic `(?>(ab)*)` and possessive `(ab)*+` **both still fail
at 4,000,000**, so no existing construct avoids it. Raising the bound would be a patch on the
symptom: the 1GB limit is tested against the DOUBLED capacity, so the largest usable stack is just
under 512MB - but that is upstream's own check ported faithfully (`_regex.c`:2357), and upstream
reaches 6,000,000 under the same cap because its cost is lower. What is left is a performance gap
behind Phase 7's benchmarks, and the failure mode is already better than the one the issue reports.

### Oracle

GREEN at seeds 7, 4242, 20260914 and 99991, 6300 rows each, expected counts 5, 1, 1 and 2 - the
pre-slice values, and **no new `ExpectedDivergences` entry**. The one written for 589 went with that
revert. 425 is a parser change upstream has no equivalent for, so the wave cannot see it.

### Negative controls

**One, because only one fix shipped.** Re-run against the code in this commit after the last change,
driven by `.scratch/controls-bcd.py`, which mutates one site, runs the suite and restores the file in
a `finally`. It is a suite control with no seed, because the suite is deterministic and the oracle
cannot see a parser change upstream has no equivalent for.

**Control C, `425-reverted`.** In `src/FuzzyRegex/Parsing/Info.cs`, `OpenGroup`, delete:

```csharp
                if (BranchGroupNumbers.Contains(GroupCount))
                {
                    continue;
                }
```

Result: **1 failure**, `Branch_reset_numbers_two_groups_in_the_same_branch_differently`. Unmutated,
0.

**Four further controls were run against the reverted fixes and are recorded here because they are
what proved the fixes wrong, not because they guard anything now.** B (`AssertionHoldsOneStepOn`
returning `false`), D and E (deleting each of the two backtrack-arm clears) and F (removing the
`AtInsertionAnchor` clause) each fired once the rows they needed were pinned - and **on their first
run B, D and E all reported 0 failures**, which is how it was discovered that no test covered the
too-loose form or either path-sensitivity defect. The rows are now pinned in the parked 563 test.
The 589 wave control did worse: it went **GREEN with the fault present** (`expected 13, 7, 7, 10`
against the unmutated 6, 2, 2, 2) because the `ExpectedDivergences` entry written for that fix
classified every row the fault produced. **That is the reason to run a control against the committed
tree rather than trust an entry.**

### Review

**Two blind passes, 9 findings raised, 8 reproduced, 8 acted on.** Both changed the slice's outcome
rather than polishing it: the first parked 589, the second parked 563 and 564.

**First pass** (whole diff; hunt: a bound that turns a finite pattern into an exception, a fix judged
from the issue thread without a run) - **7 raised, 6 reproduced, 6 acted on**:

1. The 589 fix returned `PARTIAL` before backtracking had finished, truncating a capture group on
   `(\.+?)\1\b`. Reproduced; **589 reverted entirely.**
2. The new `ExpectedDivergences` entry classified that regression as EXPECTED. Reproduced; **entry
   removed.**
3. The same defect was live in a wave row at seed 20260915. Reproduced; gone with the revert.
4. `AssertionPinsStart` was never unwound on backtracking: `(?:\bq|)(?:abc){i<=1}` over 'xabc'
   answered `'xabc'`. Reproduced; addressed, then superseded by the second pass.
5. The same through a negative lookaround, `(?!\bz)(?:abc){i<=1}`. Reproduced; same.
6. A measurement in a comment did not reproduce - "9, 6 and 6" where the control gives 8, 5 and 5,
   because the figure predated the entry. Gone with the revert.
7. A doc-comment nesting slip. Not acted on: that file is reverted.

**Second pass** (first review of the delta the first reviewer never saw: the two backtrack-arm
clears, the rewritten parked 589 test, the six new pinned rows, and the completeness of the 589
revert) - **2 raised, 2 reproduced, 2 acted on**, and both killed the 563 fix:

8. The pin survives a path abandoned by a REPEAT, which the two clears do not cover: ten of twelve
   probed shapes answered `'xabc'` where upstream says `'abc'`.
9. The two clears are unconditional, so they also discard a pin set before and outside the construct:
   an inert `(?:z|)` after `\m` threw the fix away.

It also confirmed, by running them, that the 589 revert is byte-identical to HEAD across all seven
predicates and `ExpectedDivergences.cs`, that every `_regex.c` line reference in the ledger says what
is claimed, and that all six new pinned rows match upstream.

**No third pass is owed.** The change after the second review is a revert to HEAD plus test and
documentation edits; it adds no engine code, no public API and no tooling. The only code left in
`src/` is 425's, which both reviewers saw and neither raised a finding against.

### Independent verifier

A fresh Opus verifier, briefed with nothing but the commit-ready tree, re-ran every number above.
**27 CONFIRMED, 1 DIFFERENT, 2 COULD NOT RUN.**

The DIFFERENT was a stale sentence, now corrected: the class summary of `InheritedIssueTests.cs`
still said S50 fixed 563 and 564, where every other document said fixed-and-reverted.

**The two COULD NOT RUNs are the honest limit of this slice's evidence and are flagged rather than
dressed up.** Every claim about what a REVERTED fix did - the too-loose 563 rule reddening
`test_fuzzy` 51/52/54/56, the ten-of-twelve `'xabc'` shapes, controls B/D/E reporting 0 failures and
then firing, the 589 wave control going GREEN at `expected 13, 7, 7, 10`, and the over-strong 425
draft reddening `test_branch_reset#16-17` - **is not re-runnable from this tree**, because the code
it describes is not in it. It is recorded as history, not as a pin, and nothing in the shipped code
rests on it. The second is ledger 18's `611 B/rep` port figure, which S49 had already recorded as
quotable only from a cold first call in a fresh process; the deterministic half of 554 (the 1GB bound
and the four n=4,000,000 / 6,000,000 outcomes) was re-run and CONFIRMED.

It also independently re-measured, and confirmed, the whole shipped surface: the suite at 5,968 with
no `needs:issue-` skip anywhere, the ratchet at baseline 5,860, all four oracle seeds with their
`expected` counts, control C, every upstream answer quoted anywhere in the slice, every row asserted
in `InheritedIssueTests.cs` against BOTH engines, every `_regex.c` and `_regex_core.py` line
reference, that `Matcher.cs`, `MatchState.cs` and `ExpectedDivergences.cs` are byte-identical to
HEAD, and that no compile-parity corpus row changed.
