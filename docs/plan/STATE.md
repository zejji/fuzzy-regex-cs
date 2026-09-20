# State

**Two slices are open: S60 is the one in hand, S57 stays in flight behind it.**

**S60 - prefilter family (Phase 7), checkpointed after sitting 2.** Read
`docs/plan/slices/notes/S60-sittings.md` first: the controls with their exact snippets, and the two
findings. Landed: item 1's **case-sensitive forward arm only** (`LocateRequiredString`, `StringSearch`,
`SimpleStringSearch`, `ReqStringText`), item 5 (`HasSkipVerb` withholds the start-position jump
from `(*SKIP)` patterns, refusal stays), item 15, 19 new gap tests. Suite 6451/6451, ratchet GREEN
and baselined, sync-divergence GREEN, AOT tests and smoke GREEN, AOT binary 6,985,216 bytes =
**+12,288 on the 6,972,928 reference**, against 3,072 bytes of measured build jitter (notes).
Two blind passes and the verifier are DONE over this diff (notes, "Review"). Next sitting:

1. **Judge the `(*SKIP)`-plus-partial oracle family** - rows 5014 (seed 20260920) and 3633 + 5200
   (seed 31337); seeds 7, 4242 and 999 are GREEN. All three proved PRE-EXISTING against `3f1bf91`,
   and 5014 is S57's item-1 row, so one judgement clears both slices' gate.
2. **The benchmark**, on a quiet machine (Stryker queue and the S73 demo still running).
   `ReferenceBenchmarks.BacktrackingPort` now measures the prefilter refusing `(a|a)*b`, not the
   backtracker; say so when the number is re-run.
3. Items 2, 3, 6, 8-14, 16, 17. **No `SearchValues` in `src/` yet** - item 16's set search, not
   this one's substring search, so the slice's box stays unticked.

**S57 - Phase 6 close-out, checkpointed, untouched this sitting.** Checklist and next-sitting order
in `notes/S57-sittings.md`: items 5, 7-11 untouched, 911 uncovered lines unclassified, **no blind
review and no verifier over the S57 diff**. It closes Phase 6 behind S56's survivor triage, which
waits on the Stryker queue.

Standing: `git merge-base --is-ancestor <sha> HEAD` before quoting a SHA. `-Count` on
`run-oracle.ps1` is PER GENERATOR. A hung `VBCSCompiler.exe` (PID 27404, not killed) means every
build needs `-p:UseSharedCompilation=false`.
