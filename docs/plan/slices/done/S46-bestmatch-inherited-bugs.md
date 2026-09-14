---
slice: S46
phase: 6
title: Inherited BESTMATCH and POSIX-fuzzy bugs - ledger entries 12, 13 and the port half of 9
delivers: []
---

# S46 - The inherited `BESTMATCH` family

Three ledger entries the oracle cannot see because this port reproduces upstream faithfully. All
in `Matcher.DoBestFuzzyMatch` and the POSIX save/restore, so one slice. Each is judged to amendment
16's standard before it is fixed, and each fix is a deliberate divergence pinned with an entry.

## Scope

- **Entry 12: `BESTMATCH` loses a match whose best fit needs two trailing insertions.** Measured
  mechanism (S41/S42): the guard at `_regex.c:15515-15517` double-counts, and the second pass climbs
  only to `fewest_errors`. Reproduce; confirm the intended answer from the fuzzy definition and
  from plain `{e<=n}` on the same subject, which finds it; fix; pin; enter.
- **Entry 13: `BESTMATCH` loses a partial match that the same pattern's own `match` finds.** Five
  rows, all `(?b)` plus fuzzy plus `(*SKIP)` plus `partial=True` (`bestmatch-loses-a-partial`).
  Self-refutation judges it, as S40b used: an answer the same engine's anchored door beats is wrong.
  Fix so the port answers what its own `match` answers; the entry flips from "upstream's, port
  agrees" to "upstream's, port right" and its `Example` is re-recorded.
- **Entry 9, port half: `best_fuzzy_counts` AND the changes copy in `SaveBestMatch` /
  `RestoreBestMatch`, both or neither** (S41). Upstream copies the counts without the changes and
  segfaults when `fuzzy_changes` is read on a `(?p)` fuzzy match. Port both together so a POSIX
  fuzzy match reports consistent counts and changes; pin; then lift the `interactions` generator's
  POSIX-with-fuzzy exclusion (DECISIONS) and re-run the wave. Upstream's crash must arrive as a
  recorded `error` or `timeout` row, not a dead recorder; prove that first on one row.
- **Do not touch** the cost ranking (S42) or the `same_match` override (S41).

## Verification

- A gap test per entry asserting the corrected answer with upstream's quoted beside it; a narrow
  divergence entry per fix; a negative control per fix reverting it.
- `fuzzy` with `(?b)` and the default wave GREEN at three seeds; `interactions` with POSIX no
  longer excluded, GREEN.

## Done when

- [x] ~~Entries 12, 13 and 9's port half fixed test-first, each with its definition quoted.~~
      **CORRECTED 2026-09-14 (S47b):** entry 12 was fixed, with `README.rst:592` quoted and 111 lines
      of `FuzzyBestMatchTests.cs` written for it - but the tests were written ALONGSIDE the guard
      change, not before it, and nowhere do these notes record how many were red without it. Entries
      13 and 9 were not fixed at all: they needed no work, which this slice states plainly at
      `:56-62` with `Matcher.cs:2092-2094` and `:2121-2123` cited. So the box as written overstates
      on both halves. The honest box is "entry 12's guard fixed and pinned by three new tests;
      entries 13 and 9's port halves measured as already correct". The audit graded it ASSERTED ONLY
      and it is left ticked because the WORK landed - what was missing is the failing-first evidence,
      and S47b did not re-run it from a stashed `src/` because that measures a guard nobody is
      changing. The red-first discipline is evidenced instead where S47b's own changes are.
- [x] Divergence entries, controls, ledger updates; POSIX exclusion lifted.
- [x] Ratchet GREEN and blind review done in sitting 1; the box closes with the slice.

---

# Sitting 1, 2026-09-14 - A CHECKPOINT, not a close

Entry 12 is fixed and entries 13 and 9's port half needed no work. **The POSIX exclusion is NOT
lifted**, which is why the slice file stays here. What is left is one bounded piece with a measured
design, written out in full in ledger entry 9.

## Two of the three scope items were already closed, and the slice file could not know it

