---
slice: S52d
phase: 6
title: A reversed partial match runs out of text at the slice start - ledger 24 fixed here on the owner's ruling
delivers: []
---

# S52d - Reversed partials honour the slice start

Owner ruling, 2026-09-15, Option B of `docs/plan/upstream-reports/ledger-24-briefing.md`: for a
reversed match asked with `partial=True`, the engine has run out of text when it reaches `pos`
(our `beginning`), and it reports a partial match there. Nothing else about `pos` changes: `^` and
`\A` still refuse a non-zero `pos`, lookbehind and `\b` still see the character before it, exactly
as Python `re` documents and the ported suite asserts. Only the partial run-out question moves from
the whole-string start to the slice start.

This is spec amendment 16 outcome (c): upstream contradicts itself (its node handlers read
`text_start`, its optimiser and reversed string helpers read `slice_start`) and this port
inherited both rules. Fixed here, recorded in ledger entry 24 with the mechanism already at the
line, nothing filed until Phase 8.

## Before launch (orchestrator)

S52c has landed, so the metamorphic invariant checker exists and can prove this fix over a whole
wave rather than over the hand-built grid.

## Scope

1. **Red first, from the grid.** `tools/probes/port-reversed-partial-ignores-the-slice-start.ps1`
   has 33 cells; write them as gap tests with the Option B answer as the expectation, each
   assertion carrying its provenance (the briefing's ruling, and where upstream's Rule B path
   already gives the answer, the upstream run). Run: the cells where the port follows Rule A go
   red. Provenance comments name the rule, not the port's current output.
2. **The nine sites.** Every `TextStart` read in `Engine/Matcher.cs` that sits under a partial
   check (`:967`, `:3273`, `:5788`, `:6644`, `:6705`, `:6787`, `:7393`, `:7458`, `:7556` at the
   time of the ledger; re-locate them, the file has moved) asks `SliceStart` instead. Reads of
   `TextStart` that serve `^`, `\A`, `\b`, `\B` and lookbehind are NOT touched; list them in the
   notes to prove they were seen and left. Upstream's `text_start`/`slice_start` comment block
   (`_regex.c:18435-18446`) is quoted at the port's equivalent with the ruling.
3. **The port's second rule.** Ledger 24 records that the port also answers a partial at `pos` on
   some shapes, so it carries an equivalent of upstream's optimiser bound. Find it, confirm it now
   agrees with the nine sites, and make the two paths share one helper so the question is asked
   in one place (`RanOutOnTheLeft(state, textPos)` or similar), which is what stops the two rules
   diverging again.
4. **The whole wave, not the grid.** Run the default wave at three seeds and 99991 with S52c's
   checker on: the greedy-versus-lazy and minimum-width invariants must report no violation on the
   port side for reversed partial rows, and any row where upstream's Rule A answer now differs from
   the port is classified into one pin, `reversed-partial-runs-out-at-the-slice-start`, whose
   predicate is the mechanism (a reversed pattern, `partial=True`, a non-zero `beginning`, upstream
   answering None where the port answers a partial positioned at `beginning`) and nothing wider.
   Gate row 104366 is the worked example; its answer is recorded.
5. **Records.** Ledger 24 rewritten: ruled, fixed here, the sites, the pin, `Reproduce:` naming
   both probes. `docs/DIVERGENCES.md` gains a Behaviour row (SHIPPED, this slice) with the
   one-sentence rule and the upstream behaviour a user cannot get back. DECISIONS entry. PORTMAP's
   rows for the nine sites note the deliberate departure from upstream's `text_start`.

## Verification

- Ratchet GREEN; the 33 grid tests green with red-first evidence; waves at three seeds and 99991
  with the checker on and no port-side violation of the two invariants; upstream's own 72
  `partial=True` tests still green in the ported suite (none passes a `pos`, so none may move).
- Blind review (hunt: a `TextStart` read changed that served `\b` or lookbehind; a pin predicate
  that would also classify a forward partial or a non-partial reversed row), then the verifier
  pass re-running both probes and the wave summary.

## Done when

- [x] Grid tests red first then green; nine sites changed; untouched `TextStart` reads listed.
- [x] One helper for the run-out question; the port's second rule found and unified.
- [x] Wave green with S52c's checker; one narrow pin; row 104366 recorded.
- [x] Ledger 24, DIVERGENCES, DECISIONS, PORTMAP updated.
- [x] Ratchet GREEN, blind review, verifier, commit.

---

## Closing notes (2026-09-16, one sitting)

**Ruled, fixed, green.** Ratchet GREEN, 6,153 passing, baseline updated (one accepted removal, a
rename - see below). The default oracle wave is GREEN at seeds 7, 4242 and 20260916, `diverge 0` of
6,380 each. Two further seeds were run and each has ONE diverging row, **both proven pre-existing by
the negative control** (identical with and without the fix): seed 99991 row 3825 and seed 31415 row
3756, both `interactions`, neither reversed-and-partial. They are handed on, not fixed here.

