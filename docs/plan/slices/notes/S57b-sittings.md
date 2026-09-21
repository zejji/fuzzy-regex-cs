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

## Sitting 3 (2026-09-21): the last six rows judged

Four new `ExpectedDivergences.cs` entries and one widened one, four gap tests, and the compile-time
pair folded into ledger entry 6. Every row below was judged from a door that upstream itself
answers, never from the row's shape.

**Rows 1 and 7 (seed 7 row 75921, seed 20260920 row 72433) -> `leaked-changes-beside-a-truncated-partial`.**
Two of ledger entry 11's faults compose on one row, which is why neither sibling entry can take
them: `fuzzy-changes-leaked-from-an-abandoned-attempt` needs the two engines' counts to agree, and
`fuzzy-counts-of-a-partial-are-the-innermost-sections` needs the drawn positions to be a prefix of
this port's. Row 75921 contradicts itself in KIND without a second engine - counts claiming one
INSERTION, a SUBSTITUTION in the list beside them, and a DELETION when the same compiled object is
asked anchored at the match it just found. Row 72433's door is its own recorded `leakFreeFuzzy`, upstream's
`match(pos=start, endpos=end)`, which moves the substitution from 0 to 1; deleting the pattern's
`(*PRUNE)` gives the same, so the verb is not what put 0 in the drawn list. On both rows upstream's
leak-free answer is a per-kind prefix of this port's with counts componentwise no larger, which is
`match_fuzzy_changes` cutting the list to `sum(fuzzy_counts)` (`_regex.c:20522`) beside counts that
are the innermost open section's. Probe: `tools/probes/s57b-leak-beside-truncation.py`.

**Row 8 (seed 20260920 row 74399) -> `skip-moved-slice-changes-the-match-set`.** The first row of
ledger entry 5's family where the carried slice changes WHICH matches a reversed overlapped scan
reports rather than one match's edit script. No sibling can take it: the three reversed
`overlapped-skip-*` entries are keyed on the recorded walk, which this row has not got because the
pattern ends in `\b`, and the `\b` tell is useless here since the assertion holds at every codepoint
of the subject. The judge is upstream's own `(*PRUNE)` spelling, which prunes the same backtracking
and moves no bound: it and the verb-deleted control both report three matches, this port reports
exactly those three, and upstream's drawn scan reports four that are not a superset - it adds
`(2, 4)` and `(2, 3)` and loses `(1, 3)`. Probe:
`tools/probes/s57b-skip-moved-slice-changes-the-match-set.py`.

**Row 2 (seed 7 row 76160) -> the existing `bestmatch-walk-truncated-by-a-skip`, now eight rows.**
Three sittings left this row unjudged because its `(?b)` + `(*PRUNE)` door does not answer: 3,092
seconds and then `MemoryError`. What opened it was minimising the row - nine rounds of the gate's own
comparer over every one-edit shortening, keeping any that still diverged - down to
`(?b:(}){e}(*SKIP)|)` over a lone U+00DF at the row's own flags, where every control answers in
milliseconds. Upstream keeps a one-substitution match where its own `(*PRUNE)` and verb-deleted
spellings find a zero-error one, and with `(?b)` removed all three spellings agree on both the drawn
and the minimised row. So the pruning is innocent and the moved bound is the cause, which is this
entry exactly. The old objection that the anchored door pointed the wrong way is NOT answered, and
sitting 5 withdrew the answer this paragraph first gave. It said asking the DRAWN object
`match(4, 7)` re-uses the object whose slice the verb has already moved; a freshly compiled object
that has never scanned answers `(4, 7)` with `(1, 0, 1)` as well, so the state re-use it blames does
not exist. The anchored door is still contaminated and the reason is unknown; the row rests on the
minimisation. Probe: `tools/probes/s57b-bestmatch-walk-row76160.py`.

