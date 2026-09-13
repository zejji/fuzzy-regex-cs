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

- [x] The partial pass starts where the search was asked to start, or the reason it cannot is
      written down with its measurement.
- [x] The reversed row is judged, not merely observed.
- [x] S37's pinned answer re-judged, and both "wrong answer on purpose" tests now assert the right
      one.
- [x] The four wave rows agree; ratchet GREEN, baseline updated, blind review, commit.

---

## Closing notes (2026-09-13)

**The restore goes at BOTH ends, and the premise that said otherwise had the sign backwards.**
`Matcher.DoMatch` now saves `SliceStart` and `SliceEnd` alongside `textPos` and restores all three
before the partial retry. Three lines of engine change; everything else in this slice is judgement
and evidence.

**What this slice overturned.** S40a recorded the reversed row as the reason NOT to restore
`slice_end` - "restoring it moves what every end-of-subject assertion means" - and left the whole
fix out on that basis. Measured instead of inherited, the row says the opposite: **upstream has this
defect too, in reverse**, where its `search_start` prefilter does not mask it. A reversed search is
anchored by its end and tries the highest `endpos` first, so the answer it owes is the first anchor
that matches. Upstream's search answers the zero-width partial at (0, 0), the *last* anchor it would
try, while its own `match(0, 1, partial=True)` answers (0, 1). `(*PRUNE)` - which prunes backtracking
identically and moves no bound - answers (0, 1) as well, and that is what makes the bound move the
cause rather than the pattern's meaning. So restoring `slice_end` does not introduce a defect; it
removes one this port shared with upstream, and the divergence that appears is this port being right.

The one premise of the slice file that was NOT overturned: its "(2, 2)" for the S37 row is index and
length, and reads (2, 4) as a span. Same answer. A units mismatch nearly filed as a correction.

**Four rows fixed, three reclassified, and the gate is down from fifteen to nine.**
`tools/run-oracle.ps1 -Count 6000` at seeds 7, 4242 and 20260913 now leaves **2 + 4 + 3 = 9**
diverging rows, and every one is S40c's seven (the group-call partial leak) or S40d's two (the
reversed carried slice). All four rows this slice owned - 97927, 99961, 96679, 99823 - agree.

Three reversed rows became new divergences *because of* the fix, all the same shape, all judged the
same way, and all three are now the `partial-retry-reversed-slice` entry in `ExpectedDivergences.cs`:
rows 101560 (seed 7), 96397 and 99556 (seed 20260913), **two seeds of the three, none at 4242** -
stated the way the S37 review required. Each row is listed individually, so the staleness alarm
checks each one.

**Two pinned answers flipped, and both now assert the right thing.**
`A_skip_in_the_non_partial_pass_moves_the_slice_and_the_partial_pass_is_no_longer_leftmost` became
`..._does_not_move_the_slice_the_partial_pass_searches`, and S37's
`A_skip_alternation_partial_starts_where_this_port_ran_out_of_text` became
`..._starts_at_the_leftmost_position_that_matches`. The S37 row is still a divergence, but only
upstream's prefilter is left in it: this port now answers (2, 2), which is upstream's own
`match(pos=2, partial=True)`, and `search-start-partial`'s judged rows 1 and 4 moved from (4, 0) and
(5, 0) to (2, 2) and (3, 2) - both upstream's own anchored answers. **That narrows the family**: a
row whose only difference is where this port *started* is now a defect to fix, not a row to classify.

**What settles a row of this kind is self-refutation, not upstream.** A search that reports a
position its own anchored matcher beats is wrong whatever upstream says. That is what decided all
three parts of this slice, and it is why the reversed half could be judged against an upstream that
disagrees with it.

### Negative controls

Both re-run against the exact committed code and generator, after the last change, on
`pwsh -File tools/run-oracle.ps1 -Generator partial,verbs -Count 6000`. Committed code:
**1 + 2 + 2 = 5** diverging at seeds 7, 4242, 20260913, and **3** at seed 31.

> **Control A, `no-restore`**: in `src/FuzzyRegex/Engine/Matcher.cs`, `DoMatch`'s partial-retry arm,
> change
> ```
>                 state.TextPos = textPos;
>                 state.SliceStart = sliceStart;
>                 state.SliceEnd = sliceEnd;
> ```
> to
> ```
>                 state.TextPos = textPos;
>                 _ = sliceStart;
>                 _ = sliceEnd;
> ```
> Wave: `partial,verbs`, 6000 rows per generator. Result at seeds 7, 4242, 20260913:
> **2 + 3 + 4 = 9 diverge** against the committed 5. Re-run at seed 31: **4 diverge** against the
> committed 3. Also reds three permanent tests (the two flipped ones and the new reversed one).

