---
slice: S62c
phase: 7
title: SPIKE - selective memoisation of failed positions (Davis et al. 2021, Fujinami and Hasuo 2024), one sitting, a measured answer and a recommendation, no production code merged unless the exit criteria pass
delivers: []
---

# S62c - SPIKE: does selective memoisation make this backtracker linear?

**This slice is a time-boxed spike, and its deliverable is a measured answer, not a feature** (spec
amendment 28, owner decision 2026-09-19: no stone should be left unturned). One sitting. **No
production code is merged unless the exit criteria below pass**; if they do not, the spike commits
its measurements, its recommendation and its negative result, and the engine is left untouched. That
is a successful spike.

It is the general attack on the class S62b attacks cheaply, and the two are deliberately adjacent:
auto-atomicity seals the loops it can prove nothing re-enters, memoisation makes every loop linear
by remembering what has already failed. The research note is
`docs/plan/2026-09-18-optimisation-research.md` §3, "The single biggest bet: selective memoisation".

**The papers**, cited because the design is theirs and the soundness argument is theirs:

- Davis, Servant, Lee, *"Using Selective Memoization to Defeat Regular Expression Denial of
  Service"*, IEEE S&P 2021,
  https://davisjam.github.io/files/publications/DavisServantLee-SelectiveMemo-IEEE-SP21.pdf -
  a Spencer-style backtracker made linear **without changing its answers** by remembering the
  simulation positions that have already failed. *"Full memoization is sound and compatible, but its
  space costs are too high."*; *"selective memoization lowers the space cost of memoization by an
  order of magnitude for the median regex, and that run-length encoding lowers the space cost to
  constant for 90% of regexes."* Table I: O(|Q|² × |w|) time, O(|Q| × |w|) space. Representation
  measured, not assumed: *"the RLE representation (green) achieves constant space costs for most
  regexes"*, and against a hash table *"30-50% of the possible simulation positions are explored,
  and the overheads of the hash table outweigh the savings in unfilled entries."* They also name
  this port's exact shape as the one partial schemes miss: Perl's *"is not sound - e.g., it protects
  (a*)* (otherwise exponential), but not (a|a)* (exponential) nor a*a* (quadratic)."*
- Fujinami and Hasuo, ESOP 2024, https://arxiv.org/abs/2401.12639 - the extension this port needs,
  covering *"look-around atomic grouping"* with *"linear-time backtracking matching algorithms"*
  whose *"efficiency relies on memoization, much like one Davis et al."*