**Rows 10 and 11 (seed 20260920 rows 81232 and 87091) -> `reversed-anchor-cannot-compile-a-full-fold`,
and ledger entry 6.** Upstream raises `IndexError: tuple index out of range` while
compiling, so the comparer sees a recorded error against an answer. Minimised to `(?r)^İﬀ` under
IGNORECASE|FULLCASE, with four necessary ingredients: the `(?r)`, the anchor, `FULLCASE`, and a
literal whose full fold changes its length. `Sequence._fix_full_casefold`
(`_regex_core.py:3636-3667`) finds fold chunks in the FOLDED text and slices the UNFOLDED tuple with
those offsets, so on `İﬀ` the chunk at folded `(2, 4)` slices empty, and `(?r)` asks that empty
`String` first, where `get_firstset` indexes `characters[-1]`. The same mis-slice answers wrongly
where it does not raise: upstream's forward `^İﬀ` does not match `i̇ff`, its own full fold, under the
flag whose only job is to make it. The entry is keyed on this port's exact answer, not on "upstream
raised", because a row upstream cannot compile checks nothing otherwise. Probes:
`tools/probes/s57b-upstream-firstset-indexerror.py` and `s57b-port-firstset-indexerror.cs`.

**That pair was first drafted as ledger entry 26 and the entry was then deleted.** S35 had already
filed the same defect as entry 6, from `(?r)^İﬁ` with U+FB01 rather than U+FB00, and entry 6 already
carried the mechanism, the silent forward half and a 2026-09-12 verification against 2026.9.10. What
S57b actually adds is three lines, now inside entry 6: `\A` raises exactly as `^` does, the second
ligature need not be the same one, and `_flush_characters` could skip an empty chunk as a second
guard. **Before drafting a ledger entry, grep `LEDGER.md` for the upstream function the mechanism
names** - `_fix_full_casefold` would have found entry 6 in one search, and the duplicate survived
being written, tested and referenced from three files before the sitting's own reading of
`docs/DIVERGENCES.md` caught it.

**The flag word is read from the row, not from the pattern.** Two of the four tests first transcribed
a row's flags from how the row reads - `0x400a` taken for REVERSE because the family is reversed -
and `0x400a` is `FULLCASE|IGNORECASE|MULTILINE`; row 72433 carries no `(?r)` at all. Both were caught
by the test failing, and `tools/probes/s57b-gate-rows.jsonl` plus a two-line decode of
`regex.A/B/D/E/F/I/L/M/P/R/S/U/V0/V1/W/X` is the only reliable route.

## Sitting 3, second half (2026-09-21): the re-record, and the eleven rows the new day drew

**The gate at `-Count 6000`, re-recorded.** Seed 7 GREEN, 0 diverging of 126,080 rows. Seed 4242
GREEN, 0 of 126,080. Seed 20260921 RED, 11 of 126,080. The twenty rows this slice judged came from
seeds 7, 4242 and 20260920, and **the third default seed is the run date** (`run-oracle.ps1:256`
builds its default as `"7,4242,$(Get-Date -Format 'yyyyMMdd')"`), so the day the slice re-runs its
own gate it draws a sample of 126,080 rows nobody has ever looked at. None of the eleven is a
regression: seeds 7 and 4242 draw the same rows they drew yesterday and both are green.

**Nine of the eleven are members of families this repo has already judged**, and the batch
instrument said so in one run - `python tools/probes/gate-divergence-doors.py 20260921`, which puts
every judged family's own control to every diverging row. That bare-seed form read the seed's RED
report; sitting 5 re-recorded the gate green, so re-run it as `gate-divergence-doors.py --rows
<file>` over the rows themselves. Each row went into the entry whose discriminator it meets, with
the row's own JSON out of `wave-20260921.jsonl` - JSON-equal, with literal characters where the
wave escapes them - and this port's answer out of `report-20260921.txt`:

| Rows | Entry | What its own control answered |
| --- | --- | --- |
| 100927, 101194, 101236, 105994 | `search-start-partial` | `searchOnlyPartial` true, the anchor grid holds this port's span and not upstream's, `(*PRUNE)` and the verb deleted both equal this port |
| 104371 | `partial-retry-carried-slice-forward` | upstream answers the zero-width partial at the far end of the slice it searched, pos 3 gives this port's (3, 6) |
| 119504 | `end-of-line-reads-a-skip-moved-slice` | upstream's four separators end at 4, 3, 2 and 1 where its own `$` is true at 4 alone |
| 124755 | `bestmatch-loses-a-candidate` | upstream answers no match; without `(?b)` the same engine answers this port's two-insertion fit |
| 74201 | `posix-fuzzy-contradicts-its-own-flagless-answer` | dropping `(?p)` moves upstream from two deletions to this port's one; dropping `(?e)` moves nothing |
| 99993 | `reversed-anchor-cannot-compile-a-full-fold` | the same `IndexError` from `_regex_core.py:4035`, read off the traceback |

