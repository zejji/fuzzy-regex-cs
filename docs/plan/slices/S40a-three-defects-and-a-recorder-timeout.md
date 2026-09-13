---
slice: S40a
phase: 5
title: The three defects S40 recorded, and the recorder timeout that lets a 6000-row wave finish
delivers: []
---

# S40a - Three recorded defects, and a recorder that survives an upstream hang

Added at S40's close on 2026-09-13, on the Phase 4 precedent that S33, S34 and S35 set: **every one
of these came out of verification getting stricter, and each is a real defect.** None is S40's
doing and none is a new feature; S40 proved each one and deliberately fixed none of them, because
fixing four unrelated engine and tooling problems inside a slice about the `{...:test}` constraint
is how a slice stops being reviewable.

The ROADMAP's Phase 5 note predicts exactly this ("its 5-8 counts fuzzy features, and the oracle
will add slices to it"), and amendment 16 is why they cannot simply be logged: a bug identified with
overwhelming evidence is fixed here or filed upstream before 1.0.

**Do the recorder first.** Items 2 and 3 are only measurable once a 6000-row default wave can
finish, and item 1 is what stops it finishing.

## Scope

### 1. The recorder has no per-row timeout, so one hanging upstream row kills a whole wave

`tools/record-oracle.py` runs every row through upstream and waits forever. At 6000 rows a
generator, `verbs` row 5944 at seed 4242 never returns, so the wave never gets written and the
`run-oracle.ps1` run simply stops - no output, no error, no partial file. S40 lost about forty
minutes to that before bisecting it.

`regex` takes a `timeout=` keyword on the match methods and raises `TimeoutError`, measured on
2026.7.19:

```python
pat.search(subject, timeout=5)   # TimeoutError: regex timed out
```

So the row becomes a recorded outcome rather than a wave-killer. Decide what the row's outcome
should be - a new `timeout` kind alongside `error`, most likely, which the consumer skips the way
it skips `unsupported` - and say why in DECISIONS. **Do not** simply drop the row silently: a
generator that quietly stops emitting a shape is the failure S38's and S39's controls each hit from
a different direction.

### 2. Upstream never returns from `(*SKIP)` inside an atomic group after an optional item

Minimised by hand from the row above, on regex 2026.7.19:

> `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')` never returns.

All three parts are needed. `(*PRUNE)` in place of `(*SKIP)` returns `None`; a non-atomic `(?:...)`
returns `None`; dropping the leading `.?` returns `None`. The original was
`[^a]?\U0001f600(?>[a\d]{1,3}(*SKIP)\p{Ll})` on `'\U00010400\r_\U0001f600\xdf\U0001f600aA'`.

This is a new upstream infinite loop, the same family as issues 551 and 554 but a different shape.
Give it the S33 treatment - probe, research the tracker for a duplicate, verdict, blind review -
and then a **ledger entry** in `docs/plan/upstream-reports/LEDGER.md`. Nothing is filed upstream
until everything else in the plan is done, and the owner approves the text first. Check what this
port does with the same pattern: if it also hangs, that is a second, separate defect and this port's
is the one that gets fixed.

### 3. Four rows of the 6000-row default wave diverge at seed 7, all of them predating S40

Proven against HEAD `26e2a20` by consuming the identical saved wave with `-SkipRecord` in a
worktree: HEAD reports the same four rows.

| row | generator | operation | pattern |
|---|---|---|---|
| 97927 | `partial` | search | `\b\K\K(?:\D(*SKIP)[^\p{L}]\|.)` |
| 98956 | `partial` | search | `𐐀(?P<g1>[𐐀])(?:(?(1)(?<=(?P>g1))[\p{L}\p{N}]\|\p{L}))?\b` |
| 103926 | `partial-sliced` | fullmatch | `(?r)\b(?P<g1>[\w\s]+)(?:(?(1)(?!(?P>g1))\s))+?\b\M` |
| 117679 | `verbs` | finditer-overlapped | `\b(?:[^a](*SKIP))*` |

Two of the four are partial matching plus a stale-`\K`/group-call interaction and two are `(*SKIP)`;
117679 looks like the `overlapped-skip-stale-slice` family whose predicate does not cover it.
Minimise each into a permanent test and judge it by amendment 16: port's fault, fixed; upstream's
fault, pinned and entered in the ledger; predicate too narrow, widened in
`ExpectedDivergences.cs` with the reason.

### 4. A fuzzy section inside a lookbehind reports change positions that contradict its own counts

Found by S40's blind review and reproduced there against both engines.

| pattern | subject | upstream | port |
|---|---|---|---|
| `(?<=(?:[ab][cd]){e<=1})$` | `axc` | `counts=(1,0,0) changes=([2],[],[])` | `counts=(1,0,0)`, `dels=[1]` |
| `(?<=(?:abc){e<=2})$` | `ac` | `counts=(1,0,1) changes=([1],[],[0])` | `counts=(1,0,1)`, `dels=[1,2]` |

The port's own `FuzzyCounts` and `FuzzyChanges` disagree with each other, which makes this a defect
on its face before upstream is consulted at all. **It is not `(?r)` in general**:
`(?r)(?:[ab][cd]){e<=1}` against `'axc'` reports `subs=[2]`, correctly. So the fault is in what
`record_fuzzy` writes when the section is inside a lookbehind, which is S38 and S39 territory
(`Matcher.RecordFuzzy`, and the change-position arithmetic S38's control F covers).

When it is fixed, **strengthen
`Gaps/Engine/FuzzyTestConstraintTests.The_REV_arms_test_the_character_before_the_position`** to
assert `FuzzyChanges` as well; a comment in that test says so and says why it currently does not.

## Verification

- A gap test per defect, measured against upstream and quoted, before any fix.
- The default wave GREEN at three seeds, **6000 rows** - the thing S40 could not run. That is the
  point of item 1 and it is the slice's real exit gate.
- A negative control for the recorder timeout: a row that hangs must appear in the wave as a
  recorded outcome, and the wave must still be written.
- Ledger entries drafted for anything judged upstream's, not filed.

## Done when

- [ ] The recorder survives a hanging row and records it; DECISIONS says what the outcome kind is.
- [ ] The `(*SKIP)`-in-an-atomic-group hang is researched, judged, and in the ledger; this port's
      behaviour on the same pattern is known and, if it hangs too, fixed.
- [ ] All four seed-7 divergences minimised into permanent tests and judged by amendment 16.
- [ ] The lookbehind `FuzzyChanges` bug fixed test-first, and the S40 test strengthened.
- [ ] Default wave GREEN at three seeds at 6000 rows; ratchet GREEN, baseline updated, blind
      review, commit.
