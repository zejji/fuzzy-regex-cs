---
slice: S88
phase: 7
title: A repeated fuzzy backreference with deletions no longer exhausts the backtracking stack
delivers: []
---

# S88 - A 1 GB stack for a two-character subject

Found by S84's blind review (`docs/plan/slices/notes/S84-sittings.md`), listed as open in
`docs/plan/STATE.md` by S87. The port fails where upstream answers at once, so it is this port's
bug alone, and no known bug ships.

Verified 2026-09-23 against `regex` 2026.9.10 (orchestrator):

```
regex.search(r'(?i)(x)(?:(?:\1){d<=2})+$', 'xy', regex.V1)   -> None, instantly
```

The port throws "backtracking stack exceeded its 1GB limit" on the same call.

## Scope

1. Reproduce and minimise (IgnoreCase on/off, V0/V1, the `+` vs `{1,n}`, `d<=2` vs `d<=1` vs
   `e<=2`, a literal `x` in place of `\1`). Say which one factor makes it explode.
2. Find the cause by comparing with upstream's C for the same path (a group-reference fuzzy item
   inside a repeat body that can match empty through deletions: upstream's repeat guards / body
   progress checks). S84 and S85 changed the `REF_GROUP_FLD` arms and their leftovers loops - check
   whether this is their deletion guard not covering the non-full-folded `REF_GROUP_IGN` arm, or a
   repeat guard the port does not apply to a zero-width fuzzy body.
3. Tests first, each seen red (a stack exception or a timeout is red): the call above gives None;
   the one-factor variants pinned to upstream's answers.
4. The fix, the smallest that matches upstream's mechanism. Oracle at seeds 7, 4242, 20260923.
5. No ledger entry if the port simply now agrees with upstream; say so in the commit.

**Stop by 06:40 on 2026-09-23** with a green checkpoint if it cannot land - the orchestrator merges
and benchmarks after that. Commit every 30 minutes.
