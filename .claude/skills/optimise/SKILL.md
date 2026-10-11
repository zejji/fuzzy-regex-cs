---
name: optimise
description: Use when a Phase 7 slice is about to change engine code for speed or allocation - the order to work in, the techniques that apply to this engine and the ones that do not, and what has to be recorded before the change can land. Pairs with the benchmark skill, which runs the measurements this checklist demands.
---

# Optimising this engine

Phase 7 is the only phase allowed to trade upstream's shape for speed, and the only one where a
change can be silently wrong in a way the test suite still calls green. So the order below is not
advice, it is the method: **measure, hypothesise, change one thing, measure again, keep it only if
the number moved.**

The research behind every judgement here is `docs/plan/phase7-research/OPTIMISATION-TECHNIQUES.md`
(technique by technique, against this engine), `BENCHMARKING-METHOD.md` (how a number is taken) and
`PROFILING.md` (how a hypothesis is found). The candidate list is in
`docs/plan/2026-09-18-optimisation-research.md`, `docs/plan/OPTIMISATION-NOTES.md` and
`.claude/driver/perf-phase-plan-2026-10-11.md`; the target is the gap against Python regex in
`.claude/driver/perf-gap-table-2026-10-11.md`. This file is the checklist; those are the reasoning.
The `benchmark` skill owns the commands and the v1.0 gate.

## Owner policy (2026-10-11)

- **The library must stay Native AOT compatible.** An optimisation that works in only one mode (JIT
  or Native AOT) is allowed when it is useful, as long as the other mode still works and is not
  slower. Measure both modes when the benefit depends on the JIT.
- **Unsafe code is allowed when it gives a significant, measured speedup**, and only after checking
  that .NET 10/11 has no safe equivalent (`MemoryMarshal`, `CollectionsMarshal`, `Unsafe` on refs,
  `SearchValues`, the vectorised span methods) that gets the same code. Record that check beside the
  code. `AllowUnsafeBlocks` is off in `src/` today; turning it on is part of the change that needs it.
- **Major structural divergence from upstream is decided case by case with the owner.** The bigger
  the measured gain, the stronger the case. Bring the number, the sync cost, and the alternatives
  that keep upstream's shape.

## Before you change anything

1. **Read the deferral, not just the code.**
   `grep -rn -i 'ponytail:\|Phase 7' src/FuzzyRegex --include=*.cs`, and read the comment at the
   line. `docs/plan/OPTIMISATION-NOTES.md` is the index; the source is the record, and it usually
   names the ceiling and the lift already.
2. **Know the noise floor.** `bench/baselines/<machine-id>/noise-floor.md` records this machine's,
   how it was taken and what it covers, and `tools/compare-benchmarks.ps1` defaults to it. If this
   machine has no floor, measure it before anything else (two runs of an unchanged tree, A recorded
   with `-UpdateBaseline -BaselinePath`, B compared against it). **A delta smaller than the floor is
   not a delta**, and a floor raised to make a red run green has stopped measuring the machine.
3. **Measure before.** Release, `--job medium` or longer, into
   `artifacts/bench/<date>-<slice>-before`. Nothing else running: hold the test lock
   (`.claude/driver/test-lock.sh`) for every build, test and timing run. A benchmark taken beside
   other work measures the scheduler.
4. **Write the hypothesis down first**: which workload, which direction, roughly how much, and *why*
   - which mechanism does the work. "It should be faster" is not a hypothesis. A profile is how a
   hypothesis earns its place (`PROFILING.md`); a guess is how a slice spends a day proving nothing.
   Estimate the worst case on paper too, with an adversarial input, before building.

## Changing

5. **One technique per slice**, at the smallest site that tests the hypothesis. Two changes in one
   measurement are two unattributed numbers.
6. **Correctness first.** Anything in the prefilter family can change an answer: write the test that
   pins the answer *before* the change. The pinned answers in `BacktrackingVerbTests`,
   `PartialMatchingTests` and `ReverseMatchingTests` are **permanent** - a Phase 7 slice that turns
   one red has ported an upstream bug, not found one (ROADMAP, owner rule 2026-09-12).
7. **If the change gives up upstream's shape, mark it where it happens.** A `sync-divergence:`
   comment at the line saying what upstream does, what we do instead, why, and what a sync slice
   should do when upstream next touches that code - plus one row in `docs/plan/SYNC-DIVERGENCE.md`.
   `tools/check-sync-divergence.ps1` runs from the ratchet and fails on either half being missing,
   so this is not a thing you can mean to do afterwards.

## After

8. **Measure after**: same machine, same job, same filter, ideally the same session, compared with
   the same script both times. **Per-workload ratios, never an average** - a mean lets one badly
   regressed case hide behind twenty unchanged ones.
9. **Report the allocation delta beside the timing one.** A speed win that allocates more is a trade
   to declare, not to hide.
