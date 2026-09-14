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

- [x] Entries 12, 13 and 9's port half fixed test-first, each with its definition quoted.
- [ ] Divergence entries, controls, ledger updates; POSIX exclusion lifted.
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
