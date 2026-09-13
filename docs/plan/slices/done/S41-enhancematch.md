---
slice: S41
phase: 5
title: ENHANCEMATCH - do_enhanced_fuzzy_match and the capture save/restore trio
delivers: [fuzzy-enhancematch]
---

# S41 - `ENHANCEMATCH`

The smaller of the two "improve the match" modes: after finding a fuzzy match, try again inside
its span with a tighter error limit until the fit stops improving. Depends on S40. Six tests.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **`do_enhanced_fuzzy_match`** (`:17862-18026`) replaces the `fuzzy-enhancematch` seam at
  `Matcher.cs:6588`. Read the whole loop before porting: it narrows `slice_start`/`slice_end` to
  the previous best match's span, lowers `max_errors` to one below the best so far (not below
  `RE_MAX_ERRORS`, which is 10, `:203`), and ends on a perfect match or on the first run that fails
  to improve.
- **The dead `same_match` check is NOT ported as live code** (owner decision 2026-09-12, DECISIONS).
  At `:17942-17943` upstream computes `same_match` and overrides it to `FALSE` on the next line, so
  the `same_span_of_group` loop never runs and the early exit `if (same_match || ...)` reduces to
  `total_errors == 0`. Evidence it is deliberate: the 2014 and 2015.09 releases had the check live
  (`if (same) break;`) in one combined best/enhanced loop, and release 2015.11.5 - the Hg issue 165
  "Performance / hung search" rework that split the loop into `do_simple`/`do_enhanced`/`do_best` -
  introduced `same_match` already overridden. Analysis: the check is only an EARLIER exit; honouring
  it can never find a match the current code misses and can lose one (a run that improves 2 to 1
  errors on the same span stops there instead of trying for 0), and the override's whole cost is
  one failing run per enhanced match on an already-narrowed slice. Port the effective behaviour;
  leave the two upstream lines and the loop as a comment quoting `:17942-17955`, and move
  `same_span_of_group` (`:11646`) to PORTMAP's deliberately-not-ported table with this evidence.
- **Experiment (cheap, do it):** port the honoured check behind an internal `static` switch for the
  duration of the slice, run the `fuzzy` generator with `(?e)` both ways at three seeds, and count
  `BasicMatch` runs per row. Expected: results identical or better with the override, and at most
  one extra run per row. Quote the counts in the closing notes and remove the switch before commit.
- **Ranking rule, shared with S42.** `better = state->total_errors < fewest_errors` (`:17930`) ranks
  runs by error COUNT. Owner decision 2026-09-12: this port ranks by COST (`total_cost`, `:9649`),
  ties by fewer errors, then earliest, for both `(?e)` and `(?b)`. Port upstream's count ranking
  first and get the tag green, then switch, exactly as S42 does; the two slices must use one
  comparison helper. With unit costs the two rules agree, so every ported test is unaffected; the
  divergence is confined to cost equations and is pinned by a gap test plus an
  `ExpectedDivergences` entry whose predicate requires `(?e)` and a non-unit cost.
- **`save_captures` / `restore_groups` / `discard_groups`** (`:17403`, `:17468`, `:17500`), whose
  only callers are this function and S42's. `GroupData.Copy` already snapshots live spans (the
  S32 `save_best_match` precedent); decide whether these three collapse onto it or need upstream's
  shape for S42's repeated use, and say which in PORTMAP.
- **`save_fuzzy_changes` / `restore_fuzzy_changes`** (`:9899`, `:9930`) if S38 did not already port
  them for the POSIX copy.
- The restored `slice_start`/`slice_end` at exit (`:18005-18006`) is what makes a following scan
  step correct: pin it with a `finditer` test under `(?e)`.

## Verification

- Un-skip `needs:fuzzy-enhancematch` (6 tests: `FuzzyEnhanceMatchTests.cs` and the flagged
  siblings elsewhere, each of which sits beside a note naming its unflagged sibling per DECISIONS
  2026-08-30).
