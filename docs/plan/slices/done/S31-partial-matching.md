---
slice: S31
phase: 4
title: Partial matching - the fallback in do_match and Match.PartialMatch
delivers: [partial]
---

# S31 - Partial matching

The largest single win left on the board (82 tests) and, per the S26 handover, the cheapest per
test: every one-character and string opcode already has its partial arm, `MatchState.PartialSide`
is already set from the `partial` argument (`MatchState.cs:379`), and the whole seam is the guard
in `FuzzyRegex.Run` (`FuzzyRegex.cs:318`) that throws on `partial: true`. Needs nothing from
S27-S30, but is placed after them so the partial arms of the new opcodes are written with their
opcodes rather than retrofitted.

## Scope

All line references are `upstream/src/_regex.c` unless marked.

- **`do_match`'s fallback** (`:18121-18162`): try a normal match first with `partial_side` forced
  to none; on failure restore `text_pos` and `partial_side` and run again. The second run is the
  one that may return `RE_ERROR_PARTIAL`.
- **`RE_ERROR_PARTIAL` as a result**, not an error: `pattern_search_or_match` sets
  `match->partial = status == RE_ERROR_PARTIAL` (`:20774`). Ours is `Match.PartialMatch`
  (`Match.cs:208`, stubbed by S01, never yet true). Check what a partial match reports for its
  span and groups - upstream stores results for both `SUCCESS` and `PARTIAL` (`:18164`).
- **The partial arms already in the matcher** (`CountOne` reports `isPartial`; each
  `CHARACTER*`/`STRING*`/`RANGE`/`SET*` arm returns `RE_ERROR_PARTIAL` at the text end) were
  ported without a test that could reach them. Every one is now reachable: expect the wave to
  find at least one that was ported wrong, and minimise it.
- **The three public overloads** already take `partial` (`Match`, `MatchAtStart`, `FullMatch`,
  `FuzzyRegex.cs:500-528`). The scanner does not: upstream's `finditer`/`findall` have no
  `partial` argument, so `Matches` stays as it is. Confirm against `_main.py` and record.
- **Upstream issue 367**: `partial=True` returns a true positive when two lookaheads are jointly
  unsatisfiable. Port faithfully; gap test pins current behaviour for Phase 6.
- **`partial_string_match_ign` (`:11683`)** stays unported: its only callers are the
  `*_REPEAT_ONE` string arms, which are Phase 7 optimisations.

## Verification

- **Un-skip** `needs:partial` (82 tests, `PartialMatchTests.cs`, `RegressionsPartialTests.cs`).
- **Oracle generator `partial`**: every S16-S25 construct with the subject cut short at every
  prefix length from 0 to full, under `partial=True`, through `match`, `search` and `fullmatch`,
  forward and `(?r)` (where the cut is at the *start*); the comparison includes `m.partial`, the
  span and every group. Also cases where a full match exists *and* a longer partial would - the
  fallback must prefer the full match. Zero divergences. Negative controls: the fallback skipped
  (always partial); `text_pos` not restored between the two runs; `PartialMatch` reported for a
  full match.
- **Add `partial` to the default oracle list.**

## Done when

- [x] Tag delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green; controls recorded in full. Any partial arm found wrong is a permanent
      test.
- [x] PORTMAP: `do_match` row updated; `Match.partial` row moved from S01's stub to delivered.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a partial arm returning partial on
      `slice_end` rather than `text_end`; `(?r)` partial at the wrong end; group spans on a
      partial match), commit.

---

## Closing notes (2026-09-12)

**What landed.** 82 `needs:partial` tests un-skipped and green (18 in `PartialMatchTests.cs`, 64 in
`RegressionsPartialTests.cs`); no `needs:partial` skip is left anywhere. Suite 5,750 tests, 5,557
passing, 193 skipped, ratchet GREEN, baseline 5,449 distinct ids (was 5,354). Eleven new gap tests
across `Gaps/Engine/PartialMatchingTests.cs` (new file), `Gaps/Engine/IterationTests.cs` and
`Gaps/Api/ApiSurfaceTests.cs`.

**The seam was three lines, not a slice's worth - the scope estimate was wrong in our favour.**
`do_match`'s partial fallback (`:18140-18162`) and its `text_pos` forcing (`:18175-18180`) were
already ported, correctly, in `Matcher.DoMatch`; the only thing missing was that
`FuzzyRegex.NewMatch` treated `RE_ERROR_PARTIAL` as an error rather than a result. Removing the
`FuzzyRegex.Run` guard, widening `NewMatch` to upstream's `status > 0 || status == RE_ERROR_PARTIAL`
(`:20741`) and passing `partial: status == Partial` (`:20774`) turned all 82 green at once. The
engine work in this slice is all in the two places the oracle found afterwards.

