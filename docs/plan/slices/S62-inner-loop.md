---
slice: S62
phase: 7
title: The per-character inner loop - Node.Values as an array, inlining one predicate at a time, layout and dispatch measured before anything structural
delivers: []
---

# S62 - The inner loop, one measured step at a time

The research's third target, and the cheapest of the three: `Node.Values` is a `List<uint>` read on
every set-membership test, and `src/` has **zero** `AggressiveInlining` and **zero**
`SkipLocalsInit`. These are shape-preserving wins - they cost `sync-upstream` nothing. The
expensive version (flattening the node graph, replacing the dispatch switch) is deliberately not in
scope: it is measured here and decided later, because *"every structural change makes the next
`sync-upstream` more expensive"* and the minimum gain that justifies one is still deferred
(DECISIONS 2026-09-16, ground rule (c)).

This is also the technique family most likely to make things slower while looking like an
optimisation, so the rule is one change per measurement and revert on no win. *"An optimisation with
no measured win is just a bug you have not found yet"* (`benchmark` skill).

## Scope

1. **`Node.Values` from `List<uint>` to `uint[]`** (`Node.cs`), frozen when compilation ends, which
   is when it stops being appended to. Prefer the array over `CollectionsMarshal.AsSpan`: it removes
   the indirection instead of working around it, and it cannot be invalidated by a resize.
   `PatternObject` immutability (S52b's structural test) must still hold - an array field is easier
   to prove immutable than a list, so that test should get simpler, not be relaxed.
2. **The hottest predicates inlined one at a time, each behind its own measurement.** Candidates,
   in the order the research names them: `Matcher.MatchesCharacter`, `Matcher.InSetUnion`,
   `MatchState.NextPos`/`StepBy`. `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on one, a
   before-and-after run, keep it only if the number moved beyond the noise floor, then the next.
   Applied broadly it is documented to *reduce* performance; .NET's own `RegexInterpreter` marks
   exactly one decode helper this way, which is the precedent to match, not to exceed.
3. **`SkipLocalsInit` where it measures**, method level only, never module level, and only at
   `stackalloc` sites whose buffer is provably written before it is read - in an engine that indexes
   buffers by computed positions, a module-wide application turns a latent bug into leaked memory.
   While there, check the eighteen existing `stackalloc` sites for the hazard the docs name: one
   inside a loop, or one whose size depends on input length.
4. **Struct layout of the hot state** measured, not assumed: the sizes of the per-step structs
   (`GroupData`, `RepeatData`, `BestEntry`, the fuzzy counters) probed and `[StructLayout(
   LayoutKind.Auto)]` applied where a probe shows padding removed. `BestEntry` already carries it.
   .NET 7+ handles the common small-wrapper case, so a change with no measured size or time
   difference is reverted.
5. **Dispatch cost measured before any structural change is proposed.** The research marks the
   switch-versus-table claim **UNVERIFIED** and this port already does what the BCL does. Measure
   what `switch (node.Op)` and the two `NextNode` pointer hops actually cost - a profile
   (EventPipe topN exclusive, then dotTrace call tree if topN is too coarse) - and write the number
   into `docs/plan/SYNC-DIVERGENCE.md` as the case for or against flattening the graph. **Propose,
   do not implement**: an AOT-safe delegate chain or a flat node array is a phase-sized change and
   needs the owner's gain threshold first.
6. **Records.** Every landed item deletes its `ponytail:`/`Phase 7` comment and OPTIMISATION-NOTES
   row in the same commit; every reverted item gains a row recording the **negative** result with
   its number, so it is not re-attempted (the S19 precedent, `Matcher.cs:8894`). Any divergence that
   does land carries a `sync-divergence:` comment and a `SYNC-DIVERGENCE.md` row naming the file,
   the reason, the measured gain and how to re-align.

## Verification

- Before and after **per item, not per slice** - two changes in one measurement are two unattributed
  numbers: `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*' --job medium
  --exporters json --artifacts artifacts/bench/<date>-S62-<item>-<before|after>`, each compared with
  `pwsh -File tools/compare-benchmarks.ps1` against the committed baseline at S58's floors. The
  class-heavy and backtracking workloads are where the set tests and predicates show.
- Inlining, layout and devirtualisation are JIT-dependent mechanisms, so each kept item is
  **also** measured under the `nativeaot` runtime moniker and the result recorded per runtime; a win
  that exists only under the JIT is half a win for a library that ships AOT.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict.
- `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/ThreadSafetyTests/*"` for
  the structural immutability test after the `Values` change, and `OptimiserTrapsTests` green.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN, binary size
  against 6,972,928 bytes - inlining grows code, so a size rise is a number to report, not a
  surprise to absorb.
- `pwsh -File tools/check-sync-divergence.ps1` GREEN. No public surface change expected; if one
  appears, `pwsh -File tools/update-public-api.ps1` and justify it.

## Done when

- [ ] `Node.Values` is a `uint[]` frozen at the end of compilation; the immutability test still
      proves it, unrelaxed.
- [ ] Each inlining, `SkipLocalsInit` and layout change kept only on its own measured win beyond the
      noise floor, JIT and AOT numbers both recorded; the rest reverted.
- [ ] Existing `stackalloc` sites checked for in-loop and input-dependent sizes; findings fixed or
      recorded.
- [ ] Dispatch cost measured and written up in `SYNC-DIVERGENCE.md` as a proposal for the owner; no
      structural change made in this slice.
- [ ] Negative results recorded in OPTIMISATION-NOTES with their numbers; landed rows and their
      comments deleted; anything deferred carries a `ponytail:`/`Phase 7` comment and a row.
- [ ] Any divergence that landed has a `sync-divergence:` marker paired with a ledger row.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a `Values` array still
      mutable after compilation; an inlining attribute kept without a number; `SkipLocalsInit` over a
      read-before-write buffer or at module scope; a layout attribute that changed nothing; a
      dispatch change smuggled in as a refactor), commit.
