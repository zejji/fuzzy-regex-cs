---
slice: S26
phase: 3
title: Oracle hardening, symbol accounting, and closing Phase 3
delivers: []
---

# S26 - Oracle hardening, symbol accounting, and closing Phase 3

The last Phase 3 slice. After it, every construct in the phase's scope matches, the oracle has
swept the whole surface at once, and Phase 4's author has a handover.

## Scope

- **A combined oracle hardening wave**: the S16-S25 generators composed rather than run
  per-family - classes inside quantified groups with backreferences under `(?i)`, `(?r)` and
  MULTILINE, over subjects mixing ASCII, expanding-fold characters and astral planes, through
  search, sub and split alike. Feature *interactions* are what per-slice waves structurally miss
  and what this wave exists to catch. Budget it: a bounded row count with the seed recorded, so
  it is reproducible. Zero unexplained divergences; every real one minimised into a permanent
  test before the phase closes.
- **The five rejections re-checked against the CI-built pinned oracle** (2026.8.12 from
  `upstream/`), not just the local 2026.7.19 - the S15 evidence was recorded against the PyPI
  build, and the phase close is the cheap moment to re-run it via the oracle workflow
  (`workflow_dispatch` on oracle.yml) and quote the result.
- **Every in-scope `_regex.c` symbol accounted for.** List every function
  (`grep -n "^Py_LOCAL\|^static " upstream/src/_regex.c`) and check each appears in
  `docs/PORTMAP.md` as ported, deliberately not ported with a reason (locale encoding, bytes
  variants, pickling, GIL/lock machinery, scanner/splitter objects, `match_detach_string`,
  deallocs), ported-in-Phase-3, or **explicitly deferred**: the Phase 4/5 matching opcodes
  (lookaround, atomic, recursion, conditional-with-lookaround, partial, fuzzy, named-list
  matching, POSIX best-match) and the Phase 7 prefilters (`locate_required_string`, the
  `string_search`/`fast_string_search` family, the `try_match` test-node path - DECISIONS
  2026-08-31). S13 did this for `_regex_core.py`; this is the engine's turn.
- **The prefilter contingency, checked**: confirm no ported test is skipped-for-timeout because
  of the deferral (S19's contingency). If any is, port the needed prefilter now rather than hand
  Phase 4 a slow engine with a hidden hole.
  **The quadratic half of this box is closed.** It was widened on 2026-09-01 to cover a test that
  passes *slowly* rather than one that is skipped, because `.*?cd` over 60,002 characters took 69
  seconds against upstream's 0.0003. The cause was not a missing prefilter - the call is anchored,
  so `locate_required_string` is not what saves upstream - it was `StepBy`/`CountBetween` walking
  the subject to convert a character count into a UTF-16 position, exactly `3n^2` steps. Fixed the
  same day in `1d9f71c` and `d39a3f1`. So this box now only has to:
  - confirm the two permanent complexity guards still pass:
    `RepeatTests.A_lazy_repeat_over_a_long_subject_costs_time_proportional_to_its_length` and
    `A_lazy_repeat_over_a_long_astral_subject_costs_time_proportional_to_its_length`;
  - re-run the recorded negative control - force `MatchState.OneUnitPerCharacter` to `false` and
    confirm the first guard fails at its 20-second ceiling - because a guard nobody has watched
    fail is not evidence that it is load bearing;
  - state plainly whether any *other* ported test is now slow enough to want a prefilter, and if
    none is, say so and leave the prefilters to Phase 7.
  DECISIONS 2026-09-01.
- **The `NextPos`/`PrevPos` asymmetry, decided.** Forward stepping refuses to pair a high and low
  surrogate when `pos + 1 >= TextEnd`; backward stepping pairs regardless of any bound, so above
  `TextEnd` the two disagree about where a character starts. Surfaced by `CharacterIndexTests`
  sweeping positions the engine cannot reach, and proven unreachable from the engine. Left alone on
  2026-09-01 because it is a semantic decision about what `beginning` and `length` mean when they
  split a surrogate pair, upstream has no opinion to port (it indexes by codepoint, so an `endpos`
  inside a character cannot exist), and bundling a semantic change into a performance fix makes
  both harder to review. Decide it here, in writing, one of three ways: make the two symmetric;
  keep the asymmetry and document it on the public `beginning`/`length` parameters; or record it as
  a Phase 6 hardening item. Do not leave it undecided a second time. DECISIONS 2026-09-01.
- **Un-skip sweep**: walk the remaining skipped tests whose tags Phase 3 delivered - any test
  still skipped on a delivered tag is either a defect (fix it) or mis-tagged (retag with prose),
  the S13 rule that a phase does not close with its own tags still on the board.
- **Phase 4 handover notes** in the closing notes, for whoever authors the Phase 4 slices: where
  the lookaround/atomic/recursion seams sit in `basic_match` and what they throw, what
  `state_init_2` already allocates for them, what partial matching needs of the search loop, the
  named-list matching seam (`STRING_SET` opcodes), and anything this phase learned that changes
  Phase 4's shape.
- **Roadmap and budget**: record Phase 3's measured sessions-per-slice from
  `docs/plan/slice-log.jsonl` against the 13-slice estimate, as the roadmap asks; flag for the
  owner whether the Phase 4 estimate (8-12 slices) still looks right from here.

## Verification

This slice's verification is the phase's: the hardening wave, the symbol accounting, and the
sweep are each checkable and their outputs quoted.

## Done when

- [x] Hardening wave ran with seed and counts quoted; zero unexplained divergences; every real
      divergence pinned as a permanent test.
- [x] **Every negative control recorded in a Phase 3 slice's closing notes re-run from those
      notes, AND at a seed the slice never used.** This is the moment the recorded values pay
      for themselves. At the recorded seed, a control that no longer reproduces means either
      the generator has lost its teeth or the notes are wrong, and both are findings. At a
      fresh seed it is the thin margins that show: re-running at the recorded seed reproduces
      the same number forever and can never distinguish a control that catches a fault from
      one that caught a coincidence. S18's are exempt - they predate the rule and are
      unreproducible, as its slice file records.
- [x] Five rejections confirmed against the CI-built 2026.8.12 oracle, output quoted. **Settled by
      source identity instead** - see the closing notes; neither the CI route nor a local build was
      available, and the diff between the two releases proves the rejecting code is byte-identical.
- [x] PORTMAP complete for `_regex.c`: every symbol ported, not-ported-with-reason, or
      deferred-to-named-phase; prefilter contingency checked and recorded.
- [x] No test remains skipped on a tag Phase 3 delivered; `docs/STATUS.md` regenerated and the
      overall parity figure quoted in the closing notes.
- [x] `CHANGELOG.md` gains Phase 3's entry. Pre-1.0 it tracks phases, not slices, so a phase
      close is the only place it is written.
- [x] Roadmap updated with Phase 3's measured rate; STATE.md says Phase 3 is complete and the
      driver stops at the boundary; Phase 4 handover notes written.
- [x] Ratchet GREEN, baseline updated, blind review over this slice's unreviewed changes (hunt:
      a hardening-wave divergence explained away in prose instead of minimised into a test, a
      PORTMAP row claiming "ported" for a symbol whose opcode still throws a seam), commit.
