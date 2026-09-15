---
slice: S52
phase: 6
title: Oracle hardening - a seed sweep tool, all Unicode planes, longer subjects, and timeout rows
delivers: []
---

# S52 - Oracle hardening

The one lesson Phase 5 paid for five times (S33, S34, S35, S40a, S43): scope hardening on SEEDS, not
on rows. Each of those found a real defect at a seed no earlier slice had used, on generators
already swept at higher row counts. A new seed costs about a minute.

## Scope

- **`tools/sweep-seeds.ps1`**: run every generator at N fresh seeds (default 20, 2000 rows each),
  drawn and recorded so a red seed is reproducible, consuming each wave and summarising per seed
  and per generator. Detached-friendly (writes progress to a log, resumable per seed) so the
  orchestrator can run it overnight. Add a weekly CI job on `oracle.yml` with a rotating seed.
- **All Unicode planes.** Every generator's subject and literal alphabets gain astral characters
  (SMP letters and digits, emoji with modifiers and ZWJ), unpaired surrogates where the .NET string
  allows and Python does not (recorded as `unsupported` on the Python side rather than dropped, so
  the asymmetry is visible), and the `\X` grapheme cases. Indices are UTF-16 here and codepoints
  there; the recorder's `_to_index_length` already converts, so the risk is in the port, not the
  harness.
- **Longer subjects**: a `long` variant of `literals`, `quantifiers`, `partial` and `fuzzy` with
  subjects of 1,000 to 20,000 characters, to reach the paths that only a long text takes
  (`search_start`-shaped scanning, repeat guards, fuzzy insert budgets far from the start).
- **Timeout rows**: patterns known to be slow on both engines under a small `timeout=`, comparing
  that both time out (upstream's `TimeoutError` versus this port's `RegexMatchTimeoutException`)
  rather than skipping them. Depends on S51's `timeout` comparison.
- **Lift the last `interactions` exclusions** if S46 and S47 left any, and re-run at the fourth
  seed.
- Every divergence found is judged to amendment 16; the slice does not end with an unjudged row.

## Verification

- Seed sweep run once here with its output committed to the closing notes: 20 seeds, every
  generator, and the verdict per seed. Default wave GREEN at three seeds, 6000 rows; `fuzzy` and
  `interactions` at 99991.

## Done when

- [ ] Sweep tool committed and run; CI job added; astral, long and timeout generators recorded.
- [ ] Every divergence judged, fixed or entered with a control; nothing unjudged.
- [ ] Ratchet GREEN, blind review (hunt: an astral row whose index is converted twice; a long-subject
      generator that never reaches the path it was written for), commit.

---

## Sitting 2 (2026-09-15) - CHECKPOINT, not closed

Sitting 1's handover made judging the eleven divergences sitting 2's first job. **Five of the
eleven are now judged, classified and pinned; the six `(*SKIP)` rows are not, and they are what
keeps the wave red.** Everything below is measured on the committed tree.

### The triage sitting 1 left was wrong on two rows, and measuring is what caught it

Every one of the eleven was re-triaged here with an ablation run - `(*SKIP)` to `(*PRUNE)`, the
verb deleted, POSIX cleared, the Turkic letters swapped out of the subject and out of the named
list - fed through `tools/run-oracle.ps1 -Rows` so the consumer says per variant whether the two
engines still disagree. Two of sitting 1's verdicts did not survive:

- **seed 4242 row 24256 is Turkic, not POSIX.** Its flags are `0x4002`, which carries no POSIX bit
  at all; what moves it is the dotless small i in the subject.
- **seed 20260915 row 34508 is Turkic, not `search-start-partial`.** Sitting 1 swapped the U+0130
  out of the SUBJECT and saw no change - correctly, because the U+0130 is in the PATTERN. The row's
  own recorded `searchOnlyPartial` is `false`, which is the field `search-start-partial` requires:
  upstream's own `match` reports the same zero-width partial its `search` did, so the prefilter is
  not what answered.

### What landed