**Scope correction: `finditer` DOES take `partial`, and this slice file said it does not.** The
Scope section asked to "confirm against `_main.py` and record"; the confirmation failed.
`_main.py:351` and `pattern_scanner`'s `kwlist` (`:21089`) both carry it, and
`regex.finditer('abc', 'abc xab', partial=True)` yields `[(0,3) complete, (5,7) partial]` (measured
2026-09-12). `findall` genuinely refuses it - `ValueError: unused keyword argument 'partial'` - so
`Matches` gained a `partial` parameter and `Count` did not. `scanner_search_or_match` builds a match
for a `PARTIAL` status exactly as for `SUCCESS` (`:20898`) and ends the walk on the next turn
(`:20886`), so the partial is yielded and is always last. Pinned in `IterationTests.cs` with nine
measured sequences.

**Two engine defects, both found by the oracle, both now fixed and controlled.**

1. `Matcher.TryMatch`'s reduction to its default arm is *not* transparent for a `PARTIAL` answer.
   Every caller stops on a negative status, so upstream stops inside the branch or repeat where this
   port carried on and let the opcode raise the partial a step later - by which time an enclosing
   group had re-closed. `regex.search(r'(\D*?)z', 'a', partial=True)` leaves group 1 unset upstream
   and had it as (0,1) here. `TryMatch` now consults the one-character test nodes through `MatchOne`
   and propagates only `PARTIAL`; `FAILURE` still reads as `SUCCESS`, so the Phase 7 fast path S19
   measured and reverted is not reinstated.
2. `LAZY_REPEAT_ONE`'s backtrack default arm never asked `partial_side`, where all ten of upstream's
   specialised tail arms ask it at the top of their loop before trying to extend.
   `regex.match(r'([^a-f]{3,}?)x', '__AAb', partial=True)` is a partial at (0,5) upstream and was no
   match here. The ten guards are collected in `Matcher.IsTailPartial`, per tail op, because a
   `CHARACTER` tail guards one step further out than a `STRING` tail.

Both are confined to partial matching by construction - `IsTailPartial` and `TryMatchOne` answer
`PARTIAL` only when `partial_side` is set, and `do_match`'s first pass forces `PartialNone` - and the
second blind pass replayed 51,000 non-partial rows and 1,980 non-partial `finditer` rows against HEAD
and against the working tree with **0 differences**.

**Three divergence families are known, pinned and parked. None is a guess; each has its own test.**

- **`search_start`.** Upstream's unported start-position prefilter can answer `RE_ERROR_PARTIAL` of
  its own (`:8402`, `:8411`, and every anchor arm `:8662`-`:8960`).
  `regex.compile(r'(?r)\b$').search('', partial=True)` is `(0,0)` partial upstream and `None` here,
  and upstream's own `match` and `fullmatch` answer `None` too - `search_start` is consulted on a
  search and nowhere else, which is what identifies the cause rather than merely suggesting it.
  Same mechanism as S29's four `verbs` rows. About one row in 2,000; the `partial` generator stays
  in the default list on that measured rate, and the number is in `run-oracle.ps1`.
- **The narrowed slice.** Half of upstream's partial arms are bounded by `slice_*` and half by
  `text_*` - compare `try_match_STRING` (`:7396`, `slice_end`) with the `STRING` opcode's own arm in
  `basic_match` (`text_end`) - and the two agree exactly as long as the slice is the whole subject.
  `regex.compile(r'(?r)a(bc)*').match('abc', 1, 1, partial=True)` is a partial at (1,1) upstream and
  `None` here. Raised by the first blind pass on an axis no generator could reach; the new
  `partial-sliced` generator reaches it and finds 8 rows in 2,000 at seed 31, every one `(?r)`. It is
  deliberately **not** in the default list.
- **The bounded lazy repeat.** `regex.match('ba??x', 'baa', partial=True)` is `(0,3)` partial
  upstream and `None` here. **S31 tried to fix this and reverted the fix, which is the most useful
  thing in these notes.** The obvious repair - ask `IsTailPartial` once more before the loop's
  `pos == limit` break, where upstream's specialised arms return to the top and ask before their own
  limit check (`:16545-16549`) - fixes four cases and breaks more, because those arms also *cap* the
  limit per tail op (`min(limit, slice_end - 1)`, `:16543`) and so never reach the position the added
  guard fires at. The second blind pass measured it over 20,160 targeted rows: **125 rows fixed, 219
  introduced**, among them `regex.match('.{0,2}?x', 'baa', partial=True)` (None upstream, a partial
  with the guard). It needs the specialised arms themselves, which this slice file holds out of scope
  as Phase 7. No wave here reaches this family; it was found by hand.

**Oracle.** `partial` added to the default list. Default run at `-Count 400 -Seed 20260912`:
**6,800/6,800 agree** across all seventeen generators. The generator composes its patterns with
`_interaction_pattern` - "every S16-S25 construct" is `interactions`' pattern pool asked a different
question - and owns the *subject*: built whole, pattern composed against the whole of it, then cut,
at the end for a forward pattern and at the start for a `(?r)` one. At 600 rows / seed 31: 196
matches of which **119 are partial and 77 complete**, 458 rows ask for a partial, 286 are reversed,
91 have an empty subject, 200 per operation.

