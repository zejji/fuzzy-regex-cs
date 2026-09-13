# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S40c is next, then S40d, S41, S42, S43.
Launch: `pwsh -File tools/launch-slice.ps1 s40c`.

**S40b is closed.** `Matcher.DoMatch` now restores `SliceStart` and `SliceEnd` with `textPos` before
the partial retry, so a `(*SKIP)` in the non-partial pass no longer moves the slice the partial pass
searches. Its four gate rows agree; two pinned "wrong answer on purpose" tests were flipped and S37's
verdict re-judged (this port now answers upstream's own `match(pos=2)`, narrowing
`search-start-partial` to the prefilter alone).

**S40a's reason for not making that fix was measured and reversed.** The reversed row it recorded as
a regression is **upstream's own defect**: upstream's search answers (0, 0) where its own
`match(endpos=1)` and its own `(*PRUNE)` spelling both answer (0, 1). Three such rows are now the
`partial-retry-reversed-slice` entry (seeds 7 and 20260913, none at 4242).

**The gate is down from fifteen rows to nine:** `tools/run-oracle.ps1 -Count 6000` gives 2+4+3 at
seeds 7, 4242, 20260913, all of it **S40c's seven** (upstream leaking a partial through a group call
in an opposite-direction lookaround) and **S40d's two** (the reversed carried slice). Each slice file
carries its rows and its probes.

**Two things S40b learned that the next slices need.** (1) **The gate's three seeds cannot separate a
half-fix from a fix here** - S40b's control B scored identically at 7/4242/20260913 and only seed 31
and the port's own self-consistency told them apart. (2) **Seed 31 has three unjudged divergences on
`partial,verbs` at 6000 rows**, present with and without S40b's fix; they are listed in S40b's closing
notes and are worth S40d's attention when it fixes the gate's final shape.

**A row index needs its command:** "row 101560" only means anything under
`tools/run-oracle.ps1 -Count 6000` (full generator list, 126,000 rows a seed). The S40b review lost
time to this.

**Probes, not descriptions:** `tools/probes/upstream-partial-retry-slice-restore.py` (new),
`upstream-call-partial-leak.py` (`--newer` for 2026.9.10), `upstream-reversed-skip-scan-shapes.py`,
`upstream-skip-in-atomic-hang.py`, `upstream-fuzzy-restart-leak.py`, and the last section of
`upstream-group-call-loses-matches.py`.

**Where the port stands:** ratchet GREEN, 5826 tests, 5771 passing, parity **97.2%**, **29** areas at
100%. 55 skipped, all `(?e)`/`(?b)` - S41's and S42's scope.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Rows per generator is **`-Count`**;
`-Rows` is a path to a JSONL file and still re-records - use `-SkipRecord` to consume one. The gate
is CHEAP - about a minute for three seeds. What costs is judging the rows it finds.
**Delete `.scratch/control-waves/<generator>-<count>-<seed>.jsonl` after widening a generator.**

**Still open for the owner:** the design spec's amendment 20 (the Phase 5 re-plan; ROADMAP carries
the repo half); `slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main`
trails local and needs a push.