Row 74201 needed a door the batch instrument does not ask - which of `(?e)` and `(?p)` spends the
extra error - so it has its own probe, `tools/probes/s57b-posix-overcharges-a-named-list-section.py`.
Re-consuming the recorded wave against the new pins (`pwsh -File tools/run-oracle.ps1 -SkipRecord`,
which re-reads `wave.jsonl` without re-recording) takes seed 20260921 from 11 diverging to 2.

**Row 74947 is not pinned, and the reason is evidence rather than time.** It is a reversed
`fullmatch` asked with `partial=True` where upstream answers no match and its own `(*PRUNE)`
spelling answers the partial this port answers, change for change. That is the one-attempt argument
`reversed-skip-invents-a-match` rests on - inside a single attempt the two verbs must prune
identically, because the only lines that differ are the two that move a bound
(`upstream/src/_regex.c:14553` reversed, `:14555` forward) - with the symptom INVERTED: the verb
loses a match instead of inventing one. The file splits by symptom (`overlapped-skip-extra-match-reversed`
against `-missing-match-reversed`), so this wants its own entry and its own gap test, and the
second control is missing: the verb deleted raises `MemoryError` here, so only one of the usual two
doors answers.

**Row 72790 is open, and it may be this port's defect rather than upstream's.** A reversed
`finditer` whose third match is upstream contradicting itself - counts `(1, 0, 0)`, one
substitution, and the one position reported as a DELETION - which is
`fuzzy-changes-of-the-wrong-kind-for-their-own-counts` exactly. But its SECOND match diverges in the
positions alone, and there `tools/probes/s57b-row72790-changes-in-a-scan.py` says something the
triage did not expect:

```
scan, match 0 (4, 4)   deletions [4, 5, 6]   both engines agree
scan, match 1 (3, 3)   deletions [4, 5, 5]   upstream inside the scan
alone,        (3, 3)   deletions [3, 4, 5]   upstream asked on its own, three ways
this port     (3, 3)   deletions [3, 5, 6]
```

Upstream inside the scan carries two positions of the previous match and repeats one; asked alone
it answers a clean run. **This port matches neither**, and its list shares its last two positions
with the previous match, which is the shape of a carry-over on THIS side. Judge that before pinning
anything: if the port is leaking, the row is an engine fix with a failing test first, and pinning it
under the neighbouring entry would hide it.

**The structural finding, and it is the owner's call.** The gate's third seed is the run date, so
every day draws a fresh 126,080-row sample, and ten or so of its rows are members of families
already judged. The pins are keyed on the row - the 2026-09-14 ruling for this file, and the right
default, because a predicate over two rendered answers has twice absorbed a real engine defect. The
consequence is that the gate is green on the day its rows are judged and red the next morning, and
"GREEN at three seeds" can only ever mean the three seeds that ran. Three ways out, none of them
this slice's to take:

1. **Leave it.** Each day's gate costs an hour of judging. Honest, and it keeps finding real rows -
   this sitting's 72790 among them.
2. **Fix the seed.** Replace the date with a third constant. The gate stops sampling new ground,
   which is the property VERIFICATION.md rule 7a bought.
3. **Give the mature families a predicate arm** keyed on the RECORDED controls rather than on a
   judged string - `searchOnlyPartial && ours == pruneOutcome && expected != pruneOutcome` for the
   skip families, `ours == bestmatchFreeOutcome` for `bestmatch-loses-a-candidate`. These are live
   facts the recorder computes per row, not transcriptions, so the alarm keeps its teeth; the risk
   is the one the S37 review found, a port defect landing exactly on upstream's control answer.
   Narrower than it sounds now that the controls exist, and it is the only option that converges.

**The cut-subject door, re-measured against the re-recorded gate.** Sitting 2 left this owed: the
figures in `reversed-partial-answers-the-cut-subject`'s Reason were taken before the re-record, so
they described reports that no longer exist. `python tools/probes/s57b-cut-subject-door.py 7 4242
20260921` now gives **853 rows named, 795 SAME, 58 DIFFERENT, 0 could not ask** (was 831 / 777 / 54
at seeds 7, 4242 and 20260920). The claim the entry rests on survives the re-measure unchanged:
every one of the 58 DIFFERENT rows is one the report already gives to
`reversed-partial-runs-out-at-the-slice-start`, and every one of the 58 records `upstream no match`
over the slice, so this entry never sees them. Checked by reading each DIFFERENT row's block out of
its own `report-<seed>.txt`. The same numbers appear in `record-oracle.py`'s docstring for the
recorded `cutSubjectOutcome` fact, and both were updated together.

