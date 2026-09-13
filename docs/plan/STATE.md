# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S42 is done and in `done/`. Next is **S43, the phase 5 close** -
`pwsh -File tools/launch-slice.ps1 s43`. It is the last slice in the queue, so the driver stops
after it and the owner authors phase 6.

**S42's second sitting landed the cost ranking for `BESTMATCH`.** Ratchet GREEN at 5854 tests, 5854
passing, 5746 distinct ids, 0 skipped. Full default wave and the `fuzzy` wave both green at three
seeds. `Matcher.DoBestFuzzyMatch` walks the slice twice - walk 0 bounded by the new
`MatchState.MaxCost` finds the cheapest match, walk 1 is upstream's walk with that cost pinned - which
is how "cheapest, then fewest errors, then earliest" gets expressed at all: one scalar budget cannot
carry a lexicographic order. Gated on one fuzzy section and a weighted equation, both measured.

**Read the slice file's closing notes before S43.** Three things in them change what a later session
should assume:

1. **The first sitting's "real `ENHANCEMATCH` defect" was not one** - it is the cost divergence at a
   different span, and its "proved not to be the ranking rule" measurement does not reproduce.
2. **One ported test now asserts this port's answer**, `test_fuzzy#44`, with upstream's own engine
   quoted beside it as proof the cheaper match is real. The first sitting recorded that none would.
3. **Both blind passes found a real defect in code**, and both were HANGS rather than wrong answers -
   a per-section quantity standing in for a whole-match one, twice. Anything that later ranks or
   bounds a whole match must read the live `state.FuzzyCounts`, not the `END_FUZZY` snapshots.

**Owed, and not S42's:** ledger entry 12, a real inherited upstream bug found on the way (`(?b)` loses
a match that plain fuzzy matching finds when the best fit needs two trailing insertions), with a
proposed one-line fix - Phase 6's sweep. The second of S42's two fixes has had no blind pass of its own
(three lines and a field, each pinned by a test that hangs without it); rule 4 wants one and rule 6
says a third round inside one sitting is where iterating stops paying.

**Unchanged by S42:** seed 31 has three unjudged divergences on `partial,partial-sliced,interactions`
at 6000 rows (rows 1075, 6943, 16545); `best_fuzzy_counts` in `SaveBestMatch`/`RestoreBestMatch` stays
unported on purpose, both or neither, ledger 9. **For the owner:** spec amendment 20;
`slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main` needs a push.