**Controls, re-run against the committed code and at a seed each was not first measured at.** The
unmutated baseline is 0 at 600 rows (seeds 31 and 4242) and 1, 2, 1 at 2,400 rows (seeds 31, 4242, 7)
- the `search_start` family - so subtract that from D and E.

> Control A, `S31-A`: in `Matcher.cs`, `DoMatch`, replace the whole "Try a normal match first"
> block - from `// Try a normal match first.` through the closing brace of
> `if (status == MatchStatus.Failure) { ... }` - with `_ = partialSide; _ = textPos; status =
> DoMatch2(state, search);`. Wave: `partial`, 600 rows, seed 31. Result: 584 agree, **16 diverge**.
> Re-run at seed 4242: **30 diverge**.
>
> Control B, `S31-B`: in the same block, change
> `state.TextPos = textPos;` to `_ = textPos;`. Wave: `partial`, 600 rows, seed 31. Result: 575
> agree, **25 diverge**. Re-run at seed 4242: **24 diverge**.
>
> Control C, `S31-C`: in `FuzzyRegex.cs`, `NewMatch`, change
> `partial: status == Engine.MatchStatus.Partial` to `partial: true`. Wave: `partial`, 600 rows,
> seed 31. Result: 523 agree, **77 diverge**. Re-run at seed 4242: **96 diverge**.
>
> Control D, `S31-D`: in `Matcher.cs`, `TryMatch`, change
> `return status == MatchStatus.Partial ? status : MatchStatus.Success;` to
> `_ = status;` then `return MatchStatus.Success;`. Wave: `partial`, 2,400 rows, seed 31. Result:
> 2,396 agree, **4 diverge** (3 over baseline). Re-run at seeds 4242 and 7: **5 and 5** (3 and 4 over
> baseline).
>
> Control E, `S31-E`: in `Matcher.cs`, the `LazyRepeatOne` BACKTRACK arm, change
> `if (IsTailPartial(state, test, pos))` to
> `if (IsTailPartial(state, test, pos) && state.PartialSide == MatchState.PartialNone)`. Wave:
> `partial`, 2,400 rows, seed 7. Result: 2,398 agree, **2 diverge** (1 over baseline). Re-run at
> seeds 31 and 4242: **1 and 2** (0 and 0 over baseline).

All five are in `tools/controls.json` and re-run with `python tools/run-controls.py --slices S31`;
delete `.scratch/control-waves/` first after any generator change. **D and E are thin - one to four
rows over baseline in 2,400 - and E is zero over baseline at two of its three seeds.** They were
raised from 600 to 2,400 rows when 600 caught nothing, and they are recorded as thin rather than
dressed up: the cell each reaches is genuinely narrow, and widening the generator to reach it more
often is real work the next slice should consider before trusting either number. Two further
controls, for the two changes the blind review later showed to be wrong, were written, measured at
exactly the unmutated baseline at every seed - they caught nothing at all - and deleted with the
code they guarded. That they measured nothing is why the defect reached the second pass.

**Harness.** The row shape gained `partial` and an optional `pos`/`endpos` pair, recorded translated
to UTF-16 with the untranslated `codepointSlice` beside them; `MatchOutcome` gained `Partial`,
rendered only when set so every pre-S31 row still renders identically. Negative and reversed slice
indices follow upstream's own convention (it wraps a negative index like a slice; it does not clamp
it), which the second blind pass caught the recorder getting wrong.

**Review.** Two blind passes. The first raised 3 findings; **all 3 reproduced** and all 3 were acted
on - a lazy repeat losing its partial, the narrowed-slice family, and the generator that could not
reach either. The second pass ran over the delta the first never saw (the two engine edits made in
response to it, the `pos`/`endpos` plumbing, `partial-sliced`, the new tests, two new controls) and
raised 5 findings; **all 5 reproduced**. Its first finding showed that one of the first pass's own
fixes was a net loss, measured over 20,160 rows, and that fix was reverted rather than defended -
with the measurement written into the test that now pins the divergence, so the next attempt does not
repeat it. Its second finding showed the two controls guarding that fix never fired. Findings 3, 4
and 5 (reversed slice read as "no limit", `endpos` without `pos` dropped, a negative index clamped
instead of wrapped) were fixed and verified with an 8-row edge file covering reversed, negative,
one-ended, out-of-range and astral slices: all 8 agree. **No third pass was run**, and that is a
judgement rather than an omission: reverting the two engine edits restored code the first pass had
already seen green, and the remaining new logic is six lines of harness arithmetic whose
reproductions the reviewer supplied and which those 8 rows verify directly. A third pass over it
would have been a critique loop, not a first pass over unreviewed code.
