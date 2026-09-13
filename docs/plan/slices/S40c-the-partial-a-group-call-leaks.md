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

- [ ] The ASCII leak is explained, not described.
- [ ] The family is judged one way or the other, with the minimised rows as permanent tests.
- [ ] The seven rows either agree or are accounted for by one entry with its ledger draft.
- [ ] Ratchet GREEN, baseline updated, blind review, commit.