- [x] **Every negative control re-run from the notes and at a fresh seed** (the box above), with the
      three that could not be reproduced as written written up rather than quietly re-recorded.

---

## Closing notes (2026-09-01)

### What landed

No engine behaviour changed. Phase 3 closes with the engine exactly as S25 left it, plus one
harness fix, one generator, one gap test and the accounting this slice exists for.

- **A thirteenth oracle generator, `interactions`** (`tools/record-oracle.py`), and it is in
  `run-oracle.ps1`'s default list from now on. It composes what the per-family generators hold
  fixed: a class atom inside a quantified capture group, referenced back, with a group-existence
  conditional, `\K` and a word boundary alongside, under IGNORECASE, FULLCASE, MULTILINE, VERSION1,
  ASCII and `(?r)` in combination, over subjects that hold ASCII, expanding-fold and astral
  characters *in the same string*, through all eight operations - search, match, fullmatch, sub,
  subf, finditer, overlapped finditer and split.
- **The oracle consumer now bounds each row at ten seconds** (`OracleComparer._rowTimeout`) instead
  of running it with `InfiniteMatchTimeout`. This is the slice's one real harness defect, found by
  its own control re-runs: a mutant that makes a row loop for ever used to hang the run with no
  output, which looks exactly like a slow build. DECISIONS 2026-09-01.
- **The `\K` shapes in the `boundaries` generator are weighted and two were added**, because S20's
  keep-restore control fired at one seed and not another. See "the thin controls" below.
- **Two tests.** `MatchSpineTests.A_length_that_cuts_a_surrogate_pair_leaves_a_lone_surrogate_...`
  pins what `beginning`/`length` mean when they cut a surrogate pair - which is how the
  `NextPos`/`PrevPos` question was settled - and
  `OracleWaveTests.A_row_this_port_cannot_answer_in_time_is_a_divergence_and_never_agreement` pins
  both halves of the row deadline: that it is finite, and that a timed-out row is a divergence
  rather than a rejection. The second was added by the blind review, below.
- **`docs/PORTMAP.md` gains "Every `_regex.c` function, accounted for (S26)"**, the engine's
  equivalent of what S13 did for the parser.

### The hardening wave

`pwsh -File tools/run-oracle.ps1 -Generator interactions -Count 3000 -Seed 20260901`:

```
agree 3000  unsupported 0  diverge 0  of 3000 rows
```

**Zero divergences, so nothing to minimise.** Re-run as the last thing this slice did, after every
code and generator change, because a wave that ran part-way through measures a generator that no
longer exists. What the wave holds, measured at 600 rows of seed 1
and quoted in the generator's docstring: 285 rows carry IGNORECASE, 144 FULLCASE, 301 MULTILINE, 213
VERSION1, 78 ASCII, 273 are reversed, 213 have an astral subject, 192 a subject that expands on
folding, 390 hold a backreference, 293 a quantified group, 54 a `\K`, 37 a conditional; each of the
eight operations appears exactly 75 times; 114 rows produce an answer and 16 are rejected by
upstream.

