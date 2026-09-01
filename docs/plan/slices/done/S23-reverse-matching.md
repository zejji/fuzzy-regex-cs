---
slice: S23
phase: 3
title: Reverse matching - the (?r) flag and every REV opcode variant
delivers: [right-to-left]
---

# S23 - Reverse matching: the (?r) flag and every REV variant

Needs S22 (the REV set includes IGN_REV and FLD_REV combinations, so the folding machinery must
exist first). The second cross-cutting dimension: `(?r)` searches from the end of the subject
toward the start, and every consuming opcode has a `_REV` twin that steps backward. The parser
already emits them all; this slice makes them run.

## Scope

All line references are `upstream/src/_regex.c`.

- **Direction plumbing**: `state.reverse` out of `state_init_2` (`:18275`), the reversed
  search-position advance in `basic_match`'s outer loop, reversed `beginning`/`length` clamping
  in `get_limits` (`:21627`) semantics, and backward codepoint stepping over UTF-16 (a low
  surrogate steps back two units - the mirror of S16's decode, and the first place it runs
  leftward).
- **Main-switch cases**: `ANY_REV` / `ANY_ALL_REV` / `ANY_U_REV` (`:11977`, `:11957`, `:12017`),
  `CHARACTER_REV` / `CHARACTER_IGN_REV` (`:12191`, `:12169`), `PROPERTY_REV` /
  `PROPERTY_IGN_REV` (`:13872`, `:13850`), `RANGE_REV` / `RANGE_IGN_REV` (`:13982`, `:13960`),
  `SET_*_REV` and `SET_*_IGN_REV` (`:14520-14523`, `:14496-14499`), `STRING_REV` /
  `STRING_IGN_REV` / `STRING_FLD_REV` (`:15103`, `:15046`, `:14882`), `REF_GROUP_REV` /
  `REF_GROUP_IGN_REV` / `REF_GROUP_FLD_REV` (`:14375`, `:14318`, `:14161`).
- **Backtrack cases**: the REV rows of the one-character block (`:15210-15243`), the REV string
  rows of both REPEAT_ONE sub-switches (`:15950-16342`, `:16608-17107`), the backreference REV
  rows (`:17269-17276`, `:17292`).
- **Zero-width predicates in reverse**: the boundary and anchor predicates are direction-neutral
  (they inspect both sides of a position) - verify that claim against the oracle rather than
  assume it, and pin whichever way it falls.
- `match_many_*_REV` steppers (`:3592`, `:3647`, `:3725`, `:3924`, `:3987`, `:4265`, `:4375`,
  `:4611`, `:4674`, `:4863`, `:4926`) for the REPEAT_ONE paths.

## Verification

- **Un-skip `needs:right-to-left`** (47 tests), reading each skip's prose first; stragglers
  retag with prose.
- **Oracle wave**: take the S16-S22 generators and prefix `(?r)` - literals, classes, groups,
  quantifiers, backreferences, boundaries, under `(?i)` and `(?f)` too - comparing spans and all
  groups. Reverse plus astral subjects is the highest-risk cell (upstream fixed exactly that
  interaction in 2026.5.9, `upstream/changelog.txt`: "Reverse matching with full unicode
  casefolding could lead to out-of-range string indexes"), so probe FLD_REV over astral and
  expanding-fold subjects hard. Zero divergences; negative control.

## Done when

- [x] `right-to-left` delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green including the FLD_REV/astral probes; counts quoted.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a backward step that lands on the low
      surrogate of a pair, an off-by-one between "position before the character" forward and
      "position after" backward, a REV string comparison iterating the pattern forward), commit.

## Closing notes (2026-09-01)

**What landed.** Every `_REV` opcode runs. In `Matcher.cs`: `ANY_REV`/`ANY_ALL_REV`/`ANY_U_REV` as
one case group; a second one-character case group carrying `CHARACTER_REV`, `PROPERTY_REV`,
`RANGE_REV`, the four `SET_*_REV` and all eight `_IGN_REV` twins; `STRING_REV`, `STRING_IGN_REV`,
`STRING_FLD_REV`, `REF_GROUP_REV`, `REF_GROUP_IGN_REV` and `REF_GROUP_FLD_REV` as cases of their
own; `TryMatchAnyRev`/`TryMatchAnyAllRev`/`TryMatchAnyURev`/`TryMatchOneRev`; the reverse arms of
`MatchesOne`, `MatchesMany`, `CountOne` and `MatchOne`. `Seam.Tag` lost its `right-to-left` arm.
`PORTMAP.md` gained a Reverse matching section.