- **Entry 13's port half was closed by S43.** The scope says "fix so the port answers what its own
  `match` answers"; it already did, and the entry's own text says so - the five wave rows are
  classified with this port's answer as the right one. Verified rather than assumed, and nothing was
  changed for it.
- **Entry 9's port half was closed by S43 too**: `Matcher.SaveBestMatch` and `RestoreBestMatch`
  copy `MatchState.BestFuzzyCounts` AND `BestFuzzyChanges` both ways (`Matcher.cs:2092-2094`,
  `:2121-2123`). The slice's "both or neither" was already satisfied.

So the engine work was entry 12 alone.

## Entry 12: the mechanism was right and the SYMPTOM was recorded wrong

The entry said "*n* trailing insertions need `max_errors` above *2n-1*", which reads as though a
large enough budget buys the match. Measured instead of trusted
(`tools/probes/upstream-bestmatch-trailing-insertions.py`, regex 2026.9.10, `fullmatch` of `(?:x){e<=N}` over `'x'` plus
*k* trailing characters):

```
              N=0   N=1   N=2   N=3   N=4   N=5   N=6
plain  k=2      -     -    i2    i2    i2    i2    i2     matches exactly when N >= k
(?e)   k=2      -     -    i2    i2    i2    i2    i2     matches exactly when N >= k
(?b)   k=2      -     -     -     -     -     -     -     never, at any N
```

Under `(?b)` the user never sets `max_errors`: the second pass sets it to `fewest_errors` = *n*, so
the doubled guard needs *n > 2n-2*, false for every *n >= 2* at every budget. Leading insertions and
substitutions are untouched, which places the defect in the trailing-insertion arm. The fix is the
entry's own proposed fix, one conjunct: `TotalErrors(state.FuzzyCounts) < state.MaxErrors`.

**The absence of a second engine is itself measured, not asserted.** Amendment 16 asks for a real
run of one, so `python tools/probes/pcre2-has-no-fuzzy-matching.py` was run on the `pcre2` binding
0.7.1 over libpcre2 10.47 that the orchestrator installed mid-sitting for exactly this. PCRE2 does
not merely lack fuzzy matching - **it reads the suffix as LITERAL TEXT**, which is worse than an
error, because a comparison built on it answers confidently and wrongly:
`pcre2.compile(r'(?:x){e<=3}').match('xyz')` is `None` and `.match('x{e<=3}')` is `(0, 7)`. Only
`(?b)` fails loudly, and only because PCRE2 has no such flag. Perl and .NET have no approximate
matching; TRE and agrep do, and neither implements upstream's `{...}` syntax or its `BESTMATCH`
ranking, so neither answers this question.

So the judgement quotes the definition and rests on self-refutation. `upstream/README.rst:592`:
"By default, fuzzy matching searches for the first match that meets the given constraints ... The
`BESTMATCH` flag will make it search for the best match instead." A ranking flag chooses among the
flagless engine's candidates; it cannot destroy them all.

## What the fix moved, and why nine rows made the oracle entry hard

The 6000-row three-seed gate moved **nine rows**: one at seed 7, seven at 4242, one at 20260914.
Every one was judged individually (`tools/probes/upstream-bestmatch-free-answer.py`) and **on all nine this port's
answer is upstream's own BESTMATCH-free answer EXACTLY** - groups, counts and change positions.
Four are upstream losing the match outright; three are both engines matching at the same error
count with a different mix; one is a `sub` and one a `finditer`.

Two ways of writing the `ExpectedDivergences` entry were tried and rejected with evidence:

- **Row-keyed**, as `bestmatch-loses-a-partial` is: the family's rows renumber with the ROW COUNT as
  well as the seed - the seed-7 gate draws different questions at 6000 and at 6300 - so a row-keyed
  arm reds every wave forever.
- **A predicate over the two compared answers**: seed 20260914 row 76927 has the SAME span, the SAME
  groups and the SAME counts `(2,2,0)` on both sides, differing only over which of positions 7 and 8
  is the substitution. Nothing on our side of the comparison sees that as a family.

So the recorder now asks upstream a second question on every `(?b)` row - the same row with the flag
deleted - and records it as `bestmatchFreeOutcome`, the `searchOnlyPartial` precedent. The entry
`bestmatch-loses-a-candidate` fires only where this port's answer IS that answer.