And the whole default list, all thirteen generators, immediately after
(`pwsh -File tools/run-oracle.ps1 -Count 1000 -Seed 20260901`):

```
wrote TestResults/oracle/wave.jsonl: 13000 rows, seed 20260901, regex 2026.7.19 (pypi)
agree 13000  unsupported 0  diverge 0  of 13000 rows
```

`unsupported 0` across 13,000 rows is worth noticing on its own: no generator emits a construct the
engine cannot answer, which is what makes `diverge 0` mean something.

**A generator with no control behind it is a wave nobody has shown can detect a fault**, so two
existing mutations were re-aimed at it: S21-D's (a reference reading the wrong capture) fires on 7
rows of 1200 and S22-A's (the case-fold comparison skipping a case) on 5 - see `S26-I1` and `S26-I2`
in the control table. Thinner per fault than the dedicated waves, which is the honest
characterisation of a composed generator: **broader and shallower, so it complements the per-family
waves rather than replacing them.** Both also fire at a second seed, 4242.

### Every Phase 3 negative control, re-run

**The headline: 37 controls. 35 reproduced their recorded figure exactly at the recorded seed - one
of them only after that very figure disambiguated which of two sites its notes meant - and two,
S17's, could not be reproduced as described at all.** S18's are exempt, as its slice file records:
they predate the rule and its scratch files are gone.

How to re-run any of them: `.scratch/controls.json` holds every mutation as
`(file, anchor, anchorNth, before, after, generator, count, seeds)` and
`.scratch/run-controls.py` applies it, formats the file, records each wave once, runs the consumer
and restores the file. `python .scratch/run-controls.py --check` resolves every site without
building anything; `--slices S19,S20` or `--ids S21-B` runs a selection. Both files are in
`.scratch/`, so they are **not committed** - the mutations themselves are the table below, which is
what the rule asks for, and the driver is thirty lines of glue that any session can rewrite from
this description.

The fresh seed is **20260901**, which no Phase 3 slice used. Where a `boundaries` row reads
"**n** ✔ then m", the first figure is against the generator as S25 committed it - all four
reproduced exactly - and the second is against the widened generator this slice commits, which is
the number that counts from here.

