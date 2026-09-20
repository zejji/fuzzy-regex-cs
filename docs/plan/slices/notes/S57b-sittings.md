# S57b sittings

Per-sitting working notes. The slice's spec is `../S57b-the-6000-row-gate-rows.md`; the twenty rows
themselves were found by S57 sitting 3 and are listed in `S57-sittings.md`.

## Sitting 1 (2026-09-20)

Built the instruments, captured the twenty rows as a replayable file, dated every row against the
engine's history, and gathered the per-row evidence. No verdict is written into
`ExpectedDivergences.cs` yet, and no row is fixed. That is sitting 2's work, and everything it needs
is below.

### The rows, captured once

`tools/probes/s57b-gate-rows.jsonl` holds the twenty rows cut out of `wave-7.jsonl`,
`wave-4242.jsonl` and `wave-20260920.jsonl`, each tagged with the seed and row number it came from
(`s57bSeed`, `s57bRow`). Nothing in this slice is transcribed by hand: every probe reads the row out
of that file and this port's answer out of the run's own `report-<seed>.txt`.

The file replays through the real comparer:

```
pwsh -NoProfile -File tools/run-oracle.ps1 -Rows tools/probes/s57b-gate-rows.jsonl
agree 0  unsupported 0  expected 0  timeout 0  resource 0  diverge 20  of 20 rows
```

Twenty of twenty diverge, so the file is the gate's own red rows and not a paraphrase of them. Every
later step asks about the same twenty.

### The instruments

| Probe | Question it answers |
| --- | --- |
| `tools/probes/gate-divergence-doors.py` | Every judged family's own control, put to every row. Built by S52; `--rows` takes the file above. |
| `tools/probes/s57b-gate-row-controls.py` | The four questions the doors do not ask: counts against change lists, total error cost, uncapped anchor reachability, and the Turkic cells. |
| `tools/probes/s57b-date-the-rows.ps1` | Replays the twenty rows at every engine commit since the S52 close, so each row is dated to the commit that turned it red. |

Two mistakes in the controls probe are worth remembering, because both produced a confident wrong
reading before they were caught:

- The wave's `outcome` field is **upstream's** answer, not this port's. Reading the fuzzy counts out
  of it compares upstream with itself and prints "equal" on every fuzzy row. This port's side comes
  from `report-<seed>.txt`.
- The anchor-reachability grid is built from anchored `match` calls and asks whether upstream's
  **search** answered a span its own matcher cannot make. A `match` or `fullmatch` row is already
  anchored, so the question does not arise there; printing the grid anyway puts a `search`'s span
  beside a `fullmatch`'s and invites the reader to compare two different calls.

### Dating: a cheaper route than the spec named