## Sitting 4 (2026-09-21): row 72790 judged - this port is right, and why

Row 72790 was the one row sitting 3 called possibly OURS. It is not. This sitting killed the two
hypotheses that made it look like a port defect, found the mechanism, and left the pin itself for a
sitting that can carry it through the blind review rather than adding to the unreviewed-pin debt
sitting 3 already owes.

**The row.** `(?r)(?=(?:[^\d]?\sß){1<=e<=2})\L<w1>{d<=1}` scanned over `ßß\r\n` under IGNORECASE,
`w1 = ['ß', 'İﬁİ']`, as a `finditer`. Five matches, two of which diverge. The second, the zero-width
match at 3, is the open one: both engines answer `(3, 3)` with counts `(0, 0, 3)` and disagree only
over where the three deletions went - upstream `[4, 5, 5]`, this port `[3, 5, 6]`.

**What made it look like ours.** This port's list shares its last two positions with the PREVIOUS
match of the same scan, which is the shape of a change list carried from one match into the next.

**Hypothesis 1, a carry-over across the scan: dead.** `Matches`, which holds one engine state for
the whole walk, and `EnumerateMatches`, whose state restarts per match, give the same answer. The
same match asked alone gives `[3, 4, 5]`, but that is a DIFFERENT question and not evidence:
anchoring a reversed match needs `match(pos=start, endpos=end)`, which truncates the subject at 3
and starves a lookahead that reads past the match. Upstream moves under the same truncation, from
`[4, 5, 5]` to `[3, 4, 5]`. Probe: `tools/probes/s57b-row72790-port-changes-in-a-scan.cs`.

**Hypothesis 2, ledger entry 11 mechanism A, the `start_match` leak: dead, by negative control.**
Deleting this port's own change-list clear makes it reproduce upstream's leaked answer on that
family's own row and does not move row 72790 by a single position. Re-run it exactly:

- Edit `src/FuzzyRegex/Engine/Matcher.cs`. The two lines as they read now are
  `            Array.Clear(state.FuzzyCounts);` and `            state.FuzzyChanges.Clear();`
  (`Matcher.cs:5154-5155`, inside `if (state.IsFuzzy)` under the `start_match:` label). Delete the
  second and rebuild.
- Rows, three of them, replayed with
  `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57b-row72790-leak-control-rows.jsonl`:
  row 72790 itself,
  the minimised `(?r)(?=(?:a\s){1<=e<=2})b{d<=1}` over `ab` as a `finditer`, and ledger entry 11's
  own row, `(?:[ab][bc](*PRUNE)[wx]){e<=2}` over `qab` as a `search`.
- Result: the control FIRES - the `(*PRUNE)` row's port answer goes from `[d:3]` to `[s:0]`, which
  is upstream's leaked answer exactly - and rows 1 and 2 do not change by one position. Restore the
  line with `git checkout -- src/FuzzyRegex/Engine/Matcher.cs`.

**The mechanism: where a change made OUTSIDE a fuzzy lookahead is recorded.**
`match_fuzzy_changes` (`upstream/src/_regex.c:20524-20598, the deletion shift at :20554-20557`) walks one list in the order the changes
were recorded and adds to each DELETION the number of deletions already emitted, so the same raw
positions in a different order print as different numbers. Un-shifting both engines' lists is what
makes the row readable: upstream's `[4, 5, 5]` is raw `[4, 4, 3]` and this port's `[3, 5, 6]` is raw
`[3, 4, 4]` - the SAME three deletions, ordered differently. Upstream records the body's last, this
port records it first, which is the order a reversed sequence runs in: the body is the rightmost
element, so it is the first the engine reaches.

**Upstream contradicts its own control, on a minimised row.** Take
`(?r)(?=(?:a\s){1<=e<=2})b{d<=1}` over `a`. Both engines answer the zero-width match at 0 with
counts `(0, 0, 2)`: the lookahead deletes `\s` at 1, the body deletes `b` at 0. Write the
lookahead's section without its minimum error count and ask upstream again:

