---
slice: S81
phase: 8
title: Re-sync upstream before the release, and judge every commit made since the 2026.9.10 pin
delivers: []
---

# S81 - Re-sync upstream before the release

The pin is `7dd71c15c4fb5c94206bed1763abd4c2bd2f1b33`, tag `2026.9.10`, set by S44 on 2026-09-13,
and S44 recorded that the tag was upstream's head at the time. It no longer is. Measured from the
GitHub API on 2026-09-21, upstream's `hg` branch carries one commit past the tag:

    85e568c 2026-09-19  Git issue 619: `Match.expand()` segfaults after `detach_string()`
                        when the template references a group

Run the `sync-upstream` skill, with the ROADMAP's standing rules: the pin and the local oracle move
only to a *release*, and a head-only engine or parser fix is ported anyway, test-first, recorded in
PORTMAP as "ahead of release, from commit X". Re-run every wave at three seeds;
`ExpectedDivergences` must fire on each pinned divergence upstream has since fixed.

## What the one known commit is

Two lines of `src/_regex.c`, and a changelog entry:

```c
-    init_join_list(&join_info, FALSE, PyUnicode_Check(self->string));     /* match_expand   */
+    init_join_list(&join_info, FALSE, PyUnicode_Check(self->substring));
-    default_value = PySequence_GetSlice(match->string, 0, 0);             /* capture_str    */
+    default_value = PySequence_GetSlice(match->substring, 0, 0);
```

`detach_string()` clears `self->string` so a match stops pinning a large subject alive; both call
sites then hand `NULL` to a C API that does not check for it, and the process dies. Reading
`substring` instead, which detaching leaves in place, is the fix.

**The expected verdict is NOT APPLICABLE, and the slice must prove it rather than assume it.** The
grep behind that expectation, run 2026-09-21: `grep -rn -i detach --include=*.cs src/ tests/` finds
nothing, and no `Expand` member appears in `src/FuzzyRegex/PublicAPI.Unshipped.txt`. There is no
detach on this side to leave a field null, and a managed field that was null would raise rather
than segfault. What the slice still owes:

1. Name this port's counterpart of `match_expand` - `ReplaceFormat` and the `Substitution` code
   behind it are the candidates - and say in PORTMAP whether the null the C fix guards is reachable
   there at all.
2. Do the same for `capture_str`, whose default value is an empty slice of the subject.
3. If neither is reachable, the PORTMAP row says NOT APPLICABLE with that reason, in the form S44's
   rows already use. If either is, it is a fix with a test that fails without it.

## Why the slice sits in phase 8 and not before

Syncing invalidates verdicts recorded against the pinned release (S43 handover, DECISIONS
2026-09-12), so a sync in the middle of phase 7 would move the oracle baseline under the
optimisation slices, whose whole gate is that no answer moves. Phase 8 wants it the other way
round: the divergence list and the upstream reports go out against whatever upstream's newest
release is at the time, so re-syncing is the first thing that phase should do, before S68's
`<remarks>` notes and before S69's filing and 1.0.

**Run this before S68.** If upstream has cut a release by then, sync to it and treat the list above
as one entry of the changelog delta, not as the whole job.
