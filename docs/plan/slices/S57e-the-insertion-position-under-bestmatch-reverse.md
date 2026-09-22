---
slice: S57e
phase: 6
title: The insertion position a reversed best match reports, and the generator waiting on it
delivers: []
---

# S57e - one unjudged oracle row, and `fuzzy-anchored` back on the default list

S57c added the `fuzzy-anchored` generator (`fuzzy` with a zero-width assertion in front of the
whole pattern) and it immediately found a row S57c did not judge. The row is nothing to do with
S57c's rule, so it was left for its own slice, and the generator is off the default list in
`tools/run-oracle.ps1` until this slice closes.

**The row.** `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000 -Seeds 1234567`,
row 1982, measured 2026-09-21:

```
DIVERGE row 1982 (fuzzy-anchored) finditer flags=0x0 version=V0
  pattern  '(?b)(?r)\m(?:.fo){e<=2}'
  subject  'x fx'
  upstream matches 1 | match 0:(0,4)[(0,4)] last=-1/- fuzzy=(1,1,0)[s:4][i:2][d:]
  port     matches 1 | match 0:(0,4)[(0,4)] last=-1/- fuzzy=(1,1,0)[s:4][i:1][d:]
```

Same span, same error counts, same substitution position. The engines disagree only on where the
inserted character is recorded: upstream index 2, this port index 1.

**What is already known.**

- S57c's fix is not the cause. Under `(?r)` the leading `\m` is not at the head of the reversed
  graph, so `PatternObject.AnchorGuards` is empty on this row and `Matcher.AnchorIsPinned` never
  runs. An ablation that empties the field leaves both answers unchanged.
- The `(?b)` flag is what moves upstream's answer. Measured on regex 2026.9.10, same span and
  counts either way:

  ```
  (?r)\m(?:.fo){e<=2}     over 'x fx' -> changes=([4], [1], [])
  (?b)(?r)\m(?:.fo){e<=2} over 'x fx' -> changes=([4], [2], [])
  ```

  So upstream itself reports two different insertion positions for the same span, the same counts
  and the same pattern, according to whether the best-match pass re-ran the match. That is the
  first place to look, and it is the same re-anchoring machinery that produced issue 564.
- Only the `(?b)` + `(?r)` + leading-`\m` combination diverges. Dropping `(?b)`, dropping `\m`,
  swapping `\m` for `\b`, or using the subject `'xfx'` all agree.

## Scope

1. **Judge the row to amendment 16's standard.** Which engine is right about the recorded position,
   with the mechanism named in upstream's source, a second engine or upstream's documentation where
   either can speak to it, the blind review and the independent verifier.
2. **Then either fix the port or pin the divergence**, with a permanent minimised test carrying its
   provenance and, if it is a deliberate difference, a `docs/DIVERGENCES.md` row.
3. **Put `fuzzy-anchored` back on the default generator list** in `tools/run-oracle.ps1`, and delete
   the paragraph there and in `tools/record-oracle.py` that explains why it was held out.
4. **Nothing else.** `match.fuzzy_changes` ordering questions that this row does not raise belong to
   whatever finds them.

## Verification

- `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000 -Seeds 1234567` GREEN, and
  the same at 7, 4242 and one seed this slice has not used.
- `pwsh -File tools/run-oracle.ps1` GREEN at three seeds with the generator back on the list, and
  `-Count 6000` GREEN at three seeds.
- `pwsh -File tools/check-ratchet.ps1` GREEN.

## Done when

- [x] The row judged, with the mechanism and the verifier's CONFIRMED lines.
- [x] Fixed or pinned, with a permanent test and a `DIVERGENCES.md` row if it is a pin.
- [x] `fuzzy-anchored` on the default list, both hold-out paragraphs deleted.
- [ ] Both waves green at three seeds; blind review; commit. The `fuzzy-anchored` wave is green at
      four seeds and the default wave at its three, but the 6000-row gate is RED at seed 20260922 on
      ten rows this slice did not cause and did not fix; they are S57f's. See the closing notes.

## Closing notes (2026-09-22)

**The verdict.** Ledger entry 12, the doubled `END_FUZZY` backtrack guard, and this is the first
row of that family measured from both sides. Upstream guards the backtrack with
`total_errors(state->fuzzy_counts) + total_errors(inner_counts) < state->max_errors`
(`upstream/src/_regex.c:15516-15519`), and `END_FUZZY` has already merged `inner_counts` into
`state->fuzzy_counts` on the way in (`:12475-12513`), so the same errors are counted twice
and a backtrack that fits the budget is refused. S46 dropped the second term here.

Every earlier row of the family is keyed on upstream's own flagless answer: delete `(?b)` and
upstream says what this port says. Row 1982 goes further, because the guard could be reached from
each build in turn:

- delete the doubled term from an upstream build and upstream moves to this port's answer;
- restore it in this port and this port moves to upstream's.

The flagless answer does not move in either build, which is what rules out "the port ignores
`(?b)`" as the explanation. `python tools/probes/s57e-double-count-moves-the-insertion.py` runs
both halves.

So the divergence is deliberate and pinned, not fixed: the row is a `bestmatch-loses-a-candidate`
row, `docs/DIVERGENCES.md` entry 12 now names the moved insertion position among that guard's
symptoms, and `FuzzyBestMatchTests.Bestmatch_reversed_records_the_insertion_where_its_own_flagless_answer_does`
holds the shape permanently.