| lookahead section | span | counts | upstream, raw | this port, raw |
| --- | --- | --- | --- | --- |
| `{d<=1}` | (0, 0) | (0, 0, 2) | [0, 1] | [0, 1] |
| `{e<=1}` | (0, 0) | (0, 0, 2) | [0, 1] | [0, 1] |
| `{1<=e<=2}` | (0, 0) | (0, 0, 2) | **[1, 1]** | [0, 1] |

Same span, same counts, same fit - and the fit spends 2 errors, so a floor of 1 rejects nothing. A
minimum error count decides whether a fit is ACCEPTED; it cannot move where a character was
deleted. Upstream's body deletion moves from 0 to 1 anyway, onto the position the lookahead reached,
and upstream's own other two spellings answer what this port answers. That is the judgement: the
port is right and upstream misplaces a change recorded outside a fuzzy lookahead.

**What narrows it.** `tools/probes/s57b-row72790-ladder-rows.jsonl`, ten rows replayed through both
engines in one run with `pwsh -File tools/run-oracle.ps1 -Rows <that file>`: 8 agree, 2 diverge, and
the two are the spellings whose lookahead carries a minimum error count. A non-fuzzy body agrees,
and the section on its own outside a lookahead agrees. The minimum is not the cause, only what
exposes it on the small row - row 72790's own `{d<=2}` spelling still misplaces the deletion the
same way, upstream `[4, 5, 5]` against this port's `[3, 5, 6]`. That rewrite diverges on that one
match only: the self-contradicting third match goes with the minimum, and both engines then answer
it `(1, 2)`, counts `(0, 0, 1)`, deletion at 3. Both halves of that are printed - upstream by the
`row 72790, no minimum` case of the Python probe, this port by the C# probe's
`THE ROW WITHOUT ITS MINIMUM` block, added in sitting 5 when a review found the port half asserted
and unmeasured.

**Probes.** `tools/probes/s57b-row72790-change-order.py` (upstream, both spellings, un-shifted) and
`tools/probes/s57b-row72790-port-changes-in-a-scan.cs` (this port, the three doors and the same
minimised ladder). `tools/probes/s57b-row72790-changes-in-a-scan.py` from sitting 3 still runs; its
closing paragraph, which reads the port's list as a possible carry-over, is what this sitting
disproved.

**Left for sitting 5.** (1) The entry for row 72790 and its gap test: a new family, `Reason`
carrying the table above, keyed on the row and on this port's `Describe()` string, plus the drafted
ledger entry for a new upstream defect - a change recorded outside a fuzzy lookahead in a reversed
match lands on the position the lookahead reached. Note the row also carries the recorded
`selfContradiction` `fuzzy-counts-match-changes` for its THIRD match, where upstream counts one
substitution and lists a deletion, so the entry has to cover both diverging matches. (2) Row 74947,
still as sitting 3 left it. (3) The re-record, the gate, ratchet, one blind review and one verifier
pass over sittings 3, 4 and 5 together, and close.

**Why the leak-free control could not judge this row, which is worth its own line.** The entry
`fuzzy-changes-leaked-from-an-abandoned-attempt` is keyed on the recorded `leakFreeFuzzy`, upstream
asked `match(pos=start, endpos=end)`. `_leak_free_fuzzy`'s docstring says that anchoring breaks on a
fuzzy section inside a lookahead, which has to read past `endpos`, and treats that as a question
upstream will not answer. On a REVERSED row it answers anyway, with a different span-preserving fit
computed on a truncated subject - for row 72790 it returns `[3, 4, 5]`, which is neither engine's
answer to the question actually asked. The failure is in the safe direction: a starved control makes
the strong arm REJECT, so the row is reported rather than pinned, which is exactly what happened.
Left alone deliberately - narrowing it is a change to a live recorded fact and wants its own slice.

## Sitting 5 (2026-09-21): both remaining rows pinned, the gate green at three seeds

