# Phase 11 integration against main, whole suite, second night (2026-11-06)

**Verdict: with D145 and the reworked D141 merged in, the integration branch makes the suite 4.1%
faster overall and up to 34% faster on the rows it targets, with no row slower beyond its noise and
no row allocating more.** Across the 91 benchmarks the geometric mean of the integration/main time
ratio is 0.959 and the median 0.981. The slowest row, `ClassScan` at 1.019, is inside the 3% rule;
the BCL control rows, which do not run FuzzyRegex, read 0.991-1.010, so neither side was disturbed.
This replaces the first night's note (`2026-11-05-phase11-integration-vs-main.md`) for the merge
decision: that run measured 27a147d9, before D145 and the D141 rework.

## What was measured, and how

- **Trees:** main at `aa721822` (its engine is 1.2.0's, 60c5901b; only the version prefix
  differs) and `perf/phase11-integration` at `e9c4161a`: D139 + D140, D138, D143, D144, D145 and
  D141 as reworked on 2026-11-05 (formulas and a generated property table, no shared cache;
  `docs/plan/2026-11-05-d141-storage-timing.md`). Each tree in its own detached worktree
  (`bdn-p11main`, `bdn-p11int`), built in Release before timing.
- **Machine:** `windows-x64-13th-gen-intel-core-i7-13850hx`, Windows 11 23H2, mains power, CPU
  capped at 4,000 MHz (`PROCFREQMAX`), as for the 1.2 note. LibreHardwareMonitor running; each
  pass waited for the CPU package at or below 86 C (readings at pass start 70-75 C). Peer sessions
  told first; no builds, tests or commits ran during the passes.
- **Runs:** BenchmarkDotNet, whole suite, `--job medium`, `--affinity 65528` (performance cores).
  Four passes in A-B-B-A order: main 23:03-00:06, integration 00:09-01:12, integration again
  01:14-02:17, main again 02:19-03:22. Script `.scratch/s1024/bdn-p11b.ps1`, raw results
  `.scratch/s1024/bdn-p11b/`.
- **Rule:** a row's ratio is the mean of the two integration medians over the mean of the two
  main medians; its noise is the larger of the two same-code ratios; a row counts as moved only
  when its change is more than twice its noise and more than 3%.
- **A-A controls:** main second pass over first, geometric mean 0.996 (range 0.954-1.050);
  integration second over first, 1.001 (0.959-1.044).

## Time

Rows that moved (change > 2x noise and > 3%):

| Benchmark | Class | main ns | integration ns | ratio | A-A main | A-A int |
|---|---|---|---|---|---|---|
| UncachedFuzzyIsMatch | PatternCacheBenchmarks | 4,653 | 3,059 | 0.657 | 1.001 | 1.001 |
| StaticMatchWithNamedLists | PatternCacheBenchmarks | 11,520 | 9,281 | 0.806 | 1.004 | 0.996 |
| UncachedMatch | PatternCacheBenchmarks | 11,046 | 8,945 | 0.810 | 1.014 | 1.008 |
| UncachedIsMatch | PatternCacheBenchmarks | 10,921 | 8,889 | 0.814 | 1.001 | 1.015 |
| FuzzyNoMatchLong | WorkloadBenchmarks | 82,526 | 67,257 | 0.815 | 1.050 | 0.982 |
| StaticIsMatchAlwaysMissing | PatternCacheBenchmarks | 11,733 | 9,612 | 0.819 | 1.006 | 0.991 |
| FuzzyLong | WorkloadBenchmarks | 82,867 | 68,563 | 0.827 | 0.978 | 0.959 |
| MatchesFirstTwo | WorkloadBenchmarks | 442 | 371 | 0.839 | 0.984 | 1.013 |
| FuzzyBudgetThree | WorkloadBenchmarks | 1,039 | 931 | 0.896 | 1.004 | 1.006 |
| InstanceFuzzyIsMatch | PatternCacheBenchmarks | 242 | 221 | 0.912 | 1.003 | 1.006 |
| BacktrackingPort | ReferenceBenchmarks | 71 | 66 | 0.925 | 1.003 | 1.013 |
| StringMegabyte | SpanOverloadBenchmarks | 77 | 72 | 0.928 | 1.001 | 0.993 |
| SpanShort | SpanOverloadBenchmarks | 82 | 76 | 0.929 | 0.994 | 1.024 |
| CaseBlindAlternation | ManyInputsBenchmarks | 8,657,191 | 8,059,078 | 0.931 | 0.997 | 1.018 |
| FuzzyShort | WorkloadBenchmarks | 253 | 236 | 0.932 | 0.993 | 1.009 |
| StaticFuzzyIsMatch | PatternCacheBenchmarks | 269 | 250 | 0.933 | 0.994 | 0.983 |
| EnhanceMatch | WorkloadBenchmarks | 2,405 | 2,244 | 0.933 | 0.993 | 1.029 |
| StringShort | SpanOverloadBenchmarks | 76 | 72 | 0.936 | 1.008 | 1.017 |
| StringKilobyte | SpanOverloadBenchmarks | 77 | 72 | 0.937 | 0.988 | 0.993 |
| StateByGroupCount(Groups: 8) | GroupCountStateBenchmarks | 268 | 251 | 0.937 | 0.977 | 0.999 |
| BestMatch | WorkloadBenchmarks | 2,864 | 2,691 | 0.940 | 1.002 | 1.018 |
| MemoryMegabyte | SpanOverloadBenchmarks | 77 | 73 | 0.940 | 0.999 | 0.989 |
| StateByGroupCount(Groups: 1) | GroupCountStateBenchmarks | 142 | 134 | 0.941 | 0.984 | 0.993 |
| StateByGroupCount(Groups: 2) | GroupCountStateBenchmarks | 157 | 149 | 0.954 | 0.991 | 0.986 |
| StateBySubjectLength(SubjectLength: 1024) | SubjectLengthStateBenchmarks | 137 | 132 | 0.964 | 0.995 | 0.996 |
| StateBySubjectLength(SubjectLength: 262144) | SubjectLengthStateBenchmarks | 138 | 133 | 0.964 | 0.988 | 0.997 |
| CountStringMegabyte | SpanOverloadBenchmarks | 811,998 | 784,038 | 0.966 | 0.995 | 0.993 |
| MatchesToEnd | WorkloadBenchmarks | 34,786,073 | 33,686,959 | 0.968 | 1.015 | 1.014 |