**It subsumes entry 13's family, and that was not planned.** Seed 20260914 row 76345 was diverging
UNCLASSIFIED at HEAD, carries entry 13's four conditions exactly, and is now accounted for: the
discriminator is entry 13's own weak form made checkable, and is strictly narrower than the
predicate that entry considered and rejected.

## The default wave was ALREADY RED at HEAD, and that is not S46's

Proven rather than asserted: the three recorded waves were re-consumed with **HEAD's engine in a git
worktree** (`.claude/worktrees/s46-head`, which needs the waves copied in because a worktree has no
submodule). HEAD gives 3 + 2 + 10 = **15 divergences**; with S46 the same waves give 3 + 2 + 9, the
missing one being 76345 above. So **S46 introduces zero unclassified divergences and retires one**,
and 15 rows predating it remain. S45's closing notes record "Default wave GREEN at all three seeds,
6300 rows each: expected 4 / 1 / 2" - the full default wave gives `expected` 63 / 67 / 42 at 6000
rows and 66 at 6300 on seed 7, so that figure cannot be the full default list. **Fifteen untriaged
rows are a slice of their own**, the S40a pattern repeating; they are listed in STATE.md.

## POSIX: the gate fails as the recorder stands, and the cheap way out is measured

The slice asked to prove on one row that upstream's crash arrives as a recorded `error` or
`timeout`. It does not: `python tools/record-oracle.py --rows` over a file holding
`(?p)(?:abc){e<=1}` on `'axc'` exits **139** and writes no output file at all.

Entry 9's spent-error rule holds exactly on 2026.9.10 (`tools/probes/upstream-posix-fuzzy-spent-error.py`, each case in
its own child), and it produced the sharpest demonstration the report will ever have that no
predicate is safe: `(?p)(?:abc){e<=1}` over `'abcd'` **looks** like an exact match and faults,
because POSIX leftmost-longest stretches it to spend an insertion. Not even the subject is a safe
test.

The lever is that `fuzzy_counts` is safe on a faulting row and only `fuzzy_changes` faults, and
`_describe_match` reads the changes only when the counts are non-zero. A recorder that records
`fuzzyCounts` and OMITS `fuzzyChanges` on a POSIX row never touches the faulting access - no
per-row process isolation needed. The cost is the change positions, which upstream has no answer
for anyway. **Left undone deliberately**: it needs the recorder, `OracleWave`, the comparison in
`OracleComparer` and the generator's suppression changed together, with every path that reads a
match guarded - `finditer`, `sub` and `split` as well as the single-match door - because missing one
kills the wave rather than failing a test. Full design in ledger entry 9.

## Controls

Registered in `tools/controls.json`, and every figure below re-run against the code and generator
being committed. **`S42-2G` was broken by this slice's own edit and is fixed**: its `before`
quoted the doubled conjunct, so its site stopped resolving; the snippet now quotes the committed
line and `python tools/run-controls.py --check` resolves it again. That takes the broken-site count
from six to five.

