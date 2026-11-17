# D68: save only what a called body can change

Design note, 2026-11-17. Checklist item 15 (D68). Measurements in the D68 row of
`docs/KNOWN-DEFECTS.md`; harness `tools/probes/d68-call-perf/recursion.cs`.

## The problem, measured

Since the D101 call inliner, a call to a group that does not call itself is replaced by a copy of the
group's body, and costs what the body written out costs (2026-11-16, ratio 0.991-1.011 on all 11 D68
rows). What is left is a call the inliner keeps: a recursion, or a shape it refuses.

Such a call costs time in proportion to every group and repeat in the pattern, whether or not its body
can touch them. Matching each body with K extra groups that never take part (`(?:(x)y)?` repeated),
Release, tiered compilation off, pinned to the performance cores, median of 3 runs (us per match):

| Body | K = 0 | 25 | 100 | 400 |
|---|---|---|---|---|
| `(\((?:[^()]\|(?1))*\))` over 30 nested pairs | 92 | 694 | 2,502 | 9,706 |
| `((?:\((?:[^()]\|(?1))*\))+)` over 30 pairs in a row | 116 | 365 | 1,118 | 4,121 |
| the same with no call | 62 | 68 | 84 | 149 |
| the first under `{e<=1}` | 1,354 | 2,006 | 3,877 | 11,413 |

The call-free row is the ordinary cost of more groups (2.4x at K = 400). The recursive rows grow 36-105x.
On the nested row that is about 0.8 us per group per call. A real pattern does not have 400 idle groups,
but a recursive balanced-brackets group inside a 20-30 group pattern (a parser-style pattern, the main
use of recursion) pays the same cost at a smaller scale: 694 against 92 us at K = 25.

## Why every call copies everything

`AdvanceGroupCall` (`Matcher.cs`, the `SHORTCUT (D68)` comment) pushes a frame on the saved stack:

```
repeats (PushRepeats: every repeat's data, guard lists included, and the optional-pass slots)
capture change
captures (PushCaptures: every group's count and current)
return node
```

`GROUP_RETURN` restores the caller's `current` for every group and lowers each count
(`ReturnToCallerCaptures`, D51), and its backtrack arm undoes that (`PopCalleeCaptures`).
`GROUP_CALL`'s backtrack arm puts the caller's groups back (`PopCallerCaptures`). A partial result
inside open calls restores every group from the outermost open frame (`RestoreGroupsFromOpenCall`, D62),
reading the captures at a fixed offset under the return node. All five walk all groups; the repeat
push and pop walk all repeats.

For a group or repeat the called body cannot write, saving it and putting it back is the identity:
nothing between the call and its return changes it. Every write to a group's `count` or `current` is
made by a node of the group (`START_GROUP`, `END_GROUP`), by `UNKNOWN_CAPTURE` for a group in a
lookaround body that ran out (`Node.RunOutGroups`, a subset of the body's own groups), or by
backtracking, which undoes the same writes. Every write to a repeat's data is made by that repeat's
nodes. So the nodes the call can run decide the set.

## Options

**A. Compile, for each called group, the groups and repeats its body can reach, and save only those.**
A walk from the group's first node over every exit, stopping at a `GROUP_RETURN`, following each
`GROUP_CALL` into its target (the walk `PatternObject` already does for D11's body analysis, the
`CallRefInfoList` lookup). The whole-pattern call (`(?R)`, target null) starts at the pattern's start and
reaches everything. Store the two index lists on `CallRefInfo`. The frame gains one value, the called
group's index, under the return node, so every reader finds its list: return and both backtrack arms
already pop the return node first, and the partial restore reads it at a fixed offset. Cost per call
becomes proportional to what the body reaches: on the rows above, group 1 and two repeats, whatever K is.
- For: removes the whole K-dependence; one compile-time walk per called group; readers change mechanically.
- Against: five readers and two writers change together, and D51 and D62 depend on what is restored.
  A list that misses one writer is a wrong answer, not a slow one.

**B. Copy on first write.** Save nothing at the call; the first write to a group or repeat inside the call
saves its old value. Smallest frame, but every `START_GROUP`, `END_GROUP` and repeat write gains a check of
whether this call has saved it yet, which costs the non-call path, and the partial restore has to merge
frames. More mechanism for little more gain than A on real patterns.

**C. Keep it.** Recursion is rare in the patterns this library sees and the cost is linear, not
exponential. But the owner's rule is the best algorithm per operation, and A is mechanical.

## Recommendation

A. Safety comes from the fact that a group or repeat outside the list is never written while the call is
open, which can be checked: a Debug assertion at the return compares every group's `count` and
`current` outside the list with the values when the call was made (a Debug-only copy), so the whole suite
and the oracle exercise the claim on every call they make.

Build, test-first, in its own worktree:

1. Red tests: the recursive rows above with K = 0 and K = 400 give identical answers, groups and fuzzy
   changes (they do now, and must after); a step or allocation measure that grows with K now and must
   not after (`MatchState` exposes frame bytes, or time-free: the saved stack's high-water mark per match).
2. Witnesses for what is restored: nested calls (a call inside a called body to a group outside it),
   recursion to the whole pattern, a call inside a lookaround that runs out (D11), a partial result
   inside two open calls (D62), a call whose return is backtracked into (D51), a group outside the body
   referenced by a backreference inside it (read, never written: must stay out of the list).
3. The change, the Debug assertion, and the existing D51, D62 and call suites.
4. Gates: ratchet, oracle 3 seeds, a differential against main on the D68 rows and the call rows of the
   wave, allocation check, and timing on the table above (expected flat in K for the recursive rows).
