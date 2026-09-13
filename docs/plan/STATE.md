# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S42 is next, then S43.
Launch: `pwsh -File tools/launch-slice.ps1 s42`.

**S41 is closed: `ENHANCEMATCH` landed and `fuzzy-enhancematch` is delivered.** 25 test cases
un-skipped, ratchet GREEN at 5835 tests / 5805 passing, 30 skips left and every one
`fuzzy-bestmatch` - which is exactly S42's scope. The `fuzzy` generator now draws `(?e)` on a third
of its rows: 0 diverge at three seeds, 2000 rows, and the full default wave is 0 diverge at three
seeds, 42,000 rows a seed.

**The blind review found a real defect and it is the thing to carry forward.** Upstream's `better`
(`:17930`) decides BOTH whether to keep a run and whether to loop again. Replacing it wholesale with
this port's cost comparison looks like a re-ranking and instead CUTS the improvement chain at the
first run that costs more - the kept run was worse than upstream's on cost AND on error count. It is
two tests now: upstream's for termination, `IsBetterFuzzyMatch` for what is saved. **S42 must not
repeat the mistake in `do_best_fuzzy_match`**, which has the same shape at `:17647`.

**Three things S42 inherits, none of them optional.** (1) `enhancematch-ranks-by-cost` classifies a
cost divergence only when the match is otherwise the same; a cheaper match at a DIFFERENT span is
reported, which is 31 rows against 12 on the probe wave and would go red the day a generator drew
the family. `tools/probes/enhancematch-cost-rows.py` is committed so it can be. (2) `BESTMATCH` is
to call `IsBetterFuzzyMatch`, not spell the rule again. (3) `best_fuzzy_counts` in
`SaveBestMatch`/`RestoreBestMatch` is still unported ON PURPOSE: upstream has no
`best_fuzzy_changes` beside it, so taking the counts alone imports the contradiction that segfaults
upstream - `regex.match(r'(?p)(?:cat){e<=1}', 'caz').fuzzy_changes`, ledger entry 9, which S41
amended with the narrowed cause. Both or neither.

**Still open, unchanged by S41:** seed 31 has three unjudged divergences on
`partial,partial-sliced,interactions` at 6000 rows (rows 1075, 6943, 16545), present before S40a.
The `$`-tell hole is narrowed, not closed.

**Still open for the owner:** the design spec's amendment 20 (the Phase 5 re-plan; ROADMAP carries
the repo half); `slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main` trails
local and needs a push.