> **Control S46-A, `bestmatch-doubled-guard`**: in `Matcher.cs`, the `END_FUZZY` BACKTRACK arm
> (`case Opcode.EndFuzzy: // End of fuzzy matching (:15488).`), change
> `                        && TotalErrors(state.FuzzyCounts) < state.MaxErrors`
> back to
> `                        && TotalErrors(state.FuzzyCounts) + TotalErrors(innerCounts) < state.MaxErrors`.
> Wave: `fuzzy`, 6000 rows, seeds 7, 4242 and 31337. Result: **accounted-for 0 / 0 / 0** against an
> unmutated **1 / 7 / 1**, diverge 0 / 0 / 0 either way. The signal is the `expected` column, not
> `diverge`, because reverting the fix makes this port AGREE with upstream again - which is the
> whole shape of an inherited-bug fix. **At 2000 rows it measures nothing** (0 / 0 / 0 both ways):
> the `fuzzy` generator draws none of the family at that size, which is why the control is
> registered at 6000.
>
> **Control S46-B, `bestmatch-guard-off-by-one`**: same site, `<` to `<=`. Wave: `fuzzy`, 6000 rows,
> seeds 7, 4242, 31337. Result: **1 / 7 / 1 accounted-for, 0 / 0 / 0 diverge - identical to the
> unmutated baseline, so the WAVE DOES NOT SEE IT AT ALL.** The suite does, and violently: the same
> mutation HANGS `dotnet run --project tests/FuzzyRegex.Tests`, which normally finishes in 21
> seconds, past 600 with the test host at 19 GB resident. That matches the `ponytail:` note at the
> site, which says what the third conjunct defends is a hang rather than a wrong answer. **Never run
> this mutation against the suite unattended**; the hung host then holds `FuzzyRegex.dll` and blocks
> every later Debug build, which is what happened in this sitting.
>
> **Control S46-C, `bestmatch-stops-ranking`**: in `DoBestFuzzyMatch`, change
> `                    state.MaxErrors = fewestErrors - 1;` to
> `                    state.MaxErrors = 0;`, so `(?b)` keeps the earliest match rather than the
> best. Wave: `fuzzy`, 6000 rows, seeds 7, 4242, 31337. Result: **diverge 0 / 0 / 1** against an
> unmutated 0 / 0 / 0, with accounted-for moving **1 -> 3** at seed 7. Suite: **5 of 5,947 tests
> fail**. This control exists to measure how WIDE the new entry is, and the answer is: wide enough
> to swallow two rows of a stops-ranking defect at seed 7, and the wave still reports one row of it
> at seed 31337, and the suite is the instrument that actually catches it. Recorded in the entry's
> own `Reason` rather than only here.
>
> A fourth was attempted and abandoned: routing the `BestMatch` branch of `DoMatch2` to
> `DoSimpleFuzzyMatch` does not compile - `error S1144: Remove the unused private method
> 'DoBestFuzzyMatch'` - which is the same failure `tools/controls.json` already records for S42-2G's
> mutant. S46-C is the same question asked from inside the function instead.

## Review

**One blind pass over the whole diff, and it raised NO findings** - so none was reproduced and none
was fixed. **No second pass was needed**: everything that changed after the reviewer started was
documentation, one private constant renamed (`_bestmatchDoubledGuardRows` to
`_bestmatchLostCandidateRows`) and CSharpier formatting. No public API, no new logic, and the
reviewer's own report confirms it re-checked that "the `Applies` predicate and the `Matcher.cs` guard
are unchanged from what I verified". The ratchet, the full three-seed gate and the oracle consumer
were all re-run green after those edits.

**The pass is worth more than "no findings", because it built an independent ground truth this slice
did not have.** Asked to hunt for the relaxed guard over-spending its budget, it wrote a Levenshtein
probe - the minimum edit distance computed from first principles, not from either engine - and swept
`{"", (?b), (?e), (?b)(?e), (?r), (?b)(?r)}` x literal patterns of length 1-3 x subjects of length
0-4 x budgets 0-4, plus a four-shape composite sweep covering two adjacent sections, nested sections,
an `(?e)` wrapper and a group call. **53,196 cases, 0 failures**: no match reported more errors than
its budget, and every `(?b)` match reported exactly the Levenshtein minimum. Slowest single call
63 ms, nothing thrown, so hunt item (b) - non-termination - is answered too.

**And the probe has teeth, which is the part that makes the zero meaningful.** Run unchanged against
a HEAD worktree carrying the old doubled guard it gives **252 failures**, every one a `MATCH-LOST` on
`(?b)(?:a){e<=N}` for N >= 2 - the defect this slice fixed, found independently and characterised the
same way as the `(k, N)` matrix above.

Two more hunt items were answered by measurement rather than by reading. The recorder's second pass
was checked for contamination by recording 400 seed-7 fuzzy rows twice in one interpreter, once
through the new call site and once with it rebound to plain `_record_row`: 96 of 400 rows carry
`(?b)`, all 96 got a `bestmatchFreeOutcome`, none without `(?b)` did, and the two files are
byte-identical once the new key is stripped. The unanswerable-answer branch was checked by recording
a row upstream exhausts itself on and a row that will not compile: both record `free null`, so the
key is correctly omitted and the entry does not apply.

