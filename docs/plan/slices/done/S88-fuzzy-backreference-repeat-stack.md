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

## Done when

- [x] Reproduced and minimised; the one factor named.
- [x] Cause found in upstream's C.
- [x] Tests written first, each seen red.
- [x] The fix; oracle GREEN at seeds 7, 4242 and 20260923.
- [x] Ledger entry 33, its draft, a DIVERGENCES row and a COMPARISON section, since upstream fails too.
- [x] Slice moved to `done/`.

## Closing notes (2026-09-23)

**The premise was wrong.** Upstream does not answer this pattern "at once" for a sound reason. It
answers None under V1 with IgnoreCase only through entry 28's quirk, which fails a full-folded
reference that only deletes before a real character, and S83 removed that quirk here on purpose.
Every other form raises MemoryError upstream: V0, no IgnoreCase, a literal `x` in place of `\1`,
`(?e)`, `(?b)`, `(?r)`. The minimal case is `(?:(?:x){d<=1})+y` over 'y'. The one factor is a fuzzy
section inside the body of a greedy repeat with no maximum: `{1,3}` finishes, and so does `+?`.

**Cause** (ledger 33). A fuzzy edit bumps `capture_change` (`_regex.c:10487`) and the repeat
guards are off under fuzzy matching (`:9596`), so END_GREEDY_REPEAT (`:12552`) takes an iteration
that only deleted as progress. Its fuzzy exception stops the repeat only at the slice end. The
inner section's counts start at zero on each entry, so its budget never runs out.

**What landed.** In END_GREEDY_REPEAT, past the minimum, a repeat with no maximum outside any fuzzy
section treats an iteration that did not move as no progress. The answers are upstream's where its
own slice-end rule applies: `+` is one written-out body and a loop, so it takes two deletions,
matching `(?:(?:x){d<=1})+` over '' upstream. The stop is also off when the repeat's body holds a
capture group (`RepeatInfo.BodyHasGroups`, set from the body's `HasGroups` at compile time),
because a pass that does not move can set a group a later pass tests; the blind review found that.
Pinned by `Gaps/Engine/FuzzyEmptyIterationTests.cs`. With the stop removed, five of its first six
tests failed and the bounded one passed, as intended. The two capture tests failed before
`BodyHasGroups` and pass with it.

**Why "outside any fuzzy section".** The first form of the rule had no such condition and turned
the oracle RED at all three seeds on four rows, for instance `(?:\d+a0b+?){d<=2}` over '67a0bab',
where upstream gives two deletions and the port gave one. A section's budget already bounds a
repeat inside it. The narrowed rule is GREEN at seeds 7, 4242 and 20260923 (34 of 34 oracle tests
at each). No row of the default wave reaches the new rule; the oracle skips upstream's MemoryError
rows as `resource`.

**Left open.** Two forms still loop to the 1 GB limit, as upstream does to MemoryError, each a
`SHORTCUT:` in `Matcher.cs` and STATE finding 2. A repeat inside a section whose inner section
charges nothing to it, `(?:(?:(?:x){d<=1})+y){e<=5}` over 'y', needs to know which section charged
each error. A body with a capture group, `(?:(?(1)c|z)|()(?:x){d<=1})+d` over 'cd', needs a count
of group changes kept beside `capture_change`.
Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s, where the port gives (0, 1)
with one deletion; not investigated.

**Review.** Two blind passes (Opus, VERIFICATION.md brief). The first ran 6745 of 6745 tests and
the oracle GREEN at three seeds, and raised three findings; all three reproduced and all three
were fixed. The stop fired on iterations that set a capture group (`(?:(?(1)c|z)|())*(?:d){e<=1}`
over 'cd' needed a substitution where upstream needs none), and on iterations that deleted and
set one (`(?:(?(1)c|z)|()(?:x){d<=1})*$` over 'c' ended at (1, 1) where upstream gives (0, 1)), and
the docs described the rule too broadly. The fix is `BodyHasGroups`, two red-first tests, and the
doc corrections. Of the reviewer's 56 cases, every one where upstream answers now agrees. The
second pass covered only that delta: 8 of 8 tests passing, 34 more cases across V0, `(?r)`, `(?b)`,
`(?e)`, lookarounds, atomic, branch-reset and nested repeats agreeing wherever upstream answers,
and `HasGroups` carried up by every builder. It returned "No defects found." The final ratchet is
GREEN at 6747 of 6747 and the oracle GREEN at seeds 7, 4242 and 20260923. No negative control was
run on the oracle; no row of the default wave reaches the rule.

**Probe.** `tools/probes/s88-fuzzy-empty-iteration.py` prints every upstream case the tests and
docs cite, each in a child process under a 20 s limit.
