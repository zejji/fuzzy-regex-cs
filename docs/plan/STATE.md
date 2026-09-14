# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S48b IS CLOSED (2026-09-14, sitting 3).** Suite 5,963, ratchet GREEN, baseline 5,855, default wave
GREEN at three seeds, and the three-seed 6000-row gate back to **6 + 4 + 9 = 19** - S48's baseline
exactly, which was the slice's Verification bar. **Next slice is S49** (issue sweep, triage and
reproduce). The inherited-bug group S45-S48b is finished and the known-bug list is empty.

**The independent verifier confirmed EVERY number sitting 2 quoted** - the gate, the ratchet, the
default wave, both hand-applied controls with their unmutated baselines, all three probes, every
hand-measured upstream fact, the four gap tests and the over-classification guard, and the three
`ExpectedDivergences` entries at 3 + 1 + 1 rows. It left the tree byte-identical to HEAD.

**The ratchet was RED when the sitting opened and it was NOT the slice's doing.** The stack-bound
test raced the engine's 30s `MatchTimeout` against committing a gigabyte, which is paced by free
memory: 63.4s / 17.6s at 3.4GB free, 15.1-18.8s at 9.3GB. It fails identically at `c8165b5`. Fixed
in its own commit; the one blowing call now has a five-minute budget of its own.

**The blind review killed the first draft of that fix with a reproduction, and the lesson is
general: the assembly `[Timeout(120_000)]` does not touch a CPU-bound synchronous test** - a
20-second body passes under a 3-second assembly timeout, because the `CancellationToken` is the only
enforcement and the engine never receives one. `InfiniteMatchTimeout` would have turned an
unreachable bound into a HUNG SUITE. `check-ratchet.ps1:33-36` already said so.

**Owed maintenance (unchanged, none of it S49's scope by default):** `tools/run-controls.py` cannot
measure a control that mutates the recorder, so **S42-2A is owed** and S48b's own controls C and D
live in its closing notes rather than `controls.json`; the two broken control sites S32-B and S38-A;
`FOLD_TURKIC`'s share of the `case-folding` rotation; S35-A and S29-A/D are thin; PORTMAP's
`_regex.c` line references stale after the sync; `record-oracle.py --self-check` exits 1 on a
pre-S46 message.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
**Housekeeping:** the `s48b-baseline` and `pre-s48b` worktrees can now go.
