---
slice: S33
phase: 4
title: Act on the divergence research - fix the port's one bug, pin what is right, settle the open case
delivers: []
---

# S33 - Act on the divergence research

Added at the owner checkpoint of 2026-09-12 after `docs/plan/2026-09-12-divergence-research.md`
judged every divergence S29, S31 and S32 had parked. Read that document first; it is the
specification for this slice. The owner's rule that motivates it: **every conclusively identified
bug gets fixed in the port, and a divergence where the port is right is pinned permanently, never
inverted later.**

## Scope

1. **Fix the reversed empty-slice partial** (port bug). Failing test first:
   `(?r)a(bc)*` on `abc` with `pos=1, endpos=1, partial=True` must be a partial at (1,1), as it
   already is forward and as upstream answers in both directions; also `pos=2, endpos=2`. Find
   the reversed partial arm(s) that compare against `TextStart` where the forward twin's
   behaviour on an empty slice is a partial - S31's notes say half of upstream's arms are
   bounded by `slice_*` and half by `text_*`; list which and port the reversed half to match
   what upstream *answers*, quoting the upstream line for each. Then the `partial-sliced`
   generator must run clean: **put it on the default oracle list.**
2. **Settle `(?r)(ab)+` fullmatch on a narrowed slice** (S32's review finding, pinned in
   `Gaps/Engine/ReverseMatchingTests.cs`). Research doc says upstream bug on the evidence so far:
   upstream fullmatches the same slice at `pos=0` and forward, and `match` succeeds. Find the
   upstream mechanism (a reverse general-repeat guard comparing against `text_start` rather than
   `slice_start` is the hypothesis - prove or refute it by reading `_regex.c` and by probing with
   `pos` varied while the slice text stays `ab`), run a blind review on the verdict, and then
   either mark the test permanent (port right) or fix the port. Do not leave it open.
3. **Re-mark the pinned tests.** In `Gaps/Engine/BacktrackingVerbTests.cs` (both "PHASE 7:
   INVERT THIS TEST" blocks), `Gaps/Engine/PartialMatchingTests.cs` (the lazy-repeat block around
   line 276 and the `\b$` block around line 310), and `Gaps/Engine/ReverseMatchingTests.cs`
   (line 14 and 273): replace every "inverts when Phase 7 lands" instruction with **"permanent:
   the port is right, see docs/plan/2026-09-12-divergence-research.md; a change here is a
   regression"**, quoting the second engine's answer in the comment. The narrowed-slice partial
   block (around line 358) is the one this slice fixes, so its test moves from pinning the
   divergence to asserting the partial.
4. **`verbs` back on the default oracle list**, recorded honestly. The wave's known divergences are
   upstream's (`search_start` bounds, the prefilter jump), so recording `verbs` rows against a
   prefilter-free upstream (S29's `PREFILTER_FREE_GENERATORS`) is the right ground truth for the
   `locate_required_string` shape but not for the `search_start` shape. Extend the wrapper so
   upstream's `search_start` is also neutralised for those rows if `_regex` exposes a way (check
   `pattern->do_search_start` and the `RE_FLAG`s; if not reachable from Python, say so), else
   list the residual rows in a strict manifest that fails when they stop diverging - the
   ROADMAP Phase 6 shape, pulled forward. Either way the default wave must be green with `verbs`
   in it, and every excluded row must be named.
5. **Draft the upstream report** into `docs/plan/upstream-reports/LEDGER.md`: one issue
   per defect (`..(*SKIP)xx` retry-below-commit; lazy-repeat partial `ba??x`; `\b$` reversed
   search inconsistency; the reversed fullmatch if item 2 confirms it), each with a minimal
   runnable reproduction pinned to 2026.7.19, the faulting function named, the fix proposed, and
   the PCRE2 answer quoted as the second opinion. **Nothing is filed** - the owner approves the
   text first; `gh` is outside the driver's allowlist on purpose.

## Verification

- Item 1: the failing test, then green; `partial-sliced` at 2000 rows, two seeds, zero divergences;
  the `partial` default wave unchanged (0 of 6800).
- Item 2: a written verdict with the upstream line reference and the blind review's report.
- Item 4: the default oracle run green with `verbs` present; the excluded rows enumerated.
- Full default oracle list at the end, and every S29 and S31 control re-run (`tools/run-controls.py
  --slices S29,S31`) against the committed code, at the recorded seeds and one fresh one.