- Berglund, van der Merwe, le Roux, NCMA 2026, https://arxiv.org/abs/2606.26678 - a smaller memoised
  set (*"MFN provides correctness guarantees equivalent to CN while often using fewer (memoized)
  states"*), and the honest scope note: *"counters, backreferences, and lookaheads"* remain open.

**Why it is a spike and not a slice.** This port's simulation position is not (state, offset): it is
(node, offset, **fuzzy error counters**, group and repeat stack). A position reached with fewer
errors can succeed where a costlier one failed, so memoising on (node, offset) alone is **unsound**
under `{e<=k}` and the counters must be in the key. Davis §IX.C rules out backreferences outright -
*"Because the contents of a capture group depend on the path taken through the automaton, REWBR
disrupts our path-independent memoization scheme."* - and §IX.D's side effects are
`(*SKIP)`/`(*PRUNE)`/`(*COMMIT)`/`(*MARK)` here, whose answers `BacktrackingVerbTests` pins
permanently. Both families are therefore **OFF**: the memo table is not consulted or filled when the
pattern contains a backreference or a verb.

## Scope

1. **Time-boxed to one sitting**, and bounded like any other probe in this plan: if the table cannot
   be made to work in the sitting, record exactly where it stopped and what the last measurement was
   - do not leave the question open and do not roll into a second sitting without the owner.
2. **The memo probe where the research located it**: beside the iteration and cancel check at
   `Engine/Matcher.cs:4857`, in front of the main `switch (node.Op)` at `:4864`, inside `BasicMatch`
   (`:4661`). Key = **(node, position, fuzzy counters)**. Consult on entry; on failure, record the
   position as failed; skip immediately on a hit.
3. **RLE or bitmap table, not a hash table**, on Davis's own measurement of the representation. State
   the memory bound the chosen representation gives for the spike's workloads, in bytes, measured -
   not the paper's asymptotic figure quoted as if it were this port's number.
4. **OFF for backreferences and for any of the four verbs**, decided once at compile time and
   asserted by a test, not by inspection.
5. **Extended per Fujinami and Hasuo** for lookaround and atomic groups - which this port has and
   Davis's subject language does not - or, if that does not fit the sitting, memoisation is disabled
   in their presence and the limitation is written into the answer.
6. **The answer, written into this file**, with the numbers beside each claim:
   - linear-time behaviour on `(a|a)*b` at n=24, against the **10.6 s** S54 measured, and the shape
     of the curve from n=18 to n=24 (the point is the exponent, not one data point);
   - `pwsh -File tools/run-oracle.ps1` **identical at three seeds**, `ExpectedDivergences` strict -
     the technique's whole claim is that answers do not change, so anything else falsifies the
     premise for this engine;
   - the **memory bound**, stated and measured, including the worst case among the spike's workloads;
   - the cost on patterns that gain nothing, since every pattern pays the probe.
7. **The recommendation**, in the same file, in one of two forms: **promote to a full slice**, with
   the soundness argument it would owe (the fuzzy counters in the key, captures, the group-call
   guard), the oracle wave, and the estimate - the bar `OPTIMISATION-NOTES.md` already sets for the
   counted-repeat rewrite, and a phase rather than a slice if the numbers say so; **or** record why
   not, with the number that killed it, in `OPTIMISATION-NOTES.md` so it is not re-attempted blind.

## Verification

- `pwsh -File tools/run-oracle.ps1` at its three default seeds, `ExpectedDivergences` strict, run
  against the spike build. Identical, or the exit criteria have failed.
- `(a|a)*b` timed at n=18, 20, 22, 24 with and without the table, beside S54's committed 161 ms and
  10.6 s; `(a+)+b` too, which S54 measured flat, so a regression there is visible.
- `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*' --job medium
  --exporters json --artifacts artifacts/bench/<date>-S62c-<with|without>`, compared with
  `pwsh -File tools/compare-benchmarks.ps1` at S58's floors - the tax on patterns the table never
  helps is half the decision.
- `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/<Class>/*"` for
  `BacktrackingVerbTests` and `OptimiserTrapsTests` on the spike build, to prove the OFF switches
  really are off.
- `pwsh -File tools/check-ratchet.ps1` GREEN **if** anything is proposed for merge; a spike that
  merges nothing still leaves the tree green and the branch clean.
- Nothing is merged without a `sync-divergence:` marker and a `SYNC-DIVERGENCE.md` row, because a
  memo table in `BasicMatch` is a structural divergence from upstream's shape.

## Done when

- [ ] The sitting is closed, whatever it produced, and this file carries the measured answer.
- [ ] Key = (node, position, fuzzy counters) implemented; the counters demonstrably in the key, with
      a fuzzy pattern that would answer differently without them.
- [ ] Table is RLE or bitmap; the memory bound is stated in measured bytes for the spike's workloads.
- [ ] Memoisation proven OFF for backreferences and for all four verbs by test.
- [ ] Lookaround and atomic groups either handled per Fujinami and Hasuo, or excluded with the
      limitation written down.
- [ ] `(a|a)*b` measured at n=18 to n=24 against S54's 161 ms and 10.6 s, with the curve reported,
      and the tax on unaffected workloads measured at S58's floors.
- [ ] Oracle identical at three seeds, `ExpectedDivergences` strict - or the spike ends here and
      says so.
- [ ] A recommendation written: promote to a full slice with its soundness argument and estimate, or
      the reason it is declined with the number that decided it, recorded in `OPTIMISATION-NOTES.md`.
- [ ] No production code merged unless every exit criterion passed; blind review (hunt: a memo key
      missing the fuzzy counters; a table consulted while a backreference or a verb is present; a
      "linear" claim from a single n; a memory figure quoted from the paper instead of measured here;
      an oracle run at one seed; merged code with no `sync-divergence:` marker), commit.