**1. `pruneOutcome`, a fourth recorder control** (`tools/record-oracle.py`, `OracleWave.cs`):
upstream's answer to the same row with every `(*SKIP)` spelled `(*PRUNE)` - the same backtracking
pruned, neither slice bound moved (`upstream/src/_regex.c:14545` and `:14551`). Four entries in
`ExpectedDivergences.cs` already rest their judgement on this control and each quoted a probe run
by hand; it is now a per-row fact an entry can read. Measured not to disturb anything: recording
`--generator verbs --count 300 --seed 7` against HEAD's recorder and against this one gives 198
rows gaining only `pruneOutcome`, 0 other keyset differences and 0 existing fields changed.

**2. Two rows into `posix-fuzzy-contradicts-its-own-flagless-answer`**, seed 7 rows 24430 and
24916. 24430 is row 76983's mechanism through a `\L<name>` list with POSIX set as a flag rather
than written `(?p)`: upstream charges the first match's span one deletion where its own POSIX-free
engine spends none over the identical span. **24916 is a symptom the entry had not seen** - POSIX
moves neither a cost nor a span, it invents a MATCH, replacing twice under the flag and once
without it on a template that expands to nothing either way. New **ledger entry 23**; new gap test
`FuzzyPosixTests.Posix_does_not_add_a_match_the_flagless_engine_cannot_make`.

**3. One row into `turkic-default-folding-without-spans`**, seed 4242 row 24256, plus its control
in `tools/probes/upstream-turkic-without-spans.py` and its row in the probe's `.jsonl`.

**4. A new entry, `turkic-default-folding-from-the-pattern-side`**, for the two rows where the
Turkic letter is not in the subject at all: row 25482 has it as the first letter of a `\L<w1>` word
over a subject holding no Turkic letter whatsoever, and row 34508 has it as the pattern's leading
literal on an empty slice. **Its control is stronger than the one the two older Turkic entries
use, and that is the point.** "Take the U+0130 away" does not isolate a `T` row, because swapping
it for `h` also shortens the fold from two characters to one; this control swaps it for U+00DF,
U+FB00 and U+01F0 instead - all folding to more than one character, none carrying a `T` row - and
the two engines then agree cell for cell. Two probes,
`tools/probes/upstream-turkic-from-the-pattern-side.py` and its port half
`tools/probes/port-turkic-from-the-pattern-side.ps1`; two gap tests in `CaseFoldingTests`, each
running the whole grid as `[Arguments]`.

### What is left, with the evidence already in hand

The six `(*SKIP)` rows. `tools/probes/upstream-skip-carried-slice-doors.py` carries all six inline
with their controls and is re-runnable from the committed tree. On every one of the six, upstream's
own `(*PRUNE)` answer is THIS PORT'S answer. What three of them still owe is the other half of the
standard - upstream contradicting itself - and three already have it:

- **row 24018 (`match`, partial):** a `match` is ONE attempt, so there is no next attempt for
  `(*SKIP)` to move the start of and the two verbs must prune identically. They do not: upstream's
  `(*SKIP)` answer is its VERB-FREE answer character for character, so the pruning both verbs owe
  did not happen at all.
- **rows 25854 and 38151:** upstream's own scan taken one match at a time - each `search` a fresh
  attempt, so no bound a previous match's `(*SKIP)` moved is still moved - lands on this port's
  answer span for span, where its own continuous scan does not. That is `anchoredScan`'s argument
  (S34) computed for a forward `finditer` and a reversed overlapped one.
- **rows 24737, 24224 and 38101 are NOT settled.** 24737 is a partial `search`, so the control is
  the anchored `match(pos, endpos, partial=True)` that `partial-retry-carried-slice-forward` uses.
  On 24224 (a reversed `split`) the stepwise walk finds the same NUMBER of separators this port's
  split implies but not the same spans, and on 38101 (a reversed `subf`) it finds a match where
  this port replaces nothing; both need a control that reproduces the operation rather than
  approximating it. The probe's own docstring says which is which, so nobody reads the stepwise
  line as evidence about a row it does not cover.

**The default 300-row wave reaches four of the six** (rows 3618, 5851, 3824, 5801 at seeds 7 and
20260915), so this is not a 2000-row-only problem and it is the whole of the remaining red.

Also untouched, and still sitting 3's: the long-subject generators, the timeout rows, the 20-seed
sweep and the 6000-row three-seed gate.

### Review