- Gap tests: a match `(?e)` improves and one it cannot; a cost equation where cost ranking and
  count ranking disagree, asserting the cost-ranked answer with upstream's answer quoted beside it; `(?e)` with `(?r)`; `(?e)` in a scan; the
  counts and changes of the improved match.
- **Add `(?e)` to the `fuzzy` generator** on every body shape. GREEN at three seeds, 2000 rows.
- Negative controls: `max_errors` not lowered between runs; the slice not narrowed; the best
  groups not restored when the last run is worse.

## Done when

- [x] Tag delivered; counts in closing notes.
- [x] `fuzzy` generator with `(?e)` green at three seeds.
- [x] PORTMAP rows for the five ported symbols; `same_span_of_group` in the deliberately-not-ported
      table with the 2015 evidence; experiment counts in the closing notes.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: `max_errors` allowed to reach
      `PY_SSIZE_T_MAX` again after the first run; groups restored from a stale snapshot), commit.

---

# Closing notes (2026-09-13)

**`fuzzy-enhancematch` is delivered: 25 test cases un-skipped, all passing, 30 skips left on the
board and every one of them `fuzzy-bestmatch`.** The slice file said six tests; the board said 30,
because design spec amendment 18 re-tagged 31 cases at S40 onto the capability they actually wait
on. Five of the 30 were `(?b)` rows sharing a fan-out method with a `(?e)` sibling, so four methods
were split by mode the way amendment 18 split eight of them by ranking - a `(?e)` row must not stay
skipped to keep its `(?b)` sibling company. Suite 5835 tests, 5805 passing, 30 skipped.

**What landed.** `Matcher.DoEnhancedFuzzyMatch` (upstream `:17862`), `SaveCaptures` (`:17403`),
`RestoreGroups` (`:17468`), `SaveFuzzyCounts`/`RestoreFuzzyCounts` (`:17520`, `:17526`) and
`SaveFuzzyChanges`/`RestoreFuzzyChanges` (`:9899`, `:9930`); `RestoreBestMatch`'s own group loop now
calls `RestoreGroups`, because it was the same code. `discard_groups` is not ported (a `free` on a GC
heap) and `:17985-17986` is not ported (unreachable: the only path to it has just set `max_errors`
from the error count of a match that was found). One field upstream has no counterpart for,
`MatchState.TotalCost`, written at the two `END_FUZZY` sites that write `TotalErrors`.

**The `same_match` experiment, which the slice asked for and which reversed its own expectation.**
The check was ported behind a temporary switch and three 2000-row `fuzzy` waves replayed both ways.
Honouring it saves 0.076, 0.075 and 0.063 `BasicMatch` runs per enhanced match - 1.419/1.423/1.411
down to 1.343/1.348/1.348, about 5% - which is well inside the "at most one extra run per row" the
slice predicted. But the slice also predicted "results identical or better with the override", and
they are not identical: honouring it changes the answer on 18, 15 and 14 rows of 2000, and the
override's answer is upstream's on every one of them, because the wave is green with the override at
all three seeds. Some changed answers lose a perfect match outright -
`(?e)(?:\d+b+?){2i+1d+1s<=2}` over `1bb` is an exact match at (0, 2) with the override and a
one-substitution match at (0, 3) without it. The figures are a LOWER bound: the experiment guarded
the check with "a previous best exists", which upstream's dead code does not, and that can only make
it fire less often. So the DECISIONS verdict holds and its reasoning is now stronger than "the
override is cheap". `same_span_of_group` is in PORTMAP's deliberately-not-ported table.