**The ledger's count of nine port sites was six short, and that was the one real surprise.** It had
enumerated the `state.TextPos <= state.TextStart && PartialSide == PartialLeft` SPELLING;
`TryMatchAnyRev`, `TryMatchAnyAllRev`, `TryMatchAnyURev` and `TryMatchOneRev` write the same question
as a guard whose else-branch already tests `SliceStart`, and `IsTailPartial` writes it as two switch
arms. Fifteen sites, not nine. Grepping `PartialLeft` rather than `TextStart` is what found them:
`TextStart` returns nine partial sites and twenty-odd anchors, `PartialLeft` returns exactly the
decisions. Changing only the nine would have left a reversed one-character TEST answering the old
rule while its own opcode arm answered the new one - the identical defect, moved.

**The port's "second rule" (scope item 3) turned out to be three sites that already read
`SliceStart`**: `IsStringTestPartial`'s two reversed string arms and the reversed bound of
`BasicMatch`'s scan loop. They are now routed through the same helper rather than left spelling the
question themselves, because the defect being fixed IS two places spelling one question differently.
The committed probe's own header said this port "has no `search_start` second rule, so it answers
that one question uniformly" - that was wrong, and the grid it prints disproves it on its own output.

**One helper owns the edge:** `Matcher.RanOutOnTheLeft(state, textPos)`, plus `SteppedPastTheLeft`
for the two sites whose position has already been moved by an error or a scan step and so compare
strictly (`CheckFuzzyPartial`, and `BasicMatch`'s scan bound). Both read `SliceStart`; nothing else
does. The 18 remaining `TextStart` reads all serve `^`, `\A`, `(?m)^`, `(?m)$`, `\b`, `\B`, the
grapheme walks or the lookaround slice widening, and are listed member by member in ledger 24.

**S33's pin was the same evidence read the other way, and it is rewritten rather than deleted.**
`The_narrowed_slice_partial_is_a_per_opcode_answer_and_not_a_general_rule` asserted upstream's `None`
on five cells and said in its own comment "pinned so nobody simplifies it into one rule about
slice_start"; the owner's ruling is that it IS one rule about the slice start. It is now
`The_narrowed_slice_partial_is_one_rule_about_the_slice_start_and_upstream_holds_two`, asserting the
same five cells as a deliberate divergence with upstream re-measured beside them, plus their `(0, 0)`
twins - where upstream answers a partial for all four, which is what makes the original five a
contradiction rather than a design. That rename is the ratchet's one accepted removal.

**The fix removed pre-existing divergences as well as adding pinned ones**, which is the part worth
carrying forward. On four rows across the five seeds (seed 7 row 5121, seed 20260916 rows 5197 and
5283, seed 31415 row 5173) upstream answered a partial and this port answered no match; with the fix
the port agrees. So the ruling moves the port INTO agreement with upstream wherever upstream is using
its own Rule B, and away from it only where upstream is using the rule it contradicts.

**Gate row 104366 moved and the invariant still holds.** `greedy-lazy-existence-agree` is broken on
2 of 6 cells on upstream and on **0 of 6** on this port, exactly as at the S52c close - but the
port's row of that table is now `(2, 2) partial` for lazy, greedy and possessive rather than no
match, so the port is self-consistent AND agrees with upstream's self-consistent arm on both broken
cells. `docs/ORACLE-INVARIANTS.md` carries the new table. The minimum-width invariant is pinned by
`ReversedPartialSliceStartTests.Needing_more_text_never_makes_a_reversed_match_run_out_less`, where
upstream inverts it and this port does not.

### Negative control

**Control A, `text-start-again`:** in `src/FuzzyRegex/Engine/Matcher.cs`, in `RanOutOnTheLeft`,
change

```csharp
    private static bool RanOutOnTheLeft(MatchState state, int textPos) =>
        state.PartialSide == MatchState.PartialLeft && textPos <= state.SliceStart;
```

to

```csharp
    private static bool RanOutOnTheLeft(MatchState state, int textPos) =>
        state.PartialSide == MatchState.PartialLeft && textPos <= state.TextStart;
```

Generator: the default wave list (all 22 generators), 300 rows a generator, 6,380 rows a seed.
**Run once more against the code and the generator being committed, after the blind review's fix**,
which is what these numbers are:

| Seed | Fixed | Control A applied |
|---|---|---|
| 7 | agree 6342, expected 32, **diverge 0** | agree 6365, expected 8, **diverge 1** |
| 4242 | agree 6347, expected 28, **diverge 0** | agree 6373, expected 2, **diverge 0** |
| 20260916 | agree 6354, expected 21, **diverge 0** | agree 6369, expected 4, **diverge 2** |
| 99991 | agree 6353, expected 25, **diverge 1** | agree 6376, expected 2, **diverge 1** |
| 31415 (a seed the slice had not used) | agree 6347, expected 26, **diverge 1** | agree 6367, expected 5, **diverge 2** |

So the control moves **111 rows** out of the pinned-divergence tally and into agreement (24 + 26 + 17
+ 23 + 21) and introduces **4 new divergences** (1 + 0 + 2 + 0 + 1), each of them a row where
upstream answers a partial and the pre-ruling port answers no match. It fires at four of the five
seeds tried, including the fifth the slice had never used; the one seed it does not red is 4242,
where it still moves 26 rows. The row that does NOT move at seeds 99991 and 31415 is the
pre-existing divergence named above, which is exactly how it was shown to be pre-existing.

To re-run the control without re-recording: the waves are on disk, so
`cp TestResults/oracle/wave-<seed>.jsonl TestResults/oracle/wave.jsonl` and then
`pwsh -File tools/run-oracle.ps1 -SkipRecord` replays one seed in about twenty seconds.
`TestResults/oracle/report-7.txt`, `-4242.txt` and `-20260916.txt` still hold the run made BEFORE
the pin was written, so they are the record of the 44 rows the pin accounts for, and
`python tools/probes/reversed-partial-divergence-triage.py` over those three prints
`44 diverging rows: 44 fit the mechanism, 0 do not`.

**Two traps for the next sitting, both cost time here.** Python's `Path.write_text` translates `\n`
to CRLF on Windows and this repo is `* text=auto eol=lf`, so a scripted edit to a `.cs` file reds
IDE0055 on every line CSharpier ever wrapped; open with `newline="\n"`, or run
`dotnet csharpier format <file>` after. And `tools/run-oracle.ps1` has no `-Consume`: the switch is
`-SkipRecord`, and it replays `wave.jsonl` alone, so a per-seed replay has to copy the seed's saved
wave over it first.

### Review

**Blind review: 2 findings raised, 2 reproduced, 1 fixed, 1 judged out of scope. No second pass
needed.**

- **Fixed.** The repeat walk's partial test (`MatchMany`) was upstream's `==` and the change made it
  the helper's `<=`. In codepoints the two cannot differ, because the walk stops at the slice start;
  in UTF-16 they can, because a `beginning` that SPLITS a surrogate pair lets `PrevPos` step two code
  units and land one BELOW it. Reproduced on `(?r)\A[\s\S]*` over `"a\U0001F600"` at the slice
  (2, 3): the change answered `(2, 3) partial` where HEAD answers `None`. The slice moves the BOUND
  and not the comparison, so the equality is restored and pinned by
  `ReversedPartialSliceStartTests.A_beginning_that_splits_a_surrogate_pair_answers_what_it_did_before_the_ruling`.
  The walk overrunning the slice at a split pair is pre-existing and is left alone.
- **Judged out of scope.** `IsReversed(row)` in `ExpectedDivergences.cs` admits a row whose pattern
  merely CONTAINS the text `(?r`, so a forward partial row could in principle be classified by the
  new entry. It is a pre-existing shared helper used by several older entries, and the reviewer
  measured its reach itself: over 9,000 `partial-sliced` rows at three seeds, **zero** patterns
  contain `(?r` anywhere but as the prefix the generator adds. Widening this slice to a shared helper
  on a hole with no measured reach is the scope creep the skill warns against; recorded here instead.
- **No second blind pass.** The only code the first pass did not see is the one-line restoration it
  asked for and that restoration's test. No public API, no tooling, nothing the reviewer had not
  read.

**Independent verifier (amendment 16 limb (d)): 12 claims, 10 CONFIRMED, 2 DIFFERENT, and both
DIFFERENTs were the CLAIM being wrong rather than the code.** (1) The port grid prints `None` on
**2** cells, not the 5 the brief guessed - the two are `pos = endpos = 0` and the empty subject in
"an empty slice is an empty slice", which is exactly what
`An_empty_slice_is_an_empty_slice_wherever_it_sits` asserts and why those two cells exist. (9) The
triage script over ALL 22 report files on disk prints `91 diverging rows: 44 fit the mechanism, 47
do not`, because eighteen of those reports belong to earlier slices and earlier seeds; over the
three this slice recorded it prints `44 of 44`. Everything else - both grid probes, the three
upstream probes, both gate-row probes, the 6,153-test suite, the green ratchet, the five-seed wave
tally, the eighteen untouched `TextStart` reads and the three members that name `PartialLeft` - was
re-run from the committed files and CONFIRMED.

### For the next slice

- **Two unjudged oracle rows, both pre-existing and neither this slice's**: seed 99991 row 3825
  (`interactions`, a `(?r)` fuzzy pattern with `(*SKIP)`, upstream's partial span one character
  longer than ours) and seed 31415 row 3756 (`interactions`, `(?b)` `sub`, upstream substitutes once
  and this port not at all). Both are reproducible from
  `TestResults/oracle/wave-99991.jsonl` and `wave-31415.jsonl`.
- **A question this slice deliberately did not open.** `RanOutOnTheLeft` reads `SliceStart`, which a
  `(*SKIP)` MOVES mid-attempt (S40a) and a lookaround widens, rather than `InitialSliceStart`. That
  is faithful - upstream's Rule B reads the same moved `slice_start` - and the wave is green with it,
  but nothing here tests a reversed partial whose slice a verb has moved. If a later wave finds one,
  that is the question to ask first.
