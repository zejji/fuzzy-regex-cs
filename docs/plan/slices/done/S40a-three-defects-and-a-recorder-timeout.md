---
slice: S40a
phase: 5
title: The three defects S40 recorded, and the recorder timeout that lets a 6000-row wave finish
delivers: []
---

# S40a - Three recorded defects, and a recorder that survives an upstream hang

Added at S40's close on 2026-09-13, on the Phase 4 precedent that S33, S34 and S35 set: **every one
of these came out of verification getting stricter, and each is a real defect.** None is S40's
doing and none is a new feature; S40 proved each one and deliberately fixed none of them, because
fixing four unrelated engine and tooling problems inside a slice about the `{...:test}` constraint
is how a slice stops being reviewable.

The ROADMAP's Phase 5 note predicts exactly this ("its 5-8 counts fuzzy features, and the oracle
will add slices to it"), and amendment 16 is why they cannot simply be logged: a bug identified with
overwhelming evidence is fixed here or filed upstream before 1.0.

**Do the recorder first.** Items 2 and 3 are only measurable once a 6000-row default wave can
finish, and item 1 is what stops it finishing.

## Scope

### 1. The recorder has no per-row timeout, so one hanging upstream row kills a whole wave

`tools/record-oracle.py` runs every row through upstream and waits forever. At 6000 rows a
generator, `verbs` row 5944 at seed 4242 never returns, so the wave never gets written and the
`run-oracle.ps1` run simply stops - no output, no error, no partial file. S40 lost about forty
minutes to that before bisecting it.

`regex` takes a `timeout=` keyword on the match methods and raises `TimeoutError`, measured on
2026.7.19:

```python
pat.search(subject, timeout=5)   # TimeoutError: regex timed out
```

So the row becomes a recorded outcome rather than a wave-killer. Decide what the row's outcome
should be - a new `timeout` kind alongside `error`, most likely, which the consumer skips the way
it skips `unsupported` - and say why in DECISIONS. **Do not** simply drop the row silently: a
generator that quietly stops emitting a shape is the failure S38's and S39's controls each hit from
a different direction.

### 2. Upstream never returns from `(*SKIP)` inside an atomic group after an optional item

Minimised by hand from the row above, on regex 2026.7.19:

> `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')` never returns.

All three parts are needed. `(*PRUNE)` in place of `(*SKIP)` returns `None`; a non-atomic `(?:...)`
returns `None`; dropping the leading `.?` returns `None`. The original was
`[^a]?\U0001f600(?>[a\d]{1,3}(*SKIP)\p{Ll})` on `'\U00010400\r_\U0001f600\xdf\U0001f600aA'`.

This is a new upstream infinite loop, the same family as issues 551 and 554 but a different shape.
Give it the S33 treatment - probe, research the tracker for a duplicate, verdict, blind review -
and then a **ledger entry** in `docs/plan/upstream-reports/LEDGER.md`. Nothing is filed upstream
until everything else in the plan is done, and the owner approves the text first. Check what this
port does with the same pattern: if it also hangs, that is a second, separate defect and this port's
is the one that gets fixed.

### 3. Four rows of the 6000-row default wave diverge at seed 7, all of them predating S40

Proven against HEAD `26e2a20` by consuming the identical saved wave with `-SkipRecord` in a
worktree: HEAD reports the same four rows.

| row | generator | operation | pattern |
|---|---|---|---|
| 97927 | `partial` | search | `\b\K\K(?:\D(*SKIP)[^\p{L}]\|.)` |
| 98956 | `partial` | search | `𐐀(?P<g1>[𐐀])(?:(?(1)(?<=(?P>g1))[\p{L}\p{N}]\|\p{L}))?\b` |
| 103926 | `partial-sliced` | fullmatch | `(?r)\b(?P<g1>[\w\s]+)(?:(?(1)(?!(?P>g1))\s))+?\b\M` |
| 117679 | `verbs` | finditer-overlapped | `\b(?:[^a](*SKIP))*` |

Two of the four are partial matching plus a stale-`\K`/group-call interaction and two are `(*SKIP)`;
117679 looks like the `overlapped-skip-stale-slice` family whose predicate does not cover it.
Minimise each into a permanent test and judge it by amendment 16: port's fault, fixed; upstream's
fault, pinned and entered in the ledger; predicate too narrow, widened in
`ExpectedDivergences.cs` with the reason.

### 4. A fuzzy section inside a lookbehind reports change positions that contradict its own counts