**Much less new code than the scope suggested, and that is the headline.** The direction plumbing
the slice listed was already there: S16 ported `state->reverse`, the reversed search advance, the
reversed `at_end`, the reversed `do_exact_match` width check and `PrevPos`; S19 wrote both
`*_REPEAT_ONE` backtrack cases against `node.Next2.Test!.Step`, which is `-1` for a reversed
repeat, so neither needed a line changed; the shared one-character and `REF_GROUP`/`STRING` REV
backtrack rows are nothing but `retry_fuzzy_*` and stay behind the `default` seam; and
`get_limits` is direction-neutral, its boundary arithmetic already ported as
`MatchState.ClampIndex`. What was left was the dispatch cases and two counting helpers.

**Counts.** 42 single-line `needs:right-to-left` skips removed and 3 multi-line ones retagged.
**11 of the 45 now pass**: the six `Reversed_full_match_*`, the reversed keep-marker, the reversed
alternation regression, the reversed full-case backreference, and the two
`Match_span_for_a_reversed_*`. The other 34 were mistagged and are **stragglers retagged from
evidence, not from reading**: they were run un-skipped and each was retagged to whatever it
actually threw - 19 `needs:find-all` (they call `FuzzyRegex.Matches`), 8 `needs:overlapped`,
6 `needs:recursion` (every `(?r)` row in `RecursiveTests` also recurses), 4 `needs:splitting`,
1 `needs:substitution`. The class docs on `FullMatchTests` and `RecursiveTests` that explained the
old tagging were corrected. Suite: 5666 total, 4752 passing, 914 skipped, 0 failing; parity 53.5%
(was 53.0%), and `right-to-left` is off the waiting-on-a-capability board.

That thinness is why the slice's real evidence is elsewhere: **39 new gap tests** in
`Gaps/Engine/ReverseMatchingTests.cs`, every expectation taken from `regex.match` on 2026-09-01
and quoted beside the assertion, and heavily astral - not one unskipped ported test is.

**Two things worth knowing, both found by probing rather than by reading.**

*A backreference must be written before its group to run at all under `(?r)`.* Reverse execution
takes the sequence from the right, so `(?r)(.)\1` reaches `\1` while the group is still empty;
upstream answers `None` for it, and so do we. Every backreference row in the new gap tests is
`\1(...)`, which is the shape upstream's own reverse tests use. A slice that writes `(?r)(x)\1`
and sees no match has not found a bug.

*`regex.match`, never `regex.search`* - S22's duty, and it earned its keep again: the whole gap
suite goes through `MatchAtStart`.

**Oracle.** New `reverse` generator: every earlier generator's rows with `(?r)` prefixed, drawn in
whole batches per source (asking each source for one row at a time hands out row index 0 every
time and the astral alphabet never appears), plus two shares of a new `_generate_reverse_fold`
that forces FULLCASE, an expanding-fold alphabet and a subject of at least two characters. It is
in `run-oracle.ps1`'s default list, which is now ten generators. Final wave: **agree 15000,
unsupported 0, diverge 0** at 1500 rows per generator, seed 923; the same at seed 23 earlier.

**Review.** One blind pass over the whole diff, and a second over the delta it never saw.

The first pass raised **1 finding, reproduced, fixed**: `Update-Baseline`'s
`Sort-Object -Unique -CaseSensitive` compares by culture even with the switch, so it folded
`K` into `K` (U+212A KELVIN SIGN) and the regenerated baseline silently swapped one
`CaseInsensitiveMatchingTests` id for the other. Reproduced with a two-element `Sort-Object`
(2 in, 1 out), fixed with a `SortedSet[string]` over `StringComparer.Ordinal`, pinned by a Pester
test that fails against `git show HEAD:tools/PortTools.psm1`. **It was not one id: the baseline
went from 4594 unique to 4645**, so 51 tests the ratchet had stopped watching are watched again.
The pass also re-derived every gap-test expectation from upstream and ran 8000 reverse
`pos`/`endpos` slice cases the oracle recorder cannot express; 0 mismatches.

The second pass covered `PortTools.psm1`, its Pester test, the regenerated baseline and the
generator's re-measured comments. It raised **1 finding, reproduced, fixed**: `[string[]]` turns a
`$null` id into `""`, where the pipeline it replaced dropped it, and an empty id in the baseline is
a test the ratchet reports as `Missing` for ever. Filtered before the cast; pinned by a second
Pester test. Tool tests: 74 passing.

**Analyzer decision.** S1854 (useless assignment) is **disapplied over two cases only**, by
`#pragma warning disable`/`restore` around `REF_GROUP_FLD_REV` and `STRING_FLD_REV`, each with the
reason at the site. Walking a folding downward means only `folded_pos`/`gfolded_pos` is read
afterwards, so every store to the matching `*_len` before the loop's own is dead *in this port*;
upstream's readers are `fuzzy_match_group_fld(..., gfolded_len, ...)` and
`fuzzy_match_string_fld(..., folded_len, -1)`, which are throwing seams until Phase 5. Deleting
the variables now would mean re-deriving them then, in the code where a transcription slip costs
most. Chosen over an `.editorconfig` scope deliberately: S1854 stays live over the other 4,900
lines of `Matcher.cs`, so a genuine dead store there still fails the build.