**Ranking by cost, and the defect that came with it.** Upstream's `better` at `:17930` does two jobs:
it decides whether to keep a run, and - as its `else break` at `:17972` - whether to go round again.
The first draft replaced the whole test with a cost comparison, which looks like a re-ranking and is
not: `max_errors` holds each run to fewer errors than the last, so upstream's test is all but always
true and replacing it CUTS the chain at the first run that costs more. **The blind review found it**,
with `fullmatch("(?e)(?:x|xyq){1i+9s+9d<=20}", "yzxyz")`, where the kept run was worse than
upstream's answer on the cost this port ranks by AND on the error count upstream ranks by. The two
jobs are now two tests: upstream's, unchanged, for termination, and `IsBetterFuzzyMatch` for what is
saved. So this port walks upstream's chain to the end and keeps the cheapest run on it; its answer is
never dearer than upstream's and never uses fewer errors.

**Still open, and the honest size of it.** `enhancematch-ranks-by-cost` classifies a cost-ranking
divergence only when the two answers are otherwise the same match. A cheaper match can also sit at a
different SPAN, and then the `sub`, `split` and `finditer` counts differ too; on the 2500-row probe
wave that is 31 rows reported against 12 classified, none of them dearer. Widening the predicate to
swallow them would hide exactly what a real engine defect looks like, so it is not widened. No
committed generator draws the family at all - 435 rows carrying both `(?e)` and a non-unit cost
equation across three 2000-row seeds produced none - so `tools/probes/enhancematch-cost-rows.py` is
committed to make it reachable, and S42 decides what to do about the 31.

**A find on the way past, ledgered not chased.** Checking whether POSIX's missing `best_fuzzy_counts`
was a live gap turned up an upstream crash and narrowed ledger entry 9's cause: reading
`Match.fuzzy_changes` on a `(?p)` fuzzy match segfaults, `regex.match(r'(?p)(?:cat){e<=1}',
'caz').fuzzy_changes` being the whole of it, where `.fuzzy_counts` answers `(1, 0, 0)`. Upstream has
no `best_fuzzy_changes` beside `best_fuzzy_counts`, so POSIX restores a count of 1 over a changes
list its own backtracking emptied and `match_fuzzy_changes` reads past the end. Entry 9 said "match
is not affected"; it is. **That is why S41 did not port the counts copy although it ported the
helpers**: taking it alone imports the contradiction into a memory-safe language, where it surfaces
as a match reporting one substitution and no substitution position. S42 takes both or neither.

## Gates

- Ratchet GREEN, baseline updated: 5835 tests, 5805 passing, 5697 distinct ids.
- `fuzzy` generator, 2000 rows, three seeds (7, 4242, 20260913): 0 diverge at each.
- The full default wave, 2000 rows a generator, three seeds: 42,000 rows a seed,
  **0 diverge** at each, 14 + 15 + 24 classified and every one a pre-existing family.

## Negative controls

All five re-run on the committed code after the last change, at seed 7 and at seed 31 - a seed this
slice used nowhere else. Wave: `pwsh -File tools/run-oracle.ps1 -Generator fuzzy -Count 600 -Seeds
7,31`, except Control E, which needs a generator no wave has.

**Control A, `max_errors` not tightened between runs.** In `Matcher.cs`, `DoEnhancedFuzzyMatch`,
replace

```csharp
            state.MaxErrors = state.TotalErrors;
            if (state.MaxErrors < FuzzyValue.MaxErrorsLimit)
            {
                --state.MaxErrors;
            }
```

with `state.MaxErrors = state.TotalErrors;`. Result: **22 diverge of 600 at seed 7, 27 at seed 31.**

**Control B, the slice not narrowed to the best match's span.** In the same loop, delete

```csharp
            if (state.Reverse)
            {
                state.SliceStart = state.TextPos;
                state.SliceEnd = state.MatchPos;
            }
            else
            {
                state.SliceStart = state.MatchPos;
                state.SliceEnd = state.TextPos;
            }
```

leaving `state.TextPos = state.MatchPos;`. Result: **4 diverge of 600 at seed 7, 5 at seed 31.** Thin
at both seeds and it fires at both; the generator reaches this through the narrowed re-search rather
than through the span, which is why it is the smallest of the five.

