---
slice: S40b
phase: 5
title: The partial pass inherits a slice a (*SKIP) moved, and two pinned answers have to be re-judged with it
delivers: []
---

# S40b - the leftmost-partial defect, and the two pinned answers that go with it

**This port's own defect, four rows of the 6000-row three-seed wave, and the one item S40a proved
and deliberately did not fix** - because the obvious fix turns a permanent pinned answer red and
introduces a reversed row, and those three have to be judged in one sitting or not at all.

All line references are `upstream/src/_regex.c` unless marked.

## What it is

A search runs a non-partial pass and then a partial one. A `(*SKIP)` in the first pass moves
`slice_start` (`:14553`) and nothing puts it back, so the partial pass starts from wherever the
verb left it - and the answer is no longer leftmost. Minimised by S40a from seed-7 row 97927:

> `\b\D(*SKIP)z` over `' A'` asked with `partial=True`: the search answers (2, 0), and this port's
> own `MatchAtStart(' A', 1, partial: true)` answers (1, 1). A search that reports a position its
> own anchored matcher beats is not leftmost, which settles the row without consulting upstream at
> all. Upstream answers (1, 1) too.

Pinned, deliberately asserting the WRONG answer and saying so:
`PartialMatchingTests.A_skip_in_the_non_partial_pass_moves_the_slice_and_the_partial_pass_is_no_longer_leftmost`.

**The four wave rows** - all `partial`, all the same shape (a `(*SKIP)` in an alternation, a
partial search, upstream's answer earlier than this port's):

| seed | row | pattern |
|---|---|---|
| 7 | 97927 | `\b\K\K(?:\D(*SKIP)[^\p{L}]\|.)` |
| 4242 | 99961 | `\b(?:\w+(*SKIP).\|[a\d])(\S{0})` |
| 20260913 | 96679 | `\b(?(?<=\S)[^\p{L}]\|\p{Ll})(?:[[:alpha:]]+(*SKIP)[[:digit:]]\|\p{ASCII})` |
| 20260913 | 99823 | `[a-f](?:\p{ASCII}(*SKIP)[^\p{L}]\|[[:digit:]])(?P<g1>\w*)\1` |

## Scope

### 1. Restore the slice before the partial retry, and measure what moves

S40a measured the one-line version: **it fixes the row, introduces one reversed row, and turns
S37's pinned answer red.** Do not stop at the first of those.

The reversed row it introduces, recorded so the next session does not have to re-find it:

> `(?r)\b(?:[^a-f](*SKIP)[\p{L}\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})` over `'a\n'`

Restoring `slice_end` under `(?r)` changes what every end-of-subject assertion means, which is the
same trap `_anchored_scan` refuses to walk (tools/record-oracle.py) and the same one S35 fixed from
the other side. Judge the reversed row on its own merits before choosing where the restore goes:
both ends, or the forward end only, is a decision with evidence either way and it must be recorded.

### 2. Re-judge `A_skip_alternation_partial_starts_where_this_port_ran_out_of_text` (S37)

S40a's finding, and the reason this cannot be a quiet fix: that test pins (4, 0) where **this
port's own matcher answers (2, 2) earlier**, which is the identical self-refutation as row 97927.
If it is the same defect, the pinned answer is wrong and the test is a regression test for a bug.
Prove it the same way - ask this port's `MatchAtStart` at each position, and upstream - then flip
the assertion and say in its comment that S40b flipped it and why.

### 3. Then the two pinned wrong answers go

`PartialMatchingTests.A_skip_in_the_non_partial_pass_...` asserts the wrong answer on purpose. When
the fix lands it asserts the right one. A slice that fixes the defect and leaves either test pinning
the old answer has not finished.

## Verification

- The minimised row and the reversed row both as permanent tests, measured against upstream and
  quoted, before any fix.
- A negative control that fires: mutate the restore away and count the divergences it brings back
  on the `partial` and `verbs` generators at three seeds.
- The four wave rows above, replayed with `-Rows` and `-SkipRecord`, all agreeing.
- `bounded-lazy-repeat-partial`'s rows must not move: that entry is a different mechanism and its
  staleness alarm is the check.

## Done when

- [ ] The partial pass starts where the search was asked to start, or the reason it cannot is
      written down with its measurement.
- [ ] The reversed row is judged, not merely observed.
- [ ] S37's pinned answer re-judged, and both "wrong answer on purpose" tests now assert the right
      one.
- [ ] The four wave rows agree; ratchet GREEN, baseline updated, blind review, commit.