**Row 72790 is pinned as a new family**, `reversed-body-change-lands-where-the-lookahead-reached`,
keyed on the row and on this port's `Describe()` string. The key has to carry both diverging matches
of the scan, so the `ours` string is the whole five-match render: the second match is sitting 4's
misplaced deletion, and the third is the row's own `selfContradiction`
`fuzzy-counts-match-changes`, where upstream counts one substitution and lists a deletion. Its
`Reason` carries sitting 4's table, the un-shifted raw lists, why neither neighbouring entry can
take the row, and what the row is NOT. Two gap tests in `FuzzyCountsAndChangesTests`:
`A_change_outside_a_reversed_lookahead_stays_where_the_body_matched` for the minimised shape and its
two controls, and `A_reversed_list_scan_reports_the_substitution_its_counts_claim` for the gate row
whole. Ledger entry 26 is drafted, not filed. Verified by replaying the row:
`pwsh -File tools/run-oracle.ps1 -Rows <one-row file>` reports `expected 1 ... diverge 0 of 1 rows`
and names the entry.

**Row 74947 is NOT a new family, and sitting 3's proposal for it was wrong.** Sitting 3 read it as
`reversed-skip-invents-a-match` inverted - a new `reversed-skip-missing-match` - because the door
that usually settles these rows, deleting the verb, is missing. What is actually missing is only
that ONE door, and the reason it is missing is ledger entry 14: with the verb deleted the pattern
self-recurses round a fuzzy section that can match empty and exhausts memory. The two doors that DO
answer both answer this port's result in full:

```
regex 2026.9.10, spans in CODEPOINTS (the subject is astral)
  as drawn               None
  (?b) deleted           (0, 2) PARTIAL groups=(None, None) counts=(1, 0, 0) changes=([1], [], [])
  (*SKIP) -> (*PRUNE)    (0, 2) PARTIAL groups=(None, None) counts=(1, 0, 0) changes=([1], [], [])
  verb deleted           MemoryError:
```

That pair is `bestmatch-loses-a-partial`'s own argument, and it is how S52's nineteenth sitting
placed sweep rows 4 and 15 in the same entry. The recorder agrees independently: the row carries
`selfContradiction: bestmatch-no-worse`. So the row went in as that entry's tenth row, with a
paragraph in its `Reason`, a paragraph in ledger entry 13 covering all three ablation-placed rows,
and the gap test
`FuzzyBestMatchTests.Bestmatch_reversed_keeps_the_partial_both_of_upstreams_doors_hand_back`. Probe:
`python tools/probes/s57b-row74947-two-doors.py`. The pin was verified the same way as 72790's.

**The lesson, which is S52's lesson again.** A missing control is not evidence of a different fault.
Sitting 3 reasoned from the shape of the evidence rather than from the evidence, and the fix was to
run the ablations and read the output.

**The gate, re-run at `-Count 6000` per seed on 2026-09-21 after both pins.** Seed 7 GREEN, seed
4242 GREEN, seed 20260921 GREEN - 0 of 126,080 rows diverging at each. Run one seed at a time
(`-Seeds 7`, then `-Seeds 4242`, then `-Seeds 20260921`); each fits in the ten-minute blocking cap
where all three together do not.

### Review

One reviewer, the brief in `S57b-review-brief.md`, and the amendment-16 verifier over sittings 3, 4
and 5 together, then a second blind pass over the delta those fixes made. Every finding was
reproduced before anything changed. The real ones:

- the gap test used `FuzzyRegexOptions.FullCase` (0x4000) where row 74947's flag word is 8,
  MULTILINE, and the probe's comment mislabelled it the same way;
- `match_fuzzy_changes` was cited at `_regex.c:20504-20560` in six places and its truncation site at
  `:20522` in two; the function is `:20524-20598`, the deletion shift `:20554-20557`, the sum
  `:20542-20546`;
- row 72790's recorded `selfContradiction` was called its FOURTH match; it is the third;
- the `{d<=2}` spelling was said to "diverge identically" when it diverges on one match of five, and
  its port half was asserted with nothing measuring it - the C# probe now prints that spelling, and
  the two engines agree on all five matches but the (3, 0) one, `[3, 5, 6]` here against `[4, 5, 5]`;
- five citations of `gate-divergence-doors.py 20260921` stopped reproducing when this sitting
  re-recorded that seed green, so they name the `--rows <file>` form;
- `tools/probes/s57b-port-firstset-indexerror.cs:35` failed IDE0055 and would not run;
- `bestmatch-walk-truncated-by-a-skip` explained its row-8 anchored door by state the drawn object
  carries from the verb's moved slice. A freshly compiled object answers `(4, 7)` with `(1, 0, 1)`
  too, so the explanation is withdrawn in the entry and in sitting 3's paragraph above; the row rests
  on its minimisation.

