---
slice: S37
phase: 5
title: Judge the thirteen composed-wave rows and make interactions green at 6000 rows
delivers: []
---

# S37 - The composed wave at 6000 rows

Phase 4's one deliberately unfinished item, placed first because Phase 5 will widen the same
`interactions` generator with fuzzy constraints (S43), and a generator that is red before the
widening cannot tell a new divergence from an old one. No engine work is expected: S36 measured
that every one of the thirteen rows belongs to a family somebody has already judged.

## Scope

- **The thirteen rows.** `interactions` at 6000 rows is red at all five seeds S36 tried
  (the seeds are in S36's closing notes; the default three are 7, 4242 and the run date). S36's table: 9 rows are a group call inside a
  lookaround running the other way from the pattern (S30's pinned KNOWN DIVERGENCE, upstream loses
  matches this port finds; confirmed NOT fixed by upstream issue 614); 3 are `(*SKIP)` with
  `partial=True`, where upstream's `search_start` partial arms start the partial earlier than the
  slow path does (the `search-start-partial` family, in a shape its predicate does not reach); 1 is
  `(?r)BB([^\d]??)` over `' B.\r.'`, a bounded lazy repeat losing its partial
  (`bounded-lazy-repeat-partial`, keyed on individually judged rows).
- **Judge each row the ordinary way**: reproduce it in isolation against upstream (probe under
  `tools/probes/`), confirm which side is right against the research already on file
  (`docs/plan/2026-09-12-divergence-research.md`, PCRE2 10.47 via ctypes where the construct
  exists there), and pin the answer. A row that turns out NOT to be the family S36 assigned it to is
  the finding this slice exists for: minimise it and, if it is this port's, fix it.
- **Widen or add entries** in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`. Every entry
  stays strict: a predicate that names the shape, and a remark that quotes the probe. The
  staleness alarm `Every_expected_divergence_still_diverges` must still pass, so an entry that
  matches nothing at three seeds is wrong.
- **Two shortcuts are already known to fail** and are not to be retried: recording `interactions`
  prefilter-free (removes one row, adds another, leaves the three `(*SKIP)`-plus-partial rows
  unchanged, because those are `search_start` and it is not reachable from Python), and
  retargeting the thin controls at `interactions` before the wave is green.

## Verification

- `pwsh -File tools/run-oracle.ps1 -Generator interactions -Rows 6000` GREEN at the three default
  seeds, and additionally at 99991. Then the default wave (`pwsh -File tools/run-oracle.ps1`) still
  GREEN, so no widened entry has swallowed a row it should not.
- Negative controls in `tools/controls.json`: one per new or widened entry, each a mutation of the
  port that the entry's predicate must NOT excuse (for example: make the lookaround-direction group
  call agree with upstream, and the S30 pinned test must go red). Record with
  `python tools/run-controls.py --slices S37 --seeds 2`.
- The five thin-or-dead Phase 4 controls S36 recorded: re-run at `interactions` now that it is green
  and record whether they came to life. Do not retarget them; just measure.

## Done when

- [x] `interactions` GREEN at 6000 rows at four seeds; the default wave GREEN at three.
- [x] Every new or widened entry has a quoted probe, a permanent pinned test where the port is
      right, and a negative control that fires.
- [x] Any row that is this port's bug is fixed, test first, and named in the closing notes.
      **None was.** One candidate was chased and cleared - see "The one suspected port bug".
- [x] Ratchet GREEN, baseline updated only if a test was added, blind review (hunt: an entry
      predicate wider than its remark; a control that passes because the wave is red for another
      reason), commit.

---

## Closing notes (2026-09-12)

### The thirteen rows were three families, and not the three S36 named

S36's table said 9 group-call rows, 3 `(*SKIP)`-plus-partial and 1 `bounded-lazy-repeat-partial`,
over five seeds. Re-measured at the three default seeds and 6000 rows the split is **4 group-call, 3
partial and 1 reversed overlapped `(*SKIP)` extra match**, with no bounded-lazy row at all, and seed
99991 then added a fifth group-call row in an outcome shape the other four do not take. So the
families carried forward, the per-family counts did not, which is the ordinary behaviour of a
generative wave and is worth knowing before trusting any such table.

**Every one of the eight rows was replayed against `regex` 2026.9.10** (`.venvs/regex-2026.9.10`,
loaded by path from the default interpreter): all eight answer exactly as 2026.7.19 does. Nothing
here is waiting for the Phase 6 sync.

**No engine work, as the slice predicted.** Three changes to
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` and nothing in `src/`.

### Family A: upstream loses the match, and the judgement needs no second engine

Five rows - 4182 (seed 7), 4407 (seed 20260912), 5087 and 5773 (seed 4242), 1624 (seed 99991) - all
with a group call reached from a lookaround running the other way round from the pattern, and all
with upstream finding LESS than this port. This is S30's KNOWN DIVERGENCE seen through the composed
generator; the existing `group-call-direction` entry is the same precondition with a different
symptom (a bad capture, not a lost match) and could not reach them.

The tell that settles it is upstream's own: **deleting the piece that holds the call gives upstream a
match it refused with the piece present, and in every row that piece can match zero-width, so it
cannot remove a match.** Two rows say so outright (a `??` optional and a `*` repeat can always take
zero iterations); the other three say it through upstream's answer to the call-free copy, whose match
is no wider than the pattern without the piece.

    \b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*
    over 'İİ\nİİﬁﬁ ' overlapped: upstream (0, 4) alone
    drop the trailing (?:...)* : upstream (0, 4) AND (3, 7), which is this port's answer

Re-runnable: `python tools/probes/upstream-group-call-loses-matches.py`.

**MINIMISATION FAILED, and that is the slice's most useful negative result.** An automatic shrinker
was written twice. The first kept upstream contradicting itself (call answer != inlined answer) and
produced tiny patterns - `(?P<g1>a)((?<!(?&g1)))*` over `'a'`,
`(?r)(?P<g1>[A])((?(?=(?&g1))S))` over `'A'` - **on which this port answers what upstream answers**,
verified by replaying nine such candidates through the oracle consumer (all agreed). So two things
fell at once: "a call through an opposite-direction lookaround" is necessary and not sufficient for
the divergence, and **inlining a called group is not semantics-preserving**, which had looked like a
promising recorder field and is not one - on the short forms the inlined copy differs from the call
copy for BOTH engines. The second shrinker put the port in the loop, batching every single-deletion
candidate into one rows file per round; it was still running down from 51 characters when the
judgement above made it unnecessary. The rows are listed whole, and the ledger entry carries the
caveat.

### Family B: both engines answer a partial, and they answer different ones

Three rows - one at seed 4242 and two at 20260912, none at seed 7 - every one with a `(*SKIP)`.
`search-start-partial`'s predicate ended in
`ours is NoMatchOutcome`, which was incidental to the rows S33 had rather than part of its
discriminator: all three of these rows already carry `searchOnlyPartial: true`, upstream's own
signature. What they add is a second symptom - this port answers its OWN partial somewhere else.

    (?:\w{2,}(*SKIP)\w|\w)\B  over 'a.Aa'
      upstream search(partial=True)        (0, 4) partial   - the WHOLE subject
      upstream match('a.Aa', 0, 4, ...)    None             - its own door denies it
      upstream match('a.Aa', 4, ...)       (4, 4) partial   - which is this port's answer
      verb deleted:  search gives a COMPLETE match at (2, 3)
      verb -> (*PRUNE): search gives this port's partial

**The arm is keyed on ROWS, and the first draft of it was a predicate that the blind review killed.**
That draft asked only that upstream's partial span the whole searched region, which really is
`search_start`'s fingerprint - its partial arms set `new_position->text_pos` to the end of the text
or slice (`:8471`, `:8487`) while the match start stays where the search began - but it put no
condition on this port's side beyond "a partial, rendered differently", and a one-character defect in
the port's own partial start was classified instead of reported. No predicate over this port's answer
is narrow enough here, so the arm lists the judged rows with the answer this port is judged right
about, exactly as `bounded-lazy-repeat-partial` does, and widening it means judging another row.
`python tools/probes/upstream-search-start-whole-region-partial.py`.

### Family C: one row, and a clause that was wider than its own justification

Row 5543 is `overlapped-skip-extra-match-reversed` exactly - upstream's extra match (3, 5) carries
`g2` at (5, 6), deleting the verb or making it `(*PRUNE)` removes it, writing the called group out
leaves it, and upstream's own `search` and `match` at every position answer (3, 6) and never (3, 5).
The only thing keeping it out of its own entry was `CarriesACaptureOutsideItself` excluding any
pattern holding a lookaround, and this one holds a `(?<!`.

**A negative lookaround cannot put a capture anywhere**, because it only succeeds when its body
fails. Measured rather than asserted, on both engines:
`regex.search(r'(a)(?!(?:(b))x)b', 'ab')` is (0, 2) with group 1 `['a']` and group 2 EMPTY, and this
port agrees. The clause now excludes `(?=`, `(?<=` and `\K` only.
`python tools/probes/upstream-negative-lookaround-captures.py`.

### The one suspected port bug, chased and cleared

Row 4182's port answer reports `lastindex` 1 where upstream's answer to the same pattern with the
call written out reports 2, which looked like a defect in the group that closed last. It is not: the
two are different patterns. Asked the same question with no call in it at all -
`(?P<g1>\S)([a]{0,0})?\2\b` over `'aa𐐨𐐨A'` and over `'aA'` - upstream and this port both answer
`lastindex` 2, and both rows were replayed through the oracle consumer as agreements. **No port bug
was found in this slice.**

### The fourth seed earned its place, and so did a deliberate gap in a predicate

`UpstreamFoundStrictlyLess` was written with arms for the three outcome shapes the judged rows took
and **no `split` arm**, on the rule that a shape no row has shown should red the run rather than fall
into a predicate nobody has tested. Seed 99991 then drew a `split` row of exactly that family and
reddened a 6000-row wave. It was judged by the same probe, the arm added, and the row listed. The
remaining unexercised arm is the single-match one, and the entry says so.

### Verification

- `pwsh -File tools/run-oracle.ps1 -Generator interactions -Count 6000 -Seeds 7,4242,20260912,99991`
  **GREEN at all four**: agree 5995/5993/5996/5992, expected 5/7/4/8, diverge 0.
  (Note for a future session: the slice file's own command line said `-Rows 6000`, and `-Rows` is a
  path to a JSONL file - the rows-per-generator parameter is `-Count`.)
- `pwsh -File tools/run-oracle.ps1` **GREEN at three seeds**: agree 5996/5999/5998, expected 4/1/2,
  diverge 0.
- Ratchet GREEN, 5772 tests, 5589 passing, 183 skipped (all fuzzy), parity 90.7%.

### Negative controls, one per entry, and how to re-run them

`python tools/run-controls.py --slices S37 --seeds 3`, run last against the code and the
`controls.json` being committed. Seed 555 is fresh to this repository and is each control's third.

| id | what it breaks | 7 | 4242 | 555 |
|---|---|---:|---:|---:|
| S37-A | a called group is never recompiled for the caller's direction | 22 | 23 | 17 |
| S37-B | every partial covers the whole slice, as upstream's prefilter's does | 68 | 61 | 82 |
| S37-C | a reversed overlapped scan skips a start position | 13 | 20 | 14 |

**S37-A**, `a-called-group-is-never-recompiled-for-the-callers-direction`: in
`src/FuzzyRegex/Parsing/ParseFunctions.cs`, in the "Calling a capture group" arm, change

    if ((defReverse, defFuzzy) != (reverse, fuzzy))

to

    if ((defReverse, defFuzzy) != (defReverse, fuzzy))

Wave: `recursion`, 2000 rows. **Read the `expected` column as well as `diverge` for this one.** The
mutation is upstream's own issue-614 defect, so it makes this port AGREE with upstream where it used
to diverge: `expected` goes from 5 to **0** at every seed, and
`Every_expected_divergence_still_diverges` fails outright with "group-call-direction: an entry must
account for its own example row 1". A predicate cannot notice a port that has stopped diverging; the
example rows can, and that is what the second alarm is for. The first attempt at this control -
`subargs.Forward = forward && args.Forward;` in `NodeCompiler.BuildLookaround` - was **dead, 0 and
0**, because direction reaches a called group through the parser's extra per-direction copy and not
through `CompileArgs.Forward`; it is recorded here so nobody rebuilds it.

**S37-B**, `every-partial-covers-the-whole-slice-as-upstreams-prefilter-does`: in
`src/FuzzyRegex/Engine/Matcher.cs`, after `if (status is MatchStatus.Success or MatchStatus.Partial)`,
add a second line to the partial block so it reads

    state.TextPos = state.Reverse ? state.SliceStart : state.SliceEnd;
    state.MatchPos = state.Reverse ? state.SliceEnd : state.SliceStart;

Wave: `partial-sliced`, 2000 rows. This is the mutation the widened arm most has to survive - the port
claiming exactly upstream's whole-region partial - and it survives: all 61-82 mutated rows are
reported while `expected` holds at 5-7. Two weaker versions were measured first and are recorded so
they are not retried: the same site with `SliceEnd` replaced by `TextPos` gave 0/1 on `interactions`
and 1/2 on `partial`, and 2/2 on `partial-sliced`.

**S37-C**, `a-reversed-overlapped-scan-skips-a-start-position`: in
`src/FuzzyRegex/Engine/MatchState.cs`, in `AdvancePastMatch`, change

    TextPos = Reverse ? PrevPos(MatchPos) : NextPos(MatchPos);

to

    TextPos = Reverse ? PrevPos(PrevPos(MatchPos)) : NextPos(MatchPos);

Wave: `interactions`, 6000 rows. At 2000 rows it gave 1 and 7, which is thin; 6000 is where the
reversed overlapped cells are dense enough. `expected` is unchanged at 5-7 throughout, so the widened
`CarriesACaptureOutsideItself` keeps classifying its own rows while reporting every mutated one.

### The five thin-or-dead Phase 4 controls, measured against the composed generator

S36 asked for this and could not answer it, because `interactions` was red at 6000 rows and every
figure was at or below baseline. It is green now, so the measurement means something. Measured at
`interactions`, 6000 rows, seeds 7 and 4242, by retargeting them **temporarily** - `controls.json` is
committed unchanged apart from the three S37 entries.

| id | its own generator | interactions 6000, seed 7 | seed 4242 |
|---|---|---:|---:|
| S28-E | `conditionals` 2400: 0 / 0 | **0** | **0** |
| S29-A | `verbs` 600: 0 / 1 | 1 | 2 |
| S29-D | `verbs` 600: 2 / 0 | **0** | **0** |
| S31-D | `partial` 600: 3 / 2 | 2 | 0 |
| S31-E | `partial` 600: 1 / 1 | 0 | 1 |

**One of the five came to life, and only just.** S29-A - `(*SKIP)` widening the slice instead of
moving it - fires at both seeds for the first time, where its own generator gives 0 at one of them.
The other four are no better: S28-E and S29-D are still **dead at both seeds**, and S31-D and S31-E
are at or below their own numbers. So the composed generator is not the answer to a thin control, and
the four that need one still need it: S28-E needs a conditional inside a repeat whose guard lists have
to survive, which nothing generates. They were **not retargeted** - the slice said measure, not move.

### Review

**Two blind passes. Findings raised: 6. Reproduced: 6. Fixed: 6.** An unusually high survival rate
for this repository's usual one in five, and the reason is the same as S36's: four of the six were
arithmetic or a quoted claim, which either reproduces or does not. The two that were not are the two
that mattered, and both were in the new predicates.

**Pass 1, over the whole diff. Finding 1 falsified this slice's own entry text and changed the
design.** The widened `search-start-partial` arm asked only that UPSTREAM's partial span the whole
searched region, and put no condition on this port's answer beyond "a partial, rendered differently".
Its remark claimed "a partial this port misplaces by a character or two does not look like that and
is still reported"; the reviewer added one line to `Matcher.cs`'s partial block -
`state.MatchPos = state.Reverse ? state.MatchPos + 1 : state.MatchPos - 1;` - and watched the entry's
own minimised row be classified EXPECTED with the port answering (3, 1) instead of (4, 0). So the
arm was rewritten **keyed on rows**, exactly as `bounded-lazy-repeat-partial` is, with this port's
judged answer held beside each. Re-measured both ways afterwards: mutated, `expected 0 diverge 4 of
4`; clean, `expected 4 diverge 0 of 4`.

**Finding 2 found a spelling the exclusion list could not see.** `CarriesACaptureOutsideItself`
tested for `(?=`, `(?<=` and `\K`, and under `(?x)` upstream reads the character after `(?<` with
`source.get()` rather than `get(True)` (`upstream/regex/_regex_core.py:863`), so `(?<  =(a))` is a
positive lookbehind and does hold a capture that can lie outside the match. The reviewer built two
rows differing only in that spacing, applied control S37-C, and watched the spaced one be classified
while its twin was reported. The test now reads the pattern with every space removed, which is
conservative in the safe direction - stripping can only ADD an exclusion, and an exclusion only ever
reddens the run. Verified the same two rows afterwards: `diverge 2 of 2`.

Findings 3 and 4 were quoted claims the probes' own output contradicted: the partial probe's header
said this port's answer is upstream's own "once asked at that position", which is true of three rows
and not of the reversed one (its `pos` sweep was the wrong bound; it sweeps `endpos` now, and
upstream still never gives this port's (0, 0)); and two places said "four rows" where five are
listed.

**Pass 2, over the delta pass 1 never saw** - the row-keyed arm, its three new constants, the
whitespace strip and the two probe headers. **Two findings, both reproduced, both fixed.** "One per
seed" was false: regenerating each wave shows the three partial rows are at seeds 4242, 20260912 and
20260912, and seed 7 draws none, so the family is evidenced at two seeds of three. It was written in
four places and is corrected in all of them. And the probe's `reversed_row` test, `startswith('(?r)')`,
misses `(?ri)` and `(?i)(?r)`, which would sweep the wrong bound and print "no match anywhere" for a
row upstream does answer; it now reads `'(?r' in pat`, matching `ExpectedDivergences.IsReversed`. No
generator emits those spellings, so it is latent - the cases in that probe are hand-copied.

Pass 2 also confirmed what pass 1's fixes rest on: the four judged answer strings are byte-identical
to the report's `port` lines in that order, `Question(row)` has no collision across them, the
whitespace strip cannot suppress a classification that matters (its only caller reddens on `false`),
and the `Example` block is byte-identical to the rows the arm classifies, so the staleness alarm
tests the same questions.

**No third pass.** The delta after pass 2 is two comment corrections and one substring test widened
from `(?r)` to `(?r`, in a probe that no test or tool reads.
