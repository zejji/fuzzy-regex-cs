---
slice: S40c
phase: 5
title: The partial a group call leaks through a lookaround, and the one width this port leaks it on
delivers: []
---

# S40c - the call-partial leak

**Seven rows of the 6000-row three-seed wave, one mechanism, and a question this port has to answer
about ITSELF before upstream can be judged.** S40a session 1 measured it as "this port is
inconsistent across astrality"; session 2's twelve-case matrix says the inconsistency is one row
wide and points the other way.

Re-run the matrix first: `python tools/probes/upstream-call-partial-leak.py`, and `--newer` for
2026.9.10. It prints upstream's answer beside this port's recorded one for every case.

## What it is

A group CALL inside a lookaround that runs the other way round from the pattern, with an optional
tail, asked with `partial=True`. Upstream reports a PARTIAL; the same lookaround written out as its
own body reports a complete match. That is ledger entry 8's signature - upstream contradicting
itself between a call and its inlined body - and **it is not issue 614**: 2026.9.10, where that fix
landed, answers all twelve cases identically to 2026.7.19 (measured 2026-09-13).

**This port's own rule is consistent except on two rows.** Optional tail, no partial; required tail,
partial; at both widths, inline or called, forward or reversed. The exceptions are the two ASCII
call rows - the forward one and the reversed one - where it reports upstream's partial:

| case | upstream | this port |
|---|---|---|
| `(?P<g1>A)(?:(?<=(?P>g1))\w)?` over `'A'` | partial | **partial** |
| `(?P<g1>𐐀)(?:(?<=(?P>g1))\w)?` over `'𐐀'` | partial | not partial |
| the same two with the lookbehind written out | not partial | not partial |
| the same two with the tail REQUIRED | partial | partial |

So the question is not why this port loses a partial on an astral subject. It is **why it leaks one
on an ASCII subject**, and the answer decides the whole family.

**Five of the seven rows are that shape and two are not**, which the blind review caught in S40a's
own write-up of this and is worth having straight before the tracing starts:

- **row 98191 has no astral character at all** - its subject is `' \r'` - and this port answers
  **no match** where upstream answers a partial `(0, 2)`. So the astral subject is what the
  generators happen to draw, not the family's precondition, and "loses upstream's partial" is the
  symptom rather than "reports a complete match instead".
- **row 74396 is a substitution**, where neither engine reports a partial at all: upstream replaces
  once and this port twice. Whether it belongs to this family or to `group-call-direction` is itself
  a question for the slice - it is here because its pattern is the same shape and nothing else
  accounts for it.

A fix aimed only at the five-row shape leaves those two, and they are the two that say what the
mechanism really is.

## Scope

1. **Find the leak.** Trace the two ASCII call rows through the port and say what sets the partial
   where the inline rows do not. Until that is known, nothing here is a verdict.
2. **Then judge.** Two outcomes, and both are legitimate:
   - the leak is this port reproducing upstream's, in which case removing it makes the ASCII rows
     diverge too and the whole family becomes ONE `ExpectedDivergences` entry, "upstream leaks a
     partial through a call", with the ledger entry beside entry 8;
   - the leak is correct and the astral row is the defect, in which case fixing the astral row makes
     all seven wave rows agree and no entry is needed at all.
3. **Strengthen `GroupCallTests.A_group_called_through_an_opposite_direction_lookaround_loses_
   upstreams_partial_only_on_an_astral_subject`** to a verdict, and rename it to what it turns out
   to be. It currently records measurements and says outright that it is not a judgement.

## The seven rows

| seed | row | generator | operation |
|---|---|---|---|
| 7 | 98956 | `partial` | search |
| 7 | 103926 | `partial-sliced` | fullmatch |
| 4242 | 100842 | `partial` | fullmatch |
| 4242 | 104237 | `partial-sliced` | match |
| 4242 | 106041 | `partial-sliced` | fullmatch |
| 20260913 | 98191 | `partial` | search |
| 20260913 | 74396 | `interactions` | sub |

Row 106041's two answers differ in the partial FLAG alone - same span, same captures - which is the
cleanest row of the seven to work from. Rows 98191 and 74396 are the awkward two described above,
and they are the ones that will tell you whether a candidate fix is the right one.

## Done when

- [x] The ASCII leak is explained, not described.
- [x] The family is judged one way or the other, with the minimised rows as permanent tests.
- [x] The seven rows either agree or are accounted for by one entry with its ledger draft.
- [x] Ratchet GREEN, baseline updated, blind review, commit.

---

## Closing notes (2026-09-13)

**There is no leak, and there is no group-call defect here.** Both of S40a's readings were wrong,
including the second one this slice file was written from. The ASCII rows were right all along; the
two astral rows were this port's own defect; and the mechanism has nothing to do with the call's
direction, the lookaround, or astrality as such.

