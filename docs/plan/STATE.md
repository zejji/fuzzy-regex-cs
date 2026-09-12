# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. **PHASE 4 IS COMPLETE** - S36 closed it, the pending queue is empty and the
driver stops at the phase boundary. **Next: the owner authors Phase 5's slices** (fuzzy matching,
`BESTMATCH`, `ENHANCEMATCH`), reading S36's Phase 5 handover in `docs/plan/slices/done/`.

**Blockers:** none. **One thing is deliberately unfinished and is the first candidate for a Phase 5
slice:** the composed `interactions` wave is green at 300 and 2000 rows and RED at 6000, 13 rows over
five seeds. All 13 are already-judged families needing classification work - 9 are S30's group call
inside an opposite-direction lookaround, 3 are `search_start`'s partial arms, 1 is a new
`bounded-lazy-repeat-partial` row. Details and the disposition are in S36's closing notes.

**Where the port stands:** ratchet GREEN, 5770 tests, 5587 passing, parity **90.7%**, 28 areas at
100%. Default oracle GREEN at three seeds, 6000 rows each. Every remaining skipped test (183) is
fuzzy; **no seam left anywhere in `src/` is anything but a fuzzy one**.

**What S36 did.** Judged the three `verbs` rows S35 left: all three are upstream's and the port is
right on all three, classified as `overlapped-skip-extra-match-reversed`. Row 1439 does NOT depend on
call order - `verbs` is recorded prefilter-free and the run that disagreed was not - so the recorder
needs no per-row isolation. Widened `interactions` to compose Phase 4's families; it immediately
found an upstream capture recorded outside the subject (issue 614's forward mirror, a lookbehind
rather than `(?r)`) and a recorder that raised `IndexError` rather than write it down. Re-ran all 24
Phase 4 controls at their own seed and at a fresh 99991: 20 healthy, 4 thin or dead and all 4 already
recorded as such by their own slices.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Controls:
`python tools/run-controls.py --slices S27,S28,S29,S30,S31,S32,S33,S35 --seeds 2` - `--seeds N` is
new and takes the first N, which is the recorded seed plus 99991.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`, seven entries): nothing
filed until everything else in the plan is done. Entry 7 (`İ` never reaches the full fold) is on
Phase 6's fix list, first item of the sweep's third slice.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real (S29's
`failed` entry is a genuine first attempt, followed by a `completed` one). Phase 4: 10 slices, 11
sessions, median 50.9M tokens.
