---
slice: S47
phase: 6
title: Inherited fuzzy consistency and resource bugs - ledger entries 11 and 14
delivers: []
---

# S47 - Change positions that contradict counts, and the recursion that exhausts memory

Two inherited bugs with no proposed fix yet. Both need a decision about the right answer before
code, so research is the first half of the slice.

## Scope

- **Entry 11: a fuzzy match reports change positions that contradict its own change counts.** S38
  pinned two rows where the port reproduces upstream; S40a measured a one-line fix and reverted it
  because those rows pin the contradiction. Decide the invariant from the documentation
  (`fuzzy_changes` "gives a tuple of the positions of the substitutions, insertions and deletions")
  and from the definition that counts and positions describe one edit script: the length of each
  positions list equals the corresponding count. Find the mechanism (S38: `match_fuzzy_changes`
  walking counts rather than the list, and the nested-section interaction), fix so the invariant
  holds, and pin the invariant as a property over the whole `fuzzy` wave rather than as individual
  answers. Re-judge the two S38 rows against it.
- **Entry 14: a self-recursive call round a fuzzy section that can match empty exhausts memory.**
  Reproduce under `timeout` and a memory cap and characterise: unbounded depth, unbounded
  branching, or both (S43 measured that a progress guard bounds depth, not branching). Research how
  PCRE2 bounds this (`match_limit`, `depth_limit`, `PCRE2_ERROR_RECURSIONLIMIT`) and what
  upstream's repeat guards do in the non-fuzzy case. Decide: a correctness fix if the empty-matching
  call is re-entered where the guards should stop it, or a resource bound surfaced as a clear
  exception if the pattern is genuinely unbounded. The port must not exhaust memory either way; pin
  with a test that runs under `MatchTimeout` and asserts the outcome. Then lift the `interactions`
  exclusion for a self-recursive call round a fuzzy section and re-run the wave.
- Ledger entries 11 and 14 gain the decision and the fix; nothing filed.

## Verification

- Invariant property test over the `fuzzy` wave at three seeds; the recursion test bounded in time
  and memory; both exclusions lifted; default wave GREEN at three seeds.

## Done when

- [x] Entry 11's invariant decided from the definition, fixed, pinned as a property. **Decided and
      pinned; fixed on two of its four mechanisms** - see the checkpoint note below.
- [ ] Entry 14 characterised, bounded or fixed, pinned; exclusion lifted.
- [ ] Ratchet GREEN, blind review (hunt: the invariant met by trimming the list rather than
      recording correctly; a bound that turns a finite pattern into an exception), commit.

## Checkpoint, sitting 1 (2026-09-14)

**The invariant is decided and pinned, and half of entry 11 is fixed. Entry 14 is untouched.**

*Decided.* `fuzzy_counts` and `fuzzy_changes` are two views of one edit script, so each list holds
exactly as many positions as its own count. Pinned two ways: as
`Gaps.Engine.FuzzyMatchingTests.The_reported_changes_agree_with_the_counts_on_every_shape_that_used_to_contradict_them`
over the seven minimised rows, and as
`OracleTests.OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts`, a property
of this port's answers alone over every fuzzy row of a whole wave.

*Fixed.* Mechanism A - `start_match` clears the change list beside the counts, so an attempt that a
verb or an atomic group abandoned cannot displace the winning attempt's changes. Mechanism B -
`Match.FuzzyCounts` is tallied from the change list on a PARTIAL match, where the state's counter
provably holds the innermost open section's errors alone. Suite green at 5,948, ratchet GREEN.

*Not fixed, and why.* The entry turned out to be one defect class with four mechanisms rather than
one bug with two doors: the counts are saved and restored as a block and the changes are unwound one
item at a time, and nothing keeps them in step. Mechanisms C (POSIX/BESTMATCH candidates) and D (a
lookaround under `(?e)`) are in the ledger with their reproductions; both engines agree on both, so
only the new wave property can see them - it found D at seed 4242 on its first run. Fixing them
means pairing the change list with the counts at all nineteen
`PushFuzzyCounts`/`PopFuzzyCounts` sites, each needing a "restore or merge" judgement. That is a
slice of its own.

*The open problem for sitting 2, before entry 14.* Fixing A and B makes the default wave diverge on
39 rows (B) plus 19 rows (A) per 18,000, because upstream still leaks. There is no narrow predicate
for the 19: upstream's leaked positions look exactly like a port that got a position wrong. The
design that works is a second recorded question - ask upstream the same row again ANCHORED at the
span it reported, where no earlier attempt exists to leak from, and account for a divergence when
this port's answer equals upstream's own leak-free answer. That is a recorder change, an
`OracleWave` field and one `ExpectedDivergences` entry. **Do that first**, then entry 14.

*Review.* Two blind passes. **Pass 1** over the whole diff raised six findings; **all six reproduced
and all six were fixed**, which is far above this repo's usual one-in-five and is the shape you get
when a slice renames tests and rewrites the comments that named them. The substantive one was the
strictness alarm: `Every_expected_divergence_still_diverges` went red because the `start_match`
clear moved this port's answer on `bestmatch-loses-a-partial`'s own example row 4 from a
substitution at 0 - a leftover of an attempt a `(*SKIP)` abandoned, outside the (5, 1) span - to one
at 5, and the recorded judged string had not been updated. The alarm did exactly what it exists for.
The other five were a comment in `Matcher.cs` asserting the opposite of the line beneath it and
citing two names this slice had removed; the same in `FuzzyTestConstraintTests`, whose "strengthen
these when Phase 6 fixes the inherited bug" note was now spent (so its first two rows now assert
positions, measured against upstream); the new wave property silently dropping every scan row, which
is about a fifth of a default wave (widened to check every match of a `MatchesOutcome`, and the POSIX
limit written down honestly); two ledger reproductions quoted without the flag bits they need; and
an overclaim that upstream's change list "is `[ins@1, sub@3]`", which cannot be read past
`sum(counts)` entries from Python.

**Pass 2** over the delta pass 1 never saw - the scan widening, the changed judged string, the two
new position assertions - raised two, both reproduced and both fixed: the property test was putting
`timeout` and `resource` rows to the engine that `RunWave` deliberately never asks, which cost 28.7
of its 29.0 seconds on a 12,000-row wave and ran a row upstream exhausted its heap on with nothing
here to bound it; and `:12377` was the wrong upstream line for `FUZZY`'s sstack push, which is
`:13137` with the `memset` at `:13143` (`:12377` is inside `CONDITIONAL`'s backtrack arm). Pass 2
also verified the changed judged string and both new positions against the live engine and against
regex 2026.9.10.

*Controls.* None run: this sitting changed no oracle generator, and the property test IS the
instrument - it went red on mechanism D at seed 4242 on its first run, on a row both engines answer
identically, which is a live demonstration that it can fail rather than a mutation of the engine.