## Done when

- [x] Reversed empty-slice partial fixed test-first; `partial-sliced` on the default list, clean.
- [x] `(?r)(ab)+` fullmatch settled with a verdict, a review, and either a fix or a permanent test.
- [x] Every inverted later comment replaced by a permanent verdict quoting the second engine.
- [ ] `verbs` on the default list; residual upstream-side rows named in a strict manifest or
      neutralised at the recorder. **Half done, and the half that is not is a finding, not a
      shortfall of effort - see "What was not done" below.** The residual rows ARE named, in
      `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`. `verbs` is NOT on the default list,
      because the blind review found a fifth, unjudged `(*SKIP)` family at seeds S29 never ran.
- [x] Upstream report drafted, not filed.
- [x] PORTMAP rows for any arm touched; ratchet GREEN, baseline updated, blind review (hunt: a
      "fix" that moves a bound and changes an answer no test covers - run the whole default wave,
      not just `partial-sliced`), commit.

---

## Closing notes, 2026-09-12

### What landed

**Item 1, the port bug, fixed.** The reversed empty-slice partial is `try_match`'s six
`try_match_STRING*` arms (`upstream/src/_regex.c:7383-7668`), which bound themselves with
`slice_end`/`slice_start` where the `STRING*` opcode arms in `basic_match` bound themselves with
`text_end`/`text_start`. `do_match` sets `text_end` and `slice_end` to the same `endpos` (`:18435`),
so the forward halves cannot disagree at the top level and the reversed halves part company the
moment `pos > 0`. Ported as `Matcher.IsStringTestPartial`, reduced to the partial answer only, on
the same terms S31 used for the one-character tests. Test first: the failing assertion is in
`Gaps/Engine/PartialMatchingTests.A_reverse_partial_at_the_left_edge_of_a_narrowed_slice_is_found`,
watched fail, then green.

**The trap is pinned beside it.** This is an opcode-by-opcode answer and not a rule about
`slice_start`: `try_match_CHARACTER_REV` (`:7137`) keeps the `text_start` bound, so `(?r)a` on
`'abc'[1:1]` is `None` in both engines while `(?r)a(bc)*` is a partial in both. A second test,
`The_narrowed_slice_partial_is_a_per_opcode_answer_and_not_a_general_rule`, holds six such rows so
that nobody "simplifies" the fix into one bound.

**Item 2 settled, and it absorbed a second row.** Upstream has three `match_all` checks and one of
them disagrees with the other two: `try_match`'s `RE_OP_SUCCESS` arm (`:7832`) bounds a reversed
fullmatch by `text_start`, where `basic_match`'s own `SUCCESS` opcode (`:15167`) and the search loop
(`:11880`) bound it by `slice_start`. Only a general repeat consults `try_match` for its tail, which
is exactly the four conditions the symptom needs. **Upstream bug; the port is right**, and the test
is permanent. The `partial-sliced` wave's row 756 at seed 31, which looked like a separate `\K`
family, turned out to be the same defect's second symptom - a complete zero-width match reported as
a partial - and is pinned in the same test.

**Item 3.** Every "PHASE 7: INVERT THIS TEST" and "this test inverts" instruction is replaced by a
permanent verdict quoting the second engine where there is one: PCRE2 10.47 answering `(4, 8)` to
`(?:..(*SKIP)x|q)x` with and without `PCRE2_NO_START_OPTIMIZE`, PCRE2 answering no match to `ba??x`
against `baa`, and for the two reversed families - where PCRE2 has no reverse matching to ask -
upstream disagreeing with itself.

**Item 4, the half that landed.** `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` is a new
accounted-for list: three named families, each with the reason, which engine is right, the permanent
test that pins this port's answer, and the minimised row it was found on. A classified row is
tallied as `expected` and printed in the report as `EXPECTED <id>`, never dropped.
`OracleWaveTests.Every_expected_divergence_still_diverges` runs each entry's minimised row through
the live engine on every oracle run and fails when one stops diverging, which is the staleness alarm
a random seed cannot give. `partial-sliced` is on the default list. `search_start` was checked and
is **not** reachable from Python (`do_search_start` is set unconditionally at `:25732` and is not a
`_regex.compile` argument; the only outside lever is `req_string` happening to equal the start test
node, `:11770`), so `locate_required_string` is the only prefilter the recorder can switch off -
and S33 added `partial-sliced` to `PREFILTER_FREE_GENERATORS` after finding that on a partial row it
can suppress a partial outright rather than merely move an attempt.

