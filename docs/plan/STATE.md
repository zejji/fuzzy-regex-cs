# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**PHASE 5 IS COMPLETE. S43 closed it; `docs/plan/slices/` is empty, so the driver stops at the phase
boundary and the owner authors Phase 6.** Ratchet GREEN at 5,869 tests, 5,869 passing, 5,761 distinct
ids, **0 skipped**; baseline updated. **Overall parity 100.0%, 1,967 of 1,967 ported upstream tests.**

**Fuzzy matching works** - the constraint grammar including weighted costs and `{...:test}`, errors
against characters, strings, backrefs and folded text, the counts and change positions, and both
ranking modes. Two deliberate divergences, one owner decision: `(?e)` and `(?b)` rank by COST where
upstream ranks by error count (upstream issue 470).

**The oracle is green at every seed the gate asks for.** Default list, 3 seeds, 6,000 rows a
generator: GREEN. `fuzzy`+`interactions` at seed 99991, 6,000 rows: GREEN. Fourteen
`ExpectedDivergences` entries, two of them new in S43.

**Sitting 2 classified the seven rows sitting 1 left, then the fourth seed drew three more** -
`partial-retry-carried-slice-forward` (new entry, the forward twin of S40b's reversed one), a fourth
`partial-retry-reversed-slice` row, and a `group-call-loses-the-match` row that **overturned ledger
entry 8's written claim that no minimal form existed**. It is
`(?P<g1>\w)(?<=(?&g1))\W` over `'aa '` - three items, and upstream loses the match at every subject
length. The subject must be three characters: at two, S40c's `min_width` mask hides it on both
engines. The pinned test was written with `'a '` first and FAILED, which is how that was found.

**Phase 5's measured rate: 11 slices, 14 sessions (1.27), median 69.9M tokens, 702M for the ten
completed slices before the close.** Authored as 7; the four additions (S40a-S40d) were all the
oracle finding real defects. ROADMAP and design spec amendment 21 carry the reasoning.

**Owed to Phase 6, in the S43 closing notes**: the sync procedure (newest RELEASE, re-record every
`ExpectedDivergences.Example` or the staleness alarm goes quiet), the fix list in priority order
(ledger 7 first), oracle hardening scoped on SEEDS not rows, the AOT gate, mutation testing.

**Two known gaps, both in the closing notes.** S41's and S42's controls were prose-only and are now
in `tools/controls.json` (14 added, 97 resolve); **5 controls are DEAD** - S31-A/B/C, S32-B, S38-A -
their `before` text no longer appears in the source. `Seam.For` in `src/` is 4, not 0: all four are
unreachable `default` arms, not unported capability.

**For the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main` has
had nothing pushed for the whole of Phase 5; `budget.json` caps still read 5/day and 12/week.