One blind pass over the whole diff, dispatched inside the turn and read as a tool result.
**Findings raised: none - "No defects found."** The reviewer ran the build, the full suite
(6103/6103, ratchet GREEN), the tool tests (81/81) and the eleven-row oracle run, and it worked the
hunt list rather than only reading: it mutated `_turkicPatternSideOurs[0]` and
`_posixOvercostOurs[4]` and confirmed both rows drop back to DIVERGE (so the answer keying is
live), re-recorded all five new embedded `Example` rows and found them byte-identical today,
diffed a 300-row `verbs` recording against HEAD's recorder field by field (198 rows gaining only
`pruneOutcome`, 0 existing fields changed), scanned 9,819 `(*SKIP)` patterns across every wave on
disk for the verb inside a character class, a comment or `\Q...\E` (0 hits), and mutated all three
new gap tests to confirm each goes red. No second pass was needed: nothing was fixed, so no
unreviewed delta was created.

No control run this sitting mutates the engine, so there are no control figures to re-run; the
controls here are upstream-side ablations and every one of them is in a committed probe.
---

## Sitting 3 (2026-09-15) - CHECKPOINT, not closed

Sitting 2 left six `(*SKIP)` rows unjudged and said they were the whole of the remaining red.
**All six are now judged, classified and pinned, and the three-seed 2000-row wave is GREEN.** The
slice file stays pending: the long-subject generators, the timeout rows, the CI job, the 20-seed
sweep run and the 6000-row gate are all still untouched. Everything below is measured on the
commit-ready tree.

### The six rows, and what judges each

`(*PRUNE)` answers this port's answer on all six, which sitting 2 had already recorded as
`pruneOutcome`. That is not a judgement on its own - `(*SKIP)` is documented to do something
`(*PRUNE)` does not, so the two differing is the ordinary case on a scan. What each row needed was
upstream contradicting itself, and the six turned out to be three families, not one.

**Family 1, rows 24018 and 24737 -> `partial-retry-carried-slice-forward`.** The tell is new and it
needs no model of the two-pass restore: upstream's partial call answers a match that is NOT PARTIAL,
where its own non-partial call to the same compiled pattern over the same subject answers `None`.
`partial=True` is documented to ALSO allow a partial match; it cannot conjure a complete one the
same engine denies without it. Row 24018 is a `match`, which is ONE attempt - so there is no next
attempt for `(*SKIP)` to move the start of, the two verbs must prune identically within it, and that
closes the "a `(*SKIP)` is allowed to differ on a scan" objection for the whole family. With no
partial asked for, the `(*SKIP)` and `(*PRUNE)` lines agree (both `None`) and only the verb-free line
matches; what the partial pass gets back is the verb-free answer, character for character.

**Family 2, rows 24224 (a reversed `split`) and 38101 (a reversed `subf`) -> a NEW entry,
`end-of-line-reads-a-skip-moved-slice`.** Upstream has eight predicates that ask whether a position
is at an edge of the text. Seven read a TEXT bound; `try_match_END_OF_LINE` (`:7110`) alone reads
`slice_end`, the field `RE_OP_SKIP` writes under `(?r)` (`:14553`). The seven include `$`'s OWN
Unicode twin, `try_match_END_OF_LINE_U` (`:7117`, through `at_line_end` at `:922`/`:1966`), so
upstream's `$` disagrees with itself in one file. Both rows report a match ending where upstream's
own anchored `$` is false; spelling `$` out as its definition gives this port's answer on both.
**The two rows are different distances apart**, which is why they are one entry and not filed with
the scan families: on 24224 a single `search` over the whole subject agrees with the `(*PRUNE)` line
and only a later match of the scan diverges, so the bound crossed BETWEEN matches; on 38101 a single
`search(subject, 0, 10)` gives (0, 5) as drawn and `None` under `(*PRUNE)`, so a FAILED attempt's
moved bound was read by a later attempt inside one call.

**Family 3, rows 25854 (a forward non-overlapped `finditer`) and 38151 (a reversed overlapped one)
-> a NEW entry, `skip-carried-slice-on-a-scan-with-no-walk`.** The carried-slice defect the four
`overlapped-skip-*` entries judge, on two rows none of them can key on because the recorder writes no
`anchoredScan` for either - and for two different reasons, both already argued in
`tools/record-oracle.py`. **Neither refusal was widened**: the probe computes the walk for these two
rows only, and says why it is sound on each (row 25854 draws no zero-width match, and `must_advance`
is set only after one, `:20932`; row 38151's lookahead sits where both questions read the subject
identically).