**Control C, the best groups not restored when the last run is worse.** In the exit block, delete
`RestoreGroups(state, bestGroups);`, leaving `RestoreFuzzyCounts(state, bestFuzzyCounts);`. Result:
**8 diverge of 600 at seed 7, 13 at seed 31.**

**Control D, the ranking reverted to upstream's error count.** Replace `IsBetterFuzzyMatch`'s body

```csharp
        cost != bestCost ? cost < bestCost : errors < bestErrors;
```

with `cost >= 0 && bestCost >= 0 && errors < bestErrors;` - the guards are there only to keep the
unused-parameter analyzer quiet. Result: the `fuzzy` wave stays GREEN, which is the point: **with
unit costs the two rules agree**, so no wave and no ported test can tell them apart. What goes red is
the pair that pins the divergence on purpose -
`FuzzyEnhanceMatchTests.Cost_ranking_keeps_the_cheaper_match_where_upstream_takes_the_one_with_fewer_errors`
(1 of 12 failed) and `OracleWaveTests.Every_expected_divergence_still_diverges` (1 of 1 failed).

**Control E, termination and ranking merged back into one test - the defect the blind review found.**
Needs `python tools/probes/enhancematch-cost-rows.py .scratch/cost.jsonl 777`, then
`pwsh -File tools/run-oracle.ps1 -Rows .scratch/cost.jsonl`. Delete the local
`long fewestErrors = long.MaxValue;`, replace

```csharp
            if (state.TotalErrors >= fewestErrors)
            {
                // The fit has stopped improving, so there is nothing further down the chain.
                break;
            }

            fewestErrors = state.TotalErrors;
            state.MaxErrors = fewestErrors;
```

with

```csharp
            if (!IsBetterFuzzyMatch(state.TotalCost, state.TotalErrors, lowestCost, lowestCostErrors))
            {
                break;
            }

            state.MaxErrors = state.TotalErrors;
```

Result: **54 diverge of 2500, against 31 on the committed code**, and some of the 54 are strictly
worse than upstream on both measures - row 455, `(?e)(?:(?:cat|x)){1i+2s+9d<=30}` over `acx`, is
upstream `(1, 0, 0)` costing 2 and this port `(3, 0, 0)` costing 6, verified directly against regex
2026.7.19. Reverting the ranking instead (Control D's edit) gives **0 diverge of 2500**, which is
what says the whole family is the ranking rule and nothing else in the loop.

## Review

**Two blind passes, and the first one earned the slice.** Pass 1 saw the whole diff and raised two
findings. Finding 1 - the cost test cutting the improvement chain short - was reproduced here before
anything was touched, with its own row through `run-oracle.ps1 -Rows`, and is the defect the
"Ranking by cost" section above describes; it was real and it was this slice's. Finding 2, that the
`ExpectedDivergences` entry's narrowing premise was false, was a consequence of finding 1 and was
answered by correcting the entry's Reason rather than by widening the predicate. Its clean list -
`max_errors`, the stale `available`, the slice restore, `TotalCost`'s two write sites, the
`:17985-17986` unreachability claim, `TryReadCosts` over 12,504 probe rows, and the `(?e)` draw
against the generator's other flags - was verified with waves rather than asserted.

**Pass 2 was a first pass over unreviewed code, not a second opinion**: the loop restructure, the new
probe and the rewritten Reason, none of which pass 1 saw. It found no defect in the loop - it
isolated termination, the exit guard and the snapshot consistency by reverting only the ranking and
getting 0 diverge on three waves - and raised two prose findings. Both were checked and both were
right: "which `BESTMATCH` shares" was present tense about a seam, and the probe's "7 of them strictly
worse" was one scoring rule's number where another honest rule gives 19. The first is now written as
the intention it is; the second is gone, replaced by the figure that is exactly reproducible and one
worked row, with the disagreement itself recorded rather than a number picked.

**Reproduction of the slice's own gates is in the section above; nothing here rests on a claim that
was not run.**