**Two things the reviewer reported that are NOT this slice's, both confirmed against HEAD:**

1. **`python tools/record-oracle.py --self-check` exits 1**, on "an interpreter limit rather than a
   judgement about the pattern: was recorded as if it were upstream's answer". Loading
   `git show HEAD:tools/record-oracle.py` and calling its `_self_check()` gives the identical
   message, so it predates this diff. Added to the owed-maintenance list in STATE.md.
2. The `interactions` seed-7 row `(?e)\b\L<w1>{e<=2}([^a-f]{1}?)` over `'ﬁ ı'` is red and
   unaccounted - the dotted/dotless-I family, no `(?b)`, no trailing insertion. It is one of the
   fifteen pre-existing rows listed in STATE.md.

---

# Sitting 2, 2026-09-14 - THE POSIX EXCLUSION IS LIFTED, and the slice closes

Sitting 1 fixed entry 12 and found entries 13 and 9's port half already closed by S43. This sitting
did the one piece left: the `interactions` generator draws POSIX beside a fuzzy section again, and
both engines are compared on it.

## It was ONE guard, where ledger entry 9's design feared four

The design said the change needed "every path that reads a match - `finditer`, `sub`, `split` as
well as the single-match door - guarded, because missing one kills the wave rather than failing a
test". They all funnel through `_describe_match`: the single-match door (`record-oracle.py:684`),
`finditer` (`:639`), a `(*SKIP)` substitution's `subMatches` (`:607`) and `_anchored_scan` (`:896`).
`sub` and `split` answer a string and a list of parts and read no match at all. So one guard in that
function covers every door, and it is also the only place the recorder has ever read the faulting
attribute.

The guard keys off POSIX rather than off "this match spent errors", which is the real faulting
condition, because nothing readable off the pattern OR the subject predicts a spent error -
`(?p)(?:abc){e<=1}` over `'abcd'` looks exact and faults, because leftmost-longest stretches it into
spending an insertion.

## Four measurements, not four arguments

1. **`compiled.flags` is a safe POSIX test on every spelling.** The flag, a leading `(?p)`, one
   written mid-pattern, one inside a group: all set the bit
   (`tools/probes/upstream-posix-flag-is-visible-on-compiled.py`, regex 2026.9.10). The row's own
   `flags` field would miss an inline `(?p)`, which is how `interactions` writes half of them.
2. **Every other read `_describe_match` makes is safe on a faulting match** - `span(n)` and
   `spans(n)` over the whole group range, `lastindex`, `lastgroup`, `partial`, plus the `finditer`,
   `subn` and `split` doors. One child process per read:
   `tools/probes/upstream-posix-fuzzy-safe-attributes.py`. Only `fuzzy_changes` dies (0xC0000005).
3. **Before and after on the same sixteen rows.** `git show HEAD:tools/record-oracle.py` over
   `.scratch/posix-fuzzy-rows.jsonl` exits **139** and writes no file; the guarded recorder writes
   all sixteen, and the consumer then agrees with upstream on **16 of 16** - spans, groups and error
   counts, with the positions dropped from both sides.
4. **Only `interactions` reaches the cell.** A 12,000-row `posix,fuzzy` wave at seed 7 holds **zero**
   POSIX-and-fuzzy rows, so no other generator's rows move.

The consumer renders a marked row as `fuzzy=(s,i,d)[changes unavailable upstream]` on both sides,
spelled out rather than shown as three empty lists so a divergence block says why it carries no
positions instead of looking like an engine that spent errors nowhere.

## Lifting it found a bug in THIS PORT on the first run, and that bug is left open

Seed 31337 row 3343, the one new divergence the whole change introduced. Minimised from a
hundred-character pattern to this, twelve candidate rows at a time through `run-oracle.ps1 -Rows`:

    regex.compile(r'(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}', regex.POSIX)
      .fullmatch('+ aBA')
    # upstream:  (0, 5) fuzzy_counts (0, 1, 1)   - two errors
    # this port: (0, 5) fuzzy_counts (1, 1, 1)   - three, for the same span and the same groups

