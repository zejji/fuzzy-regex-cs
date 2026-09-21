# S57d sittings

Per-sitting working notes. The slice file stays the spec.

## Sitting 1 (2026-09-21)

### What the second engine says, and the two questions it settles

Two probes. `tools/probes/pcre2-partial-truncation-assertions.py` already existed and re-ran
unchanged, so ledger entry 21's evidence still reproduces on PCRE2 10.47 (2025-10-21):

```
'(?!(True|False)\\b)(.*)'  'True'    plain=None           SOFT=PARTIAL (0,4)    HARD=PARTIAL (0,4)
'True\\b'                  'True'    plain=match (0,4)    SOFT=match (0,4)      HARD=PARTIAL (0,4)
'True\\B'                  'True'    plain=None           SOFT=PARTIAL (0,4)    HARD=PARTIAL (0,4)
```

`tools/probes/pcre2-hitend-partial-span.py` is new, because implementing the model forces questions
the first probe never asked: every row in it is ANCHORED at offset 0, so it cannot say which span an
escalated partial reports. Measured 2026-09-21:

```
# A. Unanchored search: is ovector[0] the end-reaching attempt's start, or the subject's?
  'cd\\B'    'xabcd'   plain=None           SOFT=PARTIAL (3,5)    HARD=PARTIAL (3,5)
  'd\\B'     'xabcd'   plain=None           SOFT=PARTIAL (4,5)    HARD=PARTIAL (4,5)

# B. Two end-reaching attempts and no match at all: which start is reported?
  'a*c\\B'   'ac'      plain=None           SOFT=PARTIAL (0,2)    HARD=PARTIAL (0,2)
  'a*c\\B'   'xac'     plain=None           SOFT=PARTIAL (1,3)    HARD=PARTIAL (1,3)

# C. Does a hitend-derived partial carry capture groups?
  '(a)(b)\\B'  'ab'  SOFT=PARTIAL (0,2)  groups: (8819262122025316210,4981658938864334708), ...
```

So: **the span is the end-reaching attempt's start to the end of the available text**, the start is
the **leftmost** such attempt (`a*c\B` over `'ac'` reports 0, not the later attempt at 1), and **no
capture groups are defined** - the garbage in C is pcre2partial(3)'s "the values in the rest of the
ovector are undefined" measured rather than quoted.

pcre2partial(3), read 2026-09-21 at <https://www.pcre.org/current/doc/html/pcre2partial.html>,
states the model in one sentence: under the soft option "the partial match is remembered, but
matching continues as normal", and "if no complete match can be found, PCRE2_ERROR_PARTIAL is
returned instead of PCRE2_ERROR_NOMATCH". It also names the assertions this is about - the buffer
end may not be the real data end, "which is why \z, \Z, \b, \B, and $ always give a partial match".

### The design

The port already has PCRE2's soft structure, in `Matcher.DoMatch` (`:10674-10751`): a partial
request runs a complete-match pass with `PartialSide` cleared, and only on failure re-runs with the
partial side set. A definite complete match therefore already wins, which is the half of the model
S50 did not need to build.

What is added is the other half, and the point of it is that **nothing returns early**:

1. A `HitEnd` flag on `MatchState`. The seven word and grapheme boundary predicates set it when they
   are asked about a position at the truncation point, **whichever way they then answer**, and go on
   to return their ordinary verdict. The `(?!(True|False)\b)(.*)` row needs the "whichever way" part:
   there the `\b` SUCCEEDS at the end of 'True', and it is the enclosing negative lookahead that
   fails, so a flag set only on a failing boundary would never fire.
2. `HitEndMatchPos`, written once and not overwritten, so the span is the leftmost end-reaching
   attempt's start, as probe row B requires.
3. In `DoMatch`, when the partial pass returns Failure and `HitEnd` is set, the answer becomes a
   partial spanning `HitEndMatchPos` to the end of the available text, with no groups.

