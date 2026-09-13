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

- [ ] Entry 11's invariant decided from the definition, fixed, pinned as a property.
- [ ] Entry 14 characterised, bounded or fixed, pinned; exclusion lifted.
- [ ] Ratchet GREEN, blind review (hunt: the invariant met by trimming the list rather than
      recording correctly; a bound that turns a finite pattern into an exception), commit.
