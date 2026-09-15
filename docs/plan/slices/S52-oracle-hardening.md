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