**What the generator drew when it went back on the list.** Putting `fuzzy-anchored` on the default
list cost four more rows of the same entry, rows 25 to 28. Rows 25 and 27 are the family's first
rows where upstream KEEPS a match and this port beats it - same span, three substitutions upstream
against two errors here - so upstream's flagless answer equals its flagged one and the usual key
says nothing about them. They are keyed on this port's own judged answer instead
(`_bestmatchWorseMatchOurs`, keyed by row index, projected onto questions at load). Row 28 came out
of this slice's negative control, at a seed the slice had not otherwise used.

**Controls.** All four run from `tools/run-oracle.ps1`. The port-side mutation is the same one line
in every case: in `src/FuzzyRegex/Engine/Matcher.cs`, in the `END_FUZZY` backtrack arm (around line
8730), change

```csharp
                        && TotalErrors(state.FuzzyCounts) < state.MaxErrors
```

to

```csharp
                        && TotalErrors(state.FuzzyCounts) + TotalErrors(innerCounts) < state.MaxErrors
```

which puts upstream's doubled term back. Every figure below was re-measured against the tree this
slice commits, on 2026-09-22.

> Control A, the judged row on its own: `-Rows tools/probes/s57e-anchored-wave-row.jsonl`, 1 row, no
> seed (the file carries the row). Committed: agree 0, expected 1, diverge 0. Mutated: agree 1,
> expected 0, diverge 0 - the port answers upstream's answer and the pin no longer fires.
>
> Control B, the three worse-match rows: `-Rows tools/probes/s57e-gate-rows.jsonl`, 3 rows, no seed.
> Committed: agree 0, expected 3, diverge 0. Mutated: agree 3, expected 0, diverge 0.
>
> Control C, `fuzzy-anchored`, 2000 rows, seeds 31337 and 8675309. Committed: agree 1992 expected 8,
> and agree 1988 expected 12. Mutated: identical at both seeds. **C does not discriminate** - the
> pinned rows those two waves draw belong to other entries - and it is recorded because a control
> that cannot see the fault is worth knowing about.
>
> Control D, `fuzzy-anchored`, 6000 rows, seed 8675309, the wave that found row 28. Committed: agree
> 5969, expected 31, diverge 0. Mutated: agree 5970, expected 30, diverge 0. Before row 28 was
> pinned the same committed code gave agree 5969, expected 30, diverge 1, which is how the row
> surfaced.

`.scratch/controls.ps1` in the session that ran them drove A to D in one pass; it is three lines of
`& tools/run-oracle.ps1` with the arguments above, so it is quicker to retype than to keep.

**The independent verifier** (fresh Opus subagent, 2026-09-22) re-ran the probe, both control sets
and the row-28 evidence from the committed files: V2 to V7 CONFIRMED, `git diff --stat -- src/`
empty afterwards. One claim came back DIFFERENT and was fixed rather than argued: entry 12's merge
citation `:12473-12484` covers the arithmetic that adds the inner counts to the outer, but the write
into `state->fuzzy_counts` is the `Py_MEMCPY` at `:12513`. Every copy of that citation in the tree
now reads `:12475-12513`, and the sentences that said the merge happens "twenty lines earlier" than
the guard - it is in a different function, about three thousand lines away - now say "on the way
in".

**The waves, measured 2026-09-22 against the committed tree.** `fuzzy-anchored`, 2000 rows: seed
1234567 agree 1997 expected 3, seed 7 agree 1995 expected 5, seed 4242 agree 1991 expected 9, and
seed 271828 - one the slice had not used - agree 1991 expected 9. All `diverge 0`. The default wave
at its three seeds: agree 6639, 6641 and 6658 of 6680, `diverge 0` at each. The 6000-row gate: seeds
7 and 4242 `diverge 0`, seed 20260922 `diverge 10`, which are S57f's ten rows and no others.

**Review.** One reviewer, two blind passes so far, brief unchanged. The first pass raised two findings and
both reproduced: the `Applies` predicate excused ANY answer cheaper than upstream's, which an
always-true mutant survived, so the judged answers are now pinned by exact string
(`_bestmatchWorseMatchOurs`); and the row-28 paragraph quoted pre-pin numbers as if they were the
committed tree's. The second pass, over the delta the first never saw, raised three and all three
reproduced: `docs/PORTMAP.md` kept the phrase "twenty lines earlier" after its citation was
corrected; the comment above `Bestmatch_keeps_the_folded_match_its_own_flagless_run_finds`
misassigned every position the test asserts; and the "both waves green at three seeds" box was
ticked while the 6000-row gate is red at the date seed. All three fixed here. A third pass, over
those three fixes, is what this slice still owes; the allowance window ran out first, so this commit
is a checkpoint and the next sitting sends it.

**What the next slice should know.**

- The three-seed 6000-row gate at seed 20260922 draws ten rows that are nothing to do with this
  slice. They are written up as S57f, `docs/plan/slices/S57f-the-ten-rows-the-date-seed-drew.md`,
  and an ablation with the pre-S57e generator list proves they are not `fuzzy-anchored` rows.
- A `-Rows` file that contains no iteration row makes `run-oracle.ps1` print RED and exit -1 from
  `The_lazy_walks_answer_exactly_what_the_eager_ones_do`, which wants an iteration row to
  discriminate anything. Read the `agree`/`diverge` counts, not the verdict.
- `-SkipRecord` reuses `TestResults/oracle/wave.jsonl`, and a `-Rows` run overwrites that file with
  its handful of rows. A control that skips recording after a `-Rows` run silently measures three
  rows and reports them as a wave.
- A wedged `VBCSCompiler` (PID 39948, started 2026-09-21 23:06) held
  `src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json` for this whole sitting, so every
  Release build ran with `$env:IntermediateOutputPath = 'obj/Release/net10.0-s57e/'`. If a build
  fails with "Error writing to source link file ... used by another process", that is the same
  thing: redirect the intermediate path rather than killing the compiler server.
