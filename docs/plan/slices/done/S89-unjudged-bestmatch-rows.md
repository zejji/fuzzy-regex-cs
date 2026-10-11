---
slice: S89
phase: 7
title: The unjudged BESTMATCH, ENHANCEMATCH and (*SKIP) oracle rows are each judged, then fixed or pinned
delivers: []
---

# S89 - Six findings nobody has judged

`docs/plan/STATE.md` lists findings 1 and 3-6 under "Findings that need a slice". Each is an oracle
row where the port and upstream disagree and nobody has said who is right. The owner's rule
(2026-09-12) is that no known bug ships in 1.0, so each one ends this slice as one of: fixed here,
pinned as a port-right divergence (research, isolating probe, blind review, per the upstream-bug
rule), or handed to a named slice of its own in writing.

Several were found before S87's fix (an undone fuzzy section left a stale error total). S87 may
already explain them, so **re-run every row against main first**. That could shrink this slice to
a list of "fixed by S87" lines.

## Scope

1. For each finding, reproduce on main and in upstream (`regex` 2026.9.10, the same version flags
   as the port: V1 where the port runs V1). Record the three answers (upstream, port before, port
   now) in the notes file.
   - Finding 1: `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch `fxf`, and row 67 of `fuzzy-overhang` at
     seed 20260923 (`(?b)(?fi)(f)(?:\d+a00(?:\1)){e<=3}`). Details in
     `docs/plan/slices/notes/S85-sittings.md`. Upstream matches the literal form `(?:f)`; say why
     the reference form differs there, from `_regex.c`, before calling either side wrong.
   - Finding 3: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` fullmatch `oelFin becf` (`fuzzy-literal` seed
     20260923, row 1611), and `(?b)(?r)(?:\L<phrases>){e<=3}`, phrases `['', 'amber lantern']`,
     fullmatch `znz`.
   - Finding 4: `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf (`fuzzy-anchored` seed 20260923, row 5821).
   - Finding 5: S60b's reviewer saw 18 `(?b)`/`(?e)` divergences in 14,000 rows; the generator was
     not kept. Re-derive a generator that exercises BESTMATCH and ENHANCEMATCH over the
     `fuzzy-*` pattern shapes, run it at seeds 7, 4242 and 20260923, and judge what it finds.
   - Finding 6: oracle row 4957 at seed 99 (partial, `(*SKIP)`, like row 5185).
2. Group the rows that remain by cause. Minimise each group to one pattern.
3. Tests first, each seen red, pinned to upstream's answer (or to the port's, with a DIVERGENCES
   row and its COMPARISON section in the same commit, if upstream is shown wrong).
4. The smallest fix that matches upstream's mechanism. A shared-with-upstream bug gets a ledger
   entry and a draft report in `docs/plan/upstream-reports/`, as S83-S87 did. Nothing is filed.
5. Once finding 1 is settled, put `fuzzy-overhang` (and any generator this slice adds) on the
   default wave if it is green there.
6. Oracle at seeds 7, 4242 and 20260923. Update STATE.md's findings list.

If a group needs more than two sittings, hand it to a slice of its own and close the rest.

**Stop by 05:50 on 2026-09-24** with a green checkpoint if it cannot land. Commit every 30 minutes.

Sitting notes: `docs/plan/slices/notes/S89-sittings.md`.

## Closing notes (2026-09-23)

Per-finding answers (upstream, port before, port now) are in
`docs/plan/slices/notes/S89-sittings.md`. Rows 1611 and 5821 above are the numbers STATE recorded;
the generators have changed since, and finding 4 is now row 3821 of the 2000-row
`fuzzy-literal,fuzzy-anchored` wave.

**What landed.** No library behaviour changed. Every finding is judged:

- Findings 3 and 4 are ledger entry 12 alone: under BESTMATCH, upstream's END_FUZZY backtrack arm
  counts a section's errors twice when it checks whether a trailing insertion fits
  (`_regex.c`:15516), and S46 dropped that second term. The smallest case is new:
  `(?b)(?:){e<=3}` over `znz` is None upstream, and three insertions without the flag. Four rows are
  added to `bestmatch-loses-a-candidate` (rows 29-32), each with a test.
- Finding 1 is ledger 12 combined with a fold defect: `fxf` needs ledger 30 as well, and
  `fuzzy-overhang` row 67 needs ledger 29. A new oracle-only flag,
  `PatternObject.DoubleCountTrailingInsertions`, restores the doubled term, and the two fold entries
  gain ablation arms that set it beside their own flag. Both rows have tests pinned to upstream's
  literal forms.
- Finding 6 (row 4957 at seed 99) is port-right, row 15 of `partial-retry-reversed-slice`:
  `(*PRUNE)`, the verb deleted and upstream's own anchored match all answer the port's span.
- Finding 5 needs no new generator: the five `fuzzy-*` generators already put `(?b)` on about 30%
  of their rows and `(?e)` on about 30%. At 2000 rows and three seeds they leave three partial rows
  over a full-folded ligature that no ablation explains. Those are handed to **S90**
  (`docs/plan/slices/S90-partial-fold-rows.md`, probe `tools/probes/s89-partial-fold-rows.py`).
- `fuzzy-overhang` is on the default wave. Default oracle, final run after the last edit, seeds 7 /
  4242 / 20260923: agree 7462 / 7475 / 7483, diverge 0 at each.

No new upstream report: ledgers 12, 29 and 30 already hold drafts. Entry 12's draft now leads with
the empty-section reproduction.

**Surprising.** S87's stale-total fix (ledger 32) explained none of these rows, although STATE had
guessed it might. And the three rows handed to S90 minimise to a plain literal:
`(?fi)(?:ﬆxba){d<=1}` over `STba` is None upstream and (0, 4) with a deletion here, so the default
generators do not happen to draw a literal divergence that exists.

**For the next slice.** `TestResults/oracle/wave.jsonl` holds whichever wave ran last, and
`dotnet test tests/FuzzyRegex.OracleTests` judges that file. After a 2000-row run it fails on S90's
rows until the default wave is re-run; the verifier tripped on exactly this.

**Suite red check.** With both rules forced on in `Matcher.cs` (the doubled term always counted,
the retried-fold steps always skipped), all five new tests fail, alongside the older ledger-12 and
ledger-30 tests. Reverted, they pass.

**Control.** In `tests/FuzzyRegex.OracleTests/OracleComparer.cs`, `RunWithTheDoubledInsertionGuard`,
change

    compiled.PatternObject.DoubleCountTrailingInsertions = true;

to `= false`. Wave: `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-overhang -Seeds
20260923,31337`, 300 rows a seed. Result at seed 20260923: diverge 1 (row 67), and
`Every_expected_divergence_still_diverges` fails on "full-fold-backreference-leftovers: an entry
must account for its own example row 5". At the unused seed 31337: diverge 0, but the same
staleness alarm fires, since it runs over the pinned rows at every seed. Row 67 is the only draw at
seed 20260923, so the wave's reach is thin and the staleness alarm carries the control. For the
retry arm, `fxf` gives None only with both `SkipRetriedFoldSteps` and
`DoubleCountTrailingInsertions` set, and (0, 3) with any other combination of the five ablation
flags (`.scratch` sweep, recorded in the notes).

**Review.** First blind pass: 5 findings raised, 5 reproduced, 5 fixed. One was substantive: a
script bug had made the port look like it shared upstream's None on the literal ligature row, and
S90's spec said so; it now says the port finds the deletion. The other four were wrong row
provenance in two remarks and two test comments. A second blind pass over that delta returned "No
defects found." The verifier (amendment 16) re-ran every judged number: upstream and port answers
for findings 1, 3, 4 and 6, the ledger-12-alone claim on rows 29-32, both fold ablation pairs, the
three row-4957 spellings and the ledger 12 lead, all CONFIRMED. Its one DIFFERENT was the oracle
test run against a leftover 2000-row wave file (row 8938, handed to S90); the default wave re-run
afterwards is green at all three seeds.