**It refutes itself on this port's own behaviour**: drop POSIX and this port answers `(0, 1, 1)` too.
So it is not the cost-versus-error-count ranking difference S42 pinned - it is POSIX losing an error
count that the same engine finds without it, and under `(?e)`, which minimises errors, a three-error
answer beside a two-error one is wrong on its face. It needs POSIX **and** `(?e)`: `(?b)`, `(?r)`
alone, a BMP or astral *word* character in place of the `+`, and the same row without POSIX all
agree. The astral subject the wave drew turned out not to be load-bearing; a leading NON-word
character is.

Standing hypothesis for the slice that fixes it, stated as a hypothesis because it was not proven:
`RestoreBestMatch` puts back `FuzzyCounts` and `FuzzyChanges` but not `state.TotalErrors` or
`state.TotalCost`, and this port's ranking reads both where upstream's simply keeps the last
successful run. Upstream leaves `total_errors` stale in the same place (`restore_best_match`,
`:11565`), so that half is inherited; `TotalCost` is this port's own field and is not.

**No `ExpectedDivergences` entry**, deliberately: that list holds families where this port is right
and a permanent test pins its answer. Here this port is wrong, so the row stays RED until it is
fixed - which is the direction that cannot hide a defect.

## The slice's own gate says GREEN and that was never reachable

Verification asked for "`interactions` with POSIX no longer excluded, GREEN". The `interactions` wave
is **not** green and was not green at HEAD either: 1 / 1 / 5 divergences at seeds 7 / 4242 / 31337
with HEAD's recorder and HEAD's engine, all pre-existing and none of them POSIX. With this sitting's
change the same seeds give 1 / 1 / 6 - the identical rows plus row 3343 above. The gate as written
could only have been met by a slice that also cleared the pre-existing pile, which STATE.md has
called a slice of its own since sitting 1. The slice closes against the honest reading: no new
unaccounted divergence except the one it minimised and diagnosed.

**The default wave is unmoved.** 6000 rows, all 21 generators, seeds 7 / 4242 / 20260914:
3 + 2 + 9 = 14 divergences, and every row number is one STATE.md already lists. Nothing new, and the
one new row does not appear because 31337 is not a default seed.

## Controls

> **Control S46-D, `posix-restore-keeps-the-losing-counts`**: in `Matcher.cs`, `RestoreBestMatch`,
> change
> `        state.BestFuzzyCounts.CopyTo(state.FuzzyCounts, 0);`
> to
> `        state.FuzzyCounts.CopyTo(state.BestFuzzyCounts, 0);`
> so the live counts of the attempt that deliberately failed survive instead of the saved best's.
> Wave: `interactions`, 6000 rows, seeds 7, 4242, 31337 and 99991. Result: **diverge 40 / 29 / 37 /
> 27** against an unmutated **1 / 1 / 6 / 1**. Registered in `tools/controls.json` and re-run against
> the code and generator being committed.
>
> **And the same control against a wave recorded by HEAD's recorder moves NOTHING** - 5 divergences
> against an unmutated 5 at seed 31337 - because that wave holds **zero** POSIX-and-fuzzy rows. That
> is the measurement that says lifting the exclusion bought coverage rather than rows: a defect in
> the POSIX fuzzy restore was invisible to `interactions` at any wave size, and is now caught on 31
> extra rows in 6000. To re-run that half: `git show HEAD:tools/record-oracle.py` into a scratch
> file, record `interactions` 6000 at seed 31337 with it, copy the result over
> `.scratch/control-waves/interactions-6000-31337.jsonl`, and run
> `python tools/run-controls.py --ids S46-D`, which reuses a wave already on disk.
>
> **A stale wave nearly made this control lie.** `run-controls.py` reuses
> `.scratch/control-waves/<generator>-<count>-<seed>.jsonl` if it exists, and the seed 7 and 4242
> files were left there by an earlier slice, recorded with the exclusion in place. The first run gave
> 2 / 6 / 37 against a baseline of 1 / 1 / 6 and looked like a weak control at two seeds out of
> three; deleting the two stale files and re-running gave 40 / 29 / 37. **Delete the waves for any
> generator whose generator code the slice changed before running its controls.**

## Review