> **Control B, `forward-end-only`**: same place, change only the third line, leaving
> `state.SliceStart = sliceStart;` in place:
> ```
>                 state.SliceEnd = sliceEnd;
> ```
> to
> ```
>                 _ = sliceEnd;
> ```
> Wave: `partial,verbs`, 6000 rows per generator. Result at seeds 7, 4242, 20260913:
> **1 + 2 + 2 = 5 diverge - IDENTICAL TO THE COMMITTED CODE.** Re-run at seed 31: **4 diverge**
> against the committed 3.

**Control B is the one worth carrying forward, and it is a finding about the gate rather than a
tick.** On the gate's own three seeds the oracle *cannot tell the half-fix from the fix*: the count
is the same, and only the classification moves, because upstream shares the reversed defect and a
port that shares it too simply agrees. Two things do separate them - this port's own self-consistency
(the permanent test and the staleness alarm, which both red under B), and **a seed the gate does not
use**. At seed 31 B leaves 4 where the fix leaves 3, and the row it fails to fix is
`(?r)^(\p{Lu}{2}?){2,4}\1([^\p{L}])*(?:\d?(*SKIP)[\p{L}\p{N}]|[[:digit:]])` - reversed, so only the
`slice_end` half reaches it, and restoring that half makes this port **agree with upstream** there.
That is the strongest single piece of evidence for both ends, and the gate's three seeds do not
contain it. Do not re-derive this decision from a divergence count on seeds 7, 4242 and 20260913.

**Row indices need their command.** A row index counts across every generator in a run, so
"row 101560" means nothing without `-Count 6000` and the full default generator list (126,000 rows a
seed). The blind review looked for these rows in a 12,000-row `partial,verbs` wave, found them at
5560, 397 and 3556, and reported the provenance as wrong. The entry now names the command; nothing
depends on the index, because the entry is keyed on each row's own question.

### For S40c and S40d

**Seed 31 has three unjudged divergences on `partial,verbs` at 6000 rows, and they are not this
slice's.** Verified present both with and without the fix, so S40b neither caused nor cured them:
`(?r)\M(?:\d(*SKIP)\D|\D)` (upstream (0,9) partial, port (0,1)),
`([^a]+)(?P<g2>\S)(?:(?(2)(?<=(?P>g2))[\p{L}\p{N}])){0,0}?([^a-f])`, and
`(?r)((?>\D++(*PRUNE).))(?:\p{ASCII}(*SKIP)){2,3}`. The gate runs three seeds and 31 is not one of
them, which is exactly how they stayed invisible. Worth a look when S40d sets the gate's final shape.

### Review

One blind pass over the full diff, briefed for reproductions only, run as a blocking call.
**Two findings raised, one reproduced, one fixed - plus one the reviewer flagged in passing that was
real.**

- *"The three rows' indices do not exist in the waves the comment names."* **Not reproduced.** The
  reviewer replayed a 12,000-row `-Generator partial,verbs` wave; the gate is the full generator list
  at 126,000 rows a seed, where the indices are exactly 101560, 96397 and 99556 - re-confirmed on the
  final gate run. The underlying complaint was fair even though the claim was not: the provenance did
  not name the command that makes an index meaningful. **Fixed** by naming it in all three places.
- *"The tree ships red at three of four seeds."* **Reproduced and correct, and not a defect in this
  diff** - the reviewer's own toggle test confirmed the change removes divergences and adds none.
  The nine remaining rows are S40c's and S40d's scope, and the seed-31 three are recorded above.
  No code change.
- *In passing:* the `run-oracle.ps1` header said "seven named families" against eight entries at
  HEAD, and this slice carried the off-by-one forward as "eight" against nine. **Real, and fixed** -
  both statements of the count now read "nine".

A **second blind pass** was run, over the delta the first reviewer never saw. That delta is entirely
comment and doc-comment text, but it makes specific measured claims - the two control numbers, the
seed-31 row and its "agrees with upstream" verdict, the row indices, the quoted upstream output, and
the UTF-16 arithmetic on row 4 - so it was briefed to verify each claim by running it rather than to
re-review the code. It returned **ALL CLAIMS VERIFIED**, with the tree confirmed byte-identical to
the reviewed diff and the ratchet GREEN.

Ratchet GREEN: 5826 tests, 5771 passing, 5663 distinct ids baselined (up from 5662 - one net new
test, two renames accepted with `-AcceptRemovals`). 55 skipped, all `(?e)`/`(?b)`.