Found by S40's blind review and reproduced there against both engines.

| pattern | subject | upstream | port |
|---|---|---|---|
| `(?<=(?:[ab][cd]){e<=1})$` | `axc` | `counts=(1,0,0) changes=([2],[],[])` | `counts=(1,0,0)`, `dels=[1]` |
| `(?<=(?:abc){e<=2})$` | `ac` | `counts=(1,0,1) changes=([1],[],[0])` | `counts=(1,0,1)`, `dels=[1,2]` |

The port's own `FuzzyCounts` and `FuzzyChanges` disagree with each other, which makes this a defect
on its face before upstream is consulted at all. **It is not `(?r)` in general**:
`(?r)(?:[ab][cd]){e<=1}` against `'axc'` reports `subs=[2]`, correctly. So the fault is in what
`record_fuzzy` writes when the section is inside a lookbehind, which is S38 and S39 territory
(`Matcher.RecordFuzzy`, and the change-position arithmetic S38's control F covers).

When it is fixed, **strengthen
`Gaps/Engine/FuzzyTestConstraintTests.The_REV_arms_test_the_character_before_the_position`** to
assert `FuzzyChanges` as well; a comment in that test says so and says why it currently does not.

## Verification

- A gap test per defect, measured against upstream and quoted, before any fix.
- The default wave GREEN at three seeds, **6000 rows** - the thing S40 could not run. That is the
  point of item 1 and it is the slice's real exit gate.
- A negative control for the recorder timeout: a row that hangs must appear in the wave as a
  recorded outcome, and the wave must still be written.
- Ledger entries drafted for anything judged upstream's, not filed.

## Done when

- [x] The recorder survives a hanging row and records it; DECISIONS says what the outcome kind is.
- [x] The `(*SKIP)`-in-an-atomic-group hang is researched, judged, and in the ledger; this port's
      behaviour on the same pattern is known and, if it hangs too, fixed.
- [~] All four seed-7 divergences minimised into permanent tests and judged by amendment 16.
      **One fixed, three minimised and pinned; the two unsettled ones are S40c's, with the
      measurement that settles which engine has to explain itself.** See both sessions below.
- [~] The lookbehind `FuzzyChanges` bug fixed test-first, and the S40 test strengthened.
      **Resolved the other way: it is not this port's bug**, so there was nothing to fix and
      `FuzzyTestConstraintTests.The_REV_arms_test_the_character_before_the_position` is **still not
      strengthened** - it asserts `FuzzyCounts` only, and its own comment now says why, because
      asserting `FuzzyChanges` there would pin upstream's contradiction as this port's answer. The
      box stays half-ticked rather than ticked: the premise was overturned, not the work skipped.
- [~] Default wave GREEN at three seeds at 6000 rows; ratchet GREEN, baseline updated, blind
      review, commit. **Everything but the wave. The gate is S40d's**, because the fifteen rows it
      reports are three mechanisms and two of them each need a pinned answer re-judged - which is
      not one slice, and a wave red on three causes cannot tell a new divergence from an old one.
      S40b, S40c and S40d are written and carry the triage.

---

# Session 1, 2026-09-13: what landed, and why this slice is still open

**Two of the four items rested on premises that measurement overturned**, so this session's honest
output is one engine fix, the recorder, two ledger entries and six permanent tests - not four fixes.
The exit gate is not met and is much larger than the slice supposed. Read the four judgements before
re-planning it.

## 1. The recorder timeout - DONE

`tools/record-oracle.py` gives every upstream call `timeout=10` and records a row upstream never
finishes as a new outcome kind, `{"kind": "timeout", "seconds": N}`. The consumer reads it as
`TimeoutOutcome`, skips the row without asking this port (there is no ground truth to compare
against, and asking would spend `RowTimeout` per row for no information), and counts it under a
verdict of its own so the report's summary line shows it. Ten seconds to match
`OracleComparer.RowTimeout`: neither engine gets longer than the other to answer the same question.
The constant, never an elapsed time, is what keeps `--verify-determinism` sound.

**It fired on a real wave**: seed 4242 at 6000 rows records `timeout 1`. That is the row S40 lost
about forty minutes to, now a recorded outcome instead of a silently unwritten file.

## 2. The `(*SKIP)`-in-an-atomic-group hang - DONE, and it is nobody's to fix here

Upstream's, and **upstream has already fixed it**: commit `b77694a`, issue 613, released 2026.8.30,
past our pin. Ledger entry 10 has the reproduction. This port answers `None` in 17ms.

The measurement that makes that mean something is the grid, because this port still carries the
pre-fix `GreedyRepeatOne` backtrack clamp: `python tools/probes/upstream-skip-in-atomic-hang.py
--grid` puts 1296 calls of the same shape to each engine - **2026.7.19 hangs on 70, 2026.9.10 on 0,
this port on 0**. A grid that never reached the shape would have given this port the same zero.

## 3. The four seed-7 divergences - ONE FIXED, THREE OPEN

**Row 117679 (`verbs`) is fixed**, and the fix is the one ledger entry 5 proposes for upstream:
`DoMatch` now puts `SliceStart`/`SliceEnd` back at the start of every match. A `(*SKIP)` moves the
slice mid-attempt and upstream restores it nowhere, so one scanner state carried a moved slice into
the next match - and with no `search_start` prefilter this port then LOST matches, where upstream's
optimiser skips past the problem. Measured: seed-7 wave 4 divergences -> 3, suite green, all 37
`ExpectedDivergences` rows still firing.

**Row 97927 (`partial`) is a port defect this slice deliberately did not fix.** Minimised to
`\b\D(*SKIP)z` over `' A'` asked with `partial`: the search answers (2, 0) where this port's own
`MatchAtStart(' A', 1, partial)` answers (1, 1), so the search is not leftmost - which settles it
without upstream, and upstream answers (1, 1) too. The cause is the same carried slice one level
down: the non-partial pass moves `slice_start` and the partial pass re-runs with it. **Restoring it
there fixes this row and introduces another** (a reversed partial search, where restoring `slice_end`
changes what every end-of-subject assertion means) **and turns S37's permanent
`A_skip_alternation_partial_starts_where_this_port_ran_out_of_text` red** - which turns out to be the
same defect, pinning (4, 0) where this port's own matcher answers (2, 2) earlier. Those three have to
be judged together. Pinned meanwhile by
`PartialMatchingTests.A_skip_in_the_non_partial_pass_moves_the_slice_and_the_partial_pass_is_no_longer_leftmost`,
which asserts the WRONG answer on purpose and says so.

**Rows 98956 and 103926 are unsettled between two candidate defects, and no verdict was reached.**
Both minimise to a group called through an opposite-direction lookaround over an ASTRAL subject,
where upstream reports a partial and this port reports a complete match. Two facts point at
different engines:

- upstream's partial comes from the CALL - write the call out as its own body, or delete the
  lookaround, and upstream answers a complete match (ledger entry 8's signature);
- this port is inconsistent across astrality - it reports upstream's partial on an ASCII subject and
  not on the astral one, where upstream reports it for both.

Pinned with both controls by
`GroupCallTests.A_group_called_through_an_opposite_direction_lookaround_loses_upstreams_partial_only_on_an_astral_subject`,
explicitly as measurements without a verdict.

## 4. The lookbehind `FuzzyChanges` contradiction - NOT THIS PORT'S BUG

The slice's premise was that the contradiction "makes this a defect on its face before upstream is
consulted at all". The contradiction is real; it is also **upstream's, and S38 had already pinned
it**: `search(r'(?:[ab][bc](*PRUNE)[wx]){e<=2}', 'qab')` is `counts=(0,0,1)` with a SUBSTITUTION at 0
on both engines, unchanged on 2026.9.10. `basic_match`'s `start_match` clears the fuzzy counts and
leaves the change list, and `Match.FuzzyChanges` reports the first `Total` entries, so an abandoned
attempt's change displaces a real one. Ledger entry 11; `python
tools/probes/upstream-fuzzy-restart-leak.py` re-runs it.

**The one-line fix was made, measured and reverted**: clearing the list turns S38's two pinned rows
red and makes this port diverge on rows it currently agrees on. It is an inherited bug, so by the
owner's 2026-09-12 rule it is fixed here before 1.0 - in Phase 6's inherited-bug sweep, beside entry
7, because fixing it means deciding the right answer and carrying a permanent oracle divergence.
The lookbehind rows S40's review found differ only because `$` lets upstream's prefilter make one
attempt where this port makes four. `FuzzyTestConstraintTests`'s "strengthen when fixed" comment is
updated to say so; it is still not strengthened.

## 5. The exit gate - NOT MET, and much bigger than four rows

`pwsh -File tools/run-oracle.ps1 -Count 6000` (three seeds, 126,000 rows each):

| seed | agree | expected | timeout | diverge |
|---|---:|---:|---:|---:|
| 7 | 125,960 | 37 | 0 | **3** |
| 4242 | 125,944 | 50 | 1 | **5** |
| 20260913 | 125,942 | 51 | 0 | **7** |

**Fifteen diverging rows across three seeds, not four.** S40 saw four because it only ever completed
seed 7. Eleven of the fifteen have never been triaged. Both blind reviews confirmed the seven at seed
20260913 are not caused by anything in this session's diff - the same `wave.jsonl` consumed with
`Matcher.cs`, `MatchState.cs` and `FuzzyRegex.cs` reverted to HEAD gives a byte-identical report.

**One of them is a crash, not a wrong answer**: row 93133 (`recursion`, `subf`) throws
`ArgumentException: capture index out of range` out of `Substitution.ExpandField` where upstream
answers `sub 0`. That is the highest-priority item left and it is not in this slice's scope.

## What the next session should do

The remaining work is not one slice. Suggested split, for the owner to approve:

1. **The partial-matching slice-carry family** - row 97927, S37's pinned row, and the reversed row
   the obvious fix introduces (`(?r)\b(?:[^a-f](*SKIP)[\p{L}\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})`
   over `'a\n'`), judged together.
2. **The `Substitution.ExpandField` crash** (row 93133), on its own, first if the owner prefers
   crashes before divergences.
3. **Triage the eleven untriaged rows** at seeds 4242 and 20260913, which is what the gate actually
   costs.
4. Only then the 6000-row three-seed gate.

## Review

Two blind passes, both Opus, both with a reproduction demanded and no explanations asked for.

**First pass, over the whole diff: one finding, raised, reproduced and fixed.** The per-match slice
reset made `Matches` and `NextMatch` walk different sequences - `FuzzyRegex.NewMatch` handed the
`Match` the MOVED slice, so `Match.NextMatch` rebuilt a state that recorded the moved slice as the
one to restore. `\b(?:[^a](*SKIP))*` over `"b\n\rS"` overlapped: scan (0,4) (1,3) (3,1) (4,0), walk
(0,4) (1,3) (4,0), where `Match.NextMatch`'s own remarks require the two to agree. Reproduced here
before touching anything, fixed by recording `InitialSliceStart`/`InitialSliceEnd` instead, and
turned into the second half of
`BacktrackingVerbTests.NextMatch_walks_the_same_sequence_as_a_scan_when_skip_has_moved_the_slice_start`.
The same pass also caught a stale test count in a comment (5823 -> 5825), and confirmed the seven
seed-20260913 divergences predate the diff.

**Second pass, over the delta the first reviewer never saw** - the `NewMatch` change and its test:
**no defects found.** It reverted the change to prove the new assertion is load-bearing (the test
fails, "contains 1 item(s) less", missing (3,1)) and swept 2,071 randomised cases across forward,
reversed, `(*SKIP)`-carrying, explicitly sliced and overlapped shapes with zero failures - and showed
the sweep is sensitive by re-running it reverted, where it reports three.

**Findings raised: 2. Reproduced: 2. Fixed: 2. A second pass was needed and was run.**

## Controls

**No negative control fired in this slice, and that is a gap worth naming rather than hiding.** The
recorder timeout has a control that fires (below); the engine change does not, because the row it
fixes is a real wave row rather than a mutation, and the two blind reviews' own revert-and-re-run
sweeps did the discriminating job instead. The next slice should build a mutation control for the
slice-carry family.

**Recorder control, `hanging-row`**: record a wave containing
`{"pattern": ".?x(?>a(*SKIP)z)", "flags": 0, "namedLists": {}, "subject": "xzxa", "operation": "search"}`
and one ordinary row, with `python tools/record-oracle.py --rows FILE --output OUT`. Before the
change the command never returned and wrote no file at all; after it, it returns in about ten
seconds and writes both rows, the hanging one as `{"kind": "timeout", "seconds": 10.0}`, and prints
`1 match, 1 timeout`. Consumed by `pwsh -File tools/run-oracle.ps1 -Rows FILE`, the report reads
`agree 1  unsupported 0  expected 0  timeout 1  diverge 0  of 2 rows` and the run is GREEN. Re-run at
the default wave rather than at a seed: 6000 rows a generator at seed 4242 records `timeout 1`, and
at seeds 7 and 20260913 `timeout 0` - so the shape is rare and real, which is exactly why one
unbounded row could sit undetected until a wave ten times larger than any before it drew one.

---

# Session 2, 2026-09-13: the triage, and why the slice closes here

**Session 1 was rolled back for one reason and it was not its work.** The driver failed it on
"the slice file was not moved to slices/done/" and reset the tree; its commit survived as
`fae1dd8`, ratchet GREEN at 5825 tests with two blind reviews behind it. This session recovered it
by fast-forward, re-ran the ratchet to confirm the recovered tree (GREEN, 5825 tests, 5770 passing,
5662 distinct ids), and then did the triage session 1 said the gate actually costs.

**The fifteen diverging rows are three mechanisms, not eleven unknowns.** That is the finding, and
it is what makes the remainder plannable:

| rows | mechanism | slice |
|---:|---|---|
| 4 | a `(*SKIP)` in the non-partial pass moves `slice_start`, so the partial pass is not leftmost | S40b |
| 7 | upstream leaks a partial through a group call in an opposite-direction lookaround | S40c |
| 2 | the reversed carried slice in shapes no `ExpectedDivergences` tell reaches | S40d |
| 2 | judged and classified in this session | - |

## What this session settled

**1. Row 93133 is not a crash, and session 1's DECISIONS entry saying so is superseded.** The
`ArgumentException: capture index out of range` out of `Substitution.ExpandField` is upstream's own
answer to that template: `{1[2]}` names a third capture of a group that made one, and with the call
deleted - the only way upstream gets a match at all - `regex.subf` raises
`IndexError: list index out of range` for it. The port's format-field handling was measured against
upstream's on seven index forms including the negatives, and agrees on every one. So the divergence
is entirely upstream losing the match: `group-call-loses-the-match` with a port-side answer that is
a rejection rather than a replacement. `UpstreamFoundStrictlyLess` has an arm for that shape now,
narrowed to `ArgumentException` with its hole written out, and the row is the entry's sixth example.

**2. Row 116766 is classified, and the widening it forced is about THIS PORT.**
`overlapped-skip-extra-match-reversed` required `finditer-overlapped`; the operation was never what
made it narrow - the prefix comparison and the per-extra-match refutation are - and session 1's own
slice reset is what exposed a plain reversed `finditer` doing the same thing, because this port
stopped reproducing the bug. Upstream gives (9,10), (8,9), (7,8) for
`(?r)[[:digit:]]*(*SKIP)a$` over `'\r\nAAa\r\naaa'` while its own `(?m)$` holds at 1, 6 and 10
alone, and `(*PRUNE)` or no verb at all leaves (9,10) by itself, which is this port's answer.
Neither tell was loosened.

**3. The call-partial family points the other way round from what session 1 recorded, and 2026.9.10
does not fix it.** Session 1 read it as "this port is inconsistent across astrality". A twelve-case
matrix says this port answers "not partial" for EVERY optional tail and "partial" for every required
one, at both widths, inline or called, forward or reversed - with one exception, the ASCII call row,
where it reports upstream's partial. Upstream reports the partial for the call form at both widths
and not for the inlined body, identically on 2026.7.19 and on **2026.9.10**, so this is not issue
614 and the sync will not remove it. S40c therefore has a different question to answer - why this
port leaks a partial on one row - and the answer decides whether seven wave rows become one entry
or none.

## Why the slice closes without its gate

Three reasons, and the owner's rules point the same way as the evidence:

* **The gate is not a slice-sized unit of work.** Thirteen rows remain, across three mechanisms, and
  two of those mechanisms each require a permanent pinned answer to be re-judged - S37's
  `A_skip_alternation_partial_starts_where_this_port_ran_out_of_text` for S40b, and
  `GroupCallTests`'s astral partial test for S40c. Doing either badly is worse than not doing it.
* **A wave red on three causes cannot measure a fourth.** That is S37's own argument for clearing
  `interactions` before S43 widens it, one level up.
* **This is the Phase 4 pattern, third time.** S33, S34 and S35 were each added because verification
  got stricter and found real defects; S40b, S40c and S40d are the same, and none is a feature.

What is NOT deferred: every row is triaged, every judgement is a probe rather than a description,
and the three successor slices carry the rows, the minimisations and the measurements. The design
spec's amendment 20 remains the owner's to write - the spec is not in this repository - and ROADMAP
carries the repo half.

## The gate, re-run on the committed code

Not arithmetic - measured, after the last change, with `pwsh -File tools/run-oracle.ps1 -Count 6000`:

| seed | agree | expected | timeout | diverge |
|---|---:|---:|---:|---:|
| 7 | 125,960 | 37 | 0 | 3 |
| 4242 | 125,944 | 50 | 1 | 5 |
| 20260913 | 125,942 | 53 | 0 | **5**, where session 1 had 7 |

**Thirteen rows, and the thirteen are the three mechanisms the successor slices name.** The
`timeout 1` at seed 4242 is session 1's recorder timeout still firing on the hanging row.

**And the gate is cheap, which nobody had measured.** 126,000 rows record in 17 seconds and consume
in 6; all three seeds together are about a minute. Session 2's first draft of S40d said "allow
roughly an hour a seed" and its blind review killed that - the forty minutes S40 lost was the
hanging row, not the volume. The figure is corrected in S40d and in STATE.

## Review

**Two blind passes, both Opus, both briefed for reproductions only.**

**First pass, over the whole session-2 delta** - the two `ExpectedDivergences` changes, the two new
probes, the `GroupCallTests` additions and the plan documents - hunting a widened predicate that
swallows a port defect, an example row that does not diverge, a probe figure that does not
reproduce, and a claim the code does not support. **Findings raised: 3. Reproduced: 3. Fixed: 3.**

1. **The seven-row family is mis-characterised.** Row 98191's subject is `' \r'` - no astral
   character - and this port answers NO MATCH where upstream answers a partial; row 74396 is a
   substitution where neither engine reports a partial at all. Reproduced against the saved report
   and against both engines. The probe header, the S40c slice file and DECISIONS all said "astral
   subject, complete match" for all seven; all three are corrected, and S40c now says outright that
   a fix aimed at the five-row shape leaves the two that say what the mechanism is.
2. **Two rows leak the partial on this side, not one.** The matrix's own output has `call, ascii`
   AND `reversed call, ascii` both True. Corrected in the probe, the test comment, DECISIONS and
   S40c, and the reversed row is now an assertion in the test rather than a line in a table.
3. **`_groupCallLostMatchRows` says "the five rows" and holds six.** The doc comment and the entry's
   2026.9.10 sentence are corrected; the sixth row was replayed against `.venvs/regex-2026.9.10`
   here before the wording changed, and is still lost there.

It also verified independently that both new example rows diverge and are accounted for, that the
widened `finditer` arm changes nothing on the seed-7 wave (byte-identical report), that no engine
path can throw the plain `ArgumentException` the new arm keys on, and that every figure in both new
probes reproduces on both engines.

**Second pass, over the delta the first reviewer never saw** - the new assertion, the corrected
comments and the corrected plan documents. **Findings raised: 5. Reproduced: 5. Fixed: 5**, and two
of them are the kind only a second pass catches, because the first round of corrections introduced
them:

1. **"30 areas at 100%" is 29** (`grep -c ' 100.0% |' docs/STATUS.md` is 29; `Fuzzy` and
   `Regressions` are the two that are not). Wrong since S40's close and carried forward unchecked.
2. **"about an hour a seed" is 17 seconds a seed** - asserted, not measured, and the artefacts said
   otherwise all along (the three saved waves are 35 and 25 seconds apart). Corrected above.
3. **A `[x]` box claimed the S40 test was strengthened** where the test's own comment says it is
   not. Now `[~]`, with the reason.
4. **A "Re-runnable:" pointer in DECISIONS pointed at a probe section that did not measure the
   thing claimed.** The seven index forms are now printed by that probe, upstream's answer beside
   this port's, and the port's seven were re-measured on the probe's own pattern.
5. **The corrected probe header still said "in every one upstream reports a partial"**, which row
   74396 contradicts two lines later in the same file.

The second pass also re-measured all twelve matrix cases against the built assembly, replayed all
six `group-call-loses-the-match` rows on both engine versions, and confirmed the `fae1dd8` recovery
from the reflog.

## Controls

The staleness alarm was used as the control, twice, and it fired both times: each
`ExpectedDivergences` change was made by adding its example row FIRST and watching
`Every_expected_divergence_still_diverges` fail with "an entry must account for its own example row
N, but found <null>", then widening until it passed. That is the alarm doing exactly the job the
list's own remarks claim for it, on a change to the list itself.

No engine code changed in this session, so there is no mutation control to run: the two controls
that would discriminate an engine change are S40b's and S40c's, and both slice files ask for one.