**Item 5.** `docs/plan/upstream-reports/LEDGER.md`, four issues, every reproduction run
and its output quoted. Not filed.

### What was not done, and why

**`verbs` is not on the default oracle list.** The blind review ran the generators at seeds no
earlier slice had used and the default wave is red at three of them, for three families none of
which S33 caused - each reproduces identically on `fb4e705`, the commit before this one. Putting
`verbs` in the list would have made every later slice's oracle run red for a reason that is not that
slice's, which is the exact thing S29 held it out to avoid. The three are reproduced in STATE.md and
in the Generator note in `tools/run-oracle.ps1`; the shortest is

    regex.finditer(r'(?:[^\d](*SKIP)){2,3}', '\r\naabb ', regex.M, overlapped=True)
    # upstream (0,3) (1,4) (2,4) (3,4) (4,7) (5,7); this port has (2,5) and (3,6) in the middle

A **default wave is not reliably green today and was not before this slice** - the single seed each
slice happened to run is what made it look so. That is the most important thing S33 learned and it
is the next slice's work.

**No fourth entry for the bounded-lazy-repeat family**, which is judged and port-right and which the
`partial` generator does reach (seed 314159, two rows). No predicate for it has been found that does
not also swallow a genuine missed partial, and an entry without one would be a silencer.

### Review

One blind pass, Opus, over the whole diff including the untracked files, briefed to hunt for a fix
that moves a bound and changes an answer no test covers, and to hand back reproductions rather than
prose.

**Findings raised: 1. Reproduced: 1. Fixed: 1.** The finding was that adding `verbs` to the default
generator list made the default oracle command red, with the `verbs` rows at seeds 7, 31 and 4242
and a `recursion` row at seed 4242. Reproduced here independently before acting on it - `verbs` at
seed 7 gives `agree 1998 expected 1 diverge 1`, and the diverging row is the forward `(*SKIP)` one
quoted above; the `recursion` row is upstream reporting `spans('g') == [(3, 2), (1, 3)]`, a capture
whose end is before its start, which this port records as `(3, 3)`. Fixed by taking `verbs` back off
the list and recording all three families.

The reviewer also checked the `IsStringTestPartial` port character by character against
`:7383-7668` and then differentially against a prefilter-free upstream over 245,868 cases - forward
and reverse, exact/IGN/FLD, astral subjects, every `pos`/`endpos` pair, all three operations, partial
on and off, with `(*SKIP)`/`(*PRUNE)`/lookaround/atomic slice moves, and one sweep comparing every
group span - and reported **0 rows that agreed before the change and diverge after it**, against
2,236 that diverged before and agree now. It re-ran every upstream measurement quoted in the new and
edited gap-test comments and reproduced each exactly. No finding on the engine change.

**No second blind pass was run over the post-review delta**, and the reason is that the delta is the
reviewer's own recommendation carried out - one list literal in `tools/run-oracle.ps1` reverted to
its pre-slice value - plus comment and documentation text in four files. It contains no code and no
public API. Judged by what changed, as the skill asks, rather than by how the first pass went.

### Negative controls

Two new, both on `partial-sliced`, and both re-run against the code and generator being committed.

> **Control S33-A, `the reversed string test bounds itself by text_start, not slice_start`:** in
> `src/FuzzyRegex/Engine/Matcher.cs`, inside `IsStringTestPartial`, the first occurrence of
>
> ```csharp
>                     if (pos <= state.SliceStart)
> ```
>
> at or after the first `            case Opcode.StringRev:`, changed to
>
> ```csharp
>                     if (pos <= state.TextStart)
> ```
>
> Wave: `partial-sliced`, 8000 rows. **Seed 31: 7976 agree, 23 expected, 1 diverge. Seed 4242: 7963
> agree, 36 expected, 1 diverge.** The unmutated wave at seed 31 and 8000 rows is `7977 agree, 23
> expected, 0 diverge`, so the single row is the mutation's and nothing else moved.