### Two things measured here that changed the answer

**A stepwise walk is NOT a control for a row whose pattern reads the end of the subject.** On both
family-2 rows, `search(subject, 0, <the phantom end>)` reproduces the phantom span on the VERB-FREE
pattern too, because passing an `endpos` sets `slice_end` legitimately - the very bound the defect
leaves stale. A walk reproduces the bug instead of testing it, and would have said upstream was
right. The recorder's `_reads_the_end_of_the_subject` refusal already encodes exactly this;
sitting 2's notes read it as a gap and it is not one.

**`(?w)` is a control as well as a fact, and it runs on ONE of the two rows for a narrower reason
than it first looks.** `(?w)` compiles `$` to `END_OF_LINE_U` (`regex/_regex_core.py:506-510`), the
twin that reads `text_end`. But it is not a clean swap of one bound for another: it also changes
which positions are line ends, on BOTH rows and in both directions - 24224 `$` at [3, 10] against
`(?w)$` at [2, 8, 10]; 38101 `$` at [2, 10] against `(?w)$` at [1, 7, 10]. So "it moves the line
ends" does not separate them. What does is whether it moves THE PHANTOM POSITION: 8 becomes a `(?w)`
line end on 24224, so a `(?w)` run there could not tell a bound that stopped being read from a line
end that started existing; 5 is a line end under neither spelling on 38101, so `(?w)` removing the
match says the bound was the only thing holding it up. The probe derives that condition per row
rather than listing it. **The first blind pass caught this stated the wrong way round**, and a
hand-written table in the probe encoding the wrong reason went with it.

### Numbers

- Suite **6109 / 6109 / 0 skipped**; ratchet **GREEN**; baseline **5995 -> 6001**. Tool tests 81/81.
- The three-seed 2000-row wave, 126,000 rows, **diverge 0 at every seed**:
  seed 7 `agree 41939 expected 31 timeout 2 resource 28 diverge 0`;
  seed 4242 `agree 41948 expected 27 timeout 1 resource 24 diverge 0`;
  seed 20260915 `agree 41949 expected 26 timeout 1 resource 24 diverge 0`.
  `pwsh -File tools/run-oracle.ps1 -Count 2000`.
- Six new gap tests, each mutated once and watched go red (`failed: 6` of 6109).

### The negative control, and how to re-run it

**No control this sitting mutates the engine** - every control here is an upstream-side ablation and
all of them live in `tools/probes/upstream-skip-carried-slice-doors.py`. What this sitting can and
did control is the KEYING, because all three touched entries are keyed on a row and on this port's
exact answer, and a judged answer that is wrong classifies nothing while a predicate that is too
wide classifies everything.