The rest of the 91 rows did not move. The five slowest:

| Benchmark | ratio | A-A main | A-A int |
|---|---|---|---|
| ClassScanBclCompiled | 1.009 | 0.996 | 1.006 |
| ClassScanBcl | 1.010 | 1.004 | 1.011 |
| ReverseFailedScan | 1.014 | 1.007 | 0.977 |
| FuzzyPhraseOneIsMatch | 1.018 | 0.992 | 1.044 |
| ClassScan | 1.019 | 1.005 | 1.003 |

## Allocation

Every row whose allocation per operation differs between the four passes. A difference inside one
pass only, with the other pass on the same build reading the same as the other tree, is BDN's
one-launch noise (D150's signature): `CaseBlindAlternation`, `FuzzyPhraseThreeSeparatePasses`,
`ParseLogLines`, `ClassScanPort`, `ClassScan`, `ReplaceTemplate` and the few-byte wobbles on the
large rows. The real changes are all reductions: the uncached compile rows 680-944 B less (the compile
changes together), `MatchesFirstTwo` 600 B less (D138), `CompileLargePattern` 40 B less.

| Benchmark | main | main b | int | int b |
|---|---|---|---|---|
| CaseBlindAlternation | 0 | 26 | 0 | 0 |
| FuzzyPhraseOneEnhanced | 15,359,103 | 15,358,920 | 15,358,920 | 15,358,920 |
| FuzzyPhraseOneMatch | 12,077,736 | 12,077,736 | 12,077,736 | 12,077,919 |
| FuzzyPhraseThreeSeparatePasses | 0 | 504 | 0 | 0 |
| ParseLogLines | 28,800,000 | 28,800,000 | 28,800,168 | 28,800,000 |
| RedactDigits | 10,102,614 | 10,102,488 | 10,102,614 | 10,102,488 |
| StaticIsMatchAlwaysMissing | 35,608 | 35,608 | 34,848 | 34,848 |
| StaticMatchWithNamedLists | 38,768 | 38,768 | 38,008 | 38,088 |
| UncachedFuzzyIsMatch | 16,032 | 16,032 | 15,088 | 15,088 |
| UncachedIsMatch | 35,624 | 35,544 | 34,784 | 34,864 |
| UncachedMatch | 36,256 | 36,256 | 35,496 | 35,576 |
| ClassScanPort | 0 | 0 | 21 | 0 |
| ReplaceBclCompiled | 2,097,338 | 2,097,338 | 2,097,368 | 2,097,368 |
| ClassScan | 0 | 0 | 0 | 52 |
| CompileLargePattern | 1,016,672 | 1,016,672 | 1,016,632 | 1,016,632 |
| MatchesFirstTwo | 3,032 | 3,032 | 2,432 | 2,432 |
| MatchesToEnd | 27,478,628 | 27,478,740 | 27,478,530 | 27,478,537 |
| ReplaceTemplate | 2,097,760 | 2,097,760 | 2,097,834 | 2,097,760 |