| Control | Site | Recorded | At the recorded seed | At seed 20260901 |
|---|---|---:|---:|---:|
| S17-A `insetsymdiff` | `Matcher.InSetSymDiff`, accumulator starts `true` | 6 of 1200, **no seed recorded** | **10** (seed 7) | 19 |
| S17-B `ascii-property-table` | `Encodings.HasProperty`, `Ascii` becomes `Unicode` | 133 of 1200, **no seed recorded** | **138** (seed 7) | 106 |
| S19-A `greedy-min` | `GreedyRepeatOne` **forward** retreat loop, `<` becomes `<=` | 452 agree / **148** at seed 7 | **148** ✔ exact | 161 |
| S19-A (backtrack case) | the same edit in the **backtrack** `GreedyRepeatOne` | - | 14 | 8 |
| S19-B `lazy-greedy` | `LazyRepeatOne`'s `CountOne` argument `[1]` becomes `[2]` | 54 at seed 7 | **54** ✔ | 40 |
| S19-C `astral-step` | `CountOne`'s forward step becomes `pos + 1` | 1 at seed 7 | **1** ✔ | 9 |
| S20-A `ri-count` | `CountRegionalIndicatorsLeft` returns `leftPos - pos` | 1 at seed 7 | **1** ✔ then 2 † | 2, then 0 † |
| S20-B `gb9-extend` | GB9 drops `GbreakExtend` | 2 at seed 7 | **2** ✔ then 4 † | 8, then 8 |
| S20-C `wb5-letters` | WB5's `&&` becomes `\|\|` | 2 at seed 7 | **2** ✔ then 0 † | 2, then 2 |
| S20-D `keep-restore` | the `Keep` backtrack case drops the restore | 3 at seed 7 | **3** ✔ then 6 † | 0 †, then 2 |
| S21-A `unmatched-ref-matches-empty` | `RefGroup`'s `Current < 0` arm falls through | 7 at seed 1 | **7** ✔ | 13 |
| S21-B `group-exists-swapped-exits` | `GroupExists`'s two exits swapped | 59 at seed 1 | **59** ✔ | 60 |
| S21-C `group-exists-ever-matched` | `Current >= 0` becomes `Count > 0` | **0**, does not fire | **0** ✔ | 0 |
| S21-D `ref-reads-first-capture` | `Captures[Current]` becomes `Captures[0]` | 17 at seed 1 | **17** ✔ | 25 |
| S21-E `ref-steps-by-code-unit` | `NextPos` becomes `++` on both operands | **0**, correct answer | **0** ✔ | 0 |
| S22-A `same-char-ign-off-by-one` | `SameCharIgn`'s loop starts at 2 | 42 at seed 7 | **42** ✔ | 36 |
| S22-B `in-range-ign-skips-the-character` | `InRangeIgn`'s loop starts at 1 | 8 at seed 7 | **8** ✔ | 4 |
| S22-C `string-fld-advances-both-sides` | `StringFld` advances unconditionally | 4 at seed 7 | **4** ✔ | 3 |
| S22-D `ref-group-fld-does-not-fold-the-capture` | `RefGroupFld` compares the raw capture | **0** at seed 7 | **0** ✔ | 1 |
| S22-E `property-ign-drops-the-collapse` | `MatchesPropertyIgn` keeps only `PropLu` | 6 at seed 7 | **6** ✔ | 9 |
| S23-A `character-rev-reads-forward` | `CharBefore` becomes `CharAt` | 274 at seed 7 | **274** ✔ | 280 |
| S23-B `string-rev-walks-pattern-forward` | `Values[stringPos - 1]` becomes `Values[^stringPos]` | 9 at seed 7 | **9** ✔ | 12 |
| S23-C `count-one-reverse-tests-position` | `CountOne` drops the `PrevPos` | 23 at seed 7 | **23** ✔ | 26 |
| S23-D `string-fld-rev-reads-folding-from-front` | `folded[foldedPos - 1]` becomes `folded[0]` | 21 at seed 7 | **21** ✔ | 16 |
| S23-E `character-rev-steps-one-code-unit` | `Step` becomes `+= node.Step` | 12 at seed 7 | **12** ✔ | 11 |
| S23-F `ref-group-rev-steps-one-code-unit` | `PrevPos` becomes `-= 1` on both operands | **0**, correct answer | **0** ✔ | 0 |
| S24-A `last-pos` | `lastPos = state.MatchPos` | 163 at seed 7 | **163** ✔ | 172 |
| S24-B `unmatched-group` | an absent group expands to `"None"` | 37 at seed 7 | **37** ✔ | 29 |
| S24-C `count-limit` | `maxSub` becomes `count + 1` | 94 at seed 7 | **94** ✔ | 97 |
| S24-D `reverse-join` | the join list is reversed when *not* reversed | 130 at seed 7 | **130** ✔ | 149 |
| S24-E `format-negative-index` | `index += captures.Count - 1` | 16 at seed 7 | **16** ✔ | 28 |
| S24-F `min-width-code-units` | `CodepointCount` counts code units | 21 at seed 7 | **21** ✔ | 18 |
| S25-A `overlapped-from-end` | the overlapped step starts from `TextPos` | 54 at seed 7 | **54** ✔ | 63 |
| S25-B `split-tail-dropped` | an empty trailing split piece is dropped | 103 at seed 7 | **103** ✔ | 91 |
| S25-C `always-advance` | `MustAdvance = true` | 92 at seed 7 | **92** ✔ | 81 |
| S25-D `split-limit-one-end` | `maxSplits < 0` becomes `<= 0` | 21 at seed 7 | **21** ✔ | 21 |
| S25-E `overlapped-code-unit-step` | the overlapped step is `± 1` | 33 at seed 7 | **33** ✔ | 26 |
| S25-F `split-null-as-empty` | an absent group's split slot becomes `""` | 61 at seed 7 | **61** ✔ | 48 |
| **S26-I1** S21-D's mutation, `interactions` wave | 1200 rows | new | **7** (seed 20260901) | 4 (seed 4242) |
| **S26-I2** S22-A's mutation, `interactions` wave | 1200 rows | new | **5** (seed 20260901) | 2 (seed 4242) |

**S19-A was recorded against an ambiguous site, and the recorded figure is what disambiguated it.**
"The `GreedyRepeatOne` retreat loop" fits two places: the `while (true)` loop in the forward case
that walks the count back down, and the single minimum check in the backtrack case. The backtrack
one gives 14 divergences; the forward one gives **452 agree, 148 diverge** - S19's recorded numbers
to the digit. Both are in the table, because a future reader deserves to see which is which.

**S17's two could not be reproduced as described, and both failures are instructive.**
"Inverting `InSetSymDiff`'s toggle" has two readings, and the one that flips the *condition* is a
**no-op for any even-membered set** - every `~~` atom the generator emits has exactly two members,
so it diverged on 0 of 1200 rows. Inverting the accumulator's initial value fires. And S17-B as
written - deleting the ASCII substitution from `Encodings.HasProperty` - **does not compile**: it
leaves `_asciiMax` unused, which is S1144, which is an error here. Re-expressed as swapping which
encoding gets the substitution it fires on 138 rows. Neither can be checked against S17's figures,
because S17 recorded no seed. DECISIONS 2026-09-01 has both rules that follow.