The one finding that did not survive was the claim that the ledger's older `_regex_core.py`
citations sit two lines high. All three are exact: `END_OF_LINE_U` at `:506-510` (entry 5,
`LEDGER.md:626`), `_fix_full_casefold` at `:3636` (entry 6, `LEDGER.md:827`) and `GLOBAL_FLAGS` at
`:170-171` (entry 22, `LEDGER.md:2948`).

**The extra wave is RED, with ten rows nobody has judged.**
`pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions -Seeds 99991,57057` gives
four diverging rows at 99991 (683 `fullmatch`, 9204 `sub`, 9720 `split`, 9951 `finditer-overlapped`)
and six at 57057. That wave is 6000 rows PER GENERATOR, so it is a different sample from the default
gate and this is its first run in this slice. The reports are
`TestResults/oracle/report-99991.txt` and `report-57057.txt`; the waves beside them. The slice's
"Done when" box for it stays unticked and the rows are the next sitting's batch - judge them with
`python tools/probes/gate-divergence-doors.py <seed>` in one pass, as sitting 3 did for the gate.

## Sitting 6 (2026-09-21): the extra wave's ten rows judged, and the slice closed

All ten belong to families this slice has already pinned, so no new entry was needed. They went in
as rows of four existing ones: `bestmatch-loses-a-candidate` rows 22 and 23 (57057/4067,
99991/683), `bestmatch-loses-a-partial` row 11 (57057/9163), `search-start-partial` rows 24 and 25
(57057/6441 and 57057/8825), and `posix-fuzzy-contradicts-its-own-flagless-answer` rows 11 to 15
(57057/8690, 57057/9182, 99991/9204, 99991/9720, 99991/9951).

**Judged in one scripted pass, off the output.** Four commands over the whole batch, never row by
row:

```
python tools/probes/gate-divergence-doors.py --rows tools/probes/s57b-extra-wave-rows.jsonl
python tools/probes/s57b-extra-wave-flag-ablations.py tools/probes/s57b-extra-wave-rows.jsonl
python tools/probes/upstream-partial-anchor-reachability.py \
    tools/probes/s57b-extra-wave-partial-search-rows.jsonl
```

`tools/probes/s57b-extra-wave-rows.jsonl` is committed, so nothing has to be cut out of the waves
again: it is the ten diverging lines of `TestResults/oracle/wave-57057.jsonl` and `wave-99991.jsonl`
as they stand, each tagged with the `s57bSeed` and `s57bRow` it came from (the header line makes row
N line N+1). `s57b-extra-wave-partial-search-rows.jsonl` is the two search rows of the same set.

The ablation probe is new and is the sitting's main instrument. It deletes `(?b)`, `(?e)` and
`(?p)` singly and in pairs, in BOTH spellings - the inline `(?x)` text and the flag-word bit - and
prints upstream's answer for each, so the row is placed by which deletion restores this port's
answer rather than by reading its shape. On every one of the ten, this port's answer is upstream's
own answer with the offending flag deleted. None is a port defect.

What each row turned on:

- **57057/4067 and 99991/683**, both `fuzzy` `fullmatch`es upstream refuses outright, both carrying
  the recorder's `bestmatch-no-worse`. Delete `(?b)` and upstream answers: codepoints (0, 3) with
  insertions at 1 and 2, and (0, 6) with an insertion and a deletion at 1. Neither carries `(?e)`,
  so no second flag is in play.
- **57057/9163**, a `(?b)` partial `fullmatch` with a `(*SKIP)`. All three doors answer and agree:
  `(?b)` deleted, `(*SKIP)` spelt `(*PRUNE)`, and the verb deleted all give codepoints (0, 2)
  partial with nothing spent. Two of the three are on the row itself as `bestmatchFreeOutcome` and
  `pruneOutcome`. It is ledger entry 13's four conditions together.
- **57057/6441 and 57057/8825** are the first rows of the `search-start-partial` arm whose
  `(*PRUNE)` door answers UPSTREAM's span rather than this port's, so the converging controls are
  not available and the reachability grid does the whole argument. Over the searched region (0, 3)
  upstream's own anchored matcher produces (0,0), (0,1), (1,1), (1,2), (2,2), (2,3) and (3,3) on
  each row. Its search answer (1, 3) is in neither grid: it reported a span its own matcher cannot
  make. This port answers (2, 3), which is in the grid and is the first anchor a forward search
  tries that answers at all.
