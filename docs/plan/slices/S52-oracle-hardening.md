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
**[CORRECTED IN SITTING 7 - the median match length was 0, not 1, and 28 of 148 matches were
already 100+ characters LONG. See sitting 7's notes; the conclusion survives, the margin does
not.]**

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
change. **[CORRECTED IN SITTING 7: up from 28, not from 0. The filler change is worth +4 on this
column and -4 on `walked`.]** Its median length stays 1 because the base grammar draws many `?` and `{0,2}` quantifiers,
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

`run-oracle.ps1` defaults to `-Configuration Debug` (`:210`). **[Both halves of that are now stale:
sitting 6 made the default Release, and the parameter is at `:246`.]** Consuming **the identical rows**,
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

---

## Sitting 6 (2026-09-15) - CHECKPOINT, and a SHORT one

A 47-minute sitting, scoped by the orchestrator to one item: **the Debug/Release decision sitting 5
left open**, which STATE.md named as sitting 6's first job. No `.cs` file was touched, so the suite
is again exactly sitting 3's 6,109 / 6,109 / 0 skipped, baseline 6001, ratchet GREEN.

### The decision: `run-oracle.ps1` and `sweep-seeds.ps1` now default to Release

Of sitting 5's three options this is the third, and what makes it the right one is that **the two
configurations differ in speed alone**. Nothing under `src/` is conditioned on `DEBUG` - no
`#if DEBUG`, no `[Conditional("DEBUG")]`, no `Debug.Assert` - and no `.csproj`, `Directory.Build.props`
or `.editorconfig` varies `DefineConstants`, overflow checking, nullability or analyzers by
configuration. So a Release wave asks the same questions, and a Debug one merely answers fewer of
them: `OracleComparer.RowTimeout` is 10 seconds of wall clock
(`tests/FuzzyRegex.OracleTests/OracleComparer.cs:22`), so the configuration decides which rows are
compared at all. Sitting 5 measured the factor at 5-8x in Release and up to 75x in Debug.

The other two options were rejected on the same evidence: a Release-only rule for the long
generators leaves every other generator's gate measuring the build, and a second row timeout makes
`RowTimeout` stop being one constant to buy the same thing.

CI already passed `-Configuration Release` explicitly at all three call sites
(`ci.yml:69`, `oracle.yml:129`, `:193`), so the default now agrees with what CI has always run and
only a local run changes.

### And the measurement overturned the other half of the decision

Sitting 5's open question was really two: which configuration, and whether the four long generators
then join the default `-Generator` list. **They do not, and this is measured rather than deferred.**
Added to the default and run at the three default seeds in Release,
`pwsh -File tools/run-oracle.ps1` is **RED at all three**:

```
seed 7         agree 7479  unsupported 0  expected 8  timeout 4  resource 4  diverge 5  of 7500 rows
seed 4242      agree 7482  unsupported 0  expected 2  timeout 7  resource 5  diverge 4  of 7500 rows
seed 20260915  agree 7483  unsupported 0  expected 6  timeout 4  resource 3  diverge 4  of 7500 rows
```

All 13 diverging rows are in `quantifiers-long` (8) or `partial-long` (5); **the 21 short generators
contributed no divergence at any seed**, which is the independent evidence that defaulting to Release
reds nothing that was green. **Eleven of the 13 are this port exceeding the row timeout in RELEASE**
- `port error while matching RegexMatchTimeoutException`, against an upstream that answers - so
Release moves the threshold without clearing it, and sitting 5's "Release fixes the long wave" held
only for the two rows it measured. The long generators are back off the default list, and
`run-oracle.ps1`'s `.PARAMETER Generator` now records why in the file a slice actually reads.

### The two rows that are NOT timeouts, and are sitting 7's

Both `partial-long`, both a partial `search`, both carrying a verb, and on both upstream reports a
partial spanning almost the whole subject where this port answers a short match at the far end:

- seed 7, row 6997, flags `0x0`, V0:
  `(?r)(?:[a-f](*PRUNE)\d|[[:digit:]])(?(?<![[:digit:]])[abz])(?:\p{Nd}(*SKIP)\s|\p{L})`
  upstream `0:(0,3365)` partial; port `0:(0,2)` partial.
- seed 20260915, row 7094, flags `0x8`, V0:
  `(?:[\p{L}\p{N}](*SKIP)\p{Nd}|\p{Ll})(\S)*?(?P<g2>\S?)(?:(?(2)(?=(?P>g2))\p{Nd}|.))`
  upstream `0:(1,18763)` partial; port `0:(18762,2)` partial.

They are UNJUDGED. Reproduce with
`pwsh -File tools/run-oracle.ps1 -Generator literals-long,quantifiers-long,partial-long,fuzzy-long -Count 300 -Seeds 7,20260915`.
Do not assume they are one of the existing partial families: the reversed `(*PRUNE)`/`(*SKIP)` shape
invites `partial-retry-carried-slice-forward`, and the second row is forward, so that is a
hypothesis and not a classification.

### Numbers

- Ratchet **GREEN**, 6109 / 6109 / 0 skipped, baseline 6001. No `.cs` file touched.
- Default wave, Release, three seeds, 300 rows a generator: the three lines above.
- Diff is `tools/run-oracle.ps1` (default, and two help paragraphs) and `tools/sweep-seeds.ps1`
  (default and its help paragraph).

### Review

**One blind pass, dispatched inside the turn and read as a tool result, over the whole diff.
Findings raised: two. Reproduced: two. Fixed: two.** (1) The help text credited "7,479 / 7,482 /
7,483 agreeing rows" to the 21 short generators; those totals are the whole 25-generator wave, and
the 21-generator wave is 6,300 rows - the reviewer proved it from `report-20260914.txt`. The claim
is now stated as the 21 contributing no divergence, which is what the reports actually show.
(2) `sweep-seeds.ps1` splats `Configuration` on every call, so `run-oracle.ps1`'s new default could
never reach a sweep and the 2,000-row sweep would have kept running Debug - the exact state this
sitting's own text calls wrong. Its default is Release too now, and its help says the two have to
agree by hand. The reviewer also checked and cleared the load-bearing claim (no `DEBUG`
conditioning anywhere in `src/` or the build files), the three CI call sites, and that the consumer
honours `-Configuration` (`:316`).

**No second pass ran over the two fixes**, and that is a deadline, not a judgement: the sweep-seeds
default is the same one-word change the first pass approved in its sibling, and the other fix is
the reviewer's own correction of prose. It is small and it is unreviewed.

### The negative control

**Still not run** - sitting 4's padding-side gap is carried for a third sitting. Nothing this
sitting changed is engine behaviour, and the control it owes belongs to the generator, not to the
default list. It must be run in **Release**.

---

## Sitting 7 (2026-09-15) - CHECKPOINT, not closed

STATE.md made sitting 7's first job the two unjudged `partial-long` rows, and named the blind review
and independent verifier owed for sittings 4, 5 and 6. Both are done. What is NOT done, and is now
the largest single item left in this slice, is the **6000-row three-seed gate, run here for the first
time and RED at all three seeds with 29 diverging rows**.

### The two rows are judged: both are `search-start-partial`, and the port is right

They are the same family S37 found and S43 added to, measured with that family's own controls in its
own probe. On each row, upstream's `search(partial=True)` covers the whole searched region, upstream's
own `match` over that very span answers **None**, and every other way of asking gives THIS PORT'S
answer:

```
== seed 7 row 6997  '(?r)(?:[a-f](*PRUNE)\d|[[:digit:]])(?(?<![[:digit:]])[abz])(?:\p{Nd}(*SKIP)\s|\p{L})' on '𝔘😀\n' flags 0x0
   search(partial=True)                ((0, 3), 'partial')      <- the whole region
   match over that span                None                     <- upstream denies its own answer
   match(endpos=1, partial=True)       ((0, 1), 'partial')      <- ours
   verb deleted         search(partial)  ((0, 1), 'partial')    <- ours
   verb -> (*PRUNE)     search(partial)  ((0, 1), 'partial')    <- ours

== seed 20260915 row 7094  '(?:[\p{L}\p{N}](*SKIP)\p{Nd}|\p{Ll})(\S)*?(?P<g2>\S?)(?:(?(2)(?=(?P>g2))\p{Nd}|.))' on '𐐀🏻𐐀' flags 0x8
   search(partial=True)                ((0, 3), 'partial')      <- the whole region
   match over that span                None
   match(pos=2, partial=True)          ((2, 3), 'partial')      <- ours
   verb deleted         search(partial)  ((2, 3), 'partial')    <- ours
   verb -> (*PRUNE)     search(partial)  ((2, 3), 'partial')    <- ours
```

A reversed match anchors at the END, so row 6997's sweep varies `endpos` where row 7094's varies
`pos`. Both are now rows 6 and 7 of `_searchStartElsewhereRows` in `ExpectedDivergences.cs`, both are
cases in `tools/probes/upstream-search-start-whole-region-partial.py`, and both have a permanent gap
test in `Gaps/Engine/PartialMatchingTests.cs`. Sitting 6's warning not to assume they were
`partial-retry-carried-slice-forward` was right - they are not that family.

**They are the only two rows in the arm on which every control returns this port's answer exactly,
span AND partial flag.** On rows 1, 2 and 4 the verb-free spelling answers a COMPLETE match instead;
on row 3 the anchor sweep never lands on this port's answer at all. (A first draft of that sentence
said rows 3 and 5 both rested on the verb evidence alone. The blind review killed it by running row
5's sweep, which gives its judged answer at endpos 1 - as the file's own S43 paragraph already said.)

### The length was NOT what found them, and that is the finding about the generator

Both rows were drawn on subjects of **3,363 and 18,759 characters** and both **delta-debug down to
THREE codepoints** with the whole signature intact - `𝔘😀\n` and `𐐀🏻𐐀`, every character astral or a
line break. So what the `partial-long` wrapper contributed is its **astral alphabet over these
pattern shapes, not its length**; the short `partial` generator could in principle have drawn either
and never did. That is worth carrying: the long generators are earning their keep as an alphabet
widening at least as much as a length widening.

### The 6000-row three-seed gate: RED at all three seeds, 29 rows, all unjudged

Run for the first time in this slice, on the commit-ready tree, in Release:

```
pwsh -File tools/run-oracle.ps1 -Count 6000          # 8m29s here, 10m35s under concurrent load
seed 7          agree 125828  expected 85  timeout 2  resource 71  diverge 14  of 126000
seed 4242       agree 125852  expected 74  timeout 1  resource 68  diverge  5  of 126000
seed 20260915   agree 125826  expected 79  timeout 1  resource 84  diverge 10  of 126000
```

**The default wave at 300 rows a generator is GREEN at the same three seeds** (`agree 6286/6293/6293,
diverge 0 of 6300`), so this is the S40a/S43 lesson again: the row count finds what the seed count
does not, and neither substitutes for the other.

The 29 are NOT transcribed here, because a transcription rots and this one can be regenerated. Re-run
the gate and then `python tools/probes/gate-divergence-triage.py`, which reads the gitignored
`TestResults/oracle/report-<seed>.txt` files the run leaves behind and prints row, generator,
operation, flags, shape tags and both engines' answers. Measured shape of the 29:

| by generator | | by shape | |
|---|---:|---|---:|
| `partial` | 11 | carries a `(*SKIP)` | 21 |
| `interactions` | 10 | reversed `(?r)` | 16 |
| `partial-sliced` | 4 | fuzzy section | 9 |
| `verbs` | 2 | upstream ERRORED | 1 |
| `conditionals` | 1 | | |
| `fuzzy` | 1 | | |

**One row is not a wrong answer but an upstream crash**, and it is the one to look at first: seed 7
row 118133, `verbs` `subf`, pattern
`[A-Z]{1,3}(?<![a\d](*SKIP))[\w\s]\d*+(?=(*PRUNE))[^a][^a]*(?!(*SKIP)ı)\S` over
`'Aİﬀı\r\nS'` with template `{0}{0[-2]}` - **upstream raises `IndexError: list index
out of range`** where this port answers `sub 0`. S40a judged a sibling of this (`{1[2]}` naming a capture a group never made) as
upstream's legitimate error rather than a divergence, so check that reasoning before assuming a bug.

### The negative control, finally run - and sitting 4's prediction was backwards

Carried unrun for three sittings. It is now a committed probe rather than a scratch script, because
a control nobody can re-run is not evidence:

> Control A, `padding-side`: in `tools/record-oracle.py`, `_generate_long`, replace
> ```
>         reverse = "(?r" in row["pattern"] or bool(row["flags"] & REVERSE)
>         row["subject"] = row["subject"] + filler if reverse else filler + row["subject"]
> ```
> with `        row["subject"] = filler + row["subject"]` so every row is padded on the left
> regardless of direction. Generators `partial-long,fuzzy-long`, 200 rows each, REVERSED rows only.
> Run it with `python tools/probes/long-subject-padding-side-control.py <seed>`, which applies the
> mutation, records both ways and restores the file in a `finally`.
> Result, **seed 7**: `fuzzy-long` walked **21 -> 1**, spans starting at text 0 **17 -> 1**, spans
> ending at the text end **4 -> 20**; `partial-long` walked **27 -> 24**, spans starting at text 0
> **29 -> 26**.
> Re-run at **seed 31337**: `fuzzy-long` walked **22 -> 3**, spans starting at text 0 **16 -> 3**,
> spans ending at the text end **9 -> 23**; `partial-long` walked **19 -> 10**, spans starting at
> text 0 **14 -> 13**.

**The side rule is load-bearing, and `fuzzy-long` is where it shows** - padded on the wrong side its
reversed rows match at once in the original subject now sitting at the right-hand end, which is what
the "spans ending at the text end" column rising 4->20 and 9->23 says.

**Sitting 4 predicted the collapse would be in `partial-long`'s `walked` column, and it is not.**
Two reasons, both measured. First, two of the four generators draw **no reversed row at all** (0 of
200 each at seed 7, against 90 for `partial-long` and 31 for `fuzzy-long`), so the aggregate table
averages a rule in with rows it cannot reach - which is why `generator-long-subject-reach.py` now
prints a by-direction table. Second, a reversed PARTIAL runs off the left end by construction, so it
walks the whole filler whichever side the filler is on; those rows' spans still start at text 0 under
the control. Judge this control on the `fuzzy-long` lines.

Sitting 5's note that the control "must be run in Release" does not apply: the reach measurement is
pure Python against upstream and never builds or runs this port, so the configuration cannot reach it.

### One latent generator trap closed, measured to change nothing

`_generate_long` decided direction with `"(?r)" in row["pattern"]`, which misses `(?ri)` and misses
the REVERSE flag (0x400) entirely - either would be padded on the wrong side silently, the exact
failure its own docstring warns about. Now `"(?r" in pattern or flags & REVERSE`, matching what
`upstream-search-start-whole-region-partial.py` already did. **Measured over 2,400 long rows at seeds
7 and 20260915: 398 of them ARE reversed and every one of the 398 spells it `(?r)` - not one uses
`(?ri)` or the REVERSE flag.** So the old predicate and the new one classify all 2,400 identically,
no recorded wave changes, and the reach table is cell-for-cell identical before and after. (The
first draft of this paragraph said "no row is reversed by either spelling", which the independent
verifier read the only way it can be read on its own - as "no row is reversed at all" - and
reported as DIFFERENT. It was the sentence that was wrong, not the measurement.)