The slice spec proposed a one-seed 6000-row wave per bisect step. Replaying the twenty rows
themselves is about 20 seconds a step against about four minutes, and it is also **sharper**: the
recorder changed after S52 closed (S52c's metamorphic invariants, S53b), so a seed no longer draws
the wave it drew then, and a 6000-row step would be dating a row against a wave that no longer
contains it. Handing the rows in makes every step ask about the same twenty. Upstream's side is
re-recorded at each step from the installed `regex` module, which does not change, so the only thing
varying is this port.

Seventeen steps, oldest first: the S52 close `b678aeb` as the baseline, then the sixteen engine
commits of `b678aeb..HEAD`. The floor has to be named separately, because `b678aeb` is a
slice-closing commit that touches no engine file and a pathspec-filtered `git log` drops it.
Reproduced twice, identical both times:

```
=== b678aeb  S52: the last 22 sweep rows judged from one ablation table, and the slice closes
    agree 4  expected 1  diverge 15 of 20
    DIVERGE  1,2,7,8,9,10,11,12,13,14,16,17,18,19,20
    EXPECTED 15=search-start-partial
=== 024da7d  S52b
    agree 4  expected 1  diverge 15 of 20      (unchanged)
=== 30e0178  S52d: a reversed partial runs out of text at the slice start, one rule, asked in one place
    agree 0  expected 0  diverge 20 of 20
=== every commit after it
    20 of 20, unchanged
```

So the twenty split cleanly in two:

- **Rows 3, 4, 5, 6 and 15 are regressions introduced by `30e0178` (S52d).** Rows 3, 4, 5 and 6
  agreed with upstream before it. Row 15 is the sharpest: it was an accounted-for
  `search-start-partial` pin, and S52d changed this port's answer from "no match" to a span, which
  knocked it out of its own family.
- **The other fifteen are inherited from before the S52 close.** They are not regressions and no
  commit in this range moved them.

`20d0f59` does not build in isolation (it declares the public surface one commit before the code
that adds it, so RS0017 fails), so it dates nothing. It sits between two 20-of-20 steps.

Re-run all seventeen with `pwsh -File tools/probes/s57b-date-the-rows.ps1`, or one transition only
with `-Only 024da7d,30e0178`. It uses the worktree at `.claude/worktrees/s57b-date`, which is left
in place (detached, so it holds no branch) because re-dating a row in sitting 2 then costs seconds.

### The five S52d rows: what actually changed, and what did not

**A wrong reading, recorded so sitting 2 does not repeat it.** The S52d diff shows four reversed
arms changing from

```csharp
if (textPos <= state.TextStart) return state.PartialSide == MatchState.PartialLeft ? MatchStatus.Partial : MatchStatus.Failure;
```

to `if (RanOutOnTheLeft(state, textPos)) return MatchStatus.Partial;`, which reads as though the
`PartialSide` guard was dropped. It was not. `Matcher.cs:3326` defines

```csharp
private static bool RanOutOnTheLeft(MatchState state, int textPos) =>
    state.PartialSide == MatchState.PartialLeft && textPos <= state.SliceStart;
```

at `Matcher.cs:3326`, so the guard moved into the helper. The only behavioural change is the
**bound**: `TextStart`, which `init_match` sets to 0, became `SliceStart`. That is exactly what S52d
set out to do, and it is what the owner ruled on 2026-09-15.

**What the five rows have in common.** All five come from `partial-sliced`, all five are reversed,
all five pass a non-zero `pos`, and in all five **upstream answers a match too**. That is why
`reversed-partial-runs-out-at-the-slice-start` does not account for them: the fourth of its five
predicate limbs is `row.Expected is NoMatchOutcome` (`ExpectedDivergences.cs:2181`), and here
upstream matches.

| Row | Seed / row | Call | pos, endpos | Upstream | This port |
| --- | --- | --- | --- | --- | --- |
| 3 | 7 / 102670 | search partial | 2, 3 | `0:(2,0)` partial | `0:(2,1)` partial |
| 4 | 4242 / 102408 | fullmatch partial | 2, 4 | `0:(2,2) 1:(3,1)` **complete** | `0:(2,2) 1:(3,1)` **partial** |
| 5 | 4242 / 103412 | match partial | 2, 2 | `0:(2,0) 3:(2,0)` partial | `0:(2,0)` group 3 **unset** |
| 6 | 4242 / 107083 | search partial | 1, 4 | `0:(1,0)` partial | `0:(1,1)` partial |
| 15 | 20260920 / 107737 | search partial | 1, 3 | `0:(1,0) 3:unset` partial | `0:(1,2) 3:(1,2)` partial |

Spans are UTF-16 code units, as the recorder converts them at the boundary.

**The hypothesis for sitting 2, stated so it can be killed.** S52d settled *whether* a reversed
partial is reported at a non-zero `pos`. These five are about *what it reports when it is*. On rows
3, 6 and 15 upstream reports an **empty span at the slice start** and this port reports a non-empty
one beginning there - widths 1, 1 and 2. Do not read that as "the whole slice": rows 3 and 15 do
reach `endpos`, but row 6's slice is `[1, 4)` and its span is `(1, 1)`, so the width is whatever the
pattern consumed, not the slice. Rows 5 and 15 differ in a group as well, and row 4 differs only in
the `partial` flag, on identical spans and groups.

Three rows agreeing on the empty span is suggestive, not proof. Sitting 2 should put it to upstream
directly, over the S52d test grid's own cells, before touching the engine. If it holds, the fix is
one rule about the span a reversed partial reports at the slice start, and it wants a failing test
first.

### The other fifteen: evidence gathered, verdicts not written

Read off the doors and the controls. Each still needs its mechanism written, its entry added and its
pin or ledger line landed.

| Rows | Evidence | Likely outcome |
| --- | --- | --- |
| 16, 17 | Upstream's `[A-Z]` and `\p{Lu}` match U+0131 under IGNORECASE+FULLCASE. Swapping the Turkic letters for U+00DF, U+FB00 or U+01F0 makes upstream answer what this port answers. | `turkic-default-folding`, port right |
| 18, 20 | Deleting `(?b)` makes upstream answer exactly what this port answers, change lists and all. | `bestmatch-loses-a-candidate`, port right |
| 9, 12, 13 | Upstream's span is not reachable by any anchored call in the searched region, and this port's answer is the first anchor owed. | `search-start-partial` shape |
| 14 | Upstream's answer **is** its own first anchor and this port differs. | Port suspect |
| 1 | Upstream's `counts=(0,1,0)` against change-list lengths `(1,0,0)`. | `fuzzy-changes-of-the-wrong-kind-for-their-own-counts` |
| 7 | Same span; upstream spends one substitution, this port two. | Open |
| 19 | Under `(?e)`, this port's answer costs 2 and upstream's costs 3. | `enhancematch-ranks-by-cost` shape |
| 2 | Not Turkic: the one clean swap cell (U+01F0; the other two are contaminated because the row already holds those letters) leaves the divergence standing. `verb deleted` gives this port's answer exactly. | Open, verb-shaped |
| 8 | `finditer-overlapped`: upstream 4 matches, this port 3; both pruned doors give 3. | Open |
| 10, 11 | Upstream raises `IndexError: tuple index out of range` in `get_firstset` while compiling the row. | Ledger entry, not an oracle pin |

### Review

One blind pass over the sitting's whole diff, briefed to hand back reproductions only. Seven
findings raised, **all seven reproduced, all seven fixed**:

1. The controls probe's docstring said eight of the twenty rows are partial and two are
   `fullmatch partial`. It is thirteen: nine `search`, three `fullmatch`, one `match`.
2. `-Only <one commit>` crashed the dating script. PowerShell unwraps a one-element array out of an
   `if`, so `$commits` was a bare string and `.Count` threw under strict mode. Fixed with `[array]`.
3. The recorded re-run command did not produce the `b678aeb` block shown beneath it:
   `git log b678aeb..HEAD -- src/FuzzyRegex` excludes the floor, which touches no engine file. The
   script now names the floor separately and walks seventeen steps.
4. `Matcher.cs:3310` was the wrong line for `RanOutOnTheLeft`; it is `:3326`.
5. "its predicate's third limb is `row.Expected is NoMatchOutcome`" - it is the fourth of five.
6. The hypothesis paragraph said this port reports the run consumed "from `endpos` down to" the
   slice start. True of rows 3 and 15, false of row 6, whose slice is `[1, 4)` and span `(1, 1)`.
7. The controls probe printed a CHEAPER/DEARER verdict on row 7, which carries neither `(?b)` nor
   `(?e)`. Without a ranking flag upstream returns the first match it finds, not the fewest-error
   one, so a smaller total is not a better answer. The verdict is now gated on the compiled
   pattern's flags - which is where it has to be read, because three of these rows carry their
   ranking flag inline and their row-level `flags` field is 0.

The same pass independently re-ran and confirmed the load-bearing numbers: the `diverge 20 of 20`
replay, the five-row table of spans and groups read out of `report-<seed>.txt`, all twenty rows
byte-identical to their `wave-<seed>.jsonl` lines, and the dating run. No second pass was needed:
every fix is one of its own findings applied to a file it had already read.

### Left for sitting 2

1. Put the empty-span hypothesis to upstream over the S52d grid, then fix rows 3, 4, 5, 6, 15 with a
   failing test first.
2. Write the fifteen verdicts into `ExpectedDivergences.cs`, one batch, with the gap tests and any
   `docs/DIVERGENCES.md` rows.
3. Minimise rows 10 and 11 and draft (never file) the ledger entry.
4. Re-run the gate at `-Count 6000` on all three seeds plus
   `-Generator fuzzy,interactions -Seeds 99991,57057`, green, then close the slice.

## Sitting 2 (2026-09-20): fourteen of the twenty judged

Seed 4242 is GREEN, seed 7 is at 2 diverging, seed 20260920 at 4 (replay of the recorded waves, not
a re-record - that is still owed).

**Rows 3, 4, 5, 6, 15 - the S52d regressions. Port right.** Sitting 1's empty-span hypothesis was
already dead. Upstream's reversed partial span is `(slice_start, match_pos)` of whichever attempt
was current when it gave up: `_regex.c:18185-18190` overwrites the match's text position with
`slice_start` on a partial, and `search_start` (`:8400-8405`) does the same before returning
`RE_ERROR_PARTIAL`, so upstream's answer over a non-zero slice comes from a later door, after the
search has retreated, where this port's comes from the node handler on the first attempt. The
decisive control is the ruling's own falsifiable consequence - the slice `[pos, endpos)` must answer
what the same pattern answers over `subject[pos:endpos]` shifted by `pos` - and on all six rows this
port's answer equals upstream's own cut-subject answer. Recorded per row as a new second fact,
`cutSubjectOutcome`, so the predicate compares against upstream rather than against a transcription.
Pin: `reversed-partial-answers-the-cut-subject`. Tests:
`Gaps/Engine/ReversedPartialCutSubjectTests.cs`, six of them.

**Row 107758 is not a Turkic row.** The new entry sits above `turkic-default-folding` and claims it.
Proved by the isolating control that entry's own Reason demands - swap every U+0131 in pattern and
subject for `h` and for `å` and the answer does not move: `tools/probes/s57b-107758-not-turkic.py`.

**Rows 16, 17** -> `turkic-default-folding-without-spans`; **18, 20** -> `bestmatch-loses-a-candidate`
(both carry `selfContradiction: ["bestmatch-no-worse"]` and a `bestmatchFreeOutcome` equal to this
port's answer); **9, 12, 13** -> `search-start-partial`'s row-keyed arm (each has
`searchOnlyPartial` true, an anchor grid that does not contain upstream's span, and a `pruneOutcome`
equal to this port's answer); **14** -> `partial-retry-reversed-slice`.

**Row 14 corrects the anchor-reachability instrument.** Sitting 2 first read it as a port defect,
because upstream's own `match(0, endpos, partial=True)` answers at endpos 0 and nowhere else. It is
not: the pattern ends in `(?(?=[^a])[^\d]|\p{ASCII})`, matched FIRST under `(?r)`, with a lookahead
that reads to the RIGHT of the span. Truncating at `endpos` makes that lookahead see end-of-text, the
conditional takes its other branch, and the anchored call asks a different question. Force the branch
the untruncated subject takes and the sweep answers at endpos 0, 1 and 3, the highest being this
port's span. **A reachability NO is evidence only where the pattern reads nothing past the anchor.**
`tools/probes/s57b-anchor-sweep-reads-past-the-anchor.py`.

**Row 19 is a new family.** Upstream's `(?e)` keeps a three-error fit where the same engine's tighter
budget finds a two-error one, minimised to `(?e)(?:a\d+Z){e<=3}` over `'a6ZZ_'`: `{e<=2}` gives
`(1, 1, 0)` and `{e<=3}` gives `(3, 0, 0)`, and `{e<=2}` permits a subset of `{e<=3}`. The loop is
not absent - on `'a6Z_'` it improves three errors to one. Without the flag the two engines agree
exactly. Pin `enhancematch-loses-a-candidate`, ledger entry 25, probes
`tools/probes/s57b-enhancematch-loses-a-candidate.py` and `.cs`.

**Still open, for sitting 3.** Seed 20260920 rows 72433 and 74399, seed 7 rows 75921 and 76160, and
rows 10 and 11 (seed 20260920 rows 81232 and 87091), where upstream raises `IndexError: tuple index
out of range` while COMPILING and so cannot be an oracle pin at all. Then the re-record, the full
gate, and the closing ceremony. **The cut-subject door figures in
`reversed-partial-answers-the-cut-subject`'s Reason (831 named / 777 SAME / 54 DIFFERENT) were
measured BEFORE the re-record and must be re-measured before the slice closes.**
