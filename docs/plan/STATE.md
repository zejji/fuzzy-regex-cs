# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S29, **PARKED - owner decision needed (2026-09-11).** Its commit `8e80b21` is on
main (the driver rolled it back for not moving the slice file; the owner's session restored it and
killed the driver's automatic retry before it did any work). Ratchet GREEN, 5394 passing, tree
clean. **Do not launch a driver on S29 until the decision below is made.**

**The blocker is an upstream defect, not ours - root cause found.** `regex.compile(
r"(?:..(*SKIP)x|q)x").search("ab cd xx")` is `None` upstream, yet `.search(s, 4)` and `.match(s, 4)`
both give `(4, 8)`: a search from 0 misses a match that a search from 4 finds, which no
definition of `search` permits. Cause: `locate_required_string` (`_regex.c:11082`) caches where
the required literal `x` was found (`req_pos`); after a `(*SKIP)` moves `slice_start` the cache and
the moved slice disagree and the attempt fails before matching. Three probes prove the prefilter
is the mechanism (table in the S29 slice file): no required literal gives `(4, 8)`; an early `x`
gives `(4, 8)`; a fixed-offset literal makes the locator jump *below* the skip point. Our port has
no prefilter (Phase 7) and answers `(4, 8)`, as PCRE and upstream-from-4 do. Same mechanism as
upstream #612, already a Phase 6 sweep item. DECISIONS 2026-09-11, second entry.

**The four `(?r)` wave divergences** (`tools/run-oracle.ps1 -Generator verbs -Count 1200 -Seed
20260913`, rows 502, 504, 519, 863) each skip a match after a `(*SKIP)` before a required atom -
the same shape, reversed. Not re-derived individually.

**Options for the owner** (ROADMAP already specifies the mechanism for the first):
1. Pull the strict intentional-divergence allowlist forward from Phase 6 into S29, list these rows
   against upstream #612, put `verbs` back on the default list, close S29. Draft the upstream
   report for approval.
2. Close S29 as-is with `verbs` off the default list. Cheapest; hides verb regressions to Phase 6.
3. Narrow the `verbs` generator away from required literals after `(*SKIP)`. Not recommended.

**Then:** S30 recursion - `pwsh -File tools/launch-slice.ps1 s30` once S29 is closed.