> Control A, `entry-keying`: in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`, change one
> character in one judged answer of each of the three touched entries -
> `_partialRetryForwardOurs[1]` `[s:0,1]` to `[s:0,2]`;
> `_endOfLineReadsMovedSliceOurs[0]` `split 3` to `split 4`;
> `_skipCarriedSliceNoWalkOurs[1]` `0:(0,5)[(0,5)]` to `0:(0,6)[(0,6)]`.
> Rows: the six themselves, written as a `--rows` file from the probe's own `ROWS` table, and run
> with `pwsh -File tools/run-oracle.ps1 -Rows <file>`. No seed - these are explicit rows.
> Result: **6 expected / 0 diverge** unbroken, **3 expected / 3 diverge** broken. The three that
> flip are exactly the three whose answer was changed.

The second seed this control asks for does not apply: it runs on six explicit rows rather than on a
generator, so there is no seed to vary. What stands in for it is the three-seed wave above, which
draws these families independently - the default 300-row wave at seed 20260915 reaches rows 3824 and
5801 and classifies both as `end-of-line-reads-a-skip-moved-slice`.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results, and a third round of
repair against execution feedback.**

**Pass one, over the whole diff: seven findings raised, seven reproduced, seven fixed.** None was a
defect in the port; all seven were defects in the evidence, which is what this slice's output IS.
(1) and (2) `upstream/src/_regex.c` citations: `:14545`/`:14551` for the two `RE_OP_SKIP` writes and
`:20903` for the scanner's overlapped step are wrong against the 2026.9.10 pin - those lines are a
`TRACE` call, a blank line and a comment terminator, and the correct ones are `:14553`, `:14555` and
`:20927-20928`. I had copied the stale pair out of older text in this repo. (3) A quoted Python repr
in LEDGER.md carried a `partial=False,` field `regex.Match.__repr__` never emits. (4) A gap-test
control asked about UTF-16 index 5 where upstream's span is in CODEPOINTS and the subject is astral -
index 5 is a low surrogate, and the position meant is 6. (5) The probe's "a third answer on four of
the six" is one of the six. (6) LEDGER claimed every control was re-runnable from one script while
two were not in it. (7) "five edge predicates, four read a text bound" - there are eight, and
**re-reading the source to fix the count made the finding stronger**: the three `_U` variants read
`text_end`, so `$`'s own twin is on the other side of the split.

**Pass two, a first pass over the repair delta** (the corrected citations, the eight-predicate table,
the new `(?w)` control and its two assertions, the rewritten probe block): **four findings raised,
four reproduced, four fixed.** The sharpest is the one this sitting's notes now lead with: the
reason I gave for `(?w)` isolating on one row and not the other was FALSE - `(?w)` moves the line
ends on both, and only the phantom position decides. Also a latent `IndexError` in the probe if a
family-2 row ever had no phantom end, a `:20931` that cites a comment rather than the assignment,
and a citation tally I had not actually counted.

**And one defect neither pass could see, caught by running the control.** Two of my own scratch
helper scripts rewrote three `.cs` files with Python's `write_text`, which on Windows translates
`\n` to `\r\n`; `.editorconfig` says `end_of_line = lf`, and `dotnet build
tests/FuzzyRegex.OracleTests` then failed with 14 `IDE0055` errors **in code I never touched**. The
parity ratchet did not catch it because it builds `tests/FuzzyRegex.Tests` alone, and `git` hides it
on commit because the repo normalises. Fixed by converting the three files - and `DECISIONS.md` and
`LEDGER.md`, same cause - back to LF. **The lesson for the next sitting: never write a tracked file
from a Python helper on Windows without `newline=""`, and build the ORACLE tests too before
believing a green ratchet.**

### Owed, found here

**A repo-wide citation reconciliation**, and the independent verifier found it is wider and less
uniform than this sitting first wrote down. Four citations are stale against the 2026.9.10 pin:
`:14545` and `:14551` for the two `RE_OP_SKIP` writes (right: `:14553` reversed, `:14555`
forwards), `:20903` for the scanner's overlapped step (right: `:20927-20928`), and `:18160` for
`do_match`'s two-pass block, which is a blank line - the `text_pos` save it describes is
`:18159`. **A sweep cannot just follow the majority**: the correct spelling wins for the two
`RE_OP_SKIP` writes (`:14553` 45 uses, `:14555` 33, against 19 for `:14551`) and LOSES for the
scanner step, where `:20903` has 22 uses against `:20927`'s 7. The stale spellings reach source
comments, probes, done slice notes, PORTMAP and DECISIONS. Only the text this sitting wrote is
correct, and two of the four were corrected inside the entry this sitting edited rather than
left to contradict its neighbours. This is a maintenance commit of its own, not a slice job.

---

## Sitting 4 (2026-09-15) - CHECKPOINT, and a SHORT one

A 65-minute sitting under a hard laptop-off deadline, scoped by the orchestrator to one item of the
remaining list finished properly rather than several started. The item taken is **the long-subject
generators**. The timeout rows, the `oracle.yml` CI job, the 20-seed sweep run and the 6000-row
three-seed gate are untouched and remain sitting 5's.

**No blind review ran this sitting**, and no independent verifier. There was not the time for
either, and the deadline was the whole reason. Both are OWED before this slice closes, and the
delta they have to cover is exactly this sitting's diff: `_generate_long` and its two constants in
`tools/record-oracle.py`, and `tools/probes/generator-long-subject-reach.py`. Nothing else was
touched - **no `.cs` file changed at all**, which is why the ratchet result below is the same
suite sitting 3 left green.

### What landed

**Four long-subject generators - `literals-long`, `quantifiers-long`, `partial-long`,
`fuzzy-long`** - as a WRAPPER over the base generator rather than as four new grammars. The
pattern a long row carries is drawn by the base generator unchanged, so a long row and a short row
differ in the one variable under test; four bespoke long grammars would have made every divergence
a question about which grammar found it. Two rules, both about not destroying the base row's
question:

- **The filler goes on the side the pattern does not run off.** Forward patterns are padded on the
  LEFT, so the scan has text to walk and the subject's own right-hand edge - where a `partial` row
  runs out of text - is still the edge. A `(?r)` pattern reads right to left and runs out at the
  LEFT end, so it is padded on the RIGHT. Pad the wrong side and `partial-long` is just `literals`
  with a long prefix.
- **The operation is forced to `search`.** Every path the variant exists to reach is a scan, and
  `match`/`fullmatch` are anchored - on a padded subject they answer at 0 or not at all, and
  `fullmatch` cannot match. It also bounds the cost: `fuzzy` cycles ALL_OPERATIONS, and one
  `finditer`, `split` or `sub` over 20,000 characters can spend the whole 10s row timeout. The
  row's `partial` flag is left exactly as the base generator set it, because a partial `search`
  over a long text IS the interesting row.

Subjects are 1,000 to 20,000 characters, as the scope asks; measured medians are about 10,000.

### The measurement that changed the design, made BEFORE any review asked for it

The slice's own review hunt names "a long-subject generator that never reaches the path it was
written for", so the reach was measured rather than assumed, by
**`tools/probes/generator-long-subject-reach.py`** (committed, re-runnable). The first run said
`quantifiers-long` **did not reach its path**: 9 of 148 matches began 100+ characters into the
scan, median distance 0, median match length 1.

The cause is not the filler alphabet, and no spelling of it would have helped: a nullable
quantifier (`a*`, `.{0,2}`) answers a zero-width match at offset 0 without looking at the text.
**The metric was wrong for that generator.** The other three want the match to begin far from
where the scan started; a repeat guard is reached by ITERATIONS, and a repeat cannot iterate over
text it cannot consume. So `quantifiers-long` alone draws its filler from the base alphabet
(`LONG_REPEAT_FILLER_GENERATORS`), and the probe reports distance AND length so each generator is
judged on the column its path lives in.

`python tools/probes/generator-long-subject-reach.py --count 200 --seed 7`, on the committed tree:

```
generator           rows  match  walked    dist    len  long  subject
literals-long        200    147     103    6408      1     0    10902
quantifiers-long     200    155       5       0      1    32    11064
partial-long         200    119      83    5142      0     7    10050
fuzzy-long           200    148     115    8516      4     0    10304
```

`walked` counts matches beginning 100+ characters into the scan, `long` counts matches 100+
characters long. Three of the four reach a distant start on about 70% of their matches;
`quantifiers-long` reaches a 100+ iteration repeat on 32 of 155, **up from 0** before the filler
change. Its median length stays 1 because the base grammar draws many `?` and `{0,2}` quantifiers,
and widening that is a change to the base grammar rather than to this wrapper - deliberately not
done here.

### The divergences, and why they are NOT judged

`pwsh -File tools/run-oracle.ps1 -Generator literals-long,quantifiers-long,partial-long,fuzzy-long
-Count 150` is **RED**: `agree 597 unsupported 0 expected 0 timeout 1 resource 0 diverge 2 of 600`
at seed 20260915. Both diverging rows are `partial-long`, both reversed, and both carry a
construct from `_END_SENSITIVE_ITEMS`:

- row 305, `search`, flags `0x102`, V0: `(?r)^(\p{Lu}+?)+?(.)??\K`
- row 307, `search`, flags `0x4102`, V0: `(?r)^(?P<g1>[^a]*?)(.*?)*\M`

**Sitting 5 must judge both to amendment 16 and must not assume they are sitting 3's family.**
`\K` and `\M` reading a stale bound is the obvious hypothesis given
`end-of-line-reads-a-skip-moved-slice`, and it is a hypothesis: there is no `(*SKIP)` in either
pattern, so whatever moves the bound here is something else. Sitting 3 also proved that a stepwise
walk is not a control for a row whose pattern reads the end of the subject, and both of these do -
so the control has to come from somewhere other than an `endpos`.

**The four long generators are therefore NOT in `run-oracle.ps1`'s default `-Generator` list**, and
must not be added until both rows are judged. Adding a red generator to the default wave would turn
the gate every other slice depends on red for a reason unrelated to that slice.

### Numbers

- Ratchet **GREEN**; suite unchanged from sitting 3 (no `.cs` file was touched this sitting).
- `python tools/record-oracle.py --generator literals-long,quantifiers-long,partial-long,fuzzy-long
  --count 200 --seed 7` records 800 rows in about a minute: 569 match, 229 nomatch, 1 error,
  1 timeout, 287 with an astral subject. So the 10s row timeout bounds these rows adequately and a
  long wave is not the forty-minute risk S40's hanging row was.

### The negative control

**None was run, and that is a gap, not a "not applicable".** No control this sitting mutates the
engine - nothing in the engine changed - but the generator itself is the new artefact and the
question a control would answer is whether these four generators can detect a fault at all. Sitting
5 should run one: the natural site is the wrapper's own padding side, changing
`row["subject"] + filler if reverse else filler + row["subject"]` to pad the same side regardless,
which should collapse `partial-long`'s `walked` column and is the cheapest proof that the
side rule is load-bearing.

---

## Sitting 5 (2026-09-15) - CHECKPOINT, and a SHORT one

A 45-minute sitting under a hard laptop-off deadline, scoped by the orchestrator to one item.
The item taken is **the two unjudged `partial-long` divergences**, because nothing else in the
remaining list can proceed past them: the four long generators cannot join `run-oracle.ps1`'s
default list while the wave they produce is red.

**They are judged, and neither is a divergence at all. Both are a Debug build meeting a 10-second
row timeout.** No `.cs` file was touched, no engine behaviour changed, and the suite is exactly
sitting 3's.

### Sitting 4's hypothesis was wrong, and the report said so on the line nobody read

Sitting 4 recorded these as two rows "both end-reading", hypothesised sitting 3's
`end-of-line-reads-a-skip-moved-slice`, and told sitting 5 to judge them to amendment 16. The
hypothesis cannot have been right, and the evidence was already in the report it was written from:

```
DIVERGE row 305 (partial-long) search flags=0x102 version=V0
  upstream match 0:(0,1)[(0,1)] 1:(0,1)[(0,1)] 2:unset last=1/-
  port     error while matching RegexMatchTimeoutException: ...
