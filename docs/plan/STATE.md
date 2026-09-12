# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S32 is **closed** and in `done/` (2026-09-12). Next is S33, the phase close -
**the last slice of Phase 4**, after which the driver stops at the phase boundary for the owner.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5759 tests, 5574 passing, tree clean. Oracle GREEN over the
eighteen default generators - `posix` joined this slice - 5400/5400 at seed 20260913. `verbs` and
`partial-sliced` are off the default list; run both explicitly.

**What S32 landed.** All 8 `needs:posix-matching` tests, and the port was three helpers
(`SaveBestMatch`, `RestoreBestMatch`, `CheckPosixMatch`) plus the two arms that drive them. No engine
defect came out of the wave. **Two scope corrections**, both in PORTMAP: `same_values` and
`equivalent_nodes` are *not* POSIX's - their one caller is the `do_search_start` suppression at
`:11771`, so they are Phase 7 with the rest of the prefilter - and the `RE_BestList` family is
`do_best_fuzzy_match`'s and stays Phase 5. **PORTMAP's symbol-audit counts were edited by hand and no
longer match `.scratch/symbol-audit.py`'s 2026-09-01 output; re-running it is S33's.**

**POSIX is exhaustive, so it is slow**, and the first `posix` generator emitted a backtracking bomb
that red a wave on the row timeout - a 15ms row became 17.9s in Debug, where upstream takes 0.69s and
this port 1.1s optimised. It was never a divergence. The rule that fixed it is in the generator: a
quantifier goes on a piece only if the piece holds none already.

**One divergence is pinned and parked**, found by the S32 blind review and *not* POSIX's:
`regex.compile(r'(?r)(ab)+').fullmatch('xabz', 1, 3)` is `None` upstream and matches here. Needs
reversed + a general repeat + a narrowed slice, all three; only `fullmatch` sees it. Same
slice-versus-text bounds family as S31's partial rows. Test in
`Gaps/Engine/ReverseMatchingTests.cs`. **Which side is right is open** - upstream refusing the slice
that is exactly the match looks wrong - so it is recorded, not diagnosed.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S32`. Delete `.scratch/control-waves/` after a generator
change. S32-A and S32-B are strong - 338 to 451 rows of 2000, stable within 35 across three seeds.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real.