- **The five POSIX rows** carry the family across five operations at once - a partial `match`, a
  `finditer`, a `sub`, a `split` and a `finditer-overlapped`. Two turn on POSIX alone (9182 charges
  a deletion over a single `ß` the POSIX-free engine gets for nothing; 9951 has POSIX report six
  overlapped matches where the POSIX-free engine reports four). Two need POSIX with a second flag
  (8690 with `(?e)`, 9204 with `(?b)`, and dropping either restores this port's answer). 9720 moves
  a GROUP inside an identical overall match, g1 two codepoints under POSIX against one without,
  which upstream's own README rules out: "It looks for the longest overall match. It doesn't look
  for the longest match for each group" (`upstream/README.rst:177`).

**One new test for ten rows, deliberately.** Nine of the ten mechanisms are already asserted by the
tests each family's `PinnedBy` names, and writing nine near-duplicates would buy nothing. Only
9720's shape had no test, so `FuzzyPosixTests.Posix_does_not_lengthen_a_group_inside_the_same_
overall_match` was added for it. The reviewer was told this was a judgement and invited to
reproduce a counterexample; none was found.

**Two traps worth remembering.** The row's pattern carries U+10400 itself, so a C# verbatim string
(`@"...\U00010400..."`) pins the six-character escape text and not the character - the new test
uses a normal string with doubled backslashes. And SonarAnalyzer S125 reads a prose comment whose
punctuation looks like code as commented-out code; the fix was to reword, not to suppress.

**Verified on real data, 2026-09-21, in this order.** The ten rows replayed with
`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57b-extra-wave-rows.jsonl`: GREEN, each row
classified under the entry it was assigned. The extra wave re-run in full,
`-Count 6000 -Generator fuzzy,interactions -Seeds 99991,57057`: GREEN at both seeds. The default
gate re-run one seed at a time at `-Count 6000`: seed 7 GREEN, 4242 GREEN, 20260921 GREEN.
`pwsh -File tools/check-ratchet.ps1`: GREEN, 6503 tests passing, 6395 distinct ids, baseline
raised from 6394.

**Row JSON is JSON-equal to the wave line, not byte-equal.** The generator writes non-ASCII
literally where the wave file writes `\uXXXX`, which is the convention the other 152 non-ASCII
lines of `ExpectedDivergences.cs` already follow. `OracleWave.ParseRows` parses both the same, and
the green replay above is the proof. A verifier asked to check "byte for byte" will report this;
it is not a defect.

### Review

One reviewer, the brief in `S57b-review-brief.md` scoped to this sitting, and the amendment-16
verifier over the batch of ten, both blocking and both on Opus. The reviewer raised four findings
and the verifier two more; all six were reproduced here before anything changed, and all six were
real:

- the `search-start-partial` `Reason` said the verb-deleted door also gives upstream's (1, 3). It
  gives (0, 1) and (0, 3), neither partial, and the probe labels it "NOT a control". Both the
  `Reason` and the inline comment now say so.
- "the first row of this family to answer at all three doors" was false - row 8 of
  `bestmatch-loses-a-partial` answers and agrees at all three. Superlative removed.
- the POSIX paragraph said "four operations at once" and then listed five. It is five.
- `s57b-extra-wave-flag-ablations.py:59` cited the POSIX `fuzzy_changes` guard at
  `record-oracle.py:1019`, which is a docstring in `exhausted()`. The guard is at `:1338`.
- "an extra 6000-row wave" is wrong: `-Count 6000` is per generator, so each seed's wave is 12,000
  rows. The paragraph now says both numbers.
- the same paragraph's range "on rows 14 to 23 that door converged" was narrower than the truth;
  every earlier row carrying the door converges, from row 8.

No finding was rejected. A second blind pass over that delta returned "No defects found." The
verifier's remaining non-CONFIRMED lines were the byte-equality note above and the missing Sitting
6 section, which is this one.

**Left for between-slice maintenance:** `tools/probes/gate-divergence-doors.py:137` carries the same
stale `record-oracle.py:1019` citation, in committed code outside this slice's scope.
