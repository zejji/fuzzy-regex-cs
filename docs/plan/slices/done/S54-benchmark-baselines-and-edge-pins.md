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

- [x] Suite covers every area above; baselines committed with machine description.
- [x] Compare script committed and exercised; edge pins landed.
- [x] Ratchet GREEN, blind review (hunt: a benchmark whose result is dead-code-eliminated; a
      baseline taken with the driver still running tests), commit.

---

# Closing notes (2026-09-16, two sittings)

Per-sitting detail, including what was recovered from sitting 1's rescue stash, is in
`docs/plan/slices/notes/S54-sittings.md`. Ratchet GREEN 6262 / 6262 / 0, baseline 6118 -> 6154.

## What landed

- **`bench/FuzzyRegex.Benchmarks`, 30 benchmarks over one shared corpus.** `WorkloadBenchmarks`
  (18) covers every area the scope names: literal, class-heavy, case-folded, reverse, partial, the
  eager and lazy scans both walked to the end and stopped after two, `Replace` with a template,
  `Split`, fuzzy short and long, a wider error budget, `(?e)`, `(?b)`, and compile time for a
  300-way alternation. `ReferenceBenchmarks` (12) is this port beside interpreted and
  `RegexOptions.Compiled` `Regex` on the four workloads the built-in engine can express.
  Backtracking-heavy work is `ReferenceBenchmarks.BacktrackingPort`, which keeps S51's exact
  pattern and subject; S51's `MatchingBenchmarks` was deleted into this suite.
- **`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/net10.0.json`**, with the full
  machine description, the median AND minimum per benchmark, bytes per operation, and the rows
  BenchmarkDotNet or the min/median floor flagged as contended.
- **`tools/compare-benchmarks.ps1`** - runs the suite, prints the per-benchmark time and allocation
  ratio, RED above 1.25x on either or on a baselined benchmark that has left the suite.
- **`tests/FuzzyRegex.Tests/Gaps/Engine/OptimiserTrapsTests.cs`**, 36 cases, PERMANENT.
- **`tools/probes/upstream-optimiser-traps.py`** - every expectation above that upstream can
  express, against `regex` 2026.9.10.

## .NET 10 only, and why that is in scope

`dotnet --list-runtimes` has nothing above 10.0.11, so there is no .NET 11 runtime to pass to
`--runtimes` and the baselines are .NET 10 alone. The scope allows exactly this: "If no .NET 11
runtime is installed, record .NET 10 only and say so." When one is installed, the same `net10.0`
build is measured again under it - no TFM change and no `global.json` change.

## The ratio table: this port beside `System.Text.RegularExpressions`

One run, same corpus, same patterns, medians. Not the v1.0 gate (that is against Python `regex`),
but the number a .NET user compares against.

| Workload | this port | `Regex` | `Regex` compiled | port / `Regex` |
|---|---:|---:|---:|---:|
| Literal search, 1 MB, one hit at the end | 17.36 ms | 0.058 ms | 0.047 ms | **298x** |
| Class-heavy scan, 1 MB, 214,490 hits | 23.79 ms | 15.92 ms | 7.00 ms | 1.49x |
| `Replace` with a two-group template, 1 MB | 47.87 ms | 12.77 ms | 8.18 ms | 3.75x |
| `(a\|a)*b` over 18 `a`, no match | 87.17 ms | 0.0059 ms | 0.0022 ms | **14,726x** |

**The two large numbers are one cause, and it is already on the Phase 7 list.** Where the work is
genuinely per-character this port is within 1.5x of the built-in engine interpreted and 3.4x of it
compiled, which is a respectable place to start optimising from. Where the built-in engine can
decide the answer without running the engine - a literal it can find with a vectorised search, a
required character absent from the subject - it wins by two to four orders of magnitude, and this
port has none of that family yet: `locate_required_string` and the `search_start` family are ported
but unreachable, which `docs/plan/OPTIMISATION-NOTES.md` has recorded since S19. **The table says
the prefilters are worth more than every inner-loop change put together**, and Phase 7 should take
them first. It is not a claim about anyone's inner loop: measured with the `sizing` mode, `(a|a)*b`
doubles per character here (151-157 ms at n=18, and 6.5 s, 9.8 s and 10.6 s at n=24 across three
runs on this machine) and grows about six-fold on the built-in engine across a subject 2.2 times
longer, so polynomially. The exponential row's absolute value swings by more than half between
runs; what reproduces is the doubling, which is the claim.

Two more numbers worth carrying forward, both this port against itself:

- **The lazy walk's per-step state is 113x and quadratic.** A full `EnumerateMatches` walk of `\w+`
  costs 12,643 ms over 1 MB against 111 ms for `Matches`, and 117 ms against 4.51 ms over 100 KB.
  `OPTIMISATION-NOTES.md` had the mechanism and no number since S53b; it now has both.
- **What laziness buys, on the same subject**: two matches of a megabyte cost 0.129 ms lazily
  against 78.0 ms eagerly, because `MatchCollection` is materialised before the caller sees an
  element.

## Review

**Two blind passes, both Opus, both briefed to the `docs/VERIFICATION.md` format.**

The first pass, over the whole checkpoint commit, raised **seven** findings. **All seven reproduced
and all seven were fixed** - an unusually high survival rate for this repository, and the reason is
visible in the findings themselves: they are about what the benchmarks MEASURE, which is a question
that answers itself as soon as somebody runs the thing.

