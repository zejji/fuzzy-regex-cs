# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice: S43, the phase 5 close - CHECKPOINT after sitting 1.** The slice file is still in
`docs/plan/slices/`. Ratchet GREEN at 5868 tests, 5868 passing, 5760 distinct ids, 0 skipped;
baseline updated. `pwsh -File tools/launch-slice.ps1 s43` resumes it.

**Sitting 1 composed fuzzy into `interactions` and the wave found three upstream bugs.** Three new
piece kinds plus `(?e)`/`(?b)` at row level; named lists are emitted for the first time by any
generator. `partial`/`partial-sliced` are proven byte-identical to HEAD (whole row stream, 21
generators, 4 seeds), so their pinned answers are untouched.

**THE WAVE IS RED AND THAT IS THE FIRST THING TO FIX.** 7 rows of the 3-seed 6000-row default wave
diverge, all `interactions`, **all seven judged, none yet in `ExpectedDivergences`**:

1. **Five are ledger 13, a new upstream bug** - seed 7 rows 74938, 77937; seed 4242 rows 76251,
   76681; seed 20260913 row 76593. All carry `(?b)` + a fuzzy section + `(*SKIP)` + `partial`, all
   recorded `nomatch`, port answers a partial. Port right. Needs a row-keyed entry.
2. **Row 75821 is `group-call-loses-the-match`**, the existing entry - upstream finds 1 match, the
   port 2; removing the call or the whole zero-width piece gives upstream both. Needs its judged row.
3. **Row 77889 is `search-start-partial`'s second symptom** - upstream's search reports (0,2) partial
   and its own `match` denies it at every start. Needs its judged row.

S36's deferred question is answered: recording `interactions` prefilter-free changes **none** of the
seven, so it is not `locate_required_string`.

**One real port defect found and FIXED**: `SaveBestMatch`/`RestoreBestMatch` now carry the fuzzy
counts *and* the change list (ledger 9's port side, closed - upstream carries neither correctly, so
this is amendment 20 rather than a faithful port). Before it, a POSIX fuzzy match reported the
errors it spent as zero. No wave could ever have caught it: upstream faults rendering those rows.

**Also landed:** a `resource` outcome so a MemoryError row is skipped and counted instead of killing
the whole run; ledger entries 13 and 14; three committed probes under `tools/probes/`.

**Still owed by S43:** the three classifications above, symbol accounting, the tag probe, the
control re-runs, CHANGELOG, ROADMAP's measured rate, the Phase 6 handover. **For the owner:**
`slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main` needs a push.
