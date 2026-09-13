# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S40b is next, then S40c, S40d, S41, S42, S43.
Launch: `pwsh -File tools/launch-slice.ps1 s40b`.

**S40a is closed over two sessions.** Session 1 landed the recorder's per-row timeout, the
per-match slice reset (ledger entry 5's own proposed fix), ledger entries 10 and 11 and six
permanent tests; **it was rolled back only because the slice file was not moved to `done/`**, and
this session recovered its commit `fae1dd8` by fast-forward and re-ran the ratchet on it.
Session 2 triaged the gate's fifteen rows, classified two, and split the rest.

**The 6000-row three-seed gate is S40d's, and the thirteen rows left are three mechanisms:**
four are the partial pass inheriting a slice a `(*SKIP)` moved (**S40b**, and its fix re-judges
S37's pinned answer); seven are upstream leaking a partial through a group call in an
opposite-direction lookaround (**S40c**); two are the reversed carried slice in shapes no
`ExpectedDivergences` tell reaches (**S40d**). Each slice file carries its rows and its probes.

**Two corrections session 2 made to session 1's own record**, both worth knowing before reading it:
row 93133 is **not a crash** - the port's format-field handling matches upstream's on every index
form and the divergence is upstream losing the match - and the call-partial family's odd answers
are this port's **ASCII** rows, not its astral ones, with 2026.9.10 answering identically so issue
614 is not involved. The superseding DECISIONS entries are dated 2026-09-13.

**Probes, not descriptions:** `tools/probes/upstream-call-partial-leak.py` (`--newer` for
2026.9.10), `upstream-reversed-skip-scan-shapes.py`, `upstream-skip-in-atomic-hang.py`,
`upstream-fuzzy-restart-leak.py`, and the last section of `upstream-group-call-loses-matches.py`.

**Where the port stands:** ratchet GREEN, 5825 tests, 5770 passing, parity **97.2%**, **29** areas at
100% (S40's "30" was never what `docs/STATUS.md` says). 55 skipped, all `(?e)`/`(?b)` - S41's and
S42's scope.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Rows per generator is **`-Count`**;
`-Rows` is a path to a JSONL file and still re-records - use `-SkipRecord` to consume one.
**The gate is CHEAP** - measured 2026-09-13, 126,000 rows records in 17s and consumes in 6s, so
`-Count 6000` at three seeds is about a minute in all. What costs is judging the rows it finds.
**Delete `.scratch/control-waves/<generator>-<count>-<seed>.jsonl` after widening a generator.**

**Still open for the owner:** the design spec's amendment 20 (the Phase 5 re-plan; ROADMAP carries
the repo half); `slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main`
trails local and needs a push.