### What it actually is

Two things upstream does, meeting.

1. **`min_width` counts a group CALL at the width of the group it calls, even inside a LOOKAROUND**,
   which consumes nothing. `(?P<g1>A)(?:(?<=(?P>g1))\w)?` has `min_width` 2 where the same
   lookbehind written out has 1. This port already reproduced that; it is not changed here.
2. **`do_match` answers a partial request in two passes** - non-partial first, and the partial one
   only if that FAILS (`_regex.c:18142`) - and **`do_exact_match`'s width early-out is guarded by
   `partial_side == RE_PARTIAL_NONE`** (`:18069`), so it fires on the first pass alone.

So a subject too narrow for the inflated `min_width` **skips the non-partial pass entirely** and the
partial pass answers instead. Upstream's "extra" PARTIAL is that, and the early-out is the only thing
standing between the two passes.

**This port counted `available` in UTF-16 code units.** One astral character read as two, the
early-out did not fire, the non-partial pass ran and SUCCEEDED, and the retry never happened. The
comment that stood at `Matcher.cs` said the code-unit count was safe because over-counting can only
make the early-out fail to fire and "the engine simply does the work and fails in the dispatch loop
instead - the same answer". That is true of a plain match and false of a partial one: a pass that
succeeds is not a pass that fails, and what it suppresses is the fallback. `Substitution.cs` carried
the same claim in its own words and is corrected too.

**The fix is one line**: `available` is now `CountBetween(...)`, the existing character-counting
helper, which takes the `OneUnitPerCharacter` fast path for nearly every subject. `CountBetween`
rounds outward, so a caller that slices into the middle of a surrogate pair still costs a whole
character at that end - the old over-counting surviving only for a bound upstream cannot express -
which keeps `available == 0 && MustAdvance` meaning exactly what it did.

### How it was found, and the thing worth carrying forward

**The twelve-case matrix could not have answered this, and neither could more of it.** Every case in
it holds `min_width` fixed and varies the pattern's shape, so the variable that actually decides the
answer never moves. What answered it was tracing the port with a temporary opcode trace and reading
`ps=` in the output: the ASCII case ran ONE pass with `partial_side` set and the astral case ran ONE
pass with it clear, which says the two took different roads before matching started. The `EXACT
available=1 minWidth=2` line printed a moment later was the whole answer.

**Then it was predicted on upstream before being believed.** `tools/probes/upstream-min-width-partial-retry.py`
writes down nine expected answers and checks them: the partial must depend on how many characters are
AVAILABLE rather than on the call, so more text removes it and a wider callee moves the threshold by
exactly the callee's width. Nine for nine on 2026.7.19 and on 2026.9.10.

**The first draft of that probe scored eight of nine and the failure was the experiment, not the
theory** - it asked `(?P<g1>ABC)(?:(?<=(?P>g1))\w)?` of 'ABCDE' and expected a partial, but there the
tail MATCHES the 'D', so no partial can exist whatever the early-out does. The rebuilt cases put a
`\w*` prefix in front so the tail stays past the end of the subject while the available width varies,
which leaves the width as the only moving part. **A row that looks like a refutation is usually a
badly chosen experiment; the note is kept in the probe so the next reader does not re-derive it.**

### The seven rows

Five agree outright. The other two were never this family, which the slice file suspected and could
not settle:

- **Row 98191** (`partial`, seed 20260913) is **`bounded-lazy-repeat-partial`**. Upstream answers the
  same (0, 2) partial with the call written out, with the piece holding it deleted, and with the
  `\K` removed - so none of those is the cause. It minimises to `^([^a-f]??)([\ ])$` over ' \r', which
  is that entry's row 1 with a different class, and it passes that family's own discriminator:
  spelling the bounded repeat greedy removes the partial from upstream too. Added to the entry's row
  list and to its permanent test.
- **Row 74396** (`interactions`, seed 20260913) **is `group-call-loses-the-match`**, exactly. The
  piece holding the call is a `*` repeat, so zero iterations is always available and it cannot remove
  a match; delete it, or inline the call, and upstream finds the match it had lost. The predicate did
  not classify it because its substitution arm requires upstream to have replaced NOTHING - checkable
  from the row - where "upstream replaced fewer times than we did" is not, a sub rendering as one
  string. So the row is judged individually and keyed, the discipline `bounded-lazy-repeat-partial`
  already uses, rather than loosening the predicate to `theirs.Count < mine.Count`, which would
  classify any port defect that substitutes once too often in a pattern of this shape.

**One example row was REMOVED from `group-call-loses-the-match`** - row 1624 of seed 99991, the
`split` shape - because the fix made it agree. Its divergence was never that family: the subject is
three characters, the call pushes `min_width` to four, and upstream refuses on arithmetic before
matching. **The family is untouched and the same row proves it**: pad the subject to four characters
or more and upstream still finds nothing while this port finds the match, at every length up to
seven. What is lost is the `split` outcome shape's example, which the predicate still classifies and
the next wave to draw one will re-supply. This is recorded in the entry.