This is why it keeps the two `(\.+?)\1\b` capture rows that killed S50. Over `'..'` the lazy repeat's
first try reaches the `\b` at 2, sets the flag and **returns Failure as before**, so backtracking
still grows the repeat to `(0, 2)` and the match still ends as a partial by the ordinary route -
which does not consult `HitEnd` at all.

### Narrowing 1 is a deliberate departure from PCRE2, not fidelity to it

Probe section D asked whether an attempt that has consumed nothing still escalates, and PCRE2 says
it does: `\b` over `''` is `SOFT=PARTIAL (0,0)`. So S50's first narrowing - the attempt must have
consumed something - is this port choosing differently from its own model, and the closing notes
have to say so rather than imply PCRE2 backs it.

### Limb 3 of the twin's stated reasoning is refuted

`PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_finds_no_
partial_here` gives three reasons for pinning `(?r)\b$` over `''` as None. Probe section E tests the
third of them:

```
# E. Is D the boundary escalating, or a start optimisation? (PCRE2_NO_START_OPTIMIZE)
  '\\b'    ''    SOFT=PARTIAL (0,0)    SOFT+NO_START_OPTIMIZE=PARTIAL (0,0)
  '\\B'    ''    SOFT=match (0,0)      SOFT+NO_START_OPTIMIZE=match (0,0)
  '$'      ''    SOFT=match (0,0)      SOFT+NO_START_OPTIMIZE=match (0,0)
  '\\b\\b' ''    SOFT=PARTIAL (0,0)    SOFT+NO_START_OPTIMIZE=PARTIAL (0,0)
```

The comment says PCRE2's partial there comes from "the next pattern item must be one that inspects a
character" test, "which upstream deliberately does not share - so it is not a second opinion on the
same question". A bare `\b` has no next pattern item, and it is still PARTIAL; `\b\b` is still
PARTIAL; turning the start optimisations off changes nothing; and `\B` and `$`, which SUCCEED on an
empty subject and so never fail into anything, are plain matches. PCRE2 is answering on the boundary
that failed at the buffer end, which is exactly ledger entry 21's rule. That limb has to go whatever
the verdict on the row turns out to be.

Limb 2 - the maintainer's own issue-589 argument, that `\b` is evaluated against the real string -
is the argument entry 21 rejects, so this slice cannot keep a verdict resting on it either. Limb 1,
that upstream's own `match` and `fullmatch` answer None where only its `search` answers a partial,
is untouched by any of this and still stands.

### Open at the end of sitting 1

Whether the twin row stays pinned. It falls on the narrowing's side of the new rule - nothing is
consumed - so the uniform rule keeps it None without needing limbs 2 or 3. That is a tidy answer and
it is the one to be most suspicious of, so it gets the ablation measurement rather than the argument.

## Sitting 2 (2026-09-21, interrupted)

Implemented the model and wrote the tests. See the code for what landed; this section records only
what a later sitting cannot re-derive.

## Sitting 3 (2026-09-21, checkpoint at the allowance hook)

### What the reversed row cost, and what it taught

The 6000-row gate was green at seeds 7 and 4242 first time and RED at seed 20260921 on one row,
98169: `(?r)\b(?(?!\p{L}).|[^a])\K(\s)` over `"\r\n"`, fullmatch, partial. The ablation
(`status = MatchStatus.Failure` in place of the escalation) made the row agree, so the escalation
caused it and the fault was in the new `ExpectedDivergences` entry, not the engine.

The predicate's span limb was forward-only. A reversed match runs out of text at its START, so the
escalated partial reaches position 0 and the far end is wherever the attempt had got to
(`Matcher.cs:10867`, `state.TextPos = state.Reverse ? state.SliceStart : state.SliceEnd`). Widened to
`IsReversed(row) ? start == (row.Pos ?? 0) : start + length == (row.EndPos ?? row.Subject.Length)`.
Gate then GREEN at 20260921 with 11 rows classified, all read and in-family.

