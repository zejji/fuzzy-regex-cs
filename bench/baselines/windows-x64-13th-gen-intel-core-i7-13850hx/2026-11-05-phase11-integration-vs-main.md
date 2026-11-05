# Phase 11 integration against main, whole suite (2026-11-05)

**Verdict: the five phase 11 changes merged together make the suite about 4% faster overall and
up to 35% faster on the rows they target, with no row slower beyond its noise.** Across the 91
benchmarks the geometric mean of the integration/main time ratio is 0.961 and the median 0.984.
The slowest row, `CountMemoryMegabyte` at 1.034, is inside its own run-to-run spread (its
integration A-A reads 1.154). The BCL control rows, which do not run FuzzyRegex, read
0.989-1.014, so neither side was disturbed. One row allocates more: `ConditionalPerTokenNoMemo`,
0 to 504 B per call (D150, below).

## What was measured, and how

- **Trees:** main at `60c5901b` (1.2.0's engine) and `perf/phase11-integration` at `27a147d9`,
  which merges D139 + D140 (`perf/d140-flags`), D141 (`perf/ascii-fill`), D138
  (`perf/d138-lazy-state`), D143 (`perf/d143-error-permitted`) and D144 (`perf/d144-filter-ascii`).
  D145 (the partial-search prefilter) is not in it: the suite has no fuzzy partial row, so it
  would change nothing here. Each tree in its own detached worktree, built in Release before
  timing.
- **Machine:** `windows-x64-13th-gen-intel-core-i7-13850hx`, Windows 11 23H2, mains power, CPU
  capped at 4,000 MHz by the power plan (`PROCFREQMAX`), as for the 1.2 note. Nothing else ran:
  the one peer session had stopped for the night, and no builds, tests or commits ran.
- **Runs:** BenchmarkDotNet, whole suite, `--job medium`, `--affinity 65528` (performance cores).
  Four passes in A-B-B-A order: main 02:31-03:35, integration 03:35-04:38, integration again
  04:39-05:42, main again 05:42-06:46.
- **Rule:** a row's ratio is the mean of the two integration medians over the mean of the two
  main medians. Its noise is the larger of the two same-code ratios. A row counts as moved only
  when its change is more than twice its noise and more than 3%.
- **A-A controls:** main second pass over first, geometric mean 0.998 (range 0.948-1.067);
  integration second over first, 1.004 (0.960-1.154).

## Time

| Ratio integration to main | Rows |
|---|---|
| 0.80 or less | 1 (`UncachedFuzzyIsMatch` 0.650) |
| 0.80 to 0.90 | 8 |
| 0.90 to 0.98 | 31 |
| 0.98 to 1.02 | 50 |
| above 1.02 | 1 (`CountMemoryMegabyte` 1.034, inside its noise) |

The rows that moved, and the change behind each:

| Row | main | integration | Ratio | Change |
|---|---|---|---|---|
| `UncachedFuzzyIsMatch` | 6,101 ns | 3,966 ns | 0.650 | D139 (no exception for the first set) |
| `UncachedMatch` | 14,290 ns | 11,596 ns | 0.811 | D141 (ASCII answers) |
| `StaticIsMatchAlwaysMissing` | 15,129 ns | 12,288 ns | 0.812 | D141 |
| `UncachedIsMatch` | 14,073 ns | 11,554 ns | 0.821 | D141 |
| `FuzzyLong` | 109,735 ns | 90,245 ns | 0.822 | D144 (no second ASCII pass) |
| `StaticMatchWithNamedLists` | 14,436 ns | 11,975 ns | 0.830 | D141 |
| `MatchesFirstTwo` | 557 ns | 470 ns | 0.843 | D138 (lazy state fields) |
| `FuzzyBudgetThree` | 1,308 ns | 1,142 ns | 0.873 | D143 (plain error budget) |
| `InstanceFuzzyIsMatch` | 301 ns | 267 ns | 0.887 | D143 |
| `FuzzyShort` | 319 ns | 292 ns | 0.914 | D143 |
| `EnhanceMatch` | 2,958 ns | 2,751 ns | 0.930 | D143 |
| `BestMatch` | 3,529 ns | 3,346 ns | 0.948 | D143 |

`FuzzyNoMatchLong` reads 0.876 but its main A-A spread is 1.067, so it is counted as in noise.
`StringShort`/`StringKilobyte`/`StringMegabyte` (0.92-0.93) and `BacktrackingPort` (0.935) move by
about 7 ns a call, a fixed per-call saving that fits D138's smaller state.

## Allocation

- `MatchesFirstTwo` 3,032 to 2,432 B per call (D138); below 1.1.0's 2,792 B.
- Uncached and static-cache compiles 760-840 B less (`UncachedMatch` 36,256 to 35,496 B), and the
  fuzzy compile 944 B less (`UncachedFuzzyIsMatch` 16,032 to 15,088 B): D139 and D141.
- **`ConditionalPerTokenNoMemo` 0 to 504 B per call: an allocation regression (D150).** Not yet
  traced. Every other row allocates the same, within a few bytes.

## What this means

The compile-time items (D139, D140, D141) and the per-call items (D138, D143, D144) each show in
the suite where the profile said they would, and nothing outside them moved. D150 has to be traced
and fixed before the integration branch merges. D145's gain (a 1 MB fuzzy partial search from
about 160 ms to 0.13 ms) needs a suite row of its own.