† **The four boundary controls are thin, and after the `\K` widening three of them fire at one seed
and not the other at 600 rows.** That is a statement about how rare the rules are - WB5 needs a
dotted or dotless I between two letters, GB13 a run of regional indicators, and the keep restore a
branch that fails *after* the marker - not about the engine. Two things were done about it rather
than one. The `\K` shapes are now weighted towards the five that can reach the restore and two more
were added, which took S20-D from 3/0 to 6/2. And the same four controls were re-run at **3000
rows**, where the question "does the generator reach the rule at all" separates from "did this seed
happen to": see the table below. **No numeric threshold is set on any of this** - a rule that
genuinely applies to few subjects will always be thin, and inventing a floor would only make the
next slice widen a generator to satisfy an arithmetic rather than a gap.

| Control at 3000 rows | seed 7 | seed 20260901 |
|---|---:|---:|
| S20-A `ri-count` | 8 | 4 |
| S20-B `gb9-extend` | 11 | 24 |
| S20-C `wb5-letters` | 15 | 22 |
| S20-D `keep-restore` | 14 | 9 |

**All four fire at both seeds once the wave is 3000 rows**, which settles it: the generator does
reach these rules, at roughly 3 to 8 hits per thousand rows, and a 600-row wave is simply too small
a sample to say so. That is the answer to carry forward - **when a control is thin, raise the row
count before touching the generator's weights**, because the row count is the honest instrument and
the weights are the one that quietly stops the wave testing what it used to.

### Phase 4 handover

Where the work starts, for whoever authors the Phase 4 slices.

- **The seams are two `default:` arms**, not a scattering of stubs: `Matcher.BasicMatch`'s dispatch
  switch throws `Seam.For(node.Op)` (`Matcher.cs:4567`) and its backtrack switch throws
  `Seam.For((Opcode)op)` (`:5207`). Every Phase 4 opcode - `LOOKAROUND`/`END_LOOKAROUND`,
  `GROUP_CALL`/`GROUP_RETURN`, `CONDITIONAL`/`END_CONDITIONAL`, the branch-reset paths,
  `STRING_SET*`, the possessive forms - lands in those two places. `ATOMIC`/`END_ATOMIC` are
  already real (S20), so an atomic group is a worked example of the push/pop shape to copy.
- **What `state_init_2` does *not* allocate**: the fuzzy-guard and group-call-guard lists
  (`MatchState.cs:327`). `reset_guards` has the same two holes (`:3392`, `:3398` upstream). Each
  Phase 4 slice allocates what it reads, which is the convention the comment records.
- **The stack functions Phase 4 needs are unported on purpose**, and PORTMAP's S16 row lists their
  call sites: `push_groups`/`pop_groups` has six, every one `GROUP_CALL` or `GROUP_RETURN`;
  `push_repeats`/`pop_repeats` has ten, every one inside `CONDITIONAL`, `END_CONDITIONAL`,
  `GROUP_CALL` or `GROUP_RETURN`. **The backtrack half is what an opcode-by-opcode port misses** -
  it is where group state goes back when a call or a lookaround unwinds, so omitting it is wrong
  only on backtracking, which no simple test reaches.
- **`same_span_of_group` (`:11646`) is the branch-reset and `GROUP_EXISTS` helper** and is not
  ported; `GROUP_EXISTS` itself is (S21), so branch reset is what needs it.
- **Partial matching needs nothing of the search loop that is not there.** `MatchState` already
  carries `PartialSide` with `PartialLeft`/`PartialRight`, `CountOne` already reports `isPartial`,
  and every one-character and string opcode already has its partial arm. The whole seam is a
  four-line guard in `FuzzyRegex.Run` (`FuzzyRegex.cs:318-321`) that refuses `partial: true` - so
  the slice that lands it is mostly `Match`'s reporting surface and `do_match`'s partial exit,
  **not** the matcher. That is the largest single win left on the board at 82 tests.