**S33-A is the reason the `search-start-partial` entry carries a discriminator, and this is the most
useful thing in these notes.** At 2000 rows the control fired ZERO times at three seeds - not because
the generator misses the arm, but because the one row that caught it was being classified as the
prefilter family and reported as `expected`. The accounted-for list was silently eating a mutation
that reverted this slice's own fix. The repair is `searchOnlyPartial`, recorded per row by
`tools/record-oracle.py`: `search_start` is consulted on a search and nowhere else, so upstream's own
`match` DENYING the partial its `search` reported at the same position is the family's signature, and
the matcher's own partial is not. Verified on the six prefilter rows of the seed-31 wave (all deny)
and the real-bug row (agrees) in `.scratch/discriminator.py`. **Anyone adding an entry to that list
should write the control first.**

**Second finding from S33-A: it needs 8000 rows.** At 2000 rows and three seeds it does not fire even
with the discriminator in place, so `partial-sliced` reaches the plain reversed `STRING_REV` narrowed
-slice partial about once in 8000 rows. That arm's dense pin is the hand-written test, not the wave.
Widening the generator is Phase 6's oracle hardening, not this slice's.

> **Control S33-B, `try_match never consults a string test node at all`:** in
> `src/FuzzyRegex/Engine/Matcher.cs`, inside `TryMatch`,
>
> ```csharp
>         if (IsStringTest(test.Op))
> ```
>
> changed to
>
> ```csharp
>         if (test.Op == Opcode.Failure && IsStringTest(test.Op))
> ```
>
> Wave: `partial-sliced`, 2000 rows. **Seed 31: 8 expected, 2 diverge. Seed 4242: 5 expected, 8
> diverge. Seed 7: 7 expected, 3 diverge.** The unmutated wave is 0 diverge at all three.

**S29 and S31's controls, re-run at their recorded seeds and one fresh one** (the whole point being
that `Matcher.cs` changed under them). Format: seed → diverge, of the recorded count.

| id | recorded seeds → diverge | fresh seed → diverge |
|---|---|---|
| S29-A | 7 → 0, 20260913 → 0 | 4242 → 1, 314159 → 1 |
| S29-B | 7 → 36, 20260913 → 23 | 4242 → 24, 314159 → 26 |
| S29-C | 7 → 223, 20260913 → 239 | 4242 → 220, 314159 → 235 |
| S29-D | 7 → 2, 20260913 → 0 | 4242 → 0, 314159 → 1 |
| S31-A | 31 → 16 | 4242 → 30 |
| S31-B | 31 → 25 | 4242 → 24 |
| S31-C | 31 → 77 | 4242 → 96 |
| S31-D | 31 → 3, 4242 → 3 | 7 → 4 |
| S31-E | 7 → 1, 31 → 0 | 4242 → 0 |

**Two of these are findings about the generator rather than ticks, and both were findings before
S33 too.** S29-A fires at two of four seeds and never above one row; S29-D and S31-E fire at two of
three or four. A mutation a 600-row wave catches one time in two is a thin instrument, and the moment
to widen the `verbs` and `partial` generators is while somebody is holding them. S29-A's numbers also
changed shape when the accounted-for list landed - at seed 20260913 it now reads `expected 1,
diverge 0` where the four judged rows used to be counted as four divergences, which made the control
look stronger than it was. That is the list doing its job, not hiding one: the classified rows are
printed with their ids.

**S31-D covers less than it did.** It neuters the one-character half of `TryMatch`'s partial answer
and no longer neuters the whole of it, because S33 added a second half. Its numbers are unchanged
(3, 3, 4) and S33-B covers the new half. Left as recorded rather than rewritten, because editing a
recorded control's text makes it a different control and orphans the number it was measured against.

### For the next slice

1. **The three unjudged divergences above.** Each needs the treatment this slice gave items 1 and 2:
   research against a second engine where one exists, an isolating probe, a blind review of the
   verdict, then a fix or a permanent test. The `(*SKIP)`-in-a-bounded-repeat one is the port
   matching MORE than upstream and is the likeliest of the three to be a port bug, which the
   no-known-bugs rule makes non-optional before 1.0.
2. **Run every generator at three seeds, not one, before believing a wave.** Cheap - a 2000-row
   generator run is about fifteen seconds - and it is what found all three.
3. **`verbs` goes on the default list when item 1 is judged**, and the classifier is already there
   waiting for it.