10. **Differential against main.** Run the same generated rows (tens of thousands at least, biased to
    the shapes the change touches) through `tools/probes/port-probe.cs` on main and on the branch -
    never with `--no-build` - and require zero differences. The oracle cannot see a regression to an
    upstream bug that main had fixed: S60b item 3 passed the oracle and lost genuine partial matches
    (2026-10-11).
11. **Ratchet GREEN**: `pwsh -File tools/check-ratchet.ps1`.
12. **Oracle GREEN at three seeds**, `ExpectedDivergences` strict: `pwsh -File tools/run-oracle.ps1`.
    Never a single `-Seed`.
13. **AOT still green**: `tools/run-aot-tests.ps1` and `tools/run-aot-smoke.ps1`, with the binary
    size recorded against the 6,972,928-byte baseline. If the benefit is JIT-dependent - inlining,
    layout, devirtualisation, tiered PGO - **also record the benchmark under the `nativeaot`
    runtime**. A win that exists only under the JIT is half a win for a library that ships AOT.
14. **Ratchet the number**: update the committed baseline under `bench/baselines/<machine-id>/`, so
    the next slice regresses against the new floor rather than the old one.
15. **Record**: before-and-after in the commit message (house style, 58977bb); delete the
    `ponytail:`/`Phase 7` comment *and* its OPTIMISATION-NOTES row in the same commit; note a
    structural divergence in `docs/PORTMAP.md` as well as the sync ledger; drop the matching
    `.editorconfig` port relaxation if the code no longer needs it.
16. **If the number did not move, revert.** "An optimisation with no measured win is just a bug you
    have not found yet" (`benchmark` skill). Record the negative result in OPTIMISATION-NOTES so the
    next slice does not re-attempt it - S19's reverted fast path is the precedent, and it is the
    reason that row exists.

## The techniques, judged against this engine

Full reasoning per row in `OPTIMISATION-TECHNIQUES.md` section 1. AOT-safe throughout.

| Technique | Use when | Avoid when | Measure with |
|---|---|---|---|
| `stackalloc` | small fixed scratch, once per call | inside a loop; input-dependent size | MemoryDiagnoser, time |
| `Span`/`Memory` | slicing without copying; one `Span` fetched per hot loop, not per character | across `yield`/`await`; as a class field | MemoryDiagnoser, time |
| `CollectionsMarshal.AsSpan` | hot read loop over a frozen `List<T>` | the list may resize while the span lives | time; `--disasm` |
| `string.Create` | final length known | length unknown; state would box | MemoryDiagnoser |
| `ArrayPool<T>` | growable scratch reused across calls | lifetime unclear - an abandoned rental leaks | MemoryDiagnoser |
| avoid boxing | value types crossing `object` in hot paths | cold paths | MemoryDiagnoser |
| `AggressiveInlining` | tiny, provably hot helper | broadly; large bodies | time, JIT **and** AOT |
| SIMD | scanning for candidate positions | short spans; before trying `SearchValues` | time, with a scalar control |
| struct layout | bulk-instantiated or scanned structs | a handful of instances | time; struct size probe |
| `SkipLocalsInit` | hot method, buffer always written first | any read-before-write path; module scope | time (small effect) |
| `ref struct`/`scoped` | stack-only cursor or enumerator | must be a field, boxed, or cross `yield` | MemoryDiagnoser |
| `SearchValues<T>` | fixed set built once, searched often | one-off search | time, on scan workloads |
| precomputed ASCII answers | a per-character test called on mostly-ASCII text | LOCALE nodes (answer depends on the locale) | time |
| `unsafe`/`Unsafe.Add` | significant measured gain and no safe .NET 10/11 equivalent | before the proof | `--disasm` + time |
| `ValueStringBuilder` | short results, hot formatting | generic contexts | MemoryDiagnoser |
| switch dispatch | already what we and the BCL do | replacing it without a measurement | time (both shapes) |
| `RegexOptions.Compiled` | **never here** | always - it is a no-op under AOT, which this library ships | n/a |

## Two standing cautions

**Divergence from upstream has a recurring cost.** Every structural change makes the next
`sync-upstream` more expensive, forever. Prefer the wins that keep the shape - `SearchValues`
prefilters, allocation removal, work skipped that the answer never needs - and take a structural
change only with a number large enough to justify a permanent sync tax, agreed with the owner first.
That is what the sync ledger is for: if you cannot fill in its "measured gain" column, you do not
have the number yet.

**Do not simplify away the constraints.** Not timeouts and cancellation - `SafeCheckCancel` is
polled once per 256 loop turns and is provably load-bearing, since dropping it leaves all 17
cancellation cases running after two minutes - not `ExpectedDivergences` strictness, not the
permanent pins. Those are the first things an optimiser is tempted to shave and the ones the
owner's rules protect.