**Negative controls.** Six, all re-run at both seeds against the code and generator being
committed, after the last engine change. The wave in every case is generator `reverse`, 600 rows,
seeds 7 and 4242 - `python tools/record-oracle.py --generator reverse --count 600 --seed <s>
--output TestResults/oracle/wave.jsonl` then `pwsh -File tools/run-oracle.ps1 -SkipRecord`. With
nothing planted: 0 divergences at both seeds.

> **Control A**, one-character-`REV` reads at the position rather than before it: in `Matcher.cs`,
> the `case Opcode.CharacterRev:` block, change
> ```
>                         && MatchesOne(state.Encoding, node, state.CharBefore(state.TextPos)) == node.Match
>                     )
> ```
> to
> ```
>                         && MatchesOne(state.Encoding, node, state.CharAt(state.TextPos)) == node.Match
>                     )
> ```
> Result: **274 of 600 diverge at seed 7, 293 at seed 4242.**

> **Control B**, `STRING_REV` walks the pattern forward: in the `case Opcode.StringRev:` block,
> change `&& SameChar(state.CharBefore(state.TextPos), node.Values[stringPos - 1])` to
> `&& SameChar(state.CharBefore(state.TextPos), node.Values[^stringPos])`.
> Result: **9 of 600 at seed 7, 9 at seed 4242.** The thinnest of the five that fire, and it is a
> finding about the generator rather than a tick: `STRING_REV` needs a multi-character literal
> whose reversal differs, and only the `literals` and fold shares produce one. It fires identically
> at both seeds, so it is a stable detector, but a slice that widens the reverse generator should
> widen it here. (`node.Values[node.Values.Count - stringPos]` is the same fault and reads better,
> but IDE0056 rejects it as a build error, hence the `^` form.)

> **Control C**, `CountOne`'s reverse walk tests the character *at* the position: change
> `&& MatchesMany(state, node, reverse ? state.PrevPos(pos) : pos)` to
> `&& MatchesMany(state, node, pos)`.
> Result: **23 of 600 at seed 7, 17 at seed 4242.** Several rows surface as
> `IndexOutOfRangeException` rather than as a wrong span; the consumer reports both as divergences.

> **Control D**, `STRING_FLD_REV` reads the folding from the front: in the
> `case Opcode.StringFldRev:` block, change
> `&& SameCharIgn(state.Encoding, node.Values[stringPos - 1], folded[foldedPos - 1])` to
> `&& SameCharIgn(state.Encoding, node.Values[stringPos - 1], folded[0])`.
> Result: **21 of 600 at seed 7, 20 at seed 4242.** *This control is why `_generate_reverse_fold`
> exists.* With the reverse generator's fold shares coming from plain `case-folding`, the same
> mutant gave **1** divergence of 600 at seed 7 - the shares, the FULLCASE coin, the shape draw and
> the alphabet rotation multiply out to almost nothing, and this is the cell upstream's 2026.5.9
> fix was about. The generator was widened on the spot and the control re-run.

> **Control E**, a one-character `_REV` step of one code unit rather than one character: in the
> same `case Opcode.CharacterRev:` block, change
> `state.TextPos = Step(state, state.TextPos, node.Step);` to
> `state.TextPos += (int)node.Step;`.
> Result: **12 of 600 at seed 7, 10 at seed 4242.** This is the astral hazard the slice named.

> **Control F**, `REF_GROUP_REV` steps one code unit: in the `case Opcode.RefGroupRev:` block,
> change the two lines
> ```
>                             stringPos = state.PrevPos(stringPos);
>                             state.TextPos = state.PrevPos(state.TextPos);
> ```
> to
> ```
>                             stringPos -= 1;
>                             state.TextPos -= 1;
> ```
> Result: **0 of 600 at seed 7, 0 at seed 4242 - it does not fire, and that is the correct
> answer.** Both operands index the same string, so a code-unit walk stays in lockstep with a
> codepoint walk: it tests each surrogate half separately and reaches the same end. That is S21's
> recorded finding for `REF_GROUP`, re-measured here in reverse. `PrevPos` is kept for S21's reason
> - upstream's `--` means one character, and Phase 5's fuzzy retry moves `string_pos` on its own,
> where the lockstep argument stops holding. **Do not read F as a gap in the wave.**

**For S24 and S25.** The 34 retagged tests are the bulk of what `find-all`, `splitting`,
`substitution` and `overlapped` will unlock, and every one of them is a *reversed* pattern that
now matches correctly - so those slices are wiring up the enumerator, not the engine. Reverse
enumeration has its own rule upstream (`Matches` walks from the end and steps left), which is
theirs to port, not this slice's.
