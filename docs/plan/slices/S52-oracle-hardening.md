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

- [x] Sweep tool committed and run; CI job added; astral, long and timeout generators recorded.
      (Sweep tool and its run: sitting 13. CI job: `oracle.yml`'s Thursday cron. Astral: sitting 2.
      Long: sitting 4. Timeout: sitting 14. The sweep's 37 red rows are box 2's problem, not this
      box's - the instrument exists and has been run.)
- [ ] Every divergence judged, fixed or entered with a control; nothing unjudged.
      (Sitting 18: 15 of the 37 sweep rows judged. Sitting 16's and 17's nine, plus group D's six -
      rows 25, 29 and 36 into `search-start-partial`, 13 and 22 into
      `partial-retry-carried-slice-forward`, 24 into `partial-retry-reversed-slice`, split by the
      reachability instrument rather than by the shape they share. 22 to go, plus the gate's
      104366 which the owner's ruling sends to S52c/S52d.)
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

---

## Sitting 10 (2026-09-15) - CHECKPOINT, not closed

STATE.md made sitting 10's first job the eight remaining gate divergences. **Two of the eight are
now judged, classified and pinned, a third is measured to the bottom and deliberately NOT pinned,
and the gate is down from 8 to 6.** No engine code changed. The two judged are both the reversed
carried `slice_end` this port has already been judged right about eight times; the third is a
question neither engine answers consistently, which is why it is not pinned.

Sitting 9 left the eight regenerable with a door already put to each, so this sitting started from
`python tools/probes/gate-divergence-doors.py` over the reports still on disk rather than from raw
rows. Sitting 8's table was right about the mechanism on both rows judged here and **wrong about
which engine is in the wrong on the one that is not** - see the third section.

### The two, and what judges each

**Seed 4242 row 119927 joins `overlapped-skip-stale-slice-reversed`, through a new second arm of
its predicate.** The entry's own text and its moved-spans-right test both look for whole-match
spans that moved; on this row **every whole-match span agrees and only a CAPTURE'S END moves**, and
`EveryStaleSliceHasOnlyMovedSpansRight` cannot see that because it requires an inner group to keep
its length. So the row was reported rather than classified, and the fix is the arm the entry's own
"hole, said out loud" paragraph had been asking for since S40d.

The row, `verbs`, prefilter-free, `(?r)^(?:[^a]*?(*SKIP)\w|\u200d)(?P<g1>\S*(*SKIP)A)` over
`'aa\u200d\u200dAAa'`, overlapped, as `(span, g1 span)` per match:

```
as drawn           [((0, 6), (1, 6)), ((0, 5), (1, 6))]
(*SKIP)->(*PRUNE)  [((0, 6), (1, 6)), ((0, 5), (1, 5))]   <- this port's answer
verb deleted       [((0, 6), (1, 6)), ((0, 5), (1, 5))]   <- this port's answer
stepwise walk      [((0, 6), (1, 6)), ((0, 5), (1, 5))]   <- this port's answer
with the prefilter [((0, 6), (1, 6)), ((0, 5), (1, 5))]   <- this port's answer
```

Upstream's SECOND match is (0, 5) and carries g1 at (1, 6) - **a capture reaching past the end of
the match it belongs to**, in a pattern holding no lookaround and no `\K` that could put one there,
which is the same self-evident symptom the forward entry records. The predicate keys on the WALK
rather than on that, because the walk is a fact the recorder already wrote into the row
(`anchoredScan`) and the capture test would need a new inference. The new arm requires the match
COUNT to be equal, so a scan upstream ran too long or too short still belongs to the two entries
below it and the ids keep naming their mechanisms.

**PREFILTER-FREE IS NOT OPTIONAL ON THIS ROW, and an ordinary `regex.finditer` does not reproduce
it.** With upstream's required-string prefilter ON, upstream gives this port's answer; the `verbs`
generator is recorded with it off (`tools/record-oracle.py:325`). A first draft of this judgement
measured the row with the prefilter on, got this port's answer for upstream, and would have
concluded the wave was lying. The probe section added here asks the way the wave does.

**Seed 20260915 row 74889 is a NEW entry, `reversed-skip-invents-a-match`**, and its argument is
the sharpest in the file because it needs no reading at all: **a verb whose entire job is to REMOVE
backtracking positions cannot create a match that does not exist without it.** Upstream's own three
spellings of the one question, `interactions`, search over
`'\U0001F600\ufb03 _\U00010400a\ufb03\U00010400'` with IGNORECASE and MULTILINE:

```
                     partial=True                  partial=False
as drawn, (*SKIP)    (0, 6) g1=(5, 6) complete     (0, 6) g1=(5, 6) complete
(*SKIP) -> (*PRUNE)  (0, 0) PARTIAL, g1 unset      None
verb deleted         (0, 0) PARTIAL, g1 unset      None
```

`(*PRUNE)` is the same opcode body but for the two lines `(*SKIP)` has first, which are the ones
that move the slice (`upstream/src/_regex.c:14553` reversed, `:14555` forward), so it prunes
identically and moves no bound. Upstream's complete match exists when and only when a bound was
moved. Both of this port's cells are upstream's own `(*PRUNE)` and verb-free cells.

**And it is not the partial machinery**, which is what keeps the row out of
`partial-retry-reversed-slice` and `search-start-partial`: with `partial=True` dropped, upstream
still answers the complete match and this port still answers no match, while the verb-free
non-partial spelling is None on both sides. The right-hand column is what settled that, and it was
not in sitting 8's table. The entry is keyed on the row AND on the row's own recorded
`pruneOutcome`, so the classification depends on a fact the run recorded rather than on a string
judged once.

### The row measured to the bottom and deliberately NOT pinned, and why it is the important one

**Seed 20260915 row 104366**, `(?r)\xdf\ufb01(.*?)\b` asked as `match(subject, 2, 2, partial=True)`
over `'\ufb01\u0131'` - an EMPTY slice at the end of a two-character subject. Upstream answers a
zero-width partial at (2, 2); this port answers no match. Sitting 8 called it "the only one of the
thirteen that is not about a verb, a fuzzy cost or a Turkic fold", which is right, and expected it
to be a small question, which it is not.

**Upstream holds TWO rules for "has a reversed match run out of text on the left", and they
disagree about whether the slice start counts.** Both are in its own source:

- `init_match` (`upstream/src/_regex.c:18442-18446`) sets `text_end = end` - the SLICE end - but
  `text_start = 0`, the real string start, under the comment "Open start and closed end bounds,
  like in re module". Five lines above, at `:18435-18437`, sits the contract that asymmetry breaks: *"The documentation
  says that the end of the slice behaves like the end of the string."*
- Every node handler then asks `text_pos <= state->text_start && partial_side == RE_PARTIAL_LEFT`
  (`:6747`, `:12173`, `:13854`, `:14502` and about thirty more), so a reversed match that runs out
  at a NON-ZERO slice start reports no match.
- `search_start` - the optimiser's entry, taken only for the shapes it is enabled for - asks
  `start_pos < state->slice_start` and returns `RE_ERROR_PARTIAL` positioned at `slice_start`
  (`:8400-8405`), and so do the `search_start_STRING*_REV` helpers, which pass `state->slice_start`
  as the limit and let their `string_search*_rev` set `is_partial` there (`:8335-8382` - `_FLD_REV`
  at `:8335`, `_IGN_REV` at `:8361`, `_REV` at `:8373`).

So whether a reversed partial is reported at a non-zero slice start depends on **whether the
optimiser picked the pattern's leading string as the search test** - a decision about SPEED, which
must not change the answer. Upstream's own suite never asks a partial at a non-zero `pos` AT ALL: it makes 72 `partial=True`
calls (`upstream/regex/tests/test_regex.py`, the reversed ones concentrated at :4073-4112) and not
one of them passes a `pos`. That is why the two rules have never met.

The control that shows it, and it is three patterns over the SAME one visible character:

```
                              over 'a', no slice        over 'xya' slice (2,3)
(?r)ya                        (0, 1) partial=True       None
(?r)ya(.*?)\b                 (0, 1) partial=True       (2, 3) partial=True
(?r)ya(.*)\b                  (0, 1) partial=True       None
```

Greedy against lazy is the cleanest half: preference order chooses AMONG matches and cannot decide
whether one exists, and over the one character the slice shows, `(.*)` and `(.*?)` have the same
single possible behaviour.

**THIS PORT IS NOT SELF-CONSISTENT EITHER, and measuring it is what stopped this being pinned.**
The port carries upstream's `text_start = 0` as `MatchState.TextStart` and asks the same question at
every site (`Engine/Matcher.cs:3273`, `:5788`, `:6644`, `:6705`, `:6787`, `:7393`, `:7458`, `:7556`
and the rest) - and it has an equivalent of the second rule as well, because on the one-character
slice it answers `(2, 3) partial` to `(?r)ya(.*?)\b` exactly as upstream does. Over the probes' 33
cells in 7 blocks the two engines agree on 23 and differ on 10, and **the 10 split both ways**: 3
where this port reports a partial and upstream does not (the greedy and bounded spellings over the
one-character slice) and 7 where upstream does and this port does not (the drawn row, its `\b`-less
cut, and the `(?r)ab(.*?)\b` empty-slice family).

So "upstream contradicts itself" is true and is not enough here: each engine reports a reversed
partial at a non-zero slice start in some shapes and refuses it in others, the shapes do not line
up, and neither engine's set contains the other's. Pinning it under either reading would be the
guess this file spends three paragraphs on. Left for sitting 11 with the
measurement, both probes committed
(`tools/probes/upstream-reversed-partial-ignores-the-slice-start.py` and its port half
`tools/probes/port-reversed-partial-ignores-the-slice-start.ps1`, same grid, same headings).

**What it probably is, said out loud so sitting 11 does not have to re-derive it.** `text_start = 0`
exists to make `^` and `\A` refuse a non-zero `pos` - Python `re`'s documented open-start bound. The
partial handlers reuse that field for a DIFFERENT question, "have we run out of text on the left",
whose right bound is `slice_start`. Conflating the anchor rule with the run-out rule is the defect,
and upstream's own `search_start` and its own forward side both answer the run-out question with the
slice. If that reading is right then the slice-honouring answer is the correct one and **this port
has inherited the bug**, which the owner's 2026-09-12 rule says must be fixed before 1.0. It is an
engine change wherever that question is asked - 38 sites in upstream and the 9 in this port's
`Engine/Matcher.cs` that mirror them - plus whatever the port's own second rule turns out to be, so
it is a slice of its own and it needs the owner's ruling first. **Nothing was changed here.**

### A recorded control that does not do what it says

**`tools/run-oracle.ps1 -Rows <file> -SkipRecord` does not read `<file>`.** `-SkipRecord` skips the
recording step entirely (`tools/run-oracle.ps1:286`), so the consumer eats whatever wave is already
on disk and `-Rows` never reaches the recorder. Observed here directly: the eight-row file run that
way reported on rows 121402, 124034, 124474 and 125913 - rows of the 6000-row gate, not of the file.
Sitting 8's Control A is recorded as
`pwsh -File tools/run-oracle.ps1 -Rows <file> -SkipRecord`, so **as written it measures the last
wave rather than its sixteen rows**. Sitting 9's controls use `-Rows <file>` with no `-SkipRecord`
and are unaffected. Not changed here - it is someone else's recorded control and the fix is a
one-word edit to a closing note, which is between-slice maintenance rather than mid-slice.

### One more hazard, found and not fixed

`tools/probes/upstream-reversed-overlapped-skip.py`'s original three cases compile WITH upstream's
required-string prefilter, and all three are `verbs` rows, which the recorder records without it.
The probe is cited by `overlapped-skip-extra-match-reversed` and by
`overlapped-skip-stale-slice-reversed` for facts measured in 2026-09-12, and whether those three
still reproduce prefilter-free is untested. The section this sitting added to that probe does ask
prefilter-free and says so in place. Flagged rather than changed: re-measuring three older entries'
evidence is not this sitting's scope, and it is a real item for sitting 11.

### Numbers

- Ratchet **GREEN**, **6119 / 6119 / 0 skipped**, **6011 distinct ids**, baseline **6005 -> 6011**.
- Default wave, Release, three seeds, 300 rows a generator, run BEFORE the gate as STATE.md
  requires: **GREEN, diverge 0 of 6300 at each seed** (agree 6286 / 6293 / 6293,
  expected 8 / 2 / 4, timeout 2 / 0 / 0, resource 4 / 5 / 3).
- The eight remaining rows replayed through `-Rows` on the commit-ready tree: **expected 2,
  diverge 6 of 8**, and the two expected are the two judged here, each classified by the entry it
  was judged into and by no other. Before the changes the same file gave **expected 0, diverge 8**.
- **The 6000-row three-seed gate, re-run on the commit-ready tree: 8 -> 6 diverging rows**, and the
  accounted-for count rises by exactly the two judged:

  | seed | before (sitting 9) | after | expected before -> after |
  |---|---:|---:|---|
  | 7 | 3 | **3** | 96 -> 96 |
  | 4242 | 3 | **2** | 76 -> 77 |
  | 20260915 | 2 | **1** | 87 -> 88 |
  | total | 8 | **6** | 259 -> 261 |

  `agree 125828 / 125852 / 125826`, `timeout 2 / 1 / 2`, `resource 71 / 68 / 83` of 126,000 a seed.
  Seed 7 is unchanged because neither of the two was drawn there. Run as
  `pwsh -File tools/run-oracle.ps1 -Count 6000`, about ten minutes of wall clock here. The
  `timeout`/`resource` split at seed 20260915 is 2/83 where sitting 9 recorded 1/84: one row that
  hit the interpreter's resource guard last time ran out of time instead. Those two buckets are both
  "upstream did not answer" and the total is the same, so nothing rests on which of them a row lands
  in; it is recorded because a reader diffing the two sittings' numbers will see it.
- Two new gap tests, each mutated once and watched go red, each carrying its provenance beside the
  assertion as the owner's 2026-09-15 rule requires: the upstream call, the version (regex
  2026.9.10) and the answer, and the probe that reproduces it.

### The negative controls, run against the code committed here

No control this sitting mutates the engine - no engine code changed. What it controls is the two
things the sitting added: **the row-and-`pruneOutcome` keying of the new entry**, and **the new walk
arm of the old one**. Run last, after the final code change, on the tree being committed, over
`.scratch/eight-rows.jsonl` - the eight remaining gate rows written out of the waves by seed and row
number - with `pwsh -File tools/run-oracle.ps1 -Rows <file>`. No seed: these are explicit rows.

Four of them, because the sitting added two different keying mechanisms and each has a live half and
a copied half. `.scratch/control.py break-<x>` / `restore-<x>` applies and reverts each by exact
re-edit; **never revert one with git on this tree.**

> **Control A, the new entry's ROW KEY**: in `_reversedSkipInventedMatchRows`, change
> `"flags": 10,` to `"flags": 11,`. `Question(row)` is pattern, flags, subject, operation, partial,
> pos and endpos, so the set no longer holds the live row's question.
> Result: **expected 1, diverge 7 of 8** - row 7 alone stops being classified - `failed: 1`.
>
> **Control B, the new WALK ARM's direction**: in `overlapped-skip-stale-slice-reversed`'s
> `Applies`, change the arm's second comparison from
> `string.Equals(ourScan.Describe(), walked.Describe(), ...)` to
> `string.Equals(theirScan.Describe(), walked.Describe(), ...)` - the mutation that would let a
> PORT defect through, because it stops asking whether this port agrees with upstream's own walk.
> Result: **expected 1, diverge 7 of 8** - row 6 alone stops being classified - `failed: 1`.
>
> **Control C, the new entry's LIVE GATE**: in `reversed-skip-invents-a-match`'s `Applies`, change
> `string.Equals(ours.Describe(), pruned.Describe(), ...)` to
> `string.Equals(row.Expected.Describe(), pruned.Describe(), ...)`, so the entry asks whether
> UPSTREAM lands on its own `(*PRUNE)` answer rather than whether this port does.
> Result: **expected 1, diverge 7 of 8**, and `failed: 2` - row 7 stops being classified AND
> `Every_expected_divergence_still_diverges` fires.
>
> **Control D, the Example COPY's recorded control**: in the same row constant, change
> `"pruneOutcome": ... "length": 0, "captures": [[0, 0]]` to `"length": 1, "captures": [[0, 1]]`.
> Result: **classification does NOT change - expected 2, diverge 6 of 8** - and `failed: 2`, the
> two being `The_wave_agrees_with_upstream` and `Every_expected_divergence_still_diverges`.

Rows: `.scratch/eight-rows.jsonl`, the eight remaining gate rows written out of the waves by seed and
row number, run with `pwsh -File tools/run-oracle.ps1 -Rows .scratch/eight-rows.jsonl`. **No
`-SkipRecord`** - see above; with it the command reads a different file's worth of rows entirely.
Unbroken, before and after all four: **expected 2, diverge 6 of 8, `failed: 1`**, the one failure
being `The_wave_agrees_with_upstream`, which the six unjudged rows fail by design.

**Control D not flipping its row is the control working rather than failing**, and it is the third
sitting in a row to learn it: an entry keyed on a LIVE recorded field cannot be broken by editing
the `Example` copy of that field, because nothing reads the copy to classify. What the copy does is
fail the staleness alarm, which is the instrument that guards it - so D and C together show both
halves are live, and they fail DIFFERENTLY. Anyone re-running these should expect that difference.

The second seed these controls ask for does not apply: they run on eight explicit rows rather than
on a generator, so there is no seed to vary. What stands in for it is the gate itself, which drew
the two judged rows at two different seeds, 4242 and 20260915.

The second seed these controls ask for does not apply: they run on eight explicit rows rather than
on a generator, so there is no seed to vary. What stands in for it is the gate itself, which drew
the two judged rows at two different seeds, 4242 and 20260915.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results, and an independent
verifier.**

**Pass one, over the whole diff: six findings raised, six reproduced, six fixed.** None was a defect
in the port or in either judgement; all six were defects in the EVIDENCE, and the first was serious.

1. **The cell count behind the row-104366 decision was wrong, and it is the count the owner-facing
   conclusion rests on.** The notes, ledger entry 24 and DECISIONS all said "27 cells, the two
   engines agree on 26, and the one that differs is the greedy spelling". Measured on the committed
   probes it is **33 cells in 7 blocks, 23 agree and 10 differ** - and the 10 split BOTH ways: 3
   where this port reports a partial and upstream does not, 7 where upstream does and this port does
   not, the drawn row among them. The figure had been read off the first scratch grid, which was the
   empty-slice block alone, and then stated of the whole probe. **The conclusion is unchanged and
   better supported** - neither engine is consistent and neither engine's set of partials contains
   the other's - but the number was wrong and it was load-bearing.
2. **A probe comment contradicted the probe's own output four lines later**: "the cuts are
   upstream's own controls and every one of them answers None" where the `\b`-less cut answers a
   partial. Corrected, and the correction says what the cut actually isolates.
3. **The 119927 test claimed to quote the wave's pattern and did not.** The row's JSON `‍` is
   JSON for ONE ZWJ CHARACTER; the test put the six characters `‍` into the pattern. The answer
   is the same either way - measured both spellings - so this was a false provenance claim rather
   than a wrong assertion, which is the kind this file exists to prevent.
4. **The same test dropped the row's MULTILINE flag** (`flags: 8`), where the sibling test one
   method above maps the wave's flags properly. Answer unaffected; provenance wrong again.
5. **Two of this sitting's new citations reintroduced a stale line number the repo already records
   as known-wrong**: `_regex.c:14545` is a `TRACE` call, and the reversed and forward slice writes
   are `:14553` and `:14555`. Three of the five occurrences had already been found and fixed in the
   working tree while the review ran - the same defect, found independently - and the review caught
   the two left. The pre-existing `:14545` elsewhere in the repo is the carried debt STATE.md lists
   and was not touched.
6. **The new `Reason` called row 38101 "a single `search`"**, which reads as its operation; the row
   is a `subf` whose recorded outcome is an `IndexError`, and the single `search` is a DOOR that
   entry puts to it. Reworded to say which.

The reviewer also confirmed, with reproductions, everything the sitting claimed about the
classifications themselves: both new entries fire and absorb no other row, the
`_reversedSkipInventedMatchRows` constant is byte-identical to the wave row including its
`pruneOutcome`, both probes reproduce every cell of their quoted tables, `tools/run-oracle.ps1:286`
is the `-SkipRecord` line cited, every other `_regex.c` and `Matcher.cs` citation checks out, both
gap tests go red on a one-value mutation, and the ratchet is GREEN at 6119/6119. It measured the
`partial-sliced` probe's 21 distinct cells with and without the prefilter and found none that moves,
which is why the plain `regex.compile` there is not a finding.

**Pass two, a first pass over the fix delta** (the rewritten test construction and its provenance
block, the probe comment, the reworded `Reason`, the corrected counts and citations, and
`.scratch/control.py`): **one finding raised, one
reproduced, one fixed.** Entry 24 quoted `_regex.c:18436-18446` for a block that starts at 18435 -
`:18436` is the second line of a two-line comment sentence - and said `text_start = 0` sits "three
lines under" it where the comment closes at 18437 and the assignment is at 18442, so it is five. Both
corrected in all four places they appear. The reviewer additionally derived the 33 / 7 / 23 / 10
split cell by cell and both directional lists independently, ran all four controls and matched every
count in their docstring, checked the rewritten test's pattern and subject byte-for-byte against the
wave row (and confirmed `regex.M` is the only flag with value 8), watched the test go red on a
mutation, verified the six-character and literal ZWJ spellings and both flag settings give the same
four spans, and confirmed the remaining `_regex.c` citations line by line, including the three that
are line-wrapped in the source. It left the tree byte-for-byte as it found it.

**And the independent verifier ran** (amendment 16 limb (d)), a fresh agent briefed with nothing but
this tree - and with `docs/VERIFICATION.md`'s do-not-use-git clause, which sitting 9 paid for.
It re-ran every claim the notes, the ledger sub-section, entry 24 and DECISIONS make. **Four came
back DIFFERENT and all four are fixed above; three came back COULD NOT RUN and all three are
annotated in place; everything else was CONFIRMED.**

**DIFFERENT, and the first is the one that mattered.**

1. **The baseline had not actually been updated.** The notes claimed `baseline 6005 -> 6011` and
   STATE.md claimed `6011`, and `tests/parity-baseline.json` still read 6005 and was unmodified in
   the working tree - `check-ratchet.ps1 -UpdateBaseline` had simply not been run yet. It has been
   now: `Baseline updated: 6011 distinct passing test ids recorded.` A claim about a generated file
   that nobody had generated is exactly what limb (d) is for.
2. **One `_regex.c:14545` this sitting added survived the pass-one fix**, in the second new gap test
   (`A_reversed_search_of_a_skip_finds_nothing_where_pruning_alone_finds_nothing`). The earlier fix
   pass had found five and fixed four. Fixed; the `:14545` still in this repo is all pre-existing,
   which is the carried debt STATE.md lists.
3. **"`test_regex.py:4073-4112` is the whole of the reversed-partial coverage" is false** - there is
   at least one reversed partial outside it. The load-bearing half holds and is now what the text
   says, measured rather than asserted: **upstream's suite makes 72 `partial=True` calls and not one
   of them passes a `pos`**.
4. **"an engine change across some thirty sites" was upstream's number applied to this port.**
   Measured: **38** sites in `_regex.c` ask `text_pos <= state->text_start`, and **9** in this port's
   `Engine/Matcher.cs` mirror them. Both figures are in entry 24 now. (The related "about thirty
   more" after the eight cited handlers is exact: 38 minus 8.)

A fifth, found by the verifier and fixed with them: **`:8371-8382` starts one helper too late.**
There are three reversed `search_start_STRING*_REV` helpers and they span `:8335-8382` - `_FLD_REV`
at `:8335`, `_IGN_REV` at `:8361`, `_REV` at `:8373`.

**COULD NOT RUN, three, none of them a gap in the measurement.** The gate table's `before` column and
the default wave's figures, because a later run overwrites `report-<seed>.txt` - the `before` column
is sitting 9's own recorded `after` row, quoted, and the default wave was captured to `.scratch`
before the gate ran, which is also what proves the run-before-the-gate ordering was kept. And
"before the changes the same file gave expected 0, diverge 8", which would need reverting the
sitting's uncommitted work.

**One COULD NOT RUN that was a real defect in the text, and it is fixed:** the verifier looked for
row 38101 in the on-disk 6000-row wave and found a `boundaries` row there. The number belongs to the
2000-row wave that entry was judged from; both places that cite it now say so and point at the row
constant in `ExpectedDivergences.cs` that holds it.

**CONFIRMED:** the ratchet (GREEN, 6119 / 6119 / 0 skipped, 6011 distinct ids); the eight-row replay
and which entry classifies which row and that neither absorbs any other; all fifteen gate figures,
derived from the reports and waves still on disk; the six diverging rows in those reports being
row-for-row the six named as still owed; every cell of both judged rows' tables; the 33 cells in 7
blocks, 23 agree, 10 differ split AND both directional lists, derived cell by cell independently;
all four controls, each to the exact `expected` / `diverge` / `failed` triple, with `D` the one that
does not reclassify; every other `_regex.c`, `Matcher.cs`, `record-oracle.py` and `run-oracle.ps1`
citation; the `-SkipRecord` claim, reproduced empirically by running a five-row file and watching
the report name `eight-rows.jsonl`; `_reversedSkipInventedMatchRows` being byte-identical to
wave-20260915's row; both new gap tests going red on a mutation; and - under the owner's 2026-09-15
rule - **both new gap tests' expected values re-derived directly against regex 2026.9.10 with the
verifier's own prefilter-off harness rather than through this repo's probes**, every cell of both.

It used no git undo command and left the tree byte-for-byte as it found it, checking
`ExpectedDivergences.cs` by md5 before and after every one of the four controls.

### Still owed on S52 after this sitting

- **The six remaining gate divergences.** Five are `(*SKIP)` rows that sitting 8's table still
  describes correctly (seed 7 74413 and 76160, seed 4242 76778 and 77119) plus seed 7 75921, which
  sitting 9 measured fully and deliberately did not pin; and the sixth is seed 20260915 104366, the
  empty-slice partial measured above, which needs the owner's ruling before anything is done with
  it. Regenerate with `pwsh -File tools/run-oracle.ps1 -Count 6000` then
  `python tools/probes/gate-divergence-doors.py`.
- **For the owner**: whether the reversed run-out should read `slice_start` rather than
  `text_start`. If yes it is an engine slice; if no, row 104366 is pinned as upstream's and this
  port's own greedy cell needs its own look.
- The **timeout rows** generator (scope item, depends on S51's `timeout` comparison) - untouched.
- The **20-seed sweep run**; `tools/sweep-seeds.ps1` and the `oracle.yml` cron both exist, the run
  does not.
- The long generators remain **off** the default `-Generator` list, for sitting 6's reason.
- The `pos`/`endpos` versus `codepointSlice` ambiguity sitting 8 narrowed is still open, and is
  still a wave-format change wanting its own slice.
- Sitting 8's Control A's `-SkipRecord`, and the three prefilter-on cases in
  `upstream-reversed-overlapped-skip.py`, both above.

---

## Sitting 11 (2026-09-15) - CHECKPOINT, landed and verified by sitting 13

Sitting 11 judged three of the six remaining gate rows, measured a fourth and deliberately left it
unjudged, and ran the seed sweep for the first time. **It was killed before it committed**, and
sitting 12 was killed at 16:12 before it committed either; the orchestrator stashed the work and
sitting 13 applied it, re-derived every claim in it and committed it. This section is sitting 11's
finding; the sitting 13 section below says which of its claims survived measurement and which did
not. No engine code changed, and no new gap test was written: all three rows joined families that
already carry a pinning test.

### The three rows, and the one line that classifies each

`(*PRUNE)` answers this port's answer on all three, which is the ordinary case on a scan and judges
nothing on its own. What each row needed is one further line, and in each case the family it joins
already had that line written down.

**Seed 7 row 74413 -> `end-of-line-reads-a-skip-moved-slice`.** A reversed `subf`, MULTILINE.
Upstream replaces once over a span ENDING AT codepoint 3, where its own `$` is true at 4 and 5
alone. The family's own control - `$` spelled out as what `$` is defined to be - gives this port's
`sub 0`, as do `(*PRUNE)` and the deleted verb:

```
as drawn            ('-}<U+10428>\r\n', 1)             <- upstream
$ spelled out       ('<U+10428><U+10428>a\r\n', 0)     <- ours
(*SKIP)->(*PRUNE)   ('<U+10428><U+10428>a\r\n', 0)     <- ours
verb deleted        ('<U+10428><U+10428>a\r\n', 0)     <- ours
(?w)$               ('-}<U+10428>\r\n', 1)             <- upstream: the twin CANNOT isolate here
```

`(?w)$` is true at [3, 5] where `$` is true at [4, 5], so the phantom end 3 is a line end the twin
would create anyway. That is the second row on which the `(?w)` control cannot run, and it is
exactly the condition the entry already states for row 24224 rather than a new limitation.

**Seed 4242 rows 76778 and 77119 -> `bestmatch-walk-truncated-by-a-skip`, and they are the first
rows a GENERATOR has ever drawn into that family** - its five existing rows were all authored by
S48's hunt over an 11,340-shape alphabet. What classifies both is the `(?b)`-FREE PAIR, which is
one line and not a reading:

```
row 76778   a REVERSED anchored `match`, (?b) + (*SKIP)
  (?b) + (*SKIP)        (0, 8) one deletion   <- upstream
  (?b) + (*PRUNE)       (1, 8) NO errors      <- ours
  no (?b) + (*SKIP)     (0, 8) one deletion
  no (?b) + (*PRUNE)    (0, 8) one deletion

row 77119   a forward overlapped `finditer`, (?b)(?e) + (*SKIP)
  (?b)(?e) + (*SKIP)    NO MATCH AT ALL       <- upstream
  (?b)(?e) + (*PRUNE)   (0, 5) one substitution  <- ours
  verb deleted          (0, 5) one substitution
  no (?b) + (*SKIP)     (0, 5) one substitution
```

**With `(?b)` deleted the two verbs answer identically, and with `(?b)` present they do not.** So
the pruning `(*SKIP)` does - the thing it shares with `(*PRUNE)`, and the thing it is entitled to do
- is not what moves either row; a verb that only matters while a `(?b)` walk is running is a verb
acting on the walk, which is this entry's whole mechanism. Row 76778 is the sharpest statement of
the family anywhere in the file: `(?b)`'s entire promise is to improve on the non-best answer, and
with the verb present it improves by NOTHING - the drawn line and the `(?b)`-free line are the same
one-deletion match - while its own `(*PRUNE)` spelling under `(?b)` finds a zero-error one.

Row 77119's judged answer is the first in that entry that CARRIES an error, so the array's remark
that "every one is a perfect match" was corrected rather than left to rot. What the family promises
is the fewest errors among the matches that exist, not zero of them.

### The row that resembles the family and is NOT filed under it

**Seed 7 row 76160**, a `(?b)` `split` carrying a `(*SKIP)`, has the same fingerprint at a glance -
`(?b)`, a verb, and the verb-free spelling landing on a different answer from the drawn one - and
the family's OWN DOOR rules it out. Two measurements:

- **Its `(*PRUNE)` spelling does not terminate.** Sitting 8 gave it 5 seconds, sitting 11 gave it
  300, and sitting 13 gave it 300 again on the committed probe and got the same
  `TimeoutError: regex timed out`. So the one control that separates the moved bound from the
  pruning cannot be run on this row at all.
- **The anchored door that stands in for it points the OTHER WAY.** On row 1 of the family,
  upstream's own `match` at the candidate the walk never reached finds a PERFECT match where its
  `search` found a one-error one. Here upstream's own `match(4, 7)` on the drawn object costs
  `(1, 0, 1)` - two errors - where this port's own unanchored answer is (4, 7) with none. The
  candidate the truncated walk is supposed to have missed is WORSE than the answer upstream gave,
  which is the opposite of the family's signature.

And this port's own two answers differ in the same way: its unanchored search gives (4, 7) with no
errors and its own `MatchAtStart(4, ...)` gives (4, 7) with `(1, 0, 1)` and different groups -
`g1=(6, 6) g2=(6, 7)` where the search gives `g1=(5, 5) g2=(5, 6)` - which is upstream's anchored
answer span for span and group for group. So both engines agree completely on the anchored question
and disagree only on the scan, and nothing either of them says settles which scan is right.

It stays on the gate, unjudged, with the measurement recorded. Filing it on the resemblance would
have been the guess this file already spends three paragraphs on.

### One thing found while measuring it, worth three lines and no more

Row 76160 with `(?b)` deleted, asked as a single `search`, reports a change POSITION that is a
pointer-sized integer in a seven-character subject, where the same ablation with `(*PRUNE)` reports
an in-range one. It is an ablation rather than the drawn row and nothing here rests on it, but it is
a fresh symptom of ledger entry 11's change-stack pollution and the first with an out-of-range value
rather than a merely wrong one. The value is a machine address and differs per run, so what
reproduces is that it is out of range, not the number.

### Row 104366 is handed to S52c, as the orchestrator directed

Sitting 10 measured the empty-slice reversed partial to the bottom and left it for the owner. It is
now scope item 7 of `S52c-metamorphic-invariants.md` with a "Done when" box, and the reason it
belongs there rather than in a gate triage is the whole of sitting 10's finding: **neither engine is
self-consistent** - 33 cells, 23 agree, 10 differ, and the 10 split both ways - so the oracle's "do
they agree" question has no useful answer on it and only an invariant can separate right from wrong.
The owner's ruling on `slice_start` against `text_start` is still open and S52c should not wait for
it: the invariant is worth having either way, and if it fires on both engines that is the finding.

### The seed sweep ran for the first time, and it is RED at every seed

`tools/sweep-seeds.ps1` and `oracle.yml`'s Thursday cron (`0 3 * * 4`, the `sweep` job) were both
built by an earlier sitting; the RUN was still owed. Eight seeds at 2,000 rows a generator in
Release, 336,000 rows:

```
pwsh -File tools/sweep-seeds.ps1 -SeedCount 8 -Count 2000 -MasterSeed 20260915
```

**`-MasterSeed 20260915` is what makes this reproducible**: the eight seeds are drawn from it and
printed, so the same command re-draws the same list. Every one of the eight is RED, 37 diverging
rows in all, and the log (`TestResults/oracle/sweep.jsonl`, one JSON object a seed) holds the tally
and the per-generator split for each:

| seed | diverging | by generator |
|---|---:|---|
| 655924813 | 4 | interactions 2, partial 1, fuzzy 1 |
| 523701539 | 3 | interactions 1, verbs 1, fuzzy 1 |
| 31256406 | 3 | interactions 1, verbs 1, fuzzy 1 |
| 678716286 | 7 | interactions 3, verbs 1, conditionals 1, partial-sliced 1, partial 1 |
| 432860808 | 6 | interactions 4, verbs 2 |
| 793244924 | 7 | interactions 5, partial 1, fuzzy 1 |
| 613157220 | 2 | interactions 2 |
| 520159961 | 5 | interactions 3, partial 1, partial-sliced 1 |
| **total** | **37** | interactions 21, verbs 5, fuzzy 4, partial 4, partial-sliced 2, conditionals 1 |

Each red seed kept its own wave and report under `TestResults/oracle/sweep-<seed>/`, so the rows can
be triaged without re-running anything.

**The slice's own done-criterion is that no divergence is left unjudged, and a sweep at eight fresh
seeds is precisely the instrument for producing new ones.** Sitting 11's recommendation, which the
owner has to rule on: **S52 should be SPLIT** - closed on the hardening tooling and generators it
delivered, with the sweep's triage given a slice of its own.

### And the sweep says something about the CI job that nobody could know before it ran

`oracle.yml`'s Thursday job sweeps six fresh seeds every week at the same 2,000 rows, master seed
the run date (`.github/workflows/oracle.yml:191`, `COUNT=6` when the input is empty). The rate
measured here is **4.6 diverging rows a seed** (37 over 8), so that job will be **RED every Thursday
with something like 28 unjudged rows**, none of which can be judged inside a week. (Sitting 11 wrote
"three or four each time", which is the PER-SEED rate applied to a six-seed run - the blind review
caught it.) A weekly red that nobody can clear is a weekly red that stops being read, which is worse
than no job at all.

This is not an argument for deleting it - the sweep is doing exactly what it was built to do, and
what it has found is real. It is an argument that **the job needs a verdict that distinguishes "a
new seed drew a known family" from "a new seed drew something nobody has seen"**, and that belongs
to whoever owns the sweep's triage. Recorded rather than acted on: changing the CI verdict rule is
not this sitting's scope and should not be decided by the sitting that happens to have run the first
sweep.

---

## Sitting 13 (2026-09-15) - the verification sittings 11 and 12 never ran

Sitting 11 was killed before it committed and sitting 12 was killed at 16:12 before it committed;
what survived was a stash of four files - `ExpectedDivergences.cs`, `LEDGER.md`, `DECISIONS.md` and
the `S52c` slice file - and a scratch directory. **None of it had been through a ratchet, a review or
a verifier, and every measurement it quoted had been made by a script in `.scratch/` that was never
committed.** The orchestrator's instruction was to apply it and treat every claim in it as a
hypothesis. That is this sitting.

### What was re-derived, and what it cost to make it re-derivable

The hypotheses were all upstream-side ablations, so they could be put to upstream again directly. Two
probes now carry them, and **the point of committing them is that a probe the next reader cannot run
is not evidence** - the verifier's own rule:

- **`tools/probes/upstream-gate-drawn-skip-rows.py`**, with its rows in
  `tools/probes/gate-drawn-skip-rows.jsonl` beside it. It reuses `gate-divergence-doors.py`'s
  `compile_row`/`answer` rather than re-implementing "ask upstream this row's own operation", and it
  reads its four rows from the committed file rather than from `wave-<seed>.jsonl`, so it does not
  need a ten-minute gate run and a later run cannot overwrite its input. Sitting 11's scratch version
  read the wave.
- **`tools/probes/port-gate-drawn-skip-rows.ps1`**, this port's half for row 76160 - the only
  port-side claim in the ledger that the wave replay does not already show, because a `split` renders
  as parts and hides both the span and the error counts.

Every cell of every table in the sitting 11 section above was re-measured from those two probes
against regex 2026.9.10 and **all of them reproduce**. The rows file doubles as the replay control:
`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/gate-drawn-skip-rows.jsonl`.

### The gate rows themselves, re-run through the comparer

`pwsh -File tools/run-oracle.ps1 -Rows .scratch/six-rows.jsonl` - the six rows the gate still had
after sitting 10, written out of the waves by seed and row number:

```
agree 0  unsupported 0  expected 3  timeout 0  resource 0  diverge 3  of 6 rows
```

and the three that are now EXPECTED are exactly 74413 (`end-of-line-reads-a-skip-moved-slice`),
76778 and 77119 (`bestmatch-walk-truncated-by-a-skip`), each classified by the entry sitting 11
claimed and by no other. The three still diverging are 75921 (sitting 9's, measured and deliberately
unpinned), 76160 and 104366. **So the gate is 6 -> 3 and the claim survives measurement.**

### One real defect in the stash, and it was in the evidence

**`bestmatch-walk-truncated-by-a-skip` claimed to be "re-recordable in full" from
`tools/probes/bestmatch-walk-truncated-rows.jsonl`, and after sitting 11 it was not.** The entry grew
from five rows to seven; the rows file still held six (the five plus the family's agreeing control).
Anyone following that instruction would have re-recorded five of the seven and found nothing wrong.
The two drawn rows are now in the file at positions 6 and 7, so the file's order IS the entry's
order and the agreeing control moves to eighth, and the whole file replays

```
pwsh -File tools/run-oracle.ps1 -Rows tools/probes/bestmatch-walk-truncated-rows.jsonl
agree 1  unsupported 0  expected 7  timeout 0  resource 0  diverge 0  of 8 rows
```

which is the stronger check of the two rows sitting 11 added: their upstream answers are RE-RECORDED
here rather than read out of the `Example` copy, and they still diverge and still classify. The
remark's reason for the eighth row was corrected too - it says the five rows were all perfect
matches, and row 7 now carries an error of its own, so the non-degeneracy guard is no longer the only
thing keeping it.

### Two claims narrowed rather than kept as written

- **The 300-second non-termination.** Sitting 11 wrote that row 76160's `(*PRUNE)` spelling "does not
  terminate at 300 seconds". Re-run here at 300 seconds through the committed probe
  (`python tools/probes/upstream-gate-drawn-skip-rows.py 300`) it is the same
  `TimeoutError: regex timed out`, so the claim is CONFIRMED - and the probe takes the timeout as an
  argument rather than hard-coding five minutes, because the other three rows have to stay
  measurable while this one hangs.
- **The garbage change position.** Sitting 11 quoted the literal integer
  `substitutions=[1, 140726012121968]`. It is a machine address: the probe here printed
  `[1, 140735613342576]` for the same cell and the independent verifier `[1, 140735381672816]`, three
  values from three processes. (The verifier's two runs inside ONE session gave the same value twice,
  so it is stable within a process and not across them - "differs per run" was too strong and this is
  what replaced it.) Quoting the integer would have sent the next reader looking for a number they
  will never see; what reproduces is that the position is outside the seven-character subject, and
  that is what the text says now.

### The negative controls, run against the code committed here

No control mutates the engine, because no engine code changed. What there is to control is the
KEYING of the three rows sitting 11 judged: each entry classifies a row by the row's question AND by
this port's judged answer, and an answer that is wrong classifies nothing while a predicate that is
too wide classifies everything. Both were run last, after the final edit, over
`.scratch/six-rows.jsonl` - the six gate rows written out of the waves by seed and row number by
`.scratch/s11-rowsfile.py` - with `pwsh -File tools/run-oracle.ps1 -Rows <file>`. **No
`-SkipRecord`**: with it the runner ignores the file and eats whatever wave is on disk.
`.scratch/s13-control.py break-A` / `restore-A` (and `-B`) applies and reverts each by exact
re-edit, never by git, and `ExpectedDivergences.cs` has the same md5 before and after both.

> **Control A, the `end-of-line-reads-a-skip-moved-slice` answer**: in
> `_endOfLineReadsMovedSliceOurs`, change the third and last entry of the array - seed 7 row 74413's
> judged answer, `ExpectedDivergences.cs:884`, the second of the two lines beginning `sub 0` - from
> `sub 0` to `sub 1`, leaving its quoted subject exactly as it is. (That subject is written in the
> file as `\uXXXX` escapes and is deliberately NOT quoted here: every editing tool in this harness
> RESOLVES those escapes, which is how the first draft of this paragraph came to name a string that
> appears nowhere in the file. `.scratch/s13-control.py break-A` applied it here and refuses unless
> the line matches exactly once, but `.scratch` is gitignored: the edit named above is the record.)
> Result: **expected 2, diverge 4 of 6**; the row that stops being classified is 74413 and only
> 74413, the two `bestmatch` rows still classifying.
>
> **Control B, the `bestmatch-walk-truncated-by-a-skip` answer**: in
> `_bestmatchWalkTruncatedOurs`, change the seventh entry - seed 4242 row 77119's judged answer -
> from `"matches 1 | match 0:(0,5)[(0,5)] last=-1/- fuzzy=(1,0,0)[s:0][i:][d:]"` to `0:(0,6)[(0,6)]`.
> Result: **expected 2, diverge 4 of 6**; the row that stops being classified is 77119 and only
> 77119.
>
> Unbroken, before and after both: **expected 3, diverge 3 of 6**.

The second seed these controls ask for does not apply - they run on six explicit rows rather than on
a generator, so there is no seed to vary. What stands in for it is the gate itself, which drew the
three judged rows at two different seeds, and the family rows file replay above, which re-records
the two `bestmatch` rows from upstream rather than reading the `Example` copy.

### Numbers

- Ratchet **GREEN**, **6119 / 6119 / 0 skipped**, **6011 distinct ids**, baseline **6011** -
  unchanged, as it must be: no `.cs` file outside `FuzzyRegex.OracleTests` was touched and no test
  was added.
- `dotnet build tests/FuzzyRegex.OracleTests -c Release` clean, 0 warnings - sitting 3's lesson, that
  the parity ratchet builds `tests/FuzzyRegex.Tests` alone and cannot see a broken oracle project.
- The six-row replay above: **expected 3, diverge 3 of 6**.
- The family rows file replay above: **expected 7, agree 1, diverge 0 of 8**.
- The seed sweep's eight seeds and 37 rows, read back out of `TestResults/oracle/sweep.jsonl` rather
  than transcribed from a terminal.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results.**

**Pass one, over the whole diff: five findings raised, five reproduced, five fixed.** None was a
defect in the port or in either judgement; all five were defects in the EVIDENCE, and two of them
were mine rather than sitting 11's.

1. **The weekly-CI prediction was the PER-SEED rate applied to a six-seed job.** Sitting 11 wrote
   that `oracle.yml`'s Thursday sweep would be red "with three or four unjudged rows each time"; the
   measured rate is 4.6 a seed over eight seeds and the job runs six, so the figure is about 28 a
   run. The reviewer derived it from `sweep.jsonl` and from the job's own `COUNT=6` default.
2. **Control A named a string that appears nowhere in the file.** The judged answer it says to break
   is written in `ExpectedDivergences.cs` as `\uXXXX` escapes, and the editing tool RESOLVED them
   when this file was written - so the quoted string had a real astral pair and a real line break in
   it. This is the standing lesson STATE.md already carries, walked into anyway. The control now
   names the file, the line and the one-word edit instead of quoting the string.
3. **The new probe's own comment said sitting 8 gave row 76160's `(*PRUNE)` line 300 seconds.** It
   gave it five; the three other copies of that claim in this diff all say so.
4. **The ledger said "300 seconds at two sittings on two days".** Both 300-second runs are
   2026-09-15.
5. **`_endOfLineReadsMovedSliceOurs`'s remark was widened from "Both are" to "Every one is" without
   widening its citation.** It cited `upstream-skip-carried-slice-doors.py`, which holds the first
   two rows and not the third. The citation is now split between the two probes that actually hold
   each row.

The reviewer also reproduced, from the committed tree, every cell of both sitting-11 tables, the
300-second timeout, the port half's two answers, all four replays, the ratchet and the oracle build;
confirmed the four probe rows are byte-identical in their question fields to the wave rows they were
taken from and that the family rows file's rows 6 and 7 match the entry's `Example` constant; checked
that `interactions` is not a prefilter-free generator, so the probe asks the same way the recorder
did; and found no UTF-16-versus-codepoint error, which was the first thing on its hunt list.

**Pass two, a first pass over the fix delta: three findings raised, three reproduced, three fixed.**
All three were in the repairs themselves, and the first is the same class of defect as the finding it
was repairing. (1) Control A's new line citation was two lines out - fixing the remark above the
array had moved it - so it pointed at the FIRST entry, and the "from `sub 0`" instruction is
ambiguous between two lines of the array; following it literally would have broken a row that is not
in the control's input and measured nothing. It now names line 884, says it is the third and last
entry and says which of the two `sub 0` lines it is. (2) The parenthetical that replaced the quoted
string miscounted the escapes ("five", where the subject is six `\uXXXX` escapes and a literal `a`);
the count is gone rather than corrected, because it bought nothing. (3) `oracle.yml:190` is the input
read, not the `COUNT=6` default, which is `:191`.

Pass two independently re-derived the sweep arithmetic (4+3+3+7+6+7+2+5 = 37, 37/8 = 4.625, times six
= 27.75), the cron expression and row count, the three timings, and the split probe citation, and
found those clean.

**No third pass.** The fixes after pass two are three line-number and wording corrections inside text
pass two had just read, and nothing in them is a new claim.

**And the independent verifier ran** (amendment 16 limb (d)), a fresh agent briefed with nothing but
this tree and `docs/VERIFICATION.md`'s do-not-use-git clause. It re-ran every claim these two
sections, the ledger sub-section, DECISIONS and STATE.md make. **One came back DIFFERENT and is
fixed above; two came back COULD NOT RUN and both are annotated in place; everything else was
CONFIRMED.**

**DIFFERENT.** "The garbage change position differs per run" was too strong: the verifier's two runs
inside one session printed the same address twice, and a third value across processes. Stated now as
what it is - stable within a process, not across them - with all three values named.

**COULD NOT RUN, two, neither a gap in the measurement.** The sweep itself, because re-running
`sweep-seeds.ps1` would overwrite the per-seed waves the tree cites as kept evidence and its
resumption key would skip all eight seeds anyway; instead the verifier read every tally and split
back out of `sweep.jsonl` and **re-derived the eight drawn seeds independently** from
`[System.Random]::new(20260915)`, getting the table's list in the table's order. And sitting 11's own
300-second run and the two killed sittings, because no artefact of either survives - sitting 13's
300-second run reproduces, which is what the text now rests on.

**CONFIRMED:** every cell of both sitting-11 tables, re-derived TWICE - through the committed probe
and by importing `regex 2026.9.10` directly; the `$` and `(?w)$` position lists and the drawn span;
the 300-second timeout; the anchored door on both engines, group for group; all four replays
including which entry classifies which row and which three rows are still diverging, with each of the
six matched back to its wave row by question fields rather than assumed; both controls, to the exact
`expected`/`diverge` pair and the exact row that stops classifying, with the file's md5 identical
after each restore; the ratchet and the oracle build; the two entries' row constants being
field-for-field identical to the wave rows they were taken from, `pruneOutcome` and
`bestmatchFreeOutcome` included; the three judged answers being the rows' own `pruneOutcome`s; the
`_regex.c` citations `:14553`, `:14555`, `:7110` and `:17625`, the `_regex_core.py:506-510` dispatch,
`oracle.yml:191` and the Thursday cron, and `ExpectedDivergences.cs:884` as Control A's line; the
sweep arithmetic; that `upstream-skip-carried-slice-doors.py` really does not hold row 74413, which
is what the split citation says; that `interactions` is not prefilter-free, so the probe asks the way
the recorder did; and that no engine code changed and no test was added.

It used no git command but `status` and `diff`, reverted both controls with the slice's own script,
and left the tree byte-identical.

---

## Sitting 14 (2026-09-15) - CHECKPOINT, not closed

STATE.md left sitting 14 two things: a question for the owner (S52 should be SPLIT - its
done-criterion is "no unjudged row" and the seed sweep it delivered is the instrument for making new
ones, so the criterion and the deliverable fight each other) and a list of work that does not depend
on that ruling. **The ruling has not come, so nothing was judged this sitting.** What was taken
instead is the **last untouched item in the slice's own Scope list: the timeout rows.** They needed
S51's per-call budget, which landed, and they need no ruling from anyone.

### What a timeout row is, and why the old ones could not be compared

A recorded `timeout` outcome already existed (S40a) and the consumer SKIPPED it, for a reason the
docstrings state plainly: the recorder's blanket ten seconds is a fact about the recording machine's
wall clock, so a row that needed eleven seconds and a row that needed nine would record differently
on two runs and neither says anything about either engine. Filing it as a divergence fails the run on
upstream's slowness; filing it as agreement lets a port that also hangs score as parity.

**The scope bullet asks for the one case where that objection does not apply**: shapes measured
catastrophic on BOTH engines, under a budget they blow through by a wide margin. Then "the call
raised rather than running past its budget" is a property of the engine, and it is worth comparing.
The whole sitting is that margin and the plumbing for it.

### The family is much narrower than the textbook says, and that is the finding

Measured before any code was written, on regex 2026.9.10 and this port at Release. **Upstream is NOT
vulnerable to the textbook catastrophic patterns.** `(a+)+$`, `(a*)*$`, `(.*,)*z`, `(a+)+\1$` and
`(?=(a+)+$)a` all answer in MILLISECONDS at every length tried - a nested repeat over a
single-character body collapses. What does blow up is an **ambiguous alternation under a repeat**,
two branches that can match the same text, and the ambiguity has to survive compilation:
`(?:ab|a)+$` and `(?:[ab]|a)+$` are both fast, so is `(?i)(a|A)+$`, and so is the reversed
`(?r)(a|a)+^`.

That is why `TIMEOUT_SHAPES` is a curated table of ten and why `_generate_timeout` draws NO FLAGS.
A generator that composed this family with the flag alphabet the other generators use would draw
rows that answer - and a timeout row that answers is a divergence. The nine rejected shapes are in
the probe as data, not as a comment: `timeout-row-margin.py --rejected` runs them and exits 1 if one
of them becomes catastrophic.

### The margin, measured on both engines

```
python tools/probes/timeout-row-margin.py                 # 10 shapes, upstream
python tools/probes/timeout-row-margin.py --operations    # 10 shapes x 8 operations, upstream
python tools/probes/timeout-row-margin.py --knee          # where each shape starts blowing up
python tools/probes/timeout-row-margin.py --rejected      # the nine that are NOT in the family
pwsh -File tools/probes/timeout-row-margin.ps1            # the same, this port, Release
pwsh -File tools/probes/timeout-row-margin.ps1 -Operations
```

- **80 of 80 cells still running at 20x the budget on upstream, and 80 of 80 on this port** - ten
  shapes against all eight operations, at the generator's own shortest subject of 40 characters,
  against the 0.25s its rows carry. 160 of 160 cells across the two engines.
- **Knees between 24 and 36**, the worst being `(?:a|aa)+$` at 36, which is what sets
  `MIN_TIMEOUT_REPEATS = 40`. The floor is the slowest-to-blow-up shape, not the average.
- **All nine rejected shapes answer in 0ms.**

The operation is drawn rather than fixed at `search` because that is where the value is: `Replace`,
`Split`, `Matches` and `Match` each hand the budget to a DIFFERENT loop in this port, and a loop
that never polls it is a hang nothing else in the suite can see.

### `count` is a ceiling for this generator, and only for this one

Its question space is finite - ten shapes against eight operations is eighty questions, and the
subject length moves nothing but how certain the timeout is - so `_generate_timeout` enumerates the
grid and shuffles it instead of sampling. Any `--count` of 80 or more draws every cell exactly once.

This is both cheaper and STRONGER than sampling. Cheaper because every row of this generator spends
its whole budget on BOTH engines by construction: the capped grid records in **20.3 seconds** a seed,
so a row costs 0.254s and an uncapped `--count 300` would be **about 76 seconds**, and a 2000-row
sweep would have spent eight minutes a seed re-asking eighty questions twenty-five times each.
Stronger because a random draw of eighty from eighty cells with replacement misses about a third of
them, and the cells are the point.

**The 76 seconds is a derivation, and the 75 this first claimed was a direct measurement that
NOBODY CAN NOW REPRODUCE** - it was taken before the cap existed, and the cap is in the committed
code, so re-running `--count 300` gives the capped 80 rows. The independent verifier reported it
COULD NOT RUN and derived 76 from the 20.3 it could time. The derivation is what stands, because the
measurement's evidence is gone; to re-take it directly, change `grid[:count]` back to a `for _ in
range(count)` draw.

### It is ON the default generator list

Unlike the four long generators, and measured rather than assumed: `pwsh -File
tools/run-oracle.ps1 -Generator timeout -Count 300 -Seeds 7,4242,20260915` gives
`agree 80 unsupported 0 expected 0 timeout 0 resource 0 diverge 0 of 80` at **every one of the
three**, and the full default wave is GREEN at all three with it in (see Numbers).

It costs **20.3 seconds a seed to record** and about as long again to consume - the two engines each
spend the same 0.25s a row - so call it 40 seconds a seed, of which only the first half is timed.
That is noise at the 6000-row gate and at the sweep, because the cap means it does not grow with
`-Count`.

**A run of this generator ALONE never reports `Oracle: GREEN`, and that is nothing to do with the
generator.** `Our_own_change_positions_always_agree_with_our_own_counts` refuses a wave holding no
fuzzy match (`OracleWaveTests.cs:192`) and no single-generator wave has one; `-Generator literals`
fails identically. Read the `agree ... diverge` line. The independent verifier caught this sitting's
notes calling such a run "GREEN" and reported it DIFFERENT.

### What changed, in four files

- **`tools/record-oracle.py`**: a per-row `timeout` field, threaded into the PRIMARY question only.
  That is sufficient rather than sloppy - every second fact (`scanMatches`, `leakFreeFuzzy`,
  `searchOnlyPartial`, `anchoredScan`) is reached only AFTER upstream answered, because a
  `TimeoutError` returns `timed_out()` from the call itself, so a row that runs out of its budget
  reaches none of them and none of them can spend ten seconds on a row whose budget is a quarter of
  one. Plus `TIMEOUT_SHAPES`, `_generate_timeout` and the three constants.
- **`OracleWave.cs`**: `OracleRow.Timeout`, parsed from the row.
- **`OracleComparer.cs`**: three edits. The wave's skip is now conditional on the row having NO
  budget; `Run(row)` passes the row's own budget where it has one; and `Compare` decides a budgeted
  row before the blanket rule, agreeing only with a `RegexMatchTimeoutException` raised WHILE
  MATCHING.
- **`tools/run-oracle.ps1`**: `timeout` on the default list, and the help paragraph saying why it is
  capped.

**A port that ANSWERS a timeout row is a divergence, deliberately.** Upstream could not finish the
shape in twenty times the budget; a port that finishes it has either stopped being the same engine on
it - so the row no longer tests the deadline, which is the "a generator that never reaches the path
it was written for" failure this slice hunts - or answered something upstream never got to check.
Phase 7 optimisation reddening one of these rows is the CORRECT outcome and the signal to redraw the
family, not a reason to soften the rule. Said out loud in `OracleWaveTests.cs` so a later slice does
not quietly weaken it.

### Old waves still skip, checked on real files rather than reasoned about

`OracleRow.Timeout` is the ONLY discriminator, so the question "does this change reinterpret a row
recorded before it existed?" is answerable by reading the waves on disk. Across every kept wave in
`TestResults/oracle/`, the split is total: **seven** historical waves hold 1 to 3 `timeout` rows each
- twelve rows between them - and **not one carries a budget field**, so every one of them is skipped
exactly as before, while every `timeout`-generator wave holds 80 and **all 80 carry one**. (The
independent verifier corrected the count: this first said "eight", and counting the `sweep-<seed>/`
copies as separate files gives thirteen rather than either. The number of `timeout`-generator waves on
disk is not a fact about the change at all - it is whatever the last runs left - so it is stated as a
property of each wave instead.)

### Numbers

- Ratchet **GREEN**, **6119 / 6119 / 0 skipped**, **6011 distinct ids**, baseline **6011** -
  unchanged, and it must be: the one new test is in `FuzzyRegex.OracleTests`, which the ratchet does
  not build. That project goes 23 tests to **24**.
- `dotnet build tests/FuzzyRegex.OracleTests -c Release` clean, 0 warnings.
- Tool tests (Pester, `tools/tests`) **81 / 81**.
- **Default wave, Release, three seeds, 300 rows a generator: GREEN at every one**, 6,380 rows a seed
  (6,300 + the capped 80):
  seed 7 `agree 6366 expected 8 timeout 2 resource 4 diverge 0`;
  seed 4242 `agree 6373 expected 2 timeout 0 resource 5 diverge 0`;
  seed 20260915 `agree 6373 expected 4 timeout 0 resource 3 diverge 0`.
- The `timeout` generator alone at the same three seeds: `agree 80 ... diverge 0 of 80` at each.
- Margin probes: **80 of 80 cells** at 20x the budget on upstream and **80 of 80** on this port.

### The negative controls, run last against the code committed here

Both were run AFTER the final code change, both at two seeds, and both were applied and reverted by
hand - `git status --porcelain` shows neither file as modified afterwards, which is the check that
they really went back.

> **Control A, `wrong-timeout-exception`**: in `src/FuzzyRegex/Engine/MatchLimits.cs`, the last line
> of `Cancelled` (`:56`), change
> ```
>             : new System.Text.RegularExpressions.RegexMatchTimeoutException(input, pattern, MatchTimeout);
> ```
> to
> ```
>             : new TimeoutException($"{input} {pattern} {MatchTimeout}");
> ```
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator timeout -Count 300 -Seeds 7`, 80 rows.
> Result: **agree 0, diverge 80 of 80**, against `agree 80, diverge 0 of 80` unbroken.
> Re-run at seed **31337**, which no part of this slice uses: **agree 0, diverge 80 of 80**.

That is the control for the COMPARISON: it proves a wave of these rows notices when this port stops
reporting a timeout the way upstream's contract says, on every one of the eighty cells rather than on
a lucky draw.

> **Control B, `family-drift`**: in `tools/record-oracle.py`, add `("nested-plus", r"(a+)+$"),` as an
> eleventh entry of `TIMEOUT_SHAPES`, directly after `("alt-backref", r"(a|a)+(\1)$"),`.
> Probe: `python tools/probes/timeout-row-margin.py`.
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator timeout -Count 300 -Seeds 7`.
> Result: the probe **exits 1** and prints
> `*** nested-plus FINISHED inside 5.0s - the family has drifted, redraw the table. ***`,
> while the wave's comparison is **agree 88, diverge 0 of 88** - NO DIVERGENCE.
> Re-run at seed **31337**: again **agree 88, diverge 0 of 88**.
>
> Not "GREEN": a `-Generator timeout` run's own VERDICT is always `Oracle: RED`, on the committed
> tree too, because `Our_own_change_positions_always_agree_with_our_own_counts` refuses any wave
> holding no fuzzy match (`OracleWaveTests.cs:192`) and a single-generator wave holds none - it
> reproduces identically on `-Generator literals`. The first draft of this control wrote "GREEN"
> and the independent verifier reported it DIFFERENT. **What the control is about is the
> comparison tally, and that is what it now says.**

**Control B is the one that changed something, and it is a finding about the harness rather than a
tick.** A shape that stops being catastrophic does not red the wave: its eight rows answer `nomatch`
on both engines and AGREE, so the gate every other slice reads reports 88 healthy rows while eight of
them test nothing at all. **The wave cannot police its own generator here; `timeout-row-margin.py`
is the only guard**, which is why it is a committed probe rather than a measurement, why the recorder
cites it, and why every one of its four modes now exits non-zero on its own bad news.

The second seed is recorded for both because both run on a GENERATOR draw rather than on explicit
rows, so there is a seed to vary. Both fired identically at the second seed, which is what says they
catch the fault rather than a coincidence of the first draw.

### Review

**One blind pass over the whole diff, dispatched inside the turn and read as a tool result. Findings
raised: four. Reproduced: four. Fixed: four.** None was a defect in the comparison or in the margin;
all four were defects in the harness or in the evidence.

1. **Every `timeout` row was tagged `"generator": "rows"`.** `_generate_timeout` was the only
   generator body that omitted the `"generator"` key, so `_record_row` fell back to the tag reserved
   for a hand-written `--rows` file. Not cosmetic: it is what a divergence block prints and what
   `_compile_upstream` keys `PREFILTER_FREE_GENERATORS` off. Reproduced by counting the tags in a
   recorded wave (80 of 80 said `rows`) and seen in Control A's own report line,
   `DIVERGE row 1 (rows) subf`.
2. **The test's recorded measurement was false.** Its comment said both engines were still running
   after 5s on all ten shapes at 32 characters; `(?:a|aa)+$` FINISHES at 32 in 1.19s, and the probe
   committed in the same change says so - its knee is 36. The test itself was never wrong (its own
   shape's knee is 24) and the wave was never affected (the generator draws 40 to 56), but the
   sentence would have been read as the family's measurement. It now states the generator's own
   length and says explicitly that the test's 32 is not a measurement of the family.
3. **`--knee` and `--rejected` exited 0 while printing their own failure.** Reproduced by planting a
   catastrophic shape in the `REJECTED` table and watching the probe print
   `*** ... IS catastrophic now ***` and exit 0. Every mode now returns its verdict.
4. **The port probe's operation columns were in the wrong order.** Its comment claimed
   "`ALL_OPERATIONS`, in its order" while the hard-coded list put `split` sixth where
   `ALL_OPERATIONS` has it eighth, so reading the port's column six against upstream's column six
   compared `split` with `finditer`. Both halves also truncated `finditer` and `finditer-overlapped`
   to the same heading. The list is now read out of the recorder's three tuples and both halves print
   `finditer-ovl`.

The reviewer also reproduced all eight of the sitting's claims from the committed tree, confirmed the
`timeout` field survives a `--rows` round trip, that all four `_CONTROLS` row builders use
`{**row, ...}` so a control answer inherits the budget, that the five surviving `ROW_TIMEOUT_SECONDS`
uses are all in second-fact helpers reachable only after upstream answered, that `MAX_TIMEOUT_REPEATS`
bounds the draw, and that `OracleRow` has one construction site so inserting `Timeout` before
`DefaultVersion` shifts nothing.

**A second pass over the unreviewed delta was NOT dispatched, and that is a judgement rather than a
deadline.** What the fixes created is three probe edits and two comments; the probe edits were then
put through their own controls (Control B is the `--rejected`/`--knee` exit-code fix's control, and
the column order was re-run on both halves and compared line by line), and two further defects of the
same class were found and fixed WITHOUT a reviewer - the ps1 probe's parse guards, which accepted a
partial table and a zero budget silently, and the missing `--rejected` line in the Python probe's
usage block. The delta is tooling that reports on itself and every claim in it is re-run by the
independent verifier below.

### One thing this sitting cost itself, worth carrying

**Do not build or edit while a wave is consuming.** A `dotnet build` launched while
`run-oracle.ps1`'s consumer was running left four wedged `dotnet` processes, and after that a build
that takes 11 seconds ran past seven minutes, `dotnet build-server shutdown` itself hung, and
`tools/find-lock-holder.ps1` correctly reported NO process holding the DLL - so the ordinary lock
diagnosis says nothing about this failure. Stopping the background tasks cleared it and the build
came back at 11 seconds. Cost: about twenty minutes and one abandoned three-seed wave.

### The independent verifier

A fresh agent (amendment 16 limb (d)), briefed with nothing but this tree and `docs/VERIFICATION.md`'s
do-not-use-git clause, re-ran every number these notes, the four DECISIONS entries and STATE.md state.
**Three came back DIFFERENT and all three are fixed above; two came back COULD NOT RUN and both are
annotated in place; everything else was CONFIRMED.**

**DIFFERENT, and the sharpest is the third.**

1. "The EIGHT historical waves hold 1 to 3 `timeout` rows each" - there are **seven**, twelve rows
   between them, and counting the `sweep-<seed>/` copies gives thirteen files rather than either.
2. "The THREE new `timeout`-generator waves" - five such files were on disk when it looked, two of
   them 88-row Control B leftovers. The count was never a fact about the change; it is whatever the
   last runs left, and the claim is now about each wave rather than about how many there are.
3. **Control B's result was written as "GREEN" and the run's own verdict is RED** - on the committed
   tree too, and for a reason unrelated to this generator: a single-generator wave holds no fuzzy
   match and `Our_own_change_positions_always_agree_with_our_own_counts` refuses one
   (`OracleWaveTests.cs:192`), identically on `-Generator literals`. The control is about the
   comparison tally, `agree 88 diverge 0 of 88`, and that is what it says now - here, in
   DECISIONS.md and in `run-oracle.ps1`'s help, which carried the same word.

**COULD NOT RUN, two, one of them a real loss.**

- **The 75-second uncapped recording is gone as a direct measurement.** It was taken before the cap
  went into the code, and the cap is in the committed code, so `--count 300` now records the capped
  80. What survives is the verifier's own 20.3s for the capped grid and the derivation from it, 76
  seconds; the notes now claim the derivation and say how to re-take the measurement.
- **"About 40 seconds a seed" was never timed as a whole** - only the 20.3s recording half. Stated as
  what it is: 20.3 timed, the consume half inferred from the two engines spending the same budget.

**CONFIRMED:** the ratchet (GREEN, 6119/6119/0 skipped, 6011 distinct ids, baseline 6011) and that
the diff adds exactly one `[Test]`, 23 to 24; the oracle Release build clean; the oracle tests 24/24;
Pester 81/81; both margin probes at the FULL 20x margin rather than a reduced one, 80 of 80 cells on
each engine, 160 of 160; the nine knees, worst `(?:a|aa)+$` at 36; all nine rejected shapes at 0ms;
`agree 80 diverge 0 of 80` at all three seeds; the default wave's three tallies exactly, 6,380 rows a
seed; the grid drawing 80 distinct cells with max repeat 1 at counts 80, 120 and 300 across six seeds,
every row carrying `"timeout": 0.25`, `"generator": "timeout"` and `flags 0`, subjects 41 to 57
characters; both controls at both seeds, to the exact tallies and the exact probe exit code and
message; that no historical `timeout` row carries a budget field; regex **2026.9.10** on every probe
run; and the constants read back out of the recorder - 10 shapes, 8 operations, 10 x 8 = 80 = the cap,
and 6380 = 6300 + 80 on every default wave.

It used no git command but `status` and `diff`, reverted both controls by hand, rebuilt clean after
each, deleted its own scratch, and left the tree byte-identical to the snapshot it started from.

---

## Sitting 15 (2026-09-15) - CHECKPOINT, and the owner's ruling arrived

**THE SPLIT QUESTION IS ANSWERED AND S52 IS NOT SPLIT.** Relayed by the orchestrator mid-sitting from
the owner (2026-09-15, their inclination was not to cap S52): "no unjudged row" means the rows S52 has
ALREADY produced - the sweep's 37 and the gate's 3, of which 104366 is handed to S52c/S52d and counts
as judged here. No further sweeps are run inside S52 to make new ones; the 20-seed sweep and the
6000-row gate are S57's. Multiple sittings are fine. So sittings 13 and 14 were right to hold, and
the work from here is triage, not more instrument.

This sitting **made the 37 rows survive a clean checkout, fixed the two things that stopped them
being asked at all, and grouped every one of them**. It judged none, deliberately: the largest family
turns out not to belong to the entry its signature points at, and saying so with the measurement is
worth more than adding eight rows to a pin that does not explain them.

### The 37 rows now survive a clean checkout, which they did not this morning

They lived only in `TestResults/oracle/sweep-<seed>/`, which is gitignored - so the evidence for
S52's remaining done-criterion was one `git clean` from gone. They are now
**`tools/probes/sweep-divergence-rows.jsonl`**, 37 rows lifted out of the eight sweep waves by
`report.txt` row number, each carrying a `comment` naming the `sweep-<seed>` directory and the row it
came from. Replayed against upstream on the commit-ready tree:

```
pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl
agree 0  unsupported 0  expected 0  timeout 0  resource 0  diverge 37  of 37 rows
```

**All 37 still diverge and no existing entry classifies any of them**, which is the fact the triage
starts from. That run's own verdict is `Oracle: RED` and it exits 1, as a rows file of nothing but
diverging rows must; the tally is the claim, not the exit code. `gate-divergence-triage.py` gained `--report <path>` so it reads a `-Rows` replay's
single `report.txt`; a replay runs no seed, so there is no `report-<seed>.txt` for its seed form to
find.

### Two things stopped the doors opening, and both are fixed rather than worked around

**1. A wedged Roslyn compiler server, which cost the first three attempts.** `run-oracle.ps1`'s
consumer would not build: `Error writing to source link file ... because it is being used by another
process`. `dotnet build-server shutdown` reported `VB/C# compiler server failed to shut down` twice
and `rm -rf obj/Release` failed with `Device or resource busy`. `tools/find-lock-holder.ps1 -Path
tests/FuzzyRegex.OracleTests/obj/Release/net10.0/FuzzyRegex.OracleTests.sourcelink.json` named the
holder exactly - `pid 12332 VBCSCompiler` - and stopping that process cleared it. **This is NOT
sitting 14's failure mode**, whose notes record the lock tool reporting NO holder; here it reported
one and was right, so the two diagnoses are different and the tool earned its keep. Under the
driver's allowlist `Stop-Process` and `taskkill` both need approval, so the kill went through a
one-line `.scratch/*.ps1` run with `pwsh -File`, which is the harness's own documented route for what
the inline gates refuse.

**2. `gate-divergence-doors.py` HARD-CRASHED inside row 32 of 37 and lost rows 32 to 37 with it.** Exit
139, no output file, no traceback. Minimised to fifteen lines in a scratch child: it is **ledger entry
9**, upstream's POSIX access violation, reached through the probe's own renderer. `describe()` read
`m.fuzzy_changes` unguarded, and reading that on the wrong POSIX match is an access violation rather
than an exception, so `answer()`'s `except Exception` cannot see it and the process dies.
`record-oracle.py:1019` has had the guard since S43 and its comment says it lives at the one funnel
"because missing one would kill a wave rather than fail a test"; the doors probe is a second funnel
that never got it. It has it now, keyed off `m.re.flags & POSIX_FLAG` exactly as the recorder is, and
rendering `changes=unavailable upstream (POSIX)`.

The fix is proven by the run rather than by reading: the same command that died inside row 32 now
prints its `(37 rows)` header, **all 37 row blocks**, and exits 0.

**And the faulting condition the committed probe states is WIDER THAN THE FAULT, which this sitting
measured by accident and is recording rather than leaving.**
`tools/probes/upstream-posix-fuzzy-changes-crash.py` says the condition "is two things and nothing
else": POSIX, and a fuzzy match that actually spent an error. Re-run here it reproduces every one of
its own cases exactly, insertion-only included - `(?p)(?:abc){i<=1}` over `'abxc'`, counts `(0, 1, 0)`,
`*** CRASH rc=0xC0000005 ***`. **But three ablations of this sweep satisfy that condition and do not
fault**, all three visible in the guard-removed control run below:

- **row 4**, `(?b)(?e)(?r)...` with the `(*SKIP)` spelled `(*PRUNE)`, compiled flags `0x1b42a` so the
  POSIX bit IS set, a PARTIAL match with counts `(0, 1, 0)`: `changes ([], [1], [])`, survives.
- **row 18**, `(?b)(?p)^...$` with the `(?b)` deleted, a COMPLETE `fullmatch` with counts `(2, 2, 1)`:
  `changes=([0, 1, 2], [3, 1], [])`, survives.
- **row 32, the sharpest, because it is ONE pattern giving both answers.** Its `as drawn` spelling
  answers counts `(0, 0, 1)` changes `([], [], [4])` and its `(*PRUNE)` spelling answers counts
  `(3, 0, 0)` changes `([3, 2, 1], [], [])` - both POSIX, both spending errors, both safe - and only
  the verb-DELETED spelling faults. The blind review found this one; the first draft of this
  paragraph had the two rows and not the row that beats them.

So neither "partial versus complete", nor which error kind, nor even the pattern separates them, and
the true condition is **open**. Nothing is weakened by that: the recorder's guard and now the doors
probe's are keyed on POSIX alone, which is conservative in the safe direction and cannot crash
whatever the condition turns out to be. What needed correcting is the probe's own "and nothing else",
and it is corrected there, in the file that states the law, with all three counter-examples and how
to see them.

`upstream-posix-fuzzy-changes-crash.py` also gains the pair that ties this sitting's fault to POSIX
(see the control below). **Ledger entry 9 carried the same universal and is corrected too** - it read
"every row with a non-zero count faults, POSIX present", which is the sentence these three ablations
falsify. All fourteen of its recorded reproductions still hold and are untouched; what is gone is the
claim to a condition, because a bug report that states a false universal is the kind that stalls.

**Carried, not fixed here:** `tools/probes/upstream-bestmatch-free-answer.py` reads `fuzzy_changes`
the same unguarded way and runs over a rows file. It cannot crash today because no row in
`bestmatch-loses-a-candidate-rows.jsonl` carries POSIX - and **row 18 of the sweep does**, so whoever
adds that row to that pin must fix the probe in the same change.

### The 37, grouped

Regenerate the whole table from the committed tree with the replay above and then
`python tools/probes/gate-divergence-doors.py --rows tools/probes/sweep-divergence-rows.jsonl`.
Row numbers below are that file's order; each row's own `comment` gives the sweep seed and row number.

| rows | sweep rows | group |
|---|---|---|
| 3, 17, 23, 37 | 40339, 41539, 41563, 40595 (`fuzzy` `fullmatch`) | **A** - `(?b)` loses a match its own flagless engine makes |
| 4, 15, 18, 35 | 24873, 24785, 24859, 25802 (`interactions`) | **A**, the same signature on composed rows |
| 7, 9 | 25586, 39721 | **B** - upstream's `(*SKIP)` answer is its own only; `(*PRUNE)` and verb-free are both this port's |
| 16, 26, 30, 33 | 39999, 25919, 38295, 24847 | **C** - overlapped scan, the verb moving a bound between matches |
| 13, 22, 24, 25, 29, 36 | 32811, 32949, 25313, 25545, 35441, 33723 | **D** - partial `search`, `search-start-partial` shape |
| 5, 20, 21, 31 | 25154, 25002, 25230, 24329 | **E** - same span, different change positions or change KINDS |
| 1, 2, 8, 10, 11, 12, 27 | 24112, 39336, 38812, 24133, 24452, 24548, 29645 | **F** - `split`, `sub`, `subf`, where the outcome is not a match object |
| 6, 14, 19, 28, 32, 34 | 25193, 35617, 25426, 32780, 24370, 25154 | **G** - unallocated; each needs its own look |

(Row 10 is a `subf` and belongs in F by F's own definition - F is now exactly the file's non-match-object
operations and nothing else; row 34 is NOT group E, since its spans differ, upstream `(0,3)` with
`fuzzy=(2,0,0)` against this port's `(0,2)` with no errors at all. Both were mis-filed in the first
draft and a blind pass caught both. **G is a residue, not a family**: it is what A to F do not claim,
and the independent verifier was right that its rows share generator and operation with each other -
19, 32 and 34 are all `interactions match` - so do not read G as a claim that they are six different
mechanisms.)

**Group A is the one this sitting spent its evidence on, and it does NOT belong where it looks like it
belongs.** On all eight rows upstream answers `None` as drawn and, with `(?b)` deleted, answers a
match that is **this port's answer exactly** - span, groups, counts and change positions, codepoints
converted to UTF-16. **On rows 4 and 18 read that as span, groups, partialness and counts only**: both
are POSIX rows with a spent error, so neither side HAS change positions to compare - this port's own
answer line reads `changes unavailable upstream` - and the independent verifier was right to narrow
it. That is `bestmatch-loses-a-candidate`'s own discriminator, and on the two rows carrying `(?e)` as
well (4 and 35) deleting the `(?e)` instead leaves `None`, so it is `(?b)` and not the pair.

**But the mechanism that entry pins does not explain them.** Ledger entry 12 is the doubled guard in
`END_FUZZY`'s backtrack arm, which needs `n > 2n-2` for `n` TRAILING insertions and so bites only for
`n >= 2` - `tools/probes/upstream-bestmatch-trailing-insertions.py`, whose own docstring records the
`(?b)` row matching for `k <= 1`. The flagless answers here carry **one** insertion on rows 3, 4, 17,
23 and 37, **two** on row 18, and **none at all** on rows 15 and 35 - and a refusal of a ZERO-ERROR
match cannot be an insertion guard at any budget. The guard is `n > 2n-2`, which is TRUE at n=1 (so a
single trailing insertion is allowed through) and false from n=2, so **it cannot be what refused seven
of the eight**. Row 18 is the only one it could have refused, and even there it is not demonstrated:
two insertions is the whole match's count, and the guard is per fuzzy section and per TRAILING
insertion, neither of which was measured here. That is exactly the trap that entry's own Reason warns
about ("S46's flagless-only key swept in a SECOND mechanism with the same signature").

(Two drafts of this paragraph were wrong in opposite directions and both were killed by a blind pass:
the first left row 4 out of the census and said "six of the eight", the second over-corrected to "none
of the eight" and forgot that n=2 is exactly where the guard starts biting. Seven is the number, and
row 18 is the one to look at first.)

Rows 4 and 15 separately meet `bestmatch-loses-a-partial`'s four conditions - `(?b)`, a fuzzy section,
a `(*SKIP)` and `partial=True` - and their anchored door is **unsettled rather than disqualified**,
which is a distinction the first draft got wrong. Both are `(?r)`, and that entry's Reason calls
`endpos` "the natural door for a `(?r)` row"; what it disqualifies is an `endpos` hit on a FORWARD row
(its row 76251). So the door is the right one here. What is weak is the bound it answers at: on both
rows the anchor sweep answers **only at `endpos=0`**, an EMPTY slice holding no text, and the entry's
other reversed rows answer at endpos 1, 2, 4 and 5. Whether a zero-width partial on an empty slice
carries the strong argument is a question that entry never had to settle, and it is not settled here.

**Group A is therefore eight rows of one signature and at least two mechanisms, and separating them is
the next sitting's first job** - very likely a new ledger entry rather than a widening of 12 or 13.
Do not add them to either pin on the flagless control alone; the owner's 2026-09-14 ruling says a pin
widens by judging a row, and these are not judged.

### Numbers

- Ratchet **GREEN**, **6119 / 6119 / 0 skipped**, **6011 distinct ids**, baseline **6011** -
  unchanged, and it must be: **no `.cs` file was touched this sitting at all**.
- Tool tests (Pester, `tools/tests`) **81 / 81**.
- The 37-row replay: **diverge 37 of 37**, `expected 0`, `agree 0`.
- `gate-divergence-doors.py --rows tools/probes/sweep-divergence-rows.jsonl`: **37 of 37 rows**,
  exit 0.
- `upstream-posix-fuzzy-changes-crash.py` re-run: every case as its docstring states.

### The negative control, run last against the code committed here

**The POSIX guard's control is the crash itself.** Applied and reverted by hand, `git status
--porcelain` clean of it afterwards:

> **Control A, `posix-changes-guard`**: in `tools/probes/gate-divergence-doors.py`, `describe`,
> replace
> ```
>         changes = ("unavailable upstream (POSIX)" if m.re.flags & POSIX_FLAG
>                    else str(m.fuzzy_changes))
> ```
> with `        changes = str(m.fuzzy_changes)`.
> Run: `python -u tools/probes/gate-divergence-doors.py --rows tools/probes/sweep-divergence-rows.jsonl`.
> Result: **exit 139 (SIGSEGV), dying INSIDE row 32 of 37** having completed 31, with
> `(*SKIP)->(*PRUNE)` as the last line printed and the `verb deleted` call the one that faults.
> Restored: **exit 0, 37 of 37 rows**.

**No second seed, and the reason is not the usual one.** This control runs on 37 explicit rows rather
than on a generator draw, so there is no seed to vary. What stands in for it is **a matched pair now
committed to `upstream-posix-fuzzy-changes-crash.py`'s own case table** - row 32's crashing shape
reduced to its inner fuzzy section, and the SAME pattern and subject with the POSIX bit cleared:

```
'(?r)(?:\U00010428\U00010428(?:\U00010400){1i+2d+1s<=3}){1i+2d+1s<=3}' flags=65536  span (0, 5) counts (1, 2, 0) |  *** CRASH rc=0xC0000005 ***
'(?r)(?:\U00010428\U00010428(?:\U00010400){1i+2d+1s<=3}){1i+2d+1s<=3}' flags=0      span (2, 5) counts (1, 0, 0) | changes ((5,), (), ())
```

One bit changed, crash against answer, and it needs no `partial=True` - so it fits that probe's
existing child unchanged. It went in the committed probe rather than staying in `.scratch` for the
reason this skill gives: the ten-variant matrix that first established it was scratch-only, and a
control nobody can re-run is not evidence. The pair is what survives; the ten variants are not
claimed.

No control this sitting mutates the engine. None could: no engine code changed and no `.cs` file did.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results. Pass one, over the whole
diff: ten findings raised, ten reproduced, ten fixed.** Not one was a defect in the tooling's
behaviour - the reviewer re-ran the replay, the doors, the crash probe, the tool tests and every
argument form of the triage probe, verified all 37 rows byte-identical to their wave rows at file
index = row + 1, and confirmed the UTF-16 conversion on every group-A row, which was its hunt list's
first item. All ten were defects in THIS SITTING'S EVIDENCE, which is what this sitting's output is:

1. **The insertion census dropped row 4**, so "six of the eight" was seven.
2. **Row 34 was filed in group E, "same span"** - its spans differ, `(0,3)` against `(0,2)`.
3. **The `endpos=0` argument cited the wrong authority.** `bestmatch-loses-a-partial` disqualifies an
   `endpos` hit on a FORWARD row; rows 4 and 15 are `(?r)`, where that entry calls `endpos` "the
   natural door". The door is right and the BOUND is weak, which is a different claim.
4. **The POSIX counter-examples undercounted**: row 32 is a third and the sharpest, two safe ablations
   and one faulting one from ONE pattern.
5. **The LEDGER block was inserted mid-entry**, orphaning S43's own conclusion after it.
6. **The crash probe cited `docs/plan/slices/done/S52-...`**, which does not exist while S52 is open.
7. **Row 10, a `subf`, sat in G while F was defined by operation.**
8. **`--report` with no path raised `IndexError`.**
9. **A stale `regex 2026.7.19` provenance line** over cases measured on 2026.9.10.
10. **"lost the other 36"** where 31 rows completed, so six were lost.

**Pass two, a first pass over the repair delta: six findings raised, six reproduced, six fixed** -
and the first is the sharpest thing either pass produced. (1) The corrected census was over-corrected:
"none of the eight" is wrong, because the guard is `n > 2n-2`, which is TRUE at n=1 and FALSE FROM
n=2 - so row 18's two insertions are exactly where it starts biting, and the number is seven.
(2) LEDGER said row 32 "gives two of each" where it gives two safe and one faulting. (3) The "other
36" fix reached the slice file and not STATE.md, DECISIONS.md or the probe comment. (4) The new
provenance sentence said "the last two cases"; they are the seventh and eighth of sixteen. (5) "Three
ablations ... all answering" then listed four. (6) `--report <path> 7` silently discarded the seed.

**No third pass.** What pass two's fixes created is wording corrections inside text pass two had just
read, plus one guard that refuses mixed `--report`-and-seed arguments; every argument form was re-run
afterwards and the independent verifier re-ran all six of them again.

### The independent verifier

A fresh agent (amendment 16 limb (d)), briefed with nothing but this tree and `docs/VERIFICATION.md`'s
do-not-use-git clause. It re-ran every number these notes, STATE.md, the five new DECISIONS entries
and the LEDGER block assert. **Everything was CONFIRMED except four narrowings, all folded in above,
and two COULD NOT RUN.**

**Narrowed rather than confirmed as written.** (a) The doors probe does not print the string "37 of
37"; it prints a `(37 rows)` header and 37 blocks. (b) On rows 4 and 18 the flagless answer matches
this port's on span, groups, partialness and counts but NOT on change positions, because both are
POSIX rows with a spent error and neither side has change positions at all - "exactly, change
positions included" was true of six of the eight, not eight. (c) Group G's "one shape each" is false
of generator-and-operation: rows 19, 32 and 34 are all `interactions match`. G is a residue.
(d) The replay's own verdict is `Oracle: RED`, exit 1, which is what an all-diverging rows file must
give; the tally is the claim.

**COULD NOT RUN, two, neither a gap in the measurement.** The wedged VBCSCompiler episode, because the
compiler server was not wedged when the verifier looked and every build succeeded first time - it is a
one-time event with no artefact; and the owner's relayed ruling, which has nothing to re-run against.

**CONFIRMED:** the ratchet (GREEN, 6119/6119/0 skipped, 6011 distinct ids, baseline 6011); tool tests
81/81; the replay tally character for character; the doors run at 37 blocks and exit 0; all 16 cases
of the crash probe answering as labelled, the new astral pair included; **Control A on every
particular** - exit 139, 31 complete, death inside row 32, `(*SKIP)->(*PRUNE)` the last line, `verb
deleted` the faulting call, and the file byte-identical by md5 after the hand revert; **the provenance
of all 37 rows rather than a sample**, each byte-identical to its wave row minus the added `comment`,
0 mismatches, all eight sweep directories present; the whole group-A census; `None` as drawn on all
eight; the `(?e)` ablation on rows 4 and 35; the anchor sweep answering only at `endpos=0` on rows 4
and 15 with the 3-hit cap unreached; all three POSIX counter-examples with their exact counts and
change lists, each in its own child; the group table's 37 cells, distinct and matching every
`comment`; groups C, D, E and F by operation; `record-oracle.py:1019` and `:342`; all six argument
forms of the triage probe with their exit codes; that no `.cs` file changed; that LEDGER.md's diff is
a pure insertion with nothing deleted and its fourteen older cases all still answer as recorded; the
carried `upstream-bestmatch-free-answer.py` defect and that sweep row 18 really would trip it; and
sitting 14's corrected arithmetic, 20.3 x 300/80 = 76.1.

It used no git command but `status`, `diff` and `diff --stat`, reverted Control A by hand, and left
the tree byte-identical to the snapshot it started from.

---

## Sitting 16 (2026-09-15) - CHECKPOINT, and group A is two defects, not one

Sitting 15 handed over group A - eight sweep rows with one signature, upstream answering `None` and
its own `(?b)`-free answer being this port's exactly - with the warning that ledger entry 12's
mechanism could not explain seven of them. **It is two mechanisms, the split is 4/4, and a fifth row
outside group A belongs to the first one.** Five rows are judged and pinned, four more are attributed
to the second defect and left, two are open.

### What settled it: asking the PORT, not the shape

Every earlier attempt at this family reasoned from the row's shape - how many insertions, where they
fall, which flags are present - and sitting 15's notes record two drafts of that reasoning being
wrong in opposite directions. The shape is the wrong instrument here, because **this port has already
fixed both candidate mechanisms**, so each fix can be put back one line at a time and the rows sorted
by which one moves them. That is what the two controls below are, and it is why they are re-runs of
this port's own fixes rather than injected faults.

| tree | the eight group-A rows | all 37 sweep rows |
|---|---|---|
| as committed | `expected 4  diverge 4` | `expected 5  diverge 32` |
| control A, `end-fuzzy-doubled-guard` | `agree 4  diverge 4` | `agree 5  diverge 32` |
| control B, `posix-restore-stale-totals` | `agree 2  expected 4  diverge 2` | `agree 4  expected 5  diverge 28` |

Neither control moves a row the other moves, and neither moves rows 4 or 15.

### The five that ARE ledger entry 12

Rows 3, 17, 23 and 37 of the sweep file (all `fuzzy` `fullmatch`), **and row 20**. Control A is the
argument: restoring upstream's doubled `END_FUZZY` term - `TotalErrors(state.FuzzyCounts) +
TotalErrors(innerCounts) < state.MaxErrors`, the single line S46 dropped - makes this port agree with
upstream on exactly those five of the 37 and on nothing else.

**Row 20 is the one the signature would have missed, and it is why the control was run over all 37
rows rather than over the eight.** It has none of the family's signature - both engines match, at the
same span and the same counts (2, 1, 1). What differs is WHERE the errors fall. The subject is
astral, so the indices have two spellings and both are given here: in the **codepoints upstream
reports**, span (0, 5), upstream substituting at 0 and 4 and inserting at 3 against its own flagless
answer's 0 and 3 and 4; in the **UTF-16 the recorded row carries**, span (0, 8), 0 and 7 with the
insertion at 6 against 0 and 6 with it at 7. The flagless answer is this port's. So the guard does
not only lose matches, it also moves an error one position. Sitting 15's table filed it under group E; that table is right about what it
looks like and wrong about what it is.

**None of the five needs two trailing insertions, which is what entry 12's headline said the defect
needed.** Rows 3 and 23 lose a fit costing one insertion and one deletion, rows 17 and 37 one
insertion and one substitution, and row 20 loses no match at all. The headline is corrected in
LEDGER.md, in PORTMAP.md, in the oracle entry's `Reason`, in the `Matcher.cs` comment and in the two
older gap test's comments that repeated it - the blind review found all five of those files and they were
wrong in the same words. The correction is measurable, not asserted: block 6 of
`tools/probes/upstream-bestmatch-trailing-insertions.py` now carries the minimised, all-ASCII form of
row 17, where nothing about the sweep row's astral subject turns out to matter:

```
regex.fullmatch(r'(?b)(a0)(?:(?:\1)){e<=3}', 'a0x0y')   ->  None
regex.fullmatch(r'(a0)(?:(?:\1)){e<=3}',     'a0x0y')   ->  (0, 5) counts=(1, 1, 0)
```

**And the same probe says the entry still does not know its own law, which is recorded rather than
papered over.** Spell that section's body as the literal `a0` instead of the backreference and `(?b)`
answers the identical subject; block 7 shows a width-1 body refusing from two trailing insertions
while a width-2 body survives every count the probe reaches. So neither the insertion count nor the
error total is the boundary. A report that states a false universal is the kind that stalls, which is
the lesson sitting 15 paid for on ledger 9, so the entry now says what it knows and what it does not.

Landed: the five rows in `tools/probes/bestmatch-loses-a-candidate-rows.jsonl` and, recorded by
`python tools/record-oracle.py --rows`, in `ExpectedDivergences._bestmatchLostCandidateRows` as rows
14 to 18 - **in append order, which is sweep 3, 17, 23, 37 and then 20**, not the numerical order
this section lists them in; the five in `upstream-bestmatch-free-answer.py`'s inline table; a new gap test
`FuzzyBestMatchTests.Bestmatch_keeps_a_match_whose_single_trailing_insertion_is_not_its_only_error`.

### The two that are ledger entry 16, and are a STRONGER reproduction of it

Rows 18 and 35. Control B is one half of the argument and upstream's own ablations are the other:

| row | operation | as drawn | `(?b)` deleted | POSIX removed |
|---|---|---|---|---|
| 18 | `fullmatch` | `None` | (0, 5) counts (2, 2, 1) | (0, 5) counts (2, 2, 1) |
| 35 | `match` | `None` | (0, 0) g1 (1, 3) | (0, 0) g1 (1, 3) |

Codepoints, both subjects astral. Neither flag alone destroys the match; the conjunction does. That
is entry 16's four-way self-refutation **on a plain anchored call with no scan anywhere**, where the
entry's own reproduction needs `finditer(overlapped=True)`. Control B names the same place S48b fixed
here - the two running totals `RestoreBestMatch` gives back after a POSIX restore, which upstream
leaves stale. Both facts are now in entry 16, with
`tools/probes/upstream-bestmatch-sweep-group-a.py` printing the table from the committed rows file.

**Control B moves two more rows nobody has looked at: 10, a `subf`, and 12, a `sub`**, which
sitting 15 filed in group F because their outcome is not a match object. So the stale-totals
mechanism reaches four of the 37, not two. **They are not all one entry**: rows 12, 18 and 35 carry
POSIX and BESTMATCH together, which is entry 16, and **row 10 carries POSIX with no BESTMATCH at
all** (flags `0x1408a`, no `(?b)`), which puts it in entry 9's family instead. The control restores
both halves of one `RestoreBestMatch` at once, so it cannot separate them and does not claim to.

**None of the four is pinned, deliberately.** The right home is one of two existing entries and
choosing needs the discriminator checked rather than guessed:
`posix-fuzzy-contradicts-its-own-flagless-answer` keys on `posixFreeOutcome` AND on this port's
answer matching it, and row 18's recorded posix-free answer carries change positions that the drawn
side has none of (ledger 9 - a POSIX row's `fuzzy_changes` cannot be read). **Sitting 17 opens
there**, with the control already run.

### The two that are neither

Rows 4 and 15, both `(?b)(?r)` partial searches. Row 4 keeps its `None` with POSIX cleared and row 15
carries no POSIX at all, so entry 16 is not it; neither control moves either, so entry 12 is not it.
Both flagless answers are PARTIALs, which is ledger entry 13's shape, and sitting 15's `endpos=0`
worry about them is untouched. Open.

### Numbers

- Ratchet **GREEN**, **6120 / 6120 / 0 skipped**, **6012 distinct ids**, baseline **6012** - one new
  test, the gap test above; `Matcher.cs`'s only change is a comment.
- The eight group-A rows: **expected 4, diverge 4** of 8, where sitting 15 left `diverge 8 of 8`.
- All 37 sweep rows: **expected 5, diverge 32** of 37.
- `upstream-bestmatch-free-answer.py`: 14 rows. Four of the five new ones answer `None` under `(?b)`
  with the flagless and `(?e)` lines identical, which is the family's usual shape; **the fifth, sweep
  row 20, matches on all three lines** and differs only in where the errors fall. The independent
  verifier caught this sentence claiming all five behave alike.

### The negative controls, run last against the code committed here

Both were applied and reverted by hand, `git status --porcelain` clean of each afterwards. Neither is
an injected fault: each is one of this port's own fixes put back, which is what makes them evidence
about the attribution and not only about the generator.

> **Control A, `end-fuzzy-doubled-guard`**: in `src/FuzzyRegex/Engine/Matcher.cs`, the `END_FUZZY`
> backtrack arm, replace
> ```
>                         && TotalErrors(state.FuzzyCounts) < state.MaxErrors
> ```
> with
> ```
>                         && TotalErrors(state.FuzzyCounts) + TotalErrors(innerCounts) < state.MaxErrors
> ```
> Runs: `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl`, and the
> same command over a scratch file of the eight group-A rows, rebuilt with
> `python -c "import io;ls=list(io.open('tools/probes/sweep-divergence-rows.jsonl',encoding='utf-8'));io.open('.scratch/groupA.jsonl','w',encoding='utf-8',newline='\n').writelines(ls[i-1] for i in (3,4,15,17,18,23,35,37))"`.
> Result: **agree 4, diverge 4 of 8**, and **agree 5, expected 0, diverge 32 of 37** with the
> agreeing rows being 3, 17, 20, 23 and 37 exactly. `dotnet test tests/FuzzyRegex.Tests --
> --treenode-filter "/*/*/FuzzyBestMatchTests/*"` goes from 30 passing to **4 failed, 26 passed**,
> the four being this sitting's new test and the three older ones the same line holds.

> **Control B, `posix-restore-stale-totals`**: in the same file, `RestoreBestMatch`, delete
> ```
>         state.TotalErrors = state.BestTotalErrors;
>         state.TotalCost = state.BestTotalCost;
> ```
> Runs: the same two.
> Result: **agree 2, expected 4, diverge 2 of 8** and **agree 4, expected 5, diverge 28 of 37**, the
> four agreeing rows being 10, 12, 18 and 35.

**No second seed, and the reason is sitting 15's**: both controls run over explicit rows rather than
a generator draw, so there is no seed to vary. **What stands in for it is the wider row set**, and it
earned its keep rather than being a formality - run over the eight rows, control A looks like a
clean four-for-four; run over all 37 it turns up row 20, a member of the family that the signature
these rows were grouped by would never have found.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results.**

**Pass one, over the diff as it stood before row 20 was found: six findings raised, five reproduced,
five fixed, one killed.** Every one of the five was the same defect wearing five hats - the
superseded sentence "a match needing n trailing insertions needs n > 2n-2, false for every n >= 2 at
every budget" left standing in the oracle entry's own `Reason`, in `PORTMAP.md`, in the `Matcher.cs`
comment, in an older gap test's comments, and (as "rows 2 and 4 of the four") in a comment this
sitting had just written. The correction had been applied where it was noticed and missed everywhere
else, which is exactly what a blind pass is for. **The one that did not survive** was the claim that
pin rows 15 and 16 are not byte-identical to their sweep rows, the astral subjects being written as
surrogate escapes: they ARE byte-identical to `record-oracle.py --rows`'s own output, which is what
the file documents them as and what writes the escapes, checked by substring match on all five rows.

**Pass two, a first pass over the row-20 delta and the notes, STATE and DECISIONS the first pass
never saw: nine findings raised, nine reproduced, nine fixed.** Three were real errors of fact rather
than stale wording. (1) **Row 20's indices were given as upstream's and are UTF-16** - the probe that
prints them prints codepoints, (0, 5) with substitutions at 0 and 4, where the notes said (0, 8) and
0 and 7; both spellings are now given in both places. (2) **Row 10 carries POSIX with NO BESTMATCH**
(flags `0x1408a`), so calling all four of control B's rows entry 16's conjunction was wrong - row 10
is entry 9's family, and the control restores both halves of one function at once and cannot separate
them. (3) **Rows 10 and 12 are a `subf` and a `sub`, in that order**, not the other way round. The
remaining six were counts and cross-references: "thirteen rows" for a probe holding fourteen, "nine
at S46, thirteen since" matching no artifact, "the four sweep rows this entry gained" where five were
added, "all five places" for six locations, a `tools/probes/upstream-bestmatch-sweep-group-a.py`
recommended for rows it does not carry, and two more copies of the superseded sentence inside the
entry that corrects it.

**No third pass.** Pass two's fixes are wording inside text pass two had just read, plus two index
spellings it supplied itself; every probe was re-run afterwards and the ratchet re-checked, and the
independent verifier below re-ran all of it again.

### The independent verifier

A fresh agent (amendment 16 limb (d)), briefed with nothing but this tree and `docs/VERIFICATION.md`'s
do-not-use-git clause. It re-ran every tally, both controls by hand, all three probes, the ratchet,
the gap test's expected values against upstream and the byte-identity of the five pinned rows.
**Everything was CONFIRMED except one DIFFERENT and two COULD NOT RUN.**

**DIFFERENT, and folded in above.** The "Numbers" section said the five new probe rows all behave as
the family requires; four answer `None` under `(?b)` and the fifth, row 20, matches on all three
lines - which this section's own row-20 paragraph says and that sentence contradicted.

**Narrowed rather than contradicted.** The pin's rows 14 to 18 are in APPEND order, sweep 3, 17, 23,
37, 20, where the prose lists them numerically; said explicitly now. And the byte-identity of the
five holds under the second half of its own disjunction: the `.jsonl` carries only the input fields,
so each line is a verbatim substring of its sweep row rather than the whole of it, and the recorded
form the verifier regenerated with `record-oracle.py --rows` is JSON-equal to the sweep row minus
`comment` on all five.

**COULD NOT RUN, two, neither a gap in the measurement.** STATE.md's relayed scope ruling about the
gate's three rows, which has no artifact to re-run against; and this section's own Review paragraph,
which reports what two dispatched agents did - though every OUTCOME it names is in the tree and was
verified (row 20's codepoint spelling, row 10's flags, the `subf`/`sub` order, the probe's fourteen
rows, the five corrected files).

**CONFIRMED:** the ratchet (GREEN, 6120/6120/0 skipped, 6012 distinct ids, baseline 6012) and that
`tests/parity-baseline.json` gains exactly the one new test id; that `Matcher.cs`'s diff is 12
insertions and 6 deletions and every one a `//` line; both controls at their exact sites, applied and
re-edited back verbatim, with all six tallies and the four failing test names; the committed tree's
two tallies and the identity of the five EXPECTED rows; that neither control moves a row the other
moves nor rows 4 or 15; the `.scratch/groupA.jsonl` rebuild command run verbatim; the five pinned
rows in both files; the per-row error mixes; **both index spellings for row 20**, codepoint and
UTF-16, with the 5-codepoint to 8-unit mapping; all three probes running, blocks 6 and 7, and the
literal-body and body-width findings; ledger 16's ablation table on both rows; rows 4 and 15's flags
and partialness; row 10's flags and the absence of `(?b)` from it; row 18's posix-free change
positions; and all three of the new gap test's expected values, two as direct upstream runs and one
as the recorded deliberate divergence.

It used no `git checkout`, `restore`, `stash`, `reset` or `clean`, reverted both controls by
re-editing, proved the restoration by re-running the 37-row oracle and the ratchet afterwards,
deleted its own scratch, and left the tree byte-identical to the snapshot it started from.

## Sitting 17 (2026-09-15) - CHECKPOINT, and control B's four rows are two families, not one

Sitting 16 handed over four rows that one port-side control moves - 10, 12, 18 and 35 of
`tools/probes/sweep-divergence-rows.jsonl` - with rows 12, 18 and 35 provisionally down as ledger
entry 16's conjunction because all three carry POSIX and BESTMATCH, and told sitting 17 to check the
discriminator before choosing. **The check moved a row. All four are now judged and pinned; the
sweep file reads `expected 9, diverge 28`.**

### Carrying a flag is not needing it, and row 12 is the row that says so

The ablation upstream will answer is three seconds long, and it separates the four cleanly:

| row | operation | as drawn | `(?b)` removed | POSIX removed | family |
|---|---|---|---|---|---|
| 10 | `subf` | `'[{'` count 1 | no `(?b)` and no flag bit to remove | `'[{A'` count 1 | entry 9 |
| 12 | `sub` | `'\U00010428-'` count 1 | `'\U00010428-'` count 1, unchanged | `'\U00010428\U00010428-'` count 1 | entry 9 |
| 18 | `fullmatch` | `None` | (0, 5) counts (2, 2, 1) | (0, 5) counts (2, 2, 1) | entry 16 |
| 35 | `match` | `None` | (0, 0) g1 (1, 3) | (0, 0) g1 (1, 3) | entry 16 |

Spans are upstream's, so codepoints and (start, end); rows 12, 18 and 35 have astral subjects.
**Row 12 is written `(?b)(?r)(?p)` and is not the conjunction**: deleting its `(?b)` leaves
upstream's answer character for character - its recorded `bestmatchFreeOutcome` IS its drawn answer -
and only removing POSIX restores the second `𐐨` this port replaces. Rows 18 and 35 lose the match
under the conjunction and get the SAME match back under either flag alone, which is entry 16's
four-way self-refutation on a plain anchored call with no scan anywhere. One control reaching all
four is not evidence that all four are one defect: the two running totals `RestoreBestMatch` gives
back serve both arms, so the control cannot separate them and sitting 16 said as much.

`python tools/probes/upstream-bestmatch-sweep-group-a.py` now carries all six of the rows the two
controls separated - 4, 10, 12, 15, 18, 35 - rather than four, asks a `sub` row through
`subn`/`subfn` the way the recorder does (`tools/record-oracle.py:774`), and prints
`BESTMATCH absent` instead of a "no (?b)" line over an unchanged pattern, which is what row 10 would
otherwise have shown.

### The pin

All four into `posix-fuzzy-contradicts-its-own-flagless-answer`, as rows 6 to 9 in sweep order
10, 12, 18, 35. That entry keys on the row AND on this port's answer AND on `posixFreeOutcome`
having MOVED, and on all four this port's answer is upstream's own POSIX-free answer. Row 18 is the
one whose POSIX-free side carries change positions while the drawn side has none at all - the drawn
side is `None` - and it still keys cleanly, because the comparison drops the positions from both
sides on any POSIX row (ledger 9) and this port renders its own as `[changes unavailable upstream]`.
Precedent for hosting entry 16's mechanism here is the entry's own row 76101.

**Row 18 also answers with its atomic group deleted**, and that is recorded as NOT a third
contradiction: an atomic group may legitimately refuse a match by forbidding the backtracking it
needs, where POSIX may only choose among the matches the flagless engine already makes and BESTMATCH
may only rank them. A report should say so before a maintainer does.

### Row 35 minimised: three ASCII characters and a fuzzy section that spends nothing

The form to file. The four answer lines below are the probe's own, copied out of the last block of
`python tools/probes/upstream-bestmatch-sweep-group-a.py`; the first line is a hand-written header
saying what the block runs, and the block's four negative-control lines are left out here and
quoted in full in ledger entry 16, whose fenced copy IS verbatim:

```
[header, not printed] pattern (?b)(?r)\K(.(.{2}){i<=1})  subject 'baa'  flag regex.POSIX
    as drawn                   None
    no (?b)                    (0, 0)  g1=(0, 3) g2=(1, 3)  complete  counts=(0, 0, 0)
    POSIX flag cleared         (0, 0)  g1=(0, 3) g2=(1, 3)  complete  counts=(0, 0, 0)
    neither flag               (0, 0)  g1=(0, 3) g2=(1, 3)  complete  counts=(0, 0, 0)
```

**Every door that answers spends nothing**, so the defect is not about ranking fuzzy candidates by
cost - there is one candidate and it costs zero - and yet the fuzzy section has to be THERE. The
same block prints four negative controls that say which parts are load-bearing: delete the `{i<=1}`
and upstream answers under both flags, write it `{s<=1}` and it answers, and removing the `(?r)` or
the `\K` answers at `(0, 3)` rather than restoring the `(0, 0)` match. New gap test
`FuzzyPosixTests.Posix_and_bestmatch_together_keep_a_match_that_either_flag_alone_keeps`; upstream's
spans are (start, end) and the test asserts (index, length), so upstream's `g2 (1, 3)` is `(1, 2)`
there - a slip the test caught on its first run.

### Numbers

- Ratchet **GREEN**, **6121 / 6121 / 0 skipped**, **6013 distinct ids**, baseline **6013** - one new
  test. `src/` is untouched this sitting.
- All 37 sweep rows: **expected 9, diverge 28**, where sitting 16 left `expected 5, diverge 32`.
- The four rows alone: `expected 4, diverge 0 of 4`.

### The negative control, run last against the code committed here

Not an injected fault: it is this port's own S48b fix put back, which is what makes it evidence
about the attribution rather than only about the generator. It is sitting 16's control B, re-run
here because this sitting's pin claims to cover exactly the rows it moves.

> **Control B, `posix-restore-stale-totals`**: in `src/FuzzyRegex/Engine/Matcher.cs`,
> `RestoreBestMatch`, delete
> ```
>         state.TotalErrors = state.BestTotalErrors;
>         state.TotalCost = state.BestTotalCost;
> ```
> Run: `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl`, 37 rows.
> Result on THIS tree: **agree 4, expected 5, diverge 28 of 37**, where the committed tree gives
> **agree 0, expected 9, diverge 28**. The four that move from `expected` to `agree` are exactly
> this sitting's four pinned rows: the five `expected` left are all
> `bestmatch-loses-a-candidate`, sweep rows 3, 17, 20, 23 and 37.
> Also `dotnet test tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/FuzzyPosixTests/*"` goes from
> 10 passing to **4 failed, 6 passed**, the four including this sitting's new test.
> Applied and reverted by re-editing; `git diff --stat src/FuzzyRegex/Engine/Matcher.cs` empty
> afterwards, and the 37-row run re-checked at `expected 9, diverge 28`.

**No second seed, and the reason is sitting 15's and 16's**: the control runs over an explicit rows
file rather than a generator draw, so there is no seed to vary. What stands in for it here is that
the control's four rows and the pin's four rows are asserted to be the same four, and the run over
all 37 is what checks that rather than a run over the four.

### Review

**Two blind passes, both dispatched inside the turn and read as tool results.**

**Pass one, over the whole diff: two findings raised, two reproduced, two fixed.** Both were the
same failure - a claim the artifacts do not support. (1) Ledger entry 16 still said the probe reads
"the four rows" out of the sweep file when it now reads six. (2) **The ledger's minimised block
quoted `counts (0, 0, 0)` in a transcript attributed to the probe, and the probe does not print
it**: `describe` suppresses an all-zero triple (`if any(m.fuzzy_counts)`), and on this block the zero
IS the finding. Fixed at the instrument rather than in the prose - the block now prints the counts
itself - and the fabricated Python-REPL transcript was replaced by a verbatim copy of the probe's
real output.

**Pass two, a first pass over that fix delta: three findings raised, three reproduced, three fixed.**
One was a latent defect in the new code: the explicit `counts=` was appended on top of `describe`'s
own, so any future cut on that line that DID spend an error would print the field twice; it is now
supplied only where `describe` withholds it. The other two were prose the fix had just introduced -
"the six row blocks above go through `describe`" is false of the two `sub` blocks, which never call
it, and "each line below deletes one thing" is false of the `neither flag` line, which deletes both.
The fenced block in the ledger was then re-checked character for character against the live probe
output by `.scratch/check-fence.py`, and is identical.

**No third pass.** Pass two's own fixes are two prose corrections inside text it had just read, plus
the two-line guard it specified itself, whose effect is that the probe's output does not change -
proved by the fence comparison above, which was re-run after the guard landed.

### The independent verifier

A fresh agent (amendment 16 limb (d)), briefed with nothing but this tree and `docs/VERIFICATION.md`'s
do-not-use-git clause. It re-ran the ratchet, the baseline delta, both oracle tallies, every line of
the ablation table, the probe, the minimised claim and its four negative controls, the control
applied and reverted by hand, the gap test's expected values against upstream, and the four new row
literals through the recorder. **Twenty-three claims: twenty-two CONFIRMED, one DIFFERENT.**

**DIFFERENT, and fixed above.** This section's fenced block was introduced as "printed by the
probe's last block" and is not verbatim: its first line is a hand-written header the probe does not
print, and it leaves out the block's four control lines. The four ANSWER lines are character-identical
to the probe's, and the ledger's fenced copy is verbatim in full - which the verifier checked
programmatically - so the block is now labelled as what it is. That is the same defect the blind
review found in the ledger an hour earlier, surviving in the one place the fix did not reach.

**CONFIRMED:** the ratchet (GREEN, 6121/6121/0 skipped, 6013 distinct ids, baseline 6013) and that
`tests/parity-baseline.json` gains exactly the one new test id and loses none; `expected 9,
diverge 28 of 37` and the identity of all nine EXPECTED rows under their two entries; `expected 4,
diverge 0` over the four alone; every cell of the ablation table, including that row 12's
`(?b)`-removed answer is character-for-character its drawn answer and that row 10's flags `0x1408a`
carry no BESTMATCH bit; that this port's answer is upstream's recorded `posixFreeOutcome` on all
four; the probe running at rows 4, 10, 12, 15, 18, 35; the ledger's fenced block byte-identical to
the probe's last block; the minimised form and all four of its negative controls; the control at its
exact site, applied and re-edited back, giving `agree 4, expected 5, diverge 28` with the four
flipped rows being exactly 10, 12, 18 and 35 - established by the DIVERGE set being the identical 28
rows either way - and the four failing `FuzzyPosixTests`; the empty `git diff --stat` on
`Matcher.cs` afterwards and the 37-row re-check; all three of the new gap test's expected values
against a live upstream run, with the (start, end) to (index, length) conversion and
`DEFAULT_VERSION == regex.V0`; the four row literals re-recorded SAME on every key; and the four
`_posixOvercostOurs` strings against the report's own `port` lines.

It used no `git checkout`, `restore`, `stash`, `reset` or `clean`, reverted the control by
re-editing, and deleted its own scratch.

---

## Sitting 18 (2026-09-16) - CHECKPOINT, and group D is three families, not one

STATE.md made sitting 18's first job "pick the next group from sitting 15's table and judge it the
same way - ablate upstream, then pin". The group taken is **D**, sitting 15's six "partial `search`,
`search-start-partial` shape" rows. **The shape is not the classification: the six are THREE
families, split 3/2/1, and all six are now judged and pinned. The sweep file reads
`expected 15, diverge 22`.** `src/` is untouched this sitting.

### The instrument, and why the existing two could not do it

Both doors the repo already had ask ONE question each, and neither separates these six.

**`searchOnlyPartial` (`tools/record-oracle.py:947`) asks one cell of a grid**: does upstream's own
`match` over the span its `search` reported answer the same partial THERE. It is `search-start-
partial`'s recorded discriminator and it is right, but it is a single point.

**`gate-divergence-doors.py`'s anchor sweep stops after three hits** (`:212`, `if len(hits) >= 3`).
That is the leftmost `pos` on a forward row and it is NOT the highest `endpos` on a reversed one -
and the whole argument for a reversed row is about the highest, because a reversed match anchors at
its END. **Row 24 answers at endpos 0, 1 and 3, so the anchor its own search tries first is the
THIRD hit**, printed on the capped sweep's last line. Reading that line as the third-best inverts
the argument.

**`tools/probes/upstream-partial-anchor-reachability.py`** (new, committed) adds both halves: an
UNCAPPED sweep on the bound the row's own search moves, and a walk of every `(pos, endpos)` pair in
the searched region collecting every span upstream's own matcher will produce. Run it with
`python tools/probes/upstream-partial-anchor-reachability.py`; the rows are read out of
`tools/probes/sweep-divergence-rows.jsonl` by index, so nothing is transcribed.

### The split, and what decides it

| sweep rows | `searchOnlyPartial` | upstream's own answer | entry |
|---|---|---|---|
| 25, 29, 36 | true | **reachable by NO anchored call** in the region | `search-start-partial` |
| 13, 22 | false | reachable, at the wrong anchor, forward | `partial-retry-carried-slice-forward` |
| 24 | false | reachable, at the wrong anchor, reversed | `partial-retry-reversed-slice` |

On rows 25, 29 and 36 upstream's search answers (0,5), (0,5) and (0,2) against matcher grids that do
not contain them - its search door and its match door disagree, over the whole region rather than at
one point. On 13, 22 and 24 upstream's answer IS a span its own matcher makes; it is just not the one
at the anchor its own search must try first.

**On all six this port's answer is upstream's own answer at the first anchor its search is defined to
try THAT ANSWERS AT ALL** - lowest `pos` forwards, highest `endpos` reversed - span, groups and
partial flag, codepoints converted to UTF-16. **And on all six the `(*PRUNE)` spelling gives the same
thing**, which each row's own recorded `pruneOutcome` already carried. `(*PRUNE)` prunes the
backtracking `(*SKIP)` prunes and moves no slice bound (`upstream/src/_regex.c:14553` reversed,
`:14555` forwards), so the bound move is the cause rather than what the pattern means.

**The verb-free spelling is NOT a control and is not asserted anywhere**, which is new wording this
sitting put in all three entries: deleting the verb prunes nothing, so it may legitimately reach a
match the pruned spellings cannot - and on 13, 22 and 24 it does, answering a COMPLETE match at
codepoints (2,4), (1,5) and (0,3).

### A negative result kept, because the entry's own NAME invites the wrong reading

**Neutralising upstream's required-string prefilter changes not one of the six answers.** The probe
asks every row both ways and prints both lines. So whatever in `search_start` (`:8385`) answers rows
25, 29 and 36, it is not the required string - which is worth having written down beside a family
called `search-start-partial`, and beside ledger entry 1, whose whole mechanism IS the required
string. Nothing in the ledger states a universal these six falsify, so no ledger entry is corrected
here; the three ExpectedDivergences entries carry the evidence.

### Two symptoms widened, and both get a permanent test

**`partial-retry-carried-slice-forward` said upstream answers "a zero-width partial at the far end of
what it searched"**, which sitting 8 wrote from rows 4 and 5 and which was true of every row until
now. Sweep row 22 is not zero-width: upstream answers the ONE-CODEPOINT partial (5,6) where the first
answering anchor is pos 4 and gives (4,6) with g1 (4,5) and g2 (5,6). So the moved `slice_start` can
cost a START without costing the whole match. New gap test
`PartialMatchingTests.A_forward_skip_costs_the_partial_its_start_without_costing_the_match`.

**`search-start-partial`'s second arm said upstream's partial covers "the whole searched region"**,
true of rows 1 to 15. Sweep row 36 is (0,2) of a region (0,4) - the first row of the arm without the
prefilter's usual fingerprint, and it meets the discriminator anyway. New gap test
`PartialMatchingTests.A_search_only_partial_need_not_cover_the_whole_searched_region`. Its `(?a)` is
written inline because ASCII is not a public `FuzzyRegexOptions` member; upstream compiles the two
spellings to the identical flag word `0x2488` and answers identically, checked on 2026.9.10.

### Three stale row counts corrected, found by counting rather than by reading

`_searchStartElsewhereRows` said "seven rows" and held 13; `_partialRetryReversedRows` said "five"
and held 11; `_partialRetryForwardRows` said "three" and held 5. All three were left behind by
sitting 8's additions. They now read sixteen, twelve and seven, and each says what it drifted past.

### Numbers

- Ratchet **GREEN**, **6123 / 6123 / 0 skipped**, **6015 distinct ids**, baseline **6013 -> 6015** -
  the two new tests. `src/` untouched.
- All 37 sweep rows: **expected 15, diverge 22**, where sitting 17 left `expected 9, diverge 28`.
- `dotnet build tests/FuzzyRegex.OracleTests` 0 warnings 0 errors; Pester tool tests **81 / 81**.
- The six row literals **re-record byte-identically** through `record-oracle.py --rows`, key by key.

### The negative control, run last against the code committed here

It is sitting 3's and 17's keying control on this sitting's three entries, because all three are
keyed on a row AND on this port's exact answer, and a judged answer that is wrong classifies nothing.
Applied and reverted by re-editing; `git diff --stat` on the file unchanged afterwards and its md5
back to `aa0b37f465372b6110e8aa8fc6213fa8`.

> **Control A, `entry-keying`**: in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`, change one
> character in one judged answer of each of the three touched entries. The lines read, in the file:
> ```
>         "match 0:(0,1)[(0,1)] last=-1/- partial",
>     ];
> ```
> (the LAST element of `_searchStartElsewhereOurs`) to `0:(0,2)[(0,2)]`;
> ```
>         "match 0:(7,3)[(7,3)] 1:(7,2)[(7,2)] 2:(9,1)[(9,1)] last=2/g1 partial",
> ```
> (in `_partialRetryForwardOurs`) to `0:(7,4)[(7,4)]`;
> ```
>         "match 0:(0,4)[(0,4)] 1:(1,2)[(1,2)] last=1/g1 partial",
> ```
> (in `_partialRetryReversedOurs`) to `0:(0,5)[(0,5)]`.
> Rows: the committed `tools/probes/sweep-divergence-rows.jsonl`, all 37, run with
> `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl`. No seed - these
> are explicit rows.
> Result: **expected 12, diverge 25 of 37** broken, against **expected 15, diverge 22** restored. The
> three that flip are exactly sweep rows 22, 24 and 36 - one per touched entry - and 13, 25 and 29
> stay EXPECTED, which is what says the control reached the answer it aimed at and not the entry.

**No second seed, and the reason is sittings 15's, 16's and 17's**: the control runs over an explicit
rows file rather than a generator draw, so there is no seed to vary. What stands in for it here is
that each of the three entries is hit separately and the two rows of the entry NOT broken stay put.

No control this sitting mutates the engine. None could: no `src/` file changed.

### Review

**One blind pass, dispatched inside the turn and read as a tool result, over the whole diff. Findings
raised: one. Reproduced: one. Fixed: one.** The new comment over `_searchStartElsewhereOurs` rows 14
to 16 said the answers are upstream's "at the FIRST anchor its search is defined to try - lowest
`pos` forwards, highest `endpos` reversed", dropping the qualifier the probe and the other two new
comment blocks both keep. Without "that answers at all" the sentence is false on all three rows: the
literal first anchor is pos 0, pos 0 and endpos 4, and upstream answers `None` at each; the answers
listed come from pos 5, pos 2 and endpos 1. Fixed by restoring the qualifier and naming the three
anchors outright.

The reviewer worked the hunt list rather than only reading: it re-derived every asserted span from
upstream's own codepoint answer independently (`lastindex`/`lastgroup` included), re-recorded all six
row literals and found them identical key by key, ran the replay to `expected 15, diverge 22`,
confirmed `searchOnlyPartial` true on 25/29/36 and false on 13/22/24, checked `git diff -U0` for any
loosened predicate and found the only `Applies`-shaped hit to be a Reason string, mutated both gap
tests and watched each go red, and checked `(?a)` against flag bit 0x80.

**No second pass.** The one fix is a prose correction inside text the first pass had just read; it
added no API, changed no tooling and created no unreviewed code.

### The independent verifier

A fresh agent (amendment 16 limb (d)), briefed with nothing but this tree and `docs/VERIFICATION.md`'s
do-not-use-git clause. **Twenty-two claims across nine groups: twenty-two CONFIRMED, none DIFFERENT,
none COULD NOT RUN.**

**CONFIRMED:** the ratchet (GREEN, 6123/6123, 6015 distinct ids, baseline 6015) and that
`tests/parity-baseline.json` gains exactly the two new test ids and loses none; the replay tally
character for character and the entry each of the six rows lands under, by report line; the probe at
exit 0 with every one of its claimed lines on all six rows, the "required string gone" line included;
the row literals re-recorded key by key; **all six judged answers on BOTH legs** - equal to the
report's own `port` line, and equal to the verifier's own anchor sweep and codepoint-to-UTF-16
conversion against live upstream; the two gap tests at 27/27 and **every expected value in them
against a live upstream run**, the (start, end) to (index, length) conversion and the `(?a)` flag
word included; **Control A on every particular** - `expected 12, diverge 25`, the three flipped rows
being exactly 22, 24 and 36, the md5 after the hand revert and the restored tally; that no file under
`src/` changed; the oracle build at 0 warnings 0 errors and Pester at 81/81; all four citations; and
the row-16 claim checked over all sixteen rows rather than sampled.

It used no `git checkout`, `restore`, `stash`, `reset` or `clean` and reverted the control by
re-editing. Cleaning up it removed the whole of `.scratch/` rather than only its own files; `.scratch`
is gitignored throwaway and nothing outside it moved, so the tree is unaffected.

### What sitting 19 inherits

**22 of the 37 to go**, and they are sitting 15's groups B, C, E, F and G less the judged, plus
group A's two strays:

| rows | group |
|---|---|
| 4, 15 | **A**'s residue - still the two nobody can place (both `(?b)(?r)` partial searches whose flagless answer is a PARTIAL, entry 13's shape; neither port-side control moves either; sitting 15's `endpos=0` worry stands) |
| 7, 9 | **B** - upstream's `(*SKIP)` answer is its own only |
| 16, 26, 30, 33 | **C** - overlapped scan, the verb moving a bound between matches |
| 5, 21, 31 | **E** - same span, different change positions or KINDS |
| 1, 2, 8, 11, 27 | **F** - `split`, `sub`, `subf`, no match object |
| 6, 14, 19, 28, 32, 34 | **G** - a residue, not a family |

**Group C is the natural next one**: four rows, one shape, and four `overlapped-skip-*` entries plus
`skip-carried-slice-on-a-scan-with-no-walk` already carry its argument and its probes. Take the
uncapped sweep with you - this sitting's row 24 is the proof that a capped sweep can name the right
anchor by luck and the wrong one just as easily.

**Also still untouched and still not S52's**: the 20-seed sweep run and the 6000-row three-seed gate
are S57's by the owner's ruling, and S52 runs no further sweeps.
