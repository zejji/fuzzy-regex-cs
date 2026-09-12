# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S30 is **closed** and in `done/` (2026-09-11). Next is S31, partial matching.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5737 tests, 5462 passing, tree clean. Oracle GREEN over the
sixteen default generators - `recursion` joined the list this slice - 6400/6400 at seed 20260911.
`verbs` is still off the default list for S29's reason; run it explicitly.

**What S30 landed.** All six arms of `CALL_REF`, `GROUP_CALL` and `GROUP_RETURN`, plus
`push_groups`/`pop_groups`, which also completes `push_repeats`/`pop_repeats`'s last six call sites.
41 skips removed, 60 tests delivered, no `needs:recursion` left. `ByteStack.PushNode` now carries
upstream's `NULL` as index -1. `GROUP_CALL` reassigns `node` rather than calling the matcher, so a
10,000-deep `(?R)` costs heap, not .NET stack.

**Two things a later slice should not re-derive.** Upstream's `group_call_guard_list` is **write-only**
- six sites, no read - so it is now a permanent row in PORTMAP's "deliberately not ported" table
rather than an open Phase 4 hole; only the fuzzy-guard allocation is still owed, by Phase 5. And
`Match.GroupAt` used to drop the capture list of any group with `Current < 0`; upstream keeps
`current` and `count` apart, and a group call is the first construct that makes them disagree.

**One divergence, and it is upstream's.** A `(?&name)` call inside a **lookbehind** matches here and
fails upstream whenever anything else is in the sequence. Minimal:
`regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))c").match("ac", pos=1)` is `None`, ours is `(1, 2)`.
Upstream contradicts itself twice over it (its `search` and `match` disagree at one position; `c?`
matches the `c` that `c` refuses). Ruled out: our parser, and the required-string prefilter. Pinned in
`Gaps/Engine/GroupCallTests.cs`, sits with upstream issue 614 for Phase 6. **Nothing is drafted or
filed upstream - that needs the owner's approval.** The `recursion` generator does not draw the shape.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S30`. Delete `.scratch/control-waves/` after a generator
change. S30-B needs 2400 rows to fire reliably - at 600 it caught nothing at one seed; the slice
notes say why, and say to re-measure both controls after any widening.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. The 108-test baseline gap is explained and closed (2026-09-12): 107 ported tests carry two
identical `[Arguments]` rows, faithfully, so they run twice under one id; the baseline is a set. The
ratchet now prints the distinct-id count beside the result count. Nothing was hidden: it reds on
any failed result.