```

**The port does not answer differently. It does not answer.** `\K` and `\M` reading a stale bound
would have produced a wrong span; what is actually there is this port spending more than
`OracleComparer.RowTimeout` (10s, `OracleComparer.cs:22`). There was no correctness question to
judge, so amendment 16's four outcomes never applied - the row is not "the port is wrong", "upstream
is wrong", "both are wrong" or "not conclusive", because the two engines were never asked the same
question to the end.

### What the two engines actually do, measured

Two probes, both committed and re-runnable: `tools/probes/port-long-subject-cost.ps1` and
`tools/probes/upstream-long-subject-cost.py`. They take the same rows of the same recorded wave and
time each engine at truncated subject lengths, so the shape of the cost is visible instead of one
pass/fail bit. Reproduce the wave first:

```
python tools/record-oracle.py --generator literals-long,quantifiers-long,partial-long,fuzzy-long --count 150 --seed 20260915
```

Row 307, `(?r)^(?P<g1>[^a]*?)(.*?)*\M`, flags `0x4102`, milliseconds:

| n | 800 | 1600 | 3200 | 6400 | 12800 | 13391 (full) |
|---|---:|---:|---:|---:|---:|---:|
| upstream | 4 | 15 | 53 | 217 | 861 | 947 |
| port, Debug | 254 | 1,007 | 4,029 | 16,124 | 64,403 | - |
| port, Release | 32 | 120 | 369 | 1,304 | 5,214 | 5,757 |

Row 305, `(?r)^(\p{Lu}+?)+?(.)??\K`, flags `0x102`, partial: upstream 1,170 ms at its full 19,896;
port 15,129 ms in Debug and 1,820 ms in Release. **Every cell of both rows answers what upstream
answers** - `(0,13389)` and `(0,1)` - once the engine is allowed to finish.

Two things fall out, and the second is the one that matters:

- **The engines are in the same complexity class.** Both are quadratic on row 307: each doubling of
  `n` quadruples the time, upstream's 4/15/53/217/861 as plainly as the port's. So there is no
  algorithmic defect here to find, and the "nested quantifier" reading the exception text invites is
  wrong about the difference between the two engines even though it is right about the pattern.
- **The port is a constant factor slower, and in Debug that factor crosses the row timeout.**
  Roughly 5-8x in Release and up to 75x in Debug.

### The first measurement was wrong, and the flags are why

The first pass of the port probe built the pattern with `FuzzyRegexOptions.None` and found both rows
answering in **milliseconds at full length** - which would have said the wave was lying. It was the
probe that was lying: `OracleComparer` compiles with `(FuzzyRegexOptions)row.Flags`
(`OracleComparer.cs:143`) and `0x102` is `IgnoreCase, Version1`. Without `IgnoreCase`, `\p{Lu}` and
`[^a]` walk a fraction of the text and the cost never appears. The committed probe passes the row's
own flags and its header says why, because this is a trap the next person will fall into too.

### The judgement, and the proof

`run-oracle.ps1` defaults to `-Configuration Debug` (`:210`). Consuming **the identical rows**,
changing nothing but the configuration:

```
pwsh -File tools/run-oracle.ps1 -SkipRecord -Configuration Release
agree 599  unsupported 0  expected 0  timeout 1  resource 0  diverge 0  of 600 rows
```

against Debug's `agree 597 ... diverge 2 of 600`. `-SkipRecord` is what makes this a control rather
than a second sample: the rows are the ones already on disk, so the seed, the subjects and upstream's
recorded answers are byte-identical and the build is the only variable.

**So no entry goes in `ExpectedDivergences.cs` and no test pins anything.** Keying an entry on a
row the port answers correctly would be wrong, and keying one on a wall-clock measurement would be
a flaky test in a file whose whole value is that `Every_expected_divergence_still_diverges` is
strict. Nothing is owed upstream either: upstream is not wrong about anything here.

### What this says about the long generators, and what sitting 6 has to decide

**The four long generators still do NOT join `run-oracle.ps1`'s default `-Generator` list**, and the
reason has changed from "two rows are unjudged" to a sharper one: **their verdict depends on the
build configuration, and the default wave runs Debug.** Adding them would red the gate every other
slice depends on, for a reason that is not about that slice and not about correctness.

That is a genuine finding about the harness rather than a fact about these two rows. Every
generator before these produced short subjects, where a 5-8x constant factor is invisible against a
10-second budget; at 20,000 characters it is not. The options for sitting 6, none of them taken here
because the sitting was 45 minutes:

- run the long generators in Release only, and say so where the default list is documented;
- give the long generators their own longer row timeout, which means `RowTimeout` stops being one
  constant;
- default `run-oracle.ps1` to Release, which is the smallest change and the one the sweep tool
  already recommends for its own runs ("Release for an overnight sweep - the consumer is the slow
  half") - but it changes every slice's gate, so it is not a 45-minute decision.

**Whichever is chosen, the Debug/Release sensitivity is now a property of the wave that has to be
written down somewhere a slice will read**, or the next long-subject red gets triaged as a
correctness bug exactly as this one was.

### Numbers

- Ratchet **GREEN**; suite unchanged from sitting 3 (no `.cs` file was touched this sitting).
- Long wave, seed 20260915, 150 rows a generator: Debug `diverge 2 of 600`,
  Release `diverge 0 of 600`, same rows via `-SkipRecord`.
- Both probes re-run from the committed tree; `upstream-long-subject-cost.py --rows 307` reproduces
  the table above to within a millisecond or two.

### Review

**No blind review ran this sitting, and no independent verifier.** There was not the time for
either, and the deadline was the whole reason. Both remain OWED before this slice closes, and the
delta they have to cover is now sitting 4's **and** sitting 5's: `_generate_long` and its two
constants in `tools/record-oracle.py`, `tools/probes/generator-long-subject-reach.py`, and this
sitting's `tools/probes/port-long-subject-cost.ps1` and
`tools/probes/upstream-long-subject-cost.py`. The verifier's job on this sitting is the
`-SkipRecord` Release run and the two cost tables, all three of which are one command each.

### The negative control

**Still not run - sitting 4's gap is carried, not closed.** The padding-side control sitting 4
specified is still the right one and still unrun. Note for whoever runs it: it must be run in
**Release**, or a collapsed `walked` column cannot be told from a row that merely timed out, which
is the same confusion this sitting spent its 45 minutes undoing.