**One blind pass over the whole sitting-2 diff. Three findings raised, three reproduced, three
fixed** - and none of them in the recorder or the consumer, which is the part the pass was pointed
at hardest.

1. **The four `_describe_match` call-site line numbers quoted in this file and in ledger entry 9 were
   HEAD's, not the changed file's**, because the `_POSIX_FLAG` block shifted everything below it.
   Reproduced with `grep -n "_describe_match(compiled" tools/record-oracle.py`: 600 / 632 / 677 / 863
   are really 607 / 639 / 684 / 896. Corrected in both files. A stale reference in the very commit
   that writes it is the kind that never gets found later.
2. **`tools/probes/upstream-posix-fuzzy-safe-attributes.py` measured a GROUPLESS pattern** while
   three code comments cited it for a claim about `span(n)` and `spans(n)` "over the whole group
   range": `regex.compile(r'(?p)(?:abc){e<=1}').groups` is 0, so both reads only ever asked about
   group 0. The conclusion survives - the reviewer measured a grouped pattern itself and every read
   was still safe - but the evidence did not support the claim. The probe's pattern is now
   `(?p)(?P<g1>(?:abc){e<=1})(?P<g2>d)?`, two groups with one taking no part in the match, and its
   output shows `[(0, 3), (0, 3), (-1, -1)]`, a real `lastindex` of 1 and a real `lastgroup` of
   `'g1'` where the groupless version answered `None` to both.
3. **The same probe rendered an ordinary `IndexError` as a process death**, in a probe whose whole
   point is telling those apart: a `m.span(1)` case on a pattern with no group 1 printed
   `*** DIED rc=1 ***` beside the real `*** DIED rc=3221225477 ***`. The bogus case is gone with the
   grouped pattern, and `verdict()` now separates a fault (a negative return code, or Windows's
   0xC0000005) from an exception, which prints as `raised (rc=N)`.

**A second blind pass was run over the delta**, because finding 2's fix rewrote a tooling file the
first reviewer only saw in its earlier form. Scope: that one probe, hunting for a claim the probe
does not measure, a mislabelled verdict, an "ok" for an expression that never evaluated, and a
docstring stating a result the run does not produce. **No defects found**, with the re-run output and
a line-by-line check of the `READS` list against every attribute `_describe_match` touches.

**What the first pass proved rather than merely failed to disprove**, because a "no finding" on the
recorder is only worth as much as what was tried:

- **No surviving `fuzzy_changes` read.** 30,000 `interactions` rows over five seeds and 12,000
  `fuzzy,posix` rows recorded with `exit=0`, and - the sharp test - a scratch copy of the recorder
  with `INTERACTION_POSIX_PROBABILITY = 1.0` recorded 24,000 rows at four seeds, every one POSIX,
  holding 1,356 fuzzy matches, `exit=0`, and **zero** rows where a `fuzzyChanges` key leaked onto a
  POSIX row. The pre-change recorder over the same input exits -1073741819 and writes no file.
- **The two sides agree about which rows are POSIX.** The 6,000-row all-POSIX wave consumes with no
  report line anywhere containing `[s:`, so the port dropped positions on all 6,000; and a
  hand-built twenty-row file covering `(?p)` leading, mid-pattern, inside a group, inside a
  lookahead, inside an alternation branch, under `(?V1)`, with `(?e)`, `(?b)`, `(?r)` and `(?i)`,
  astral, and the flag spelling, agrees 20 of 20.
- **Non-POSIX rows kept their positions**: 303 matches with `fuzzyChanges` in the seed-7 wave, and a
  mutation still diverges on their rendered positions.
- **The draw stream did not shift**: old against new recorder at seed 1, 600 rows, gives zero
  question differences over all 600, 30 rows differing only by POSIX being added, and zero outcome
  differences on identical questions. The updated docstring counts were checked exactly.
- **Waves recorded before the change still parse and compare identically** - the same seed-1 wave
  recorded by both recorders consumes to `agree 598 unsupported 0 expected 2 diverge 0`.

Suites at the end: ratchet GREEN, 5,947 tests, 5,947 passing, 0 skipped; the oracle harness's own 15
tests pass.