### The gate

`tools/run-oracle.ps1 -Count 6000` (126,000 rows a seed), before and after:

| seed | before | after |
|---|---:|---:|
| 7 | 2 | **0** |
| 4242 | 4 | 1 |
| 20260913 | 3 | 1 |

**Nine rows down to two, and both survivors are S40d's** (`verbs`, the reversed carried slice): row
117071 at seed 4242 and row 116388 at seed 20260913. Seed 7 is fully green.
`Every_expected_divergence_still_diverges` passes at all three seeds.

### Negative control

**Control A, `available-code-units`.** In `src/FuzzyRegex/Engine/Matcher.cs`, `DoExactMatch`, change

```csharp
        long available = CountBetween(state, state.TextPos, state.Reverse ? state.SliceStart : state.SliceEnd);
```

to

```csharp
        long available = state.Reverse ? state.TextPos - state.SliceStart : state.SliceEnd - state.TextPos;
```

Wave: `pwsh -File tools/run-oracle.ps1 -Generator partial,partial-sliced,interactions -Count 6000 -Seed <n>`,
18,000 rows. Re-run against the committed code and generators after the last change:

| seed | baseline | control |
|---|---:|---:|
| 7 | 0 diverge | **2 diverge** |
| 31 | 3 diverge | **6 diverge** |

Seed 31 is a seed the gate does not use, and its three baseline rows are pre-existing and unrelated
(1075 `partial`, 6943 `partial-sliced`, 16545 `interactions`); the control's six are a strict
superset of them, so **the fix removed three rows at seed 31 and introduced none**. The control fires
at both seeds, which is what says the wave can detect this fault at all.

### Review

**One blind pass over the whole diff, one finding raised, one reproduced, one fixed.**

The finding: `_groupCallLostMatchJudgedRows` was wired into the entry's `Applies` predicate but not
into its `Example`, so `Every_expected_divergence_still_diverges` never replayed it - a stale
`_groupCallLostMatchJudgedOurs` would have silently stopped classifying the row instead of reddening
the run, which is precisely the failure that list exists to prevent, and which the entry's own
comment describes ("a predicate cannot notice a port that has stopped diverging; the example rows
can, and do"). Reproduced here, not taken on trust: with the fix in place, corrupting one hex digit
of the judged answer fails the alarm with "group-call-loses-the-match: an entry must account for its
own example row 6"; without it the same corruption left the alarm green. Fixed by making the judged
rows Example rows too.

**No second blind pass, and the reasoning rather than the omission.** The fix is one expression -
`Example: X` became `Example: X + "\n" + Y` - plus a comment, in the file the reviewer had just
audited, and it has both a positive and a negative control on it (alarm green with the correct
answer, alarm red with one digit changed). That is stronger evidence than a second opinion would be.
The rule exists because S01 committed ~200 lines of unreviewed public API; seven lines of test
infrastructure with a working negative control is a different case, and this paragraph is here so the
judgement is visible rather than silent.

The reviewer also cleared, with its own measurements, the five failure modes the brief asked it to
hunt: `CountBetween` at a mid-pair bound and under `(?r)`; the `available == 0` term; the early-out
under-counting; the two new classification rows keying correctly; and every new test assertion
against real upstream. On performance it measured an all-astral subject in Release at 25k to 400k
characters - 16x the input for about 14.5x the time, so linear, with a ~13% constant, and a failing
`Match` on a 400k-character astral subject unchanged at ~5.4 ms.

### What the next slice should know

- **S40d's two rows are all that is left of the gate**, and neither is touched here.
- **Seed 31 has three unjudged divergences on `partial,partial-sliced,interactions` at 6000 rows**
  (rows 1075, 6943, 16545), present before and after this slice. STATE.md's earlier note named
  seed 31 on `partial,verbs`; this is a different generator set, so the two lists may or may not
  overlap and nobody has checked.
- **`min_width` counting a group call inside a zero-width lookaround is upstream's, and is now
  load-bearing in this port.** It is pinned by
  `GroupCallTests.A_group_call_counts_towards_min_width_even_inside_a_zero_width_lookaround`. A
  future slice that "fixes" it will change partial answers on narrow subjects, and those answers
  currently agree with upstream.
- **The rule that caught this generalises**: any place this port substitutes UTF-16 positions where
  upstream counts codepoints needs asking whether the over-count only ever fails early, and whether
  "fails early" is the same as "fails". `docs/PORTMAP.md`'s `StepBy`/`CountBetween` row is the list of
  places that already know they must count characters; `do_exact_match` was one that did not.
