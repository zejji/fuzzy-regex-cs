# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S42, SECOND SITTING - a checkpoint, not a failure. `pwsh -File tools/launch-slice.ps1 s42`, then S43.

**First sitting landed** `do_best_fuzzy_match` as `Matcher.DoBestFuzzyMatch`, line for line, with
**upstream's error-count ranking**. Ratchet GREEN at 5846 tests, 5846 passing, **0 skipped - no
`[Skip]` is left anywhere in the suite**. The `fuzzy` generator draws `(?b)`; that wave and the full
default wave are green at three seeds. One real defect fixed: the second pass stepped one code unit
where upstream steps one character. Closing notes and three re-run controls: in the slice file.

**(1) FIRST JOB: a real `ENHANCEMATCH` defect, and it is NOT the cost divergence.**
`(?e)(?:\d\wba){1i+2d+1s<=4}` over `XX8QbaY`: upstream's `search` answers `(2, 4)` with two deletions,
this port answers `(0, 4)` with three substitutions - the improvement loop did not improve at all. No
astral character, no `(?b)`. **Proved not to be the ranking rule:** with `IsBetterFuzzyMatch` reverted
to plain `errors < bestErrors` the row still diverges, and it reproduces against `HEAD`. The committed
generator DOES draw it - twice at seed 7 once the row stream was perturbed - so today's green is luck.

**(2) THEN the cost ranking for `BESTMATCH`, which needs a cost bound inside `basic_match`.** Not
reachable from the second pass: the FIRST pass holds the next run to fewer ERRORS (`:17675`), so issue
470's `voicees` never enters the best list. The bound is what releases up to 2015.09.28 had
(`state->max_cost = state->total_cost - 1`) and the 2015.11.5 issue-165 **hang fix** removed. The
equal-count tie-break alone was measured and rejected: 3, 2 and 3 divergences of 2000, every one a
cheaper match at a different span, and 470 still wrong. Owed with it: the "where we diverge" PORTMAP row, the 470 ledger entry, the benchmark.

**Unchanged by S42:** seed 31 has three unjudged divergences on `partial,partial-sliced,interactions`
at 6000 rows (rows 1075, 6943, 16545); `best_fuzzy_counts` in `SaveBestMatch`/`RestoreBestMatch` stays
unported on purpose, both or neither, ledger 9. **For the owner:** spec amendment 20; `slice-log.jsonl`
marks S26 `failed` though its commit is real; `origin/main` needs a push.