- **Named-list matching is `STRING_SET`, `STRING_SET_IGN`, `STRING_SET_FLD` and their `_REV`
  twins.** The parser side is done (S13's `StringSet`), including the sort order that leaks into
  the bytecode - `StringSet.__init__` sorts branches by length with a *stable* sort, so the
  caller's order matters and the oracle recorder sorts both sides' input before comparing. A
  Phase 4 generator that does not do the same will file 58 false divergences, as S13's did.
- **What Phase 3 learned that changes Phase 4's shape**, in one line each: a repeat's state is
  per-attempt, so anything Phase 4 pushes must be popped on *every* backtrack path, not just the
  ones a test exercises; upstream fields that are written and never read exist (`version_0`,
  `visible_captures`), so `grep` for the reads before porting a mechanism; and a ported test that
  has never run is a hypothesis, not a fact - expect a handful per slice to be asserting the
  opposite of what upstream does.

### Roadmap and budget

`docs/plan/slice-log.jsonl` has **no `failed` and no `parked` entry between S14 and S26**: thirteen
slices, thirteen driver sessions, against Phase 2's 1.35 sessions per slice. The cost moved instead
- median 60.5M tokens per slice against Phase 2's 48M, from 10.5M (S14) to 91.5M (S22). The
ROADMAP now carries that, and the reading that goes with it: 1.0 is what a *tightly scoped* slice
costs, not what a phase costs, so budget Phase 4 at 1.0-1.35.

**Flagged for the owner, and it is a decision rather than a finding:** four capability families on
the generated board are claimed by no phase - `inline-flags` (29 tests), `backtracking-verbs` (34),
`version-flags` (11) and `comments` (4), 78 tests in all, which is more than lookaround and
lookbehind together. Three of the four are parser-adjacent, so they were plausibly assumed into
Phase 2 and are not in fact done. Either Phase 4 widens to 10-14 slices or they get a phase of
their own; the ROADMAP states both options.

### The five rejections, and why the answer is a diff rather than a run

The box asked for a re-check against the CI-built pinned 2026.8.12 rather than the PyPI 2026.7.19
the S15 evidence was recorded against. **Neither route was available in this session** - `gh` is
deliberately outside the driver's allowlist (ROADMAP, so an unattended slice cannot post upstream),
and a local build of `upstream/` fails for want of a C compiler:

```
error: Microsoft Visual C++ 14.0 or greater is required.
```

So the question was answered by diffing the two releases instead, which is a stronger answer than
either run: **the entire delta is one commit**, `1760a20 Support Python 3.15`, and

```
$ git -C upstream diff --stat 1760a20~1 1760a20 -- src/ regex/_regex_core.py regex/_main.py
 regex/_main.py | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

is a version string. `src/` - the whole C engine, including the `compile_to_nodes` failure that
raises `RuntimeError: invalid RE code` - is byte-identical in both builds, so a run against
2026.7.19 *is* a run against 2026.8.12 for this question. All five still reject
(`.scratch/five-rejections.py`):

```
regex 2026.7.19
  a{e<=1:\X}           RuntimeError: invalid RE code
  a{e<=1:\b}           RuntimeError: invalid RE code
  a{e<=1:\A}           RuntimeError: invalid RE code
  a{e<=1:\Z}           RuntimeError: invalid RE code
  a{e<=1:\L<a>}        RuntimeError: invalid RE code
```

One trap worth recording for whoever re-runs this: `regex.compile` refuses an unused keyword
argument, so passing `a=["ab"]` to the four patterns that name no list reports
`ValueError: unused keyword argument 'a'` and *looks* like the rejection under test.

### Symbol accounting

**458 function definitions in `upstream/src/_regex.c`; 240 named in PORTMAP, 218 not; none
unaccounted for.** The 218 fall into fourteen families, every one already covered by a row that
cites the family by line range rather than naming each member - and the new PORTMAP section
tabulates them with counts and dispositions. The three largest are `try_match_*` (36, Phase 7's
predicate dispatch), the fuzzy machinery (31, Phase 5) and `search_start_*` (30, Phase 7's
prefilters). Two existing rows were widened to say what they had been taken to mean: the `LOCALE`
row now names the fourteen `locale_is*`/`locale_to*`/`locale_word_*` predicates, and the audit
itself is `.scratch/symbol-audit.py`, whose bucketing is quoted in the section so a future sync
re-runs it rather than re-deriving it.

### The prefilter contingency, closed

No prefilter was ported, and the evidence is the run itself rather than a judgement.

- **Both complexity guards pass**: `A_lazy_repeat_over_a_long_subject_...` at 0.463s and
  `..._long_astral_subject_...` at 0.588s, each over a 240,000-code-unit subject with a 20-second
  ceiling. (Re-measured from the final ratchet's own TRX; the same two read 0.493s and 1.041s
  earlier in the session, which is machine noise on a figure two orders of magnitude under the
  ceiling.)
- **The guard was watched failing, and the recorded control turned out to be the wrong mutation.**
  The box said to force `MatchState.OneUnitPerCharacter` to `false` and confirm the first guard
  fails at its ceiling. **It does not fail** - the whole RepeatTests class runs in 1.2 seconds with
  the flag inverted, and the two tests that *do* fail are the two astral-correctness ones, which is
  what inverting a surrogate scan breaks. The reason is worth writing down: forcing the flag false
  only moves a BMP subject off upstream's restored arithmetic and onto the **sampled position
  table**, which is *also* linear. The flag is a refinement on top of an already-linear conversion;
  what is load bearing for the guard is `CharacterIndex` itself.
  So the control was re-expressed as the defect that actually existed - **make the conversion walk
  again** - and then the guard fails exactly as documented:

  > In `Matcher.StepBy`, replace `return state.GetCharacterIndex().StepForward(pos, count,
  > state.SliceEnd);` with `for (long i = 0; i < count && pos < state.SliceEnd; i++) { pos =
  > state.NextPos(pos); } return pos;`, and in `Matcher.CountBetween` replace
  > `return state.GetCharacterIndex().CountBetween(from, to);` with a `for` loop over
  > `state.NextPos` counting the steps; and invert the surrogate scan in `MatchState`'s constructor
  > (`< 0` becomes `>= 0`) so a BMP subject takes that path. Result:
  > `A_lazy_repeat_over_a_long_subject_costs_time_proportional_to_its_length` **fails in 20s 285ms**
  > with `RegexMatchTimeoutException`, against 0.493s unmutated.

  Two things that fall out of it. The 20-second ceiling **is** enforced by the engine's own timeout,
  as the test's comment claims - the counter is a `ushort` bumped by 0x100 per dispatch iteration, so
  the deadline is polled every 256 iterations and a quadratic scan cannot outrun it - and that claim
  had never been watched before. And a mutation that inverts a *fast-path selector* tests the fast
  path, not the algorithm: to see whether a complexity guard is load bearing, mutate the thing whose
  complexity it asserts.
- **Nothing else in the suite is slow.** From the final ratchet's own TRX: 5,716 results, 38.7s of
  test time, and **10 results take a second or more - every one of them either a Unicode table sweep
  over the whole codepoint space or a deliberate complexity guard.** The slowest is 2.362s
  (`Has_property_value_matches_upstream_for_every_property_and_value`, one of ours) and the slowest
  guard is 1.449s (`A_scan_over_a_long_subject_...`, S25's linearity ratio). Re-run it with
  `.scratch/slowest-tests.py`, which reads `TestResults/results.trx`.
- So the prefilters stay Phase 7's, behind benchmarks, and `try_match`'s predicate dispatch with
  them.

### The `NextPos`/`PrevPos` asymmetry, decided: kept, and the semantics above it pinned

Of the three options the slice offered - make them symmetric, keep and document, or defer to
Phase 6 - **keep and document**, for two reasons. The disagreement is reachable only above
`TextEnd`, which the engine never asks about, so a bound test in `PrevPos` would buy nothing
observable while sitting in the inner loop of every reverse step and every `CharBefore`; and a
change no test can turn red is a change this repo cannot protect.

What was actually missing was the semantics one layer up, and that has an oracle after all. Python
cannot express an `endpos` inside a character, but it can hold a `str` whose last character is a
lone surrogate - which is exactly the string a cut slice denotes. Measured against 2026.7.19
(`.scratch/lone-surrogate.py`):

```
  lone high, dot                     len=1 search('.') -> (0, 1)
  a + lone high, len                 len=2 search('a.') -> (0, 2)
  reversed, a + lone high            len=2 search('(?r).') -> (1, 2)
```

A lone surrogate is one character in both directions, this port answers the same for the equivalent
cut, and the new gap test pins it with the pair-intact case beside it. Documented on `PrevPos`
itself, which is where the next reader will be standing. DECISIONS 2026-09-01.

### The un-skip sweep

**Nothing is skipped on a tag Phase 3 delivered.** The twenty-seven tags S14-S25 delivered
(`basic-matching`, `full-match`, `character-classes`, `escapes`, `unicode-properties`,
`set-operations`, `ascii-flag`, `alternation`, `groups`, `named-groups`, `captures`, `quantifiers`,
`anchors`, `line-boundaries`, `word-flag`, `keep-marker`, `grapheme`, `backrefs`, `define-groups`,
`ignore-case`, `case-folding`, `right-to-left`, `substitution`, `format`, `find-all`, `splitting`,
`overlapped`) appear in no live `[Skip]` attribute - `grep` finds them only in doc comments, in the
convention tests' own fixtures, and in prose reasons explaining what *remains* unported. The
generated capability table in `docs/STATUS.md` agrees: every tag left on it belongs to Phase 4, 5
or 6. The eight `needs:conditionals` skips are the case worth naming, because they look like a
Phase 3 tag and are not: S21 delivered `GROUP_EXISTS`, and each skip reason says in prose that the
lookaround-condition form is the separate `CONDITIONAL` opcode and Phase 4's.

### Review

**One blind pass over the whole diff, on Opus, briefed to hunt for a claimed measurement the code
does not produce, the new row deadline masking a divergence rather than exposing one, the
`interactions` generator not exercising what it claims, the gap test asserting something upstream
does not do, and a PORTMAP row claiming a symbol is ported when its opcode still throws.**

**Five findings raised, all five reproduced, all five fixed.** An unusually high survival rate for
this repo, and the reason is that four of the five were arithmetic in prose rather than judgements
about code - the reviewer could check them by counting, so there was nothing to argue with.

1. **"Eleven feature areas at 100%" was wrong; the board says fifteen** - and it had reached
   `CHANGELOG.md`, which is public. Reproduced with `grep -c "| 100.0% |" docs/STATUS.md`. This is
   exactly the duty S25 handed this slice ("re-take every measurement you quote"), failed on the one
   figure that was copied out of the *previous* STATE.md rather than out of the generated board.
   Fixed in `CHANGELOG.md` and `docs/plan/STATE.md`.
2. **The symbol audit undercounted `_regex.c` by 109 functions**: 458 against a true 567. The first
   parser matched *declarations* with a regex, and upstream puts the opening brace at the end of the
   declarator, wraps long parameter lists and hides the return type in a macro. Reproduced by the
   reviewer's own parser and by `grep -c '^}$'`, which is 567 because a function body is the only
   thing in this file that closes with a bare `}` at column 0. **The script was rewritten to count
   braces**, which now agrees with that grep exactly, and the PORTMAP section was rebuilt from its
   output: 567 definitions, 274 named elsewhere in PORTMAP, 293 in sixteen family rows. Two further
   defects fell out of the rewrite - the audit was counting *its own section* as evidence that a
   symbol is named, and the `Python methods, properties and protocol` family (46 functions) had no
   row at all.
3. **The LOCALE row said "fourteen" `locale_*` functions where there are twenty.** Same root cause.
4. **`DECISIONS.md` said 32 controls reproduced where the closing notes' own table says 35**, and
   32 + 1 + 2 does not reach 37 either way. Fixed to 34 exact, plus S19-A after disambiguation,
   plus S17's two that did not.
5. **The row deadline was explained in prose but no test could go red on it** - VERIFICATION rule 8,
   and doubly awkward in a class whose own summary says "a harness that has never been shown to
   notice a wrong answer is not evidence of anything". Reproduced: setting `RowTimeout` back to
   `InfiniteMatchTimeout` left all eight tests green. Fixed by pinning both halves of the property
   in `A_row_this_port_cannot_answer_in_time_is_a_divergence_and_never_agreement`, and the fix was
   proved by mutation - with `InfiniteMatchTimeout` restored the suite reports
   `failed: 1  succeeded: 8`, and with the deadline back it is `failed: 0  succeeded: 9`.

The reviewer also checked and cleared, with reproductions: that a timing-out row cannot be filed as
agreement (`RegexMatchTimeoutException` is absent from `_rejections`, so both `Compare` branches
return `Diverge`); that every figure in the two generators' docstrings reproduces exactly; that the
new gap test's three oracle spans reproduce against regex 2026.7.19, including
`regex.search('a..', 'a\ud83d') is None`; that the ROADMAP's token figures and tag counts reproduce
from `slice-log.jsonl` and `docs/STATUS.md`; and that the mutation sites quoted in the prefilter
section exist verbatim. It declined to file the complexity-guard timings, which read 0.506s and
0.817s on its run against the 0.463s and 0.588s recorded here - noise on a figure two orders of
magnitude under the ceiling, and the notes already say so.

**A second blind pass ran over the delta the first reviewer never saw** - the rewritten symbol
audit and the PORTMAP section built from it, the `RowTimeout` visibility change and the new test,
and the four corrected figures. **Four findings, all four reproduced, all four fixed**, and two of
them were about the fix for finding 5 above, which is the case rule 4 exists for.

1. **The new test did not pin the change it exists for.** It asserts `RowTimeout`'s *value*, and
   nothing called `OracleComparer.Run`, so unwiring the deadline at its call site left the suite
   green. Fixed by giving `Run` an internal overload taking the deadline and asserting that a row
   which never stops on its own - `(a|a)*b` over 26 `a`s - comes back as a
   `RegexMatchTimeoutException` in 50ms. **The test returning at all is the proof**, and it costs
   no wall clock. What is still not pinned, deliberately and in a comment: that the one-argument
   `Run` passes `RowTimeout` rather than something else. Pinning that token needs a row that runs
   for the whole deadline, which would cost ten seconds on every oracle run for ever to catch an
   edit to one line sitting under the field whose remarks explain it.
2. **The second half of the test passed for the wrong reason.** It asserted that a timed-out row
   cannot be filed as a rejection, but the recorded row it used was rejected at *compile* time, so
   `Compare` answered on the phase rule before the allow-list was consulted - and adding
   `RegexMatchTimeoutException` to `_rejections` left the suite green. Fixed by asserting against a
   rejection that is both `whileMatching` and not `regex.error`, the two conditions under which the
   allow-list is the only thing left deciding. Proved by mutation: with the timeout added to
   `_rejections` the suite reports `failed: 1  succeeded: 8`.
3. **One symbol really was unaccounted for**: `match_many_CHARACTER_REV` (`:3987`) is absent from
   the line list in the S16 `match_many` row, which the new section pointed at as covering all
   twelve. One line added.
4. **The `try_match_*` row's breakdown clause was false for nine of the thirty-six** - the six
   `try_match_STRING*` cannot be answered by `TryMatchOne`, which tests one character;
   `try_match_SEARCH_ANCHOR` is not an `_IGN`/`_REV`/`STRING` member at all; and the two
   `_ANY_*_REV` are ported under their own names. The *accounting* survived - the Phase 7 deferral
   covers all 36 regardless - but the sentence explaining it did not. Rewritten.

The reviewer also confirmed, with reproductions, that the sixteen family counts sum to 293 and match
the script's buckets exactly, that `grep -c '^}$'` is 567, that every other family row's target
exists and covers its members, that the `locale_*` bucket is exactly twenty inside the cited range,
that `grep -c "| 100.0% |" docs/STATUS.md` is 15, and that the control arithmetic in DECISIONS and in
the table above now agree.
