# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S31 is **closed** and in `done/` (2026-09-12). Next is S32, POSIX matching.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5750 tests, 5557 passing, tree clean. Oracle GREEN over the
seventeen default generators - `partial` joined this slice - 6400/6400 at seed 20260912. `verbs` and
the new `partial-sliced` are off the default list; run both explicitly.

**What S31 landed.** All 82 `needs:partial` tests, and the seam was three lines: `do_match`'s
fallback was already ported and correct, and only `NewMatch` treated `RE_ERROR_PARTIAL` as an error.
Two real engine defects came out of the wave, not the suite - `TryMatch` swallowing a PARTIAL, and
`LAZY_REPEAT_ONE`'s default arm never asking `partial_side`. **Scope correction: `finditer` does take
`partial`** (the slice file said otherwise), so `Matches` gained it; `findall` really does refuse it,
so `Count` did not.

**Three divergence families are pinned and parked**, each with a test in
`Gaps/Engine/PartialMatchingTests.cs` marked to invert: upstream's unported `search_start` prefilter
answering a partial of its own (~1 row in 2000, same mechanism as S29's `verbs`); a partial at the
left edge of a **narrowed slice**, which the new non-default `partial-sliced` generator finds at 8
rows in 2000; and a **bounded lazy repeat** losing its partial (`ba??x` on `baa`).

**Do not re-try the lazy-repeat fix without reading its test first.** S31 tried the obvious guard and
the second blind pass measured it over 20,160 rows as 125 rows fixed and **219 introduced** - the
repair needs upstream's specialised `*_REPEAT_ONE` arms, which are Phase 7's. The measurement is in
the test comment and in `run-oracle.ps1`.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S31`. Delete `.scratch/control-waves/` after a generator
change. S31-D and S31-E are **thin** - 0 to 4 rows over baseline in 2400 - and the closing notes say
so rather than dress it up; widening the generator to reach those cells is worth a look.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. The 108-test baseline gap is explained and closed (2026-09-12): ported tests carrying two
identical `[Arguments]` rows run twice under one id, and the baseline is a set.