1. `FuzzyBudgetThree`, `EnhanceMatch` and `BestMatch` ran `(?:haystack){e<=3}` against a subject
   containing nothing like it. All three answered no match in identical time, so `ENHANCEMATCH`
   never re-ran and `BESTMATCH` - the workload the `benchmark` skill names as the likeliest place
   to regress - never explored. Fixed with `Corpus.Fuzzy`, a subject ending in a misspelled
   `haystack`, and **pinned** by a new test so it cannot silently happen again: the plain answer is
   span (54,63) with counts (0,2,1) and both ranking modes move it to (56,63) and (0,0,1). The
   numbers moved as soon as it was fixed - the two ranking modes now cost 1.9x the plain one where
   all three had been identical.
2. The reference-benchmark summaries claimed the built-in engine backtracks to exhaustion. It does
   not.
3. The timeout test scanned `\w+` over a megabyte - 214,493 matches, so an engine polling only
   BETWEEN matches would have passed it, which is exactly the Phase 7 fast path it exists to catch.
   Now `zebra`, which never matches, so the megabyte is one uninterrupted scan.
4. A filtered run reported GREEN without saying the missing-benchmark check had been skipped.
5. `allocatedBytes` was recorded in the baseline and never read back, so any allocation regression
   passed. Allocation is now compared to the same threshold.
6. Four medians carried BenchmarkDotNet's multimodality warning.
7. Benchmark counts in three documents were wrong, and the `.slnx` named a runner that never existed.

**A second pass over the delta the first reviewer never saw** - required by the skill, and it
earned its place, raising **seven more, all reproduced and all fixed**:

1. The contention was worse than finding 6 suggested: **13 of 30 rows**, not four, with minima at
   0.53-0.76 of their medians. Handled by measurement rather than prose - see the sittings notes.
2. The sittings notes' "within 0.4%" control had been run against the PREVIOUS baseline; the
   reviewer caught it on file mtimes. Re-run and recorded honestly as 1.00x / 1.17x / 1.09x.
3. The multimodal extraction globbed `BenchmarkRun-*.log`, and BenchmarkDotNet names a single-class
   run's log after the class, so it silently found nothing on every filtered run.
4. **My fix for the first pass's finding 2 was itself wrong.** I had written the first reviewer's
   numbers into a doc comment instead of measuring: it claimed the built-in engine "does not
   backtrack at all" and is "flat ... 0.017-0.054 ms throughout", where the committed baseline says
   0.0059 ms at the same n. The rule that a reviewer's claim about an external system is a
   hypothesis too applies to a reviewer's numbers as much as its verdicts. The `sizing` mode now
   measures the built-in engine on that shape, and the comment says what came out of it.
5. The new allocation check read a lost measurement as a 100% improvement, and crashed outright on
   a report with no `Memory` node.
6. STATE.md was stale in three numbers.
7. The fuzzy subject was not pinned by length the way the other three are.

No third pass: the fixes after the second are covered by its own controls, which were all re-run
against the committed tree.

## The independent verifier

A fresh Opus verifier, briefed with the commit-ready tree and nothing else, re-ran eleven claims.
**Nine CONFIRMED, two not, and both of the two were this slice's numbers being less stable than it
had written them down as** - no claim was overstated in kind, only in precision:

- CONFIRMED: every expectation in the trap tests against the probe (all 21 methods, the full
  16-row anchor grid included); the build and 36/36 tests; the ratchet at 6262 / baseline 6154; the
  lazy walk's order of magnitude and ratio (it measured 120x at 1 MB and 28.9x at 100 KB against
  the recorded 114x and 26x); `(a+)+b` flat while `(a|a)*b` doubles; the built-in engine's
  polynomial growth; all fourteen quoted baseline medians; 30 benchmarks, 13 contended, 3
  multimodal; every control in the sittings table; and that no .NET 11 runtime is installed.
- DIFFERENT: `(a|a)*b` at n=24, measured at 9,832 ms against the 6,500 ms quoted. The figure swings
  from 6.5 s to 10.6 s run to run, so the notes now give the range and rest the claim on the
  doubling rather than on any one value.
- DIFFERENT, and the one that mattered: the fresh partial re-run. It got RED once (1.78x, 1.89x)
  and GREEN twice on an unchanged binary, so the "up to 17%" this slice had recorded from a single
  re-measurement was wrong and the real spread here is 0.57x-1.89x. Corrected, and the conclusion -
  that the committed baseline is not yet a trustworthy gate on a contended machine - is now stated
  where a Phase 7 slice will read it rather than inferred.

The verifier also found a defect while doing it: with a run incomplete, `compare-benchmarks.ps1`
died on `The property 'Median' cannot be found on this object`. It now names the benchmarks that
produced no measurement and exits RED. Its arithmetic check corrected the backtracking ratio in the
table above from 14,760x to 14,726x.

## For the next slice

- **The quiet re-run is outstanding and it is the orchestrator's**, per this slice's own scope. One
  command, and the baseline it overwrites says in its own `contended` array which rows to expect to
  move.
- **`.claude/skills/benchmark/SKILL.md` still documents the old run command** (`dotnet run ...
  --filter '*'` from the repository root), which now fails without executing anything. Editing that
  file was outside this session's write permissions; it needs the `tools/compare-benchmarks.ps1`
  command and the working-directory rule. DECISIONS and OPTIMISATION-NOTES both carry it meanwhile.
- **`Replace` takes upstream's `\1` template syntax; `ReplaceFormat` takes .NET's `$1`.** A pin
  caught a benchmark measuring a five-character literal substitution instead of a group swap.
