---
slice: S54
phase: 6
title: Benchmark baselines committed, and the edge cases an optimiser is tempted to special-case pinned
delivers: []
---

# S54 - What Phase 7 regresses against

Optimisation is where silent behaviour change is likeliest, so Phase 6 pins two things before Phase
7 starts: measured benchmark baselines, and the edge cases an optimiser is tempted to special-case
(ROADMAP; amendment 12). Nothing exists under `bench/baselines/` yet, so this is work.

## Scope

- **Two runtimes, one build (owner decision 2026-09-14).** The library stays `net10.0` (LTS) as its
  only target through 1.0: .NET 11's RC1 notes are JIT work (bounds-check and redundant-branch
  elimination, devirtualisation, SIMD cost model) that a `net10.0` assembly gets for free on a .NET 11
  host, and its libraries add nothing a regex engine calls. So the baselines are recorded with the
  SAME `net10.0` build under the .NET 10 runtime and, once .NET 11 is GA (expected November 2026) or
  on its go-live RC if the slice runs earlier, under the .NET 11 runtime as well - BenchmarkDotNet
  `--runtimes net10.0 net11.0` against an installed 11 runtime, no TFM change, no `global.json`
  change. Phase 7 then knows which speed-ups the newer JIT already delivers before hand-optimising
  for them. If no .NET 11 runtime is installed, record .NET 10 only and say so.

- **The suite**, per `.claude/skills/benchmark/SKILL.md` and spec section 11: literal-heavy,
  class-heavy, backtracking-heavy, fuzzy short and long subjects, `(?e)` and `(?b)`, case-folded,
  reverse, partial, scan (`Matches`, and `EnumerateMatches` from S53b, both) over a long text, `Replace` with a template, compile time for
  a large pattern, and the built-in `Regex` (interpreted and `RegexOptions.Compiled`) on the exact
  subset it can express, as the reference point. BenchmarkDotNet in `bench/FuzzyRegex.Benchmarks`,
  Release, with memory diagnoser.
- **Baselines committed** under `bench/baselines/<machine-id>/` as the skill defines, with the
  machine description, and a `tools/compare-benchmarks.ps1` that reports the ratio per benchmark
  against the baseline and fails on a regression beyond a stated threshold. Phase 7's slices run it.
- **Edge-case pins** in `Gaps/Engine/OptimiserTrapsTests.cs`: zero-width and empty matches at
  every position including the end; anchors under every flag combination; `MatchTimeout` firing
  inside a long scan; large inputs (1 MB subject) for `Match`, `Matches`, `EnumerateMatches` -
  walked to the end AND stopped after two, which is the pair that shows what the per-step state
  costs and what laziness buys (S53b, `OPTIMISATION-NOTES.md`) - `Replace`, `Split`;
  pathological backtracking (`(a+)+b` on a long `a` run) completing or timing out as documented;
  and the Phase 4 rule's tests named as PERMANENT in the closing notes (`BacktrackingVerbTests`,
  `PartialMatchingTests`, `ReverseMatchingTests`).
- **Run the baseline on the quiet machine**: the orchestrator runs the suite detached overnight
  after the slice lands if the session's own run was contended; the numbers the slice commits are
  labelled with how they were taken.

## Verification

- Baselines committed and the compare script GREEN against itself; every edge pin green; the
  ratio table for `Regex` versus this port in the closing notes.

## Done when

- [ ] Suite covers every area above; baselines committed with machine description.
- [ ] Compare script committed and exercised; edge pins landed.
- [ ] Ratchet GREEN, blind review (hunt: a benchmark whose result is dead-code-eliminated; a
      baseline taken with the driver still running tests), commit.