Upstream's reversed rule was measured, not reasoned:  `(?r)\Ba` over `'a'` is None on both anchored
doors and `(?r)\Ba+` over `'a'` is PARTIAL (0, 1) on all three. Same text-exhaustion rule, read in
the direction of travel. Rows added to
`tools/probes/upstream-partial-needs-text-exhaustion.py`.

### The two controls, as they must be re-run

**Control A - remove the consumed-something narrowing.** In `Matcher.cs`,
`NoteBoundaryAtTruncationPoint` (`:1838`), the file reads:

```csharp
bool atTruncationPoint = state.PartialSide switch
{
    MatchState.PartialRight => textPos >= state.TextEnd && textPos > state.MatchPos,
    MatchState.PartialLeft => textPos <= state.SliceStart && textPos < state.MatchPos,
    _ => false,
};
```

Drop `&& textPos > state.MatchPos` and `&& textPos < state.MatchPos`. Wave: the default generator
set, 300 rows each. Result: **14 divergences** at each of seeds 7, 4242 and 20260921, and **12** at
unused seed 31337.

Control A earned its keep by failing first. With the narrowing gone the wave showed **diverge 0**,
because the new entry was classifying the zero-width partials the narrowing exists to prevent. That
is what put `&& length > 0` into the predicate; the 14/14/14/12 above is the re-measurement after it.

**Control B - remove `state.ClearGroups()`** from the escalation in `Matcher.DoMatch` (`:10837`),
replacing the line with `_ = state;` so no analyzer fires. No generator reaches it: 0 divergences on
the default wave at three seeds, 0 on `partial,partial-sliced,interactions` at `-Count 6000` seed 7
(18,000 rows), 0 on `verbs` at `-Count 6000` seed 7, with the baseline agree counts unchanged
(6340 / 6346 / 6359, expected 34 / 29 / 19). Hand-built rows found the one reachable shape:
`(a)(*SKIP)(b)\B` over `'ab'`, where the verb prunes the backtracking unwind, so group 1 survives at
(0, 1) with `lastindex` 1. Ordinary backtracking restores every group on the way out, which is why
`(a)(b)\B` cannot detect the fault. That row is now an `[Arguments]` case on
`A_boundary_partial_reports_no_groups` and it goes RED under the ablation. Upstream answers None to
it, measured.

So Control B's evidence is a test, not a wave number, and the generators' blind spot is worth
saying out loud: no generator emits a control verb next to a capture group next to a boundary.

### State at the checkpoint

Green and committed: full suite 6528/6528, ratchet GREEN (with `-AcceptRemovals`, for the inherited
pin's deliberate rename from `A_partial_fullmatch_still_denies_...` to
`A_partial_fullmatch_reports_...`), default oracle wave GREEN at all three seeds.

A late reading of the prose caught a wrong example. `True\b` over `'True'` was quoted in
`docs/COMPARISON.md` and in the test's doc comment as the streaming hazard this divergence fixes, and
it is not: a complete match wins in both engines, so that row is identical here and upstream.
Replaced everywhere with the row that does diverge, `(?!(True|False)\b)(.*)` over `'True'`, whose
next chunk of `'s'` makes `'Trues'` and matches (measured, regex 2026.9.10, 2026-09-21).

Still to do, in order:

1. Re-run the `-Count 6000` gate at seeds 7, 4242 and 20260921 against the committed code. Seeds 7
   and 4242 were green before the predicate's `length > 0` limb landed, so all three need re-running.
2. Final re-run of both controls against the code about to ship, per the skill's rule that a control
   measured mid-slice measures code that no longer exists.
3. `docs/PORTMAP.md`: no upstream symbol was ported here (the model is PCRE2's, not upstream's), so
   the expectation is no row, but the slice has to say so rather than skip the check.
4. Blind review, then the independent verifier over the one judged divergence (the `(?r)\b$` twin).
5. Close the slice: `git mv` to `done/`, closing notes with the Review paragraph and both control
   recipes above, `STATE.md`, `DECISIONS.md`.