### Numbers

- Ratchet **GREEN**, **6111 / 6111 / 0 skipped**, baseline **6001 -> 6003**. Tool tests 81/81.
- Default wave, Release, three seeds, 300 rows a generator: **GREEN, diverge 0 of 6300 at each**.
- 6000-row gate, Release, three seeds: **RED, 14 + 5 + 10 = 29 of 126,000 each**.
- The two minimised rows replayed through `--rows`: **expected 2, diverge 0 of 2**. The runner still
  prints `Oracle: RED` on that replay, from the unrelated guard
  `Our_own_change_positions_always_agree_with_our_own_counts` ("a wave with no fuzzy match in it
  discriminates nothing") rather than from any divergence - read the `diverge` count, not the banner.
- Two new gap tests, 13 assertions, each mutated one at a time and watched go red.

### Review

**One blind pass over the whole unreviewed delta - `git diff bdeefac`, 957 lines across sittings 4,
5, 6 and 7 - dispatched inside the turn and read as a tool result. Findings raised: four.
Reproduced: four. Fixed: four.** None was a defect in the port; all four were defects in the
evidence, which is what most of this delta IS.

1. **Sitting 4's justification for `LONG_REPEAT_FILLER_GENERATORS` misread its own table.** It
   recorded the unmatchable filler as giving "median match length 1" and the base alphabet as taking
   the generator "from 0 to 32" matches 100+ characters long. Re-running it with
   `LONG_REPEAT_FILLER_GENERATORS = ()`: the median length was **0**, and 100+ character matches were
   already **28**, not 0. The real effect is **28 -> 32 on `long` and 9 -> 5 on `walked`** - the right
   direction on the right column, but a far thinner margin than recorded, and whether that generator
   needs a filler rule of its own is now flagged as worth re-deciding rather than inheriting. The
   `record-oracle.py` comment carries the corrected table; sitting 4's two claims are marked in place.
2. **My own "rows 3 and 5 rested on the verb evidence alone" was false** and contradicted the same
   file twenty lines up. Row 5's sweep gives its judged answer at endpos 1. Corrected in
   `ExpectedDivergences.cs` and in the gap test, with the real distinction stated instead.
3. **`gate-divergence-triage.py` summed generator names and shape tags in one dict**, so the one
   `fuzzy`-generator row and the nine `fuzzy`-tagged rows were reported as an uninterpretable 10.
   Two dicts now, and the generator counts sum to 29 as they must.
4. **A citation this slice made stale.** Sitting 5 wrote "`run-oracle.ps1` defaults to
   `-Configuration Debug` (`:210`)" and sitting 6 then changed both facts: the default is Release and
   the parameter is at `:246`. Marked in place.

The reviewer also checked and CLEARED: every figure the new probe cases and gap tests quote, both
padding-control tables at both seeds, the recorder left byte-identical by the control, both projects
building with no CRLF damage, all 13 new assertions mutated individually to red, the two new oracle
rows replayed and one flipped to `diverge` by a one-character change, the UTF-16/codepoint conversion
on both astral subjects, sitting 6's 13-row gate claim reproduced exactly, and the carried `_regex.c`
citations in STATE.md.

**A second blind pass ran over the fix delta**, since finding 3's repair is code the first reviewer
never saw and the other three changed text it had read. **It raised one finding, reproduced and
fixed:** `gate-divergence-triage.py` hardcoded `DEFAULT_SEEDS = (7, 4242, 20260915)`, but
`run-oracle.ps1`'s own default is `"7,4242,$(Get-Date -Format 'yyyyMMdd')"` (`:243`) - so from
2026-09-16 the default invocation would have skipped the date seed's report and printed "the seed
was GREEN" about a file it never looked for. The seeds are computed now. It also cleared the
control's restore-on-failure path by making the recorder subprocess raise mid-run and confirming the
file came back byte-identical.

**And the independent verifier ran** (amendment 16 limb (d)), a fresh Opus subagent briefed with
nothing but this tree, re-running every runnable claim in these notes. **Two came back DIFFERENT and
both are fixed above**: the gate's wall clock (10m35s under concurrent load, not 8m29s), and "no row
is reversed by either spelling today", which as written reads as "no row is reversed at all" and is
false - 398 of the 2,400 are, all spelling it `(?r)`. Everything else was CONFIRMED, including all
nine gate figures, the triage table, both control tables at both seeds, the two probe blocks, the
ratchet, the tool tests, the `--rows` replay, both minimisations and all 13 mutated assertions. It
also sharpened two things now corrected here: row 118133's subject elided a CR LF, and the `--rows`
replay's misleading `Oracle: RED` banner.

### Still owed on S52 (as sitting 7 left it)

- **The 29 gate divergences, every one unjudged.** This is the slice's largest remaining item.
- The **timeout rows** generator (scope item, depends on S51's `timeout` comparison) - untouched.
- The **20-seed sweep run**. `tools/sweep-seeds.ps1` and the `oracle.yml` Thursday cron job both
  exist and are committed; what has never happened is a recorded 20-seed run with its output in the
  notes. STATE.md listed "the `oracle.yml` CI job" as untouched and that was stale.
- The long generators remain **off** `run-oracle.ps1`'s default `-Generator` list. Sitting 6's reason
  stands: 11 of their 13 divergences are this port exceeding the row timeout in Release, which is a
  Phase 7 performance question and not a correctness one.

---

## Sitting 8 (2026-09-15) - CHECKPOINT, not closed

STATE.md made sitting 8's first job the 29 unjudged gate divergences, and that is what this sitting
did. **Sixteen of the 29 are now judged, classified and pinned, and the gate is down from 29 to 13.**
No engine code changed - every one of the sixteen is upstream diverging in a family this port has
already been judged right about, and the port's answer was not touched on any of them.

### The 29 were triaged in one batch, not one row at a time, and that is the sitting's method

Sitting 7 left the 29 regenerable rather than transcribed, which is what made a batch possible: the
gate's reports and waves were still on disk, so `python tools/probes/gate-divergence-triage.py`
reproduced sitting 7's table exactly - 29 rows, `partial` 11, `interactions` 10, `partial-sliced` 4,
`verbs` 2, `conditionals` 1, `fuzzy` 1 - without re-running the nine-minute gate.

The new artefact is **`tools/probes/gate-divergence-doors.py`**, which puts every judged family's own
control to every diverging row in one run. It reads the row out of `wave-<seed>.jsonl` and this
port's answer out of `report-<seed>.txt`, so **nothing is transcribed and it cannot drift from the
run**. The doors are the verb ablations (`(*SKIP)` deleted, `(*SKIP)` to `(*PRUNE)`), the same call
with `partial=True` dropped, upstream's own anchored `match` over the span its `search` reported, the
anchor sweep on the bound that actually moves, the stepwise walk, and where upstream's own `$` holds.
One run over three seeds, about four minutes, and sixteen rows classified out of it.

**Two things it cost, both worth carrying:**

- **Every upstream call needs a `timeout=`.** An ablation is not the drawn row: deleting a `(*SKIP)`
  deletes the pruning that made the drawn pattern cheap. Seed 7 row 76160's `(*PRUNE)` ablation runs
  past five seconds where the row itself answers instantly, and the first two runs of this probe were
  killed by it having reported nothing about any other row.
- **The probe must honour the row's own slice.** The first version asked `partial-sliced` rows over
  the whole subject and got a different answer on all four - seed 20260915 row 105880 answered
  (9, 10) where the row is (7, 7). Caught because `as drawn` stopped matching the report.

### The sixteen, and what judges each

`searchOnlyPartial` - the recorder's own discriminator, written per row since S33 - **splits the
fourteen partial rows cleanly in two, and the ablation battery agrees with it on every one.** The two
were derived independently, which is what makes the split evidence rather than a reading.

**Six into `search-start-partial`** (seed 7 rows 98092, 99757, 100168; seed 4242 row 105103; seed
20260915 rows 99718, 100141). `searchOnlyPartial` true on all six: upstream's partial covers the
whole searched region and **its own anchored `match` over that very span answers None**. On five of
the six all four controls converge on this port's answer, as they do on sitting 7's rows 6 and 7; row
99718 loses the verb-free one, which answers a COMPLETE match instead. These are also the first rows
of that arm drawn by `partial` and `partial-sliced` rather than by `interactions`, and row 105103 is
the first whose question carries a pos/endpos at all - its slice is what makes "the whole searched
region" (0, 4) rather than the whole subject.

**Six into `partial-retry-reversed-slice`** (seed 7 rows 98857, 99490, 100234; seed 20260915 rows
98719, 99223, 105625). `searchOnlyPartial` false on all six, and every one is that entry's own
printed shape line for line: upstream's `search` answers the zero-width partial at (0, 0) - the LAST
anchor a reversed search would try - while its own `match(0, 1, partial=True)`, its `(*PRUNE)`
spelling and its verb-free spelling all answer this port's (0, 1). Row 99223 loses the verb-free
control, which answers a complete match at (5, 7) instead.

**Two into `partial-retry-carried-slice-forward`** (seed 7 row 99850, seed 20260915 row 105880), and
**they widen that entry's symptom**. Its own text says "the two engines agree on the SPAN"; on these
they do not. Upstream answers a ZERO-WIDTH partial at the far end of what it searched - codepoints
(5, 5) and (7, 7) - where its own bound-free spellings answer the wider partial (3, 5) and (5, 7)
that this port answers, group and all. So the moved `slice_start` costs a START here rather than an
alternative, which is the reversed entry's symptom appearing on a forward pattern - the one thing the
split-by-direction convention did not predict.

**Two into `bestmatch-loses-a-candidate`** (seed 7 row 76930, seed 4242 row 122115), which is exactly
what that entry's own text predicts: "this family fires about three times per three-seed gate ... so
a new draw of the same mechanism now reds the wave until someone judges it and adds it." Both were
judged with the ablation the family's own discriminator cannot make, and it is the sharper half:

```
(?b)(?e)(?:[[:alpha:]][[a-f]~~[d-k]]){e<=2}\b   match 'bab_.bB'  ->  None
(?e)     same pattern, (?b) deleted             same subject     ->  (0, 4) insertions at 2, 3
(?b)     same pattern, (?e) deleted             same subject     ->  None
neither  both deleted                           same subject     ->  (0, 4) insertions at 2, 3
```

**It is `(?b)` that destroys the match, not the pair.** The flagless control the entry keys on is the
same on both middle lines and cannot tell them apart; this can. Both rows are ledger entry 12's
doubled `END_FUZZY` guard reached through trailing insertions - row 76930 needs two of them to reach
a word boundary, which is the k >= 2 the guard refuses at every budget.

### Two rows that look like this work and are NOT pinned

Left deliberately unjudged, with the measurement recorded so sitting 9 does not start from raw rows:

- **Seed 4242 row 76778.** Its recorded `bestmatchFreeOutcome` equals the DRAWN answer, so `(?b)` is
  not what moves it, while `pruneOutcome` is this port's answer - so it is a `(*SKIP)` slice row. But
  it is a reversed anchored `match`, and no entry here covers that: a `match` is ONE attempt, so the
  argument has to be that the two verbs must prune identically within it, not that a retry skipped an
  anchor. That is a real judgement and it needs its own paragraph.
- **Seed 4242 row 77119.** `bestmatchFreeOutcome` **and** `pruneOutcome` BOTH equal this port's
  answer, so two mechanisms share one fingerprint on this row. Pinning it under either would be a
  guess, and this file spends three paragraphs on why S46 doing exactly that was wrong.

### The thirteen that remain, with the doors already put to them

Re-derive with `pwsh -File tools/run-oracle.ps1 -Count 6000` then
`python tools/probes/gate-divergence-doors.py`. What that run already says:

| seed | row | generator, operation | what the battery says |
|---|---|---|---|
| 7 | 74345 | `interactions` search partial, rev | same span and counts (0,2,0); upstream's own CHANGES contradict its own counts - it lists a substitution and a deletion under a two-insertion count - and this port's agree with the counts |
| 7 | 74510 | `interactions` finditer | same span, groups and counts (1,2,0); upstream lists two subs and one insertion under a one-sub two-insertion count |
| 7 | 75921 | `interactions` search partial | counts differ too (upstream (0,1,0) s:[2], port (0,1,1) i:[4] d:[4]), and upstream's own `match` over its own span gives a THIRD answer. Not the same question as the two above |
| 7 | 74413 | `interactions` subf, rev, `(*SKIP)` | `(*PRUNE)` and verb-free both give this port's `sub 0`; upstream's own scan span ends at 3 where its own `$` holds only at 4 and 5. `end-of-line-reads-a-skip-moved-slice`'s signature - but 3 IS a `(?w)$` position, so that entry's `(?w)` control cannot isolate here |
| 7 | 76160 | `interactions` split, `(?b)`, `(*SKIP)` | verb-free gives this port's parts; the `(*PRUNE)` ablation TIMES OUT at five seconds, so this row needs a control that terminates |
| 7 | 118133 | `verbs` subf, `(*SKIP)` | upstream raises `IndexError` from its template on a match it found at (3, 7); this port finds none. **The error is downstream of the disagreement, so S40a's template ruling is not the question** - the question is whether `[A-Z]{1,3}` under IGNORECASE reaches the dotless `ı` at position 3, which is the Turkic shape |
| 4242 | 76778 | `interactions` match partial, rev, `(?b)`, `(*SKIP)` | see above |
| 4242 | 77119 | `interactions` finditer-overlapped, `(?b)(?e)`, `(*SKIP)` | see above |
| 4242 | 119927 | `verbs` finditer-overlapped, rev, `(*SKIP)` | first match identical; the second match's group 1 differs by one character; `(*PRUNE)` and verb-free both give this port's. An `overlapped-skip-*` family, but which one needs deciding |
| 20260915 | 74889 | `interactions` search partial, rev, `(*SKIP)` | upstream answers a COMPLETE match (0, 6); `(*PRUNE)` and verb-free give this port's zero-width PARTIAL at (0, 0). A `(*SKIP)` slice family with a symptom none of them records - complete against partial rather than a moved span |
| 20260915 | 75528 | `interactions` split, `(?p)`, `\L<w1>` fuzzy | upstream drops `İ` from the parts where this port keeps it, over `İﬁ\r\nı`. Turkic, and probably `turkic-default-folding-without-spans` |
| 20260915 | 88716 | `conditionals` sub, rev, IGNORECASE, `ı` literals | upstream substitutes 2 where this port substitutes 1, over four dotless `ı`. Turkic |
| 20260915 | 104366 | `partial-sliced` match partial, rev, EMPTY slice (2, 2) | **no verb at all.** Upstream answers a zero-width partial over an empty slice where this port answers no match. Its own question, and the only one of the thirteen that is not about a verb, a fuzzy cost or a Turkic fold |

Three of the thirteen look Turkic and three look like fuzzy change-position accounting, so sitting 9
should expect to close them in groups rather than one at a time - which is the same bet this sitting
made and won on the fourteen partial rows.

### One harness bug found and fixed, and it was latent rather than theoretical

**`--rows` read a recorded row's slice in the wrong units.** A recorded row's `pos`/`endpos` are
UTF-16 and its `codepointSlice` is the codepoint pair; `--rows` read `pos`/`endpos` and treated them
as codepoints. So feeding a wave row back in - **which is exactly how an entry's `Example` is made,
and how a divergence is minimised one cut at a time** - recorded a DIFFERENT SLICE, silently and with
a plausible answer. Seed 20260915 row 105880's slice came back `[2, 10]` where the wave recorded
`[1, 7]`, and its answer with it.

Found by re-recording the sixteen judged rows through `--rows` and diffing them field by field
against the wave they came out of: 12 differences, all on the two `partial-sliced` rows. The fix is
three lines - `codepointSlice` wins wherever it is present - and after it **all 16 rows round-trip
with 0 field differences**.

**The three sliced `Example` rows already in `ExpectedDivergences.cs` survived only because nothing
astral sits before their slices** (`pos` equals `codepointSlice[0]` on all three), which is luck and
not a rule. Guarded now by two new `--self-check` cases. Reverting the three-line preference and
re-running prints all four of these lines - the first three from the round-trip guard, the fourth
from the subject-cut guard beside it:

```
self-check: a recorded row fed back through --rows changes its pos: 2 became 3
self-check: a recorded row fed back through --rows changes its endpos: 6 became 7
self-check: a recorded row fed back through --rows changes its codepointSlice: [1, 4] became [2, 5]
self-check: cutting a character past the slice of a recorded row's subject changed its
            codepointSlice from [1, 4] to [2, 4], so the cut row no longer asks the slice it was given
```

(An earlier draft of this paragraph quoted the first three and called them the whole output. The
independent verifier re-ran the revert and reported the fourth, which is the subject-cut guard doing
its job - so the quote now shows what the command actually prints.)

`--self-check` still exits 1 on the interpreter-limit guard, which STATE.md has carried as
pre-existing since S43 and which this sitting did not touch. Both new guards are separate lines and
both pass.

### The negative control, run against the code committed here

No control this sitting mutates the engine - no engine code changed. What this sitting can and did
control is **the keying**, because all four touched entries are keyed on the row and on an answer,
and a judged answer that is wrong classifies nothing while a key that is too wide classifies
everything. Run last, after the final code change, on the tree being committed:

> Control A, `entry-keying`: in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`, change one
> character in one answer of each of the four touched entries -
> `_searchStartElsewhereOurs[7]` `match 0:(0,0)[(0,0)] last=-1/- partial` to `0:(0,1)[(0,1)]`;
> `_partialRetryReversedOurs[5]` `match 0:(0,2)[(0,2)] 1:unset last=-1/- partial` to `0:(0,3)[(0,3)]`;
> `_partialRetryForwardOurs[3]` `match 0:(5,2)[(5,2)] 1:(6,0)[(6,0)] last=1/- partial` to `0:(5,3)[(5,3)]`;
> and in `_bestmatchLostCandidateRows`, row 12's `bestmatchFreeOutcome` `"length": 4, "captures": [[0, 4]]`
> to `"length": 5, "captures": [[0, 5]]`.
> Rows: the sixteen themselves, written out of the waves by seed and row number, and run with
> `pwsh -File tools/run-oracle.ps1 -Rows <file> -SkipRecord`. No seed - these are explicit rows.
> Result: **16 expected / 0 diverge** unbroken, **13 expected / 3 diverge** broken, and the three
> that flip are exactly the three whose judged answer was changed.

**The fourth mutation does NOT flip its row, and that is the control working rather than failing.**
`bestmatch-loses-a-candidate` keys on the LIVE row's own recorded `bestmatchFreeOutcome`, not on a
judged-answer string, so editing the `Example` copy cannot change how a row is classified. What it
does instead is fail `Every_expected_divergence_still_diverges` - the staleness alarm - which is the
instrument that guards that entry. Both halves of the list are therefore live, and the run that
proves it reports `failed: 2` (`The_wave_agrees_with_upstream` and the alarm) against `failed: 0`
unbroken. Anyone re-running this should expect a DIFFERENT failure from the fourth mutation than
from the other three, and that difference is the point.

The second seed this control asks for does not apply: it runs on sixteen explicit rows rather than
on a generator, so there is no seed to vary. What stands in for it is the gate itself, which drew
these sixteen at three different seeds.

### Numbers

- Ratchet **GREEN**, **6113 / 6113 / 0 skipped**, baseline **6003 -> 6005**. Tool tests 81/81.
- Default wave, Release, three seeds, 300 rows a generator: **GREEN, diverge 0 of 6300 at each**
  (agree 6286 / 6293 / 6293), measured earlier in this sitting.
  **Its reports do not survive on disk and re-running it would DESTROY the gate's**, because
  `run-oracle.ps1` writes one `TestResults/oracle/report-<seed>.txt` per seed whatever the row count.
  So the order matters: run the default wave FIRST and the 6000-row gate LAST, or the thirteen rows
  the handover above says are regenerable stop being regenerable. The independent verifier reported
  this figure COULD NOT RUN for exactly that reason, and it is the harness's shape rather than a gap
  in the measurement.
- **The 6000-row three-seed gate, re-run here on the commit-ready tree: 29 -> 13 diverging rows**,
  and the accounted-for count rises by exactly the sixteen judged:

  | seed | before (sitting 7) | after | expected before -> after |
  |---|---:|---:|---|
  | 7 | 14 | **6** | 85 -> 93 |
  | 4242 | 5 | **3** | 74 -> 76 |
  | 20260915 | 10 | **4** | 79 -> 85 |
  | total | 29 | **13** | 238 -> 254 |

  `agree 125828 / 125852 / 125826`, `timeout 2 / 1 / 1`, `resource 71 / 68 / 84` of 126,000 a seed.
  Run as `pwsh -File tools/run-oracle.ps1 -Count 6000`, about 21 minutes of wall clock here with
  other work running alongside it - slower than sitting 7's 8m29s and 10m35s, and the load is why.
  (Wall clock is an observation, not something the reports carry; the verifier could not re-derive
  it and nothing rests on it.)
- The thirteen remaining rows are row-for-row the thirteen in the table above: the gate at a fixed
  seed and row count draws the same questions, so the handover is checkable rather than a claim.
- The sixteen judged rows replayed through `--rows`: **expected 16, diverge 0 of 16**, each
  classified by the entry it was judged into and by no other.
- Two new gap tests, each mutated once and watched go red. Both carry their provenance beside the
  assertion, as the owner rule added to the skill on 2026-09-15 requires: the upstream call, the
  version (regex 2026.9.10) and the answer, and the command that reproduces it.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results, and an independent
verifier.**

**Pass one, over the whole diff: five findings raised, five reproduced, five fixed.** None was a
defect in the port; four were defects in the evidence and one was a defect in code this sitting
wrote.

1. **The `(?b)`/`(?e)` ablation table was cited to a probe that could not produce it.** The probe had
   no flag door at all - the table had been measured in a scratch script that no longer exists, which
   is exactly the "a probe nobody can re-run is not evidence" failure. The door is in the probe now,
   and because a JUDGED row leaves the gate report the seed form reads, the probe grew a `--rows`
   mode and the two rows were appended to `tools/probes/bestmatch-loses-a-candidate-rows.jsonl` so
   the citation names something runnable for the life of the entry.
2. **The bestmatch gap test claimed upstream answers both its controls.** It answers one: upstream is
   None on the `(?e)`-deleted spelling too, as the table twenty lines above in the same comment says.
   The two engines differ on both `(?b)` lines and agree on both without it, which is what the
   comment says now.
3. **`--rows` silently discarded a slice cut made in `pos`/`endpos`** - see below; this one took two
   passes to settle.
4. **The wrong rows were named as astral** in the reversed entry's comment: rows 6 and 8, not 6 and
   11. The spans themselves were right.
5. **`partial-retry-carried-slice-forward`'s `Reason` still defined the family as "the two engines
   agree on the SPAN"**, which rows 4 and 5 contradict - and the `Reason` is the text the oracle
   report prints for a classified row, so it was stating the narrow claim to every future reader.

**Pass two, a first pass over the fix delta** (the recorder guard, the probe's extracted `doors()`,
its `--rows` mode and flag door, and the two appended jsonl rows): **four findings raised, four
reproduced, three fixed and one recorded as pre-existing.**

- **The fix for finding 3 was wrong, and this pass proved it by running the other half of the same
  workflow.** Pass one showed a `pos`/`endpos` edit being silently ignored, so the first fix REFUSED
  a row whose two slices disagree. Pass two then showed that a SUBJECT cut - the commonest
  minimisation step there is - leaves the recorded UTF-16 pair stale while `codepointSlice` stays
  correct, so the guard refused a legitimate row, and the escape its own message offered (delete one
  field) produced a silently wrong slice. **The two cases are indistinguishable from inside the
  recorder**, so the guard is gone: `codepointSlice` wins, which is right on the round trip and on a
  subject cut and wrong only on a slice cut made in the other field. That residual sharp edge is
  written into the code beside it, with the real fix named - stop echoing the UTF-16 pair under the
  key names the INPUT uses - and left as a slice of its own, because it is a wave-format change
  reaching the C# consumer, every committed rows file and every wave on disk.
- **The probe's `--rows` mode ignored a hand-written row's `pos`/`endpos`**, which are codepoints
  there, so a sliced hand-written row was asked over the whole subject - the same mis-ask the
  recorded rows had had. Fixed in `slice_of`.
- **The probe crashed with `KeyError: 'flags'` on a minimal row**, which the recorder accepts
  (`row.get("flags", 0)`). Both `flags` and `generator` default now.
- `--self-check` exiting 1 was confirmed pre-existing at HEAD and untouched here.

**And the independent verifier ran** (amendment 16 limb (d)), a fresh agent briefed with nothing but
this tree, re-running every claim. **One came back DIFFERENT and is fixed above**: the quoted
`--self-check` output for the reverted fix was three lines where the command prints four, because the
subject-cut guard fires as well. Two came back COULD NOT RUN and both are now annotated in place
rather than left as bare numbers - the default-wave figures, whose reports a later gate run
overwrites, and the gate's wall clock, which no report carries. **Everything else was CONFIRMED**,
including all fifteen gate figures, the thirteen remaining rows one by one, the 16-row replay and its
6/6/2/2 split, the four-mutation keying control and which three rows flip, the `(?b)`/`(?e)` table on
both rows, the `searchOnlyPartial` split, the ratchet, the tool tests, the baseline moving by exactly
two, and - under the new owner rule - both gap tests' expected values re-derived from upstream.

### Owed, found here

- **A concurrent session committed this sitting's `DECISIONS.md` lines.** Commit `5eabf44` (the
  owner's S52c authoring) swept in the three sitting-8 entries along with its own, so they are in
  the history under someone else's message. The content is correct and nothing is lost; it is
  recorded because the diff of this commit will not contain them and a reader would otherwise look
  for them here. The driver-runs-concurrently hazard, in its mildest form.
- **The `pos`/`endpos` versus `codepointSlice` ambiguity is narrowed, not closed.** See above; the
  fix is a wave-format change and wants its own slice.

### Still owed on S52 after this sitting

- **The thirteen remaining gate divergences**, with the doors already put to them in the table above.
- The **timeout rows** generator (scope item, depends on S51's `timeout` comparison) - untouched.
- The **20-seed sweep run**; `tools/sweep-seeds.ps1` and the `oracle.yml` cron both exist, the run
  does not.
- The long generators remain **off** the default `-Generator` list, for sitting 6's reason.

---

## Sitting 9 (2026-09-15) - CHECKPOINT, not closed

STATE.md made sitting 9's first job the thirteen remaining gate divergences. **Five of the thirteen
are now judged, classified and pinned, and the gate is down from 13 to 8.** No engine code changed -
every one of the five is upstream diverging in a family this port has already been judged right
about, and the port's answer was not touched on any of them.

Sitting 8 left the thirteen regenerable and with a door already put to each, so this sitting started
from `python tools/probes/gate-divergence-doors.py` over the reports still on disk rather than from
raw rows. That table's own guesses were right on three rows and **wrong about the mechanism on a
fourth**, and measuring is what caught it - see "the Turkic label that was a guess" below.

### The five, and what judges each

**Two are ledger entry 11's defect class - the fuzzy counts and the change list drifting apart.**

- **Seed 7 row 74510 joins `atomic-group-leaks-a-change-position`** as its second row, and it is the
  STRONGER of the two. Row 74033 needs upstream's control to be judgeable at all, because its list
  is internally consistent and only the deletion's POSITION moves. This row is wrong on upstream's
  own terms before any control is applied: upstream counts `(1, 2, 0)` - one substitution and two
  insertions - and then lists **two substitutions and one insertion**. The `(?>` to `(?:` control
  then agrees with this port anyway, keeping the span `(0, 7)`, all five groups and the counts and
  re-kinding the list to one substitution at 5 and two insertions at 3 and 4.
- **Seed 7 row 74345 is a NEW entry, `fuzzy-changes-of-the-wrong-kind-for-their-own-counts`**, and
  ledger entry 11's **mechanism G**. Upstream counts `(0, 2, 0)` - two insertions and nothing else -
  and lists one substitution and one deletion and no insertion. The totals agree, two entries for
  two errors, and the KINDS do not: `match_fuzzy_changes` reports the first `sum(fuzzy_counts)`
  entries of the change stack (`:20522`) without regard to each entry's kind, so a polluted stack
  does not merely misplace a change, it mis-names it. **Both engines agree on the counts**, so the
  edit script is two insertions and two insertion positions are the only thing either engine may
  report; this port reports two and upstream reports none. The row's own `leakFreeFuzzy` reproduces
  upstream's drawn answer character for character, which is what puts it in the E/F/G half of the
  ledger's table rather than the A/B half this port fixed.

  **Which construct leaked is NOT established, and the entry says so.** Every ablation moves the
  candidate - deleting the lookahead answers `(1, 3)` complete, making its body non-fuzzy or giving
  it a zero budget answers `(2, 3)` complete, spelling it positive answers `(1, 3)` complete, against
  the drawn `(0, 3)` partial. So the KIND is settled and the positions are not, which is the same
  weak arm `fuzzy-changes-leaked-from-an-abandoned-attempt` already names.

**Three are the Turkic `T` rows, in shapes whose answers carry no match position.**

- **Seed 7 row 118133 and seed 20260915 row 75528 join `turkic-default-folding-without-spans`** as
  rows 7 and 8. 118133 is row 29165's range control again in a third operation: `[A-Z]{1,3}` and
  `[A-Y]{1,3}` both reach the dotless small i and `[A-H]{1,3}` and `[J-Z]{1,3}` find nothing at all,
  which is this port's answer. 75528 is a new symptom rather than a new argument - upstream EATS the
  U+0130, so its first match is `(0, 1)` where every swap for a letter with no `T` row gives a
  zero-width first match and hands the letter back as a part. **Fold LENGTH is measured not to be
  the variable**: U+00DF, U+FB00 and U+01F0 all fold to two characters and `h` to one, and all four
  agree with this port.
- **Seed 20260915 row 88716 is a NEW entry, `turkic-default-folding-read-by-a-lookaround`.** Both of
  upstream's matches land on the trailing `a` - `(5, 6)` and `(4, 5)` - so **no span on either side
  covers a Turkic letter**, and both the first Turkic entry's span test and the without-spans
  entry's recorded `scanMatches` alarm refuse it. Adding it to the sibling would have meant
  loosening that alarm, which the owner's 2026-09-14 ruling for this file forbids.

### The Turkic label that was a guess, and the three drafts measurement killed

Sitting 8's table called row 88716 Turkic on the strength of four dotless `ı` in the subject. It IS
Turkic, but **this sitting reached that conclusion three times by bad reasoning before measuring it
properly**, and each draft died. Worth recording because the next reader will make the same moves:

1. **The first control swapped `ı` for `h` and for `i`, and both reproduced upstream's count.** Read
   naively that says "not Turkic". It says nothing of the kind: the pattern's own
   `(?<!(?:a|\p{ASCII})+)` reads ASCII-ness, and `h` and `i` are ASCII where `ı` is not, so the swap
   changed the question. Four non-ASCII, single-fold, no-`T`-row letters - U+00F1, U+01E7, U+0125,
   U+00E5 - all AGREE between the engines, and only the dotless i diverges. Dropping IGNORECASE
   agrees too. Caught by me before the entry was written.
2. **The entry then named the wrong construct** - the inner `(?<=ı[\w\s])` that decides the
   conditional. The blind review killed it by measuring: neutering that lookbehind leaves the
   engines disagreeing (upstream 3, this port 1), while deleting only the `a|` from the OUTER
   negative lookbehind makes upstream agree with this port (1 and 1).
3. **The replacement claim - "a set is expanded by its members' case partners" - was ALSO wrong**,
   and the delta pass killed that one. `[\p{ASCII}]` refutes it: that set's member holds `I` and it
   reaches nothing. What actually divides the cells is **how many members the set has**.
   `[\p{ASCII}]` answers None and `[\p{ASCII}\p{ASCII}]` - the same member twice, so exactly the
   same characters - MATCHES, as do `[\p{ASCII}z]` and `[a\p{ASCII}]`. A one-member set behaves like
   the bare property; a two-member one case-expands the property's contents. `[ab]` reaching nothing
   says it is the PROPERTY's members expanding rather than sets in general.

**And the expansion is not itself the defect, which is the control that makes this the `T` rows.** A
multi-member set holding `\p{ASCII}` reaches every character whose partner is ASCII - U+212A KELVIN
SIGN (`212A; C; 006B`) and U+017F LATIN SMALL LETTER LONG S (`017F; C; 0073`) as well as the two
Turkic rows - and reaches neither U+00C5 nor U+00F1, whose partners are not ASCII. Over the 42-cell
grid of those six characters against seven spellings, **36 cells AGREE and the only six that diverge
are U+0131 and U+0130 against the three multi-member spellings**. So both the expansion and the
one-member exception are shared behaviour, and the `T` rows are the whole of the difference.

### One row measured and deliberately NOT pinned

**Seed 7 row 75921** (`interactions`, partial `search`). Better measured than sitting 8 left it, and
still not judged:

- Upstream's drawn answer names a change at codepoint **2, outside its own reported span `(4, 5)`**,
  under counts `(0, 1, 0)` that say one INSERTION. Its own anchored `match(4, 5, partial=True)` gives
  a THIRD answer - same span, same group, same counts, one DELETION at 4. So upstream contradicts
  itself in kind on both answers, and its drawn list is wrong on a second, independent ground.
- This port answers `(0, 1, 1)` with an insertion and a deletion at 4, self-consistent.
- **The count cannot be settled from the row.** The port spends two errors where upstream spends
  one, and upstream's own totals agree with each other, so nothing upstream says its count is wrong.
  The structural argument is mechanism B - a partial returns from inside a nested section, and the
  inner `{1<=e<=2}` demands at least one error while the outer `{e<=1:\d}` may add one - but
  `UpstreamCountedOnlyTheInnermostSection`'s prefix test fails because the list is ALSO leaked, so
  the mechanism-B signature is only half present. `(?>` to `(?:` moves the candidate, `(3, 5)`
  against `(4, 5)`, so the atomic control cannot arbitrate it either.
- Pinning it under either mechanism would be the guess this file spends three paragraphs on. Left
  for sitting 10 with the measurement above.

### Numbers

- Ratchet **GREEN**, **6117 / 6117 / 0 skipped**, **6009 distinct ids**, baseline **6005**.
- Default wave, Release, three seeds, 300 rows a generator, run BEFORE the gate as STATE.md
  requires: **GREEN, diverge 0 of 6300 at each seed** (agree 6286 / 6293 / 6293, expected 8 / 2 / 4).
- **The 6000-row three-seed gate, re-run on the commit-ready tree: 13 -> 8 diverging rows**, and the
  accounted-for count rises by exactly the five judged:

  | seed | before (sitting 8) | after | expected before -> after |
  |---|---:|---:|---|
  | 7 | 6 | **3** | 93 -> 96 |
  | 4242 | 3 | **3** | 76 -> 76 |
  | 20260915 | 4 | **2** | 85 -> 87 |
  | total | 13 | **8** | 254 -> 259 |

  `agree 125828 / 125852 / 125826`, `timeout 2 / 1 / 1`, `resource 71 / 68 / 84` of 126,000 a seed.
  Seed 4242 is unchanged because none of the five was drawn there.
- The five judged rows replayed through `--rows`: **expected 5, diverge 0 of 5**, each classified by
  the entry it was judged into and by no other.
- Three new gap tests, each mutated once and watched go red, each carrying its provenance beside the
  assertion as the owner's 2026-09-15 rule requires.

### The negative controls, run against the code committed here

No control this sitting mutates the engine - no engine code changed. What it controls is **the
keying**, because four of the five rows are keyed on a judged answer and the fifth on a recorded
discriminator. Run last, after the final code change, on the tree being committed.

> **Control A, `entry-keying`**: in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`, change one
> character in one judged ANSWER of each of the four answer-keyed entries -
> `_wrongKindChangeOurs[0]` `...fuzzy=(0,2,0)[s:][i:2,1][d:]` to `[i:2,0]`;
> `_turkicWithoutSpansOurs[6]` `sub 0 'Aİﬀı
S'` to `sub 1 '...'`;
> `_turkicWithoutSpansOurs[7]` `split 9 '' <null> 'İ' <null> '' <null> 'ı' <null> ''` to
> `split 8 ...`; `_turkicLookaroundOurs[0]` `sub 1 'ııııa'` to `sub 2 '...'`.
> Rows: the five themselves, written out of the waves by seed and row number - seed 7 rows 74345,
> 74510 and 118133, seed 20260915 rows 75528 and 88716 - run with
> `pwsh -File tools/run-oracle.ps1 -Rows <file>`. No seed - these are explicit rows.
> Result: **5 expected / 0 diverge** unbroken, **1 expected / 4 diverge** broken, and the four that
> flip are exactly the four whose judged answer was changed. Broken, three tests fail:
> `The_wave_agrees_with_upstream`,
> `An_answer_with_no_spans_is_classified_by_the_row_keyed_turkic_entry` and
> `Every_expected_divergence_still_diverges`.

> **Control B, `atomic-discriminator`**: in the same file, in `_atomicLeakedChangeRows`, change row
> 74510's `atomicFreeOutcome` `"substitutions": [5]` to `"substitutions": [6]`. Same rows, same
> command. Result: **classification does NOT change - 5 expected / 0 diverge** - and
> `Every_expected_divergence_still_diverges` fails instead, `failed: 1`.

**Control B not flipping its row is the control working rather than failing**, and it is sitting 8's
finding reproduced for the new row: `atomic-group-leaks-a-change-position` keys on the LIVE row's
recorded `atomicFreeOutcome`, not on a judged-answer string, so editing the `Example` copy cannot
change how a row is classified. What it does instead is fail the staleness alarm, which is the
instrument that guards that entry. Anyone re-running these should expect a DIFFERENT failure from
Control B than from Control A, and that difference is the point.

The second seed these controls ask for does not apply: they run on five explicit rows rather than on
a generator, so there is no seed to vary. What stands in for it is the gate itself, which drew these
five at TWO different seeds, 7 and 20260915.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results, and an independent
verifier.**

**Pass one, over the whole diff: two findings raised, two reproduced, two fixed.** Neither was a
defect in the port; both were defects in the evidence, and the first was serious.

1. **The `turkic-default-folding-read-by-a-lookaround` entry named the wrong construct.** It said the
   letter was read by the inner `(?<=ı[\w\s])`; neutering that leaves the engines disagreeing
   (upstream 3, this port 1), while deleting only the `a|` from the outer negative lookbehind makes
   upstream agree (1 and 1). The JUDGEMENT did not move - it is the `T` rows either way - but the
   mechanism did, and a one-line set control replaced the guess.
2. **The gap test was declared "the minimal form of row 88716" and was not**: it pinned a
   pattern-side `ı` against a subject `I`, a direction that appears nowhere in the row, so the row
   could regress with all four assertions still passing. Replaced by the set-union test; the
   lookbehind test was KEPT, because its four cells are measured upstream facts, with its claim
   corrected to what it actually pins.

**Pass two, a first pass over the fix delta** (the rewritten entry `Reason`, the new set-union gap
test, the probe's new sections and the corrected doc on the lookbehind test): **three findings
raised, three reproduced, three fixed.**

- **The replacement mechanism claim was ALSO wrong** - see "the three drafts measurement killed"
  above. `[\p{ASCII}]` refutes "a set is expanded by its members", and `[\p{ASCII}\p{ASCII}]` is the
  cell that says what the rule actually is. The entry, the test and the probe now state the
  member-count rule, and the test asserts the discriminating cells rather than only the refusals.
- **Two "last section" citations into the probe were wrong** after the probe grew sections. Both now
  cite the section by its HEADING, which does not rot.
- **The lookbehind test's summary sentence still carried the first draft's mechanism** ("the
  conditional it decides takes the other branch and the scan stops one match earlier"), false in
  both halves: forcing that lookbehind to fail gives one match MORE, not fewer, and the test body
  contains no conditional and no scan.

### The independent verifier ran, and it destroyed this slice's main file

**Amendment 16 limb (d)'s verifier was dispatched, a fresh agent briefed with nothing but this tree,
and it re-ran every claim. Ten of the eleven came back CONFIRMED.** Then, reverting Control A's four
one-character edits, **it ran `git checkout -- tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`
and discarded the whole of this sitting's uncommitted work in that file** - both new entries, both
new row constants, the two rows appended to `_turkicWithoutSpansRows`, row 74510 of
`_atomicLeakedChangeRows`, and three edited `Reason` texts. `.scratch/control.py` exists precisely to
revert those four edits and the verifier did not use it. No other file was touched.

**It was recoverable, and the recovery is itself checkable.** The verifier had built the good file at
12:53 and preserved the assembly, so it left `.scratch/RECOVERY-expected-divergences.json` holding
every entry's `Id`, `Reason`, `PinnedBy` and `Example` and all seven field values as the good build
computed them. The file was rebuilt from this session's own edit text and then diffed against that
dump (`.scratch/difframe.py`): **all four row constants byte-identical after JSON normalisation, all
three judged-answer arrays identical, and both new entries present.** One `Reason` differed, by two
characters, and the rebuild is the CORRECT one - "the E/F half of ledger entry 11's table" where the
rebuild says "E/F/G", which is what the table now reads. `Applies` lambdas are IL-only and could not
be recovered from the dump; they were rewritten from this session's text and are proved by the
controls below rather than by the diff.

**Everything was then re-run on the restored tree, and nothing rests on a pre-incident run**: the
ratchet, the five-row replay, Control A, Control B and the 6000-row gate. The numbers in this
sitting's "Numbers" section are all from those re-runs.

**The instruction the next verifier brief needs, said plainly: an independent verifier must never run
`git checkout`, `git restore`, `git stash` or `git reset` on a dirty tree.** It is handed a working
tree full of uncommitted work and no commit to fall back to, so the ordinary undo is the one command
that cannot be safely used. A control it applies must be reverted by re-editing exactly what it
edited, or by the slice's own revert script where one exists.

### The verifier's verdicts

Ten CONFIRMED, one COULD NOT RUN, none DIFFERENT. CONFIRMED: the ratchet (GREEN, 6117 / 6117 / 0
skipped, 6009 distinct ids, baseline 6005); all fifteen gate figures; the five-row replay and the
order of the five entries that classify it; Control A, including that the four that flip are the
four whose judged answer changed and the three tests that fail; both change-position probes; the
Turkic probe's row-118133, row-75528, row-88716 and set-grid sections; the 42-cell port-side grid,
re-derived from a rows file the verifier built itself (36 agree, 6 diverge, and the six are U+0131
and U+0130 against the three multi-member spellings); the claim that seed 4242 is unchanged because
none of the five was drawn there, which it checked twice - by seed and by asking whether any row
anywhere in `wave-4242.jsonl` carries one of the five questions. **And, under the owner's 2026-09-15
rule, all three new gap tests' expected values re-derived directly against regex 2026.9.10 rather
than through this repo's probes** - each cited upstream value is what upstream actually answers, and
the verifier additionally read `upstream/src/_regex.c:20522` and confirmed that
`match_fuzzy_changes` walks `fuzzy_changes[i]` for `i < sum(fuzzy_counts)` and bins each entry by
`change->type` with no check that the stack's kinds match the counts, which is mechanism G's claim in
upstream's own source.

COULD NOT RUN: Control B, because the file was destroyed before the verifier reached it. It was
re-run here afterwards on the restored tree and is reported in full above.

### Still owed on S52 after this sitting

- **The eight remaining gate divergences.** Sitting 8's table still describes seven of them
  correctly. What is left is six `(*SKIP)` slice rows (seed 7 74413 and 76160, seed 4242 76778,
  77119 and 119927, seed 20260915 74889), the empty-slice partial (seed 20260915 104366, the only
  one of the thirteen with no verb, no fuzzy cost and no Turkic fold), and seed 7 75921, measured
  above and deliberately unpinned. Regenerate with `pwsh -File tools/run-oracle.ps1 -Count 6000`
  then `python tools/probes/gate-divergence-doors.py`.
- The **timeout rows** generator (scope item, depends on S51's `timeout` comparison) - untouched.
- The **20-seed sweep run**; `tools/sweep-seeds.ps1` and the `oracle.yml` cron both exist, the run
  does not.
- The long generators remain **off** the default `-Generator` list, for sitting 6's reason.
- The `pos`/`endpos` versus `codepointSlice` ambiguity sitting 8 narrowed is still open, and is still
  a wave-format change wanting its own slice.
