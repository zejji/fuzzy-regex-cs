# Gap table: this port against Python `regex` (2026-10-15, pinned)

Replaces `2026-10-13-perf-gap-table.md`, taken before the cold-arm, lazy `Matches()`, D84, C4
(capture-free matching) and C6 option C (zero-width look-through) merges.

- **Ours:** BenchmarkDotNet median, `--job medium`, pinned to the performance cores, main `432ced01`,
  one run of the whole suite (00:47-01:45, 84 benchmarks). Committed as
  `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/net10.0.json`.
- **Python:** pyperf median, regex 2026.9.10, CPython 3.14.7, pyperf 2.10.0, `--affinity 0-15`
  (the same performance cores), same session straight after the .NET run (01:46-02:29). Committed
  as `python-regex.json` in the same folder. All 40 rows gave the same full answer as C# before
  timing (`measure_python.py --check-only`). FuzzyPhrase rows run `V0|I` in Python (ledger 28).
  `pyperf check` flags 10 rows as possibly unstable (7 on 10-13); none of them is near a ratio of 1.
- **Machine:** quiet. `tools/probes/sample-machine-load.ps1` beside both halves: the busiest
  non-benchmark process in any 30 s window took 1.1 s during the .NET run (Docker Desktop) and 4.3 s
  once during the Python run (JetBrains Toolbox, 01:48, one window).
- **Ratio** = ours / Python; below 1 is faster. **Noise floor** 1.10 (pinned A/B, `noise-floor.md`).
- **Gate:** at most 10% of rows slower, none slower by more than 1.25x.

**Verdict: the gate passes.** 1 of 40 paired rows is slower than Python (2%), at 1.05x
(TestedGroupRepeat, 1.22x on 10-13). ConditionalPerTokenNoMemo went from 1.16x to 0.99x and
FuzzyBudgetThree from 1.01x to 0.01x. Against the 10-13 baseline no row is slower beyond the floor
(worst FuzzyNoMatchLong 1.07x, UncachedFuzzyIsMatch 1.06x). Three rows are new:
CaseBlindAlternation (D84), WholeWordList and WholeWordRecords (C6).

| Row | Ours | Python | Ratio | Ours alloc | Gate |
|---|---|---|---|---|---|
| TestedGroupRepeat | 2.36 ms | 2.25 ms | 1.05x | 0 | slower |
| ConditionalPerTokenNoMemo | 103.30 ms | 104.01 ms | 0.99x | 0 | pass |
| MatchesToEnd | 34.47 ms | 48.60 ms | 0.71x | 27478628 | pass |
| RedactDigits | 29.13 ms | 52.34 ms | 0.56x | 10102488 | pass |
| ValidateEmails | 28.35 ms | 51.11 ms | 0.55x | 0 | pass |
| ReverseFailedScan | 258.82 us | 477.48 us | 0.54x | 0 | pass |
| ClassScan | 20.83 ms | 40.51 ms | 0.51x | 0 | pass |
| ReplaceTemplate | 20.09 ms | 40.97 ms | 0.49x | 2097760 | pass |
| ParseLogLines | 39.02 ms | 80.44 ms | 0.49x | 28800000 | pass |
| CaseFoldedScan | 1.81 ms | 3.95 ms | 0.46x | 0 | pass |
| SplitLong | 15.75 ms | 35.77 ms | 0.44x | 1716475 | pass |
| MatchesToEndDense | 1.90 ms | 4.50 ms | 0.42x | 2696592 | pass |
| EnumerateMatchesToEndDense | 1.69 ms | 4.22 ms | 0.40x | 2515000 | pass |
| CaseBlindAlternation | 30.46 ms | 92.76 ms | 0.33x | 0 | pass |
| EnumerateMatchesFirstTwo | 207.1 ns | 670.1 ns | 0.31x | 456 | pass |
| PartialMatch | 99.60 us | 328.96 us | 0.30x | 0 | pass |
| WholeWordRecords | 37.00 ms | 123.82 ms | 0.30x | 0 | pass |
| CaseFoldAccented | 24.42 ms | 125.14 ms | 0.20x | 0 | pass |
| WholeWordList | 4.08 ms | 21.23 ms | 0.19x | 0 | pass |
| LiteralMatch | 50.13 us | 405.24 us | 0.12x | 0 | pass |
| CompileLargePattern | 243.43 us | 4.93 ms | 0.05x | 1016032 | pass |
| BestMatch | 2.70 us | 70.85 us | 0.04x | 512 | pass |
| EnhanceMatch | 2.19 us | 69.38 us | 0.03x | 120 | pass |
| FuzzyShort | 228.7 ns | 7.48 us | 0.03x | 32 | pass |
| FuzzyPhraseThreeAlternation | 136.27 ms | 5.70 s | 0.02x | 0 | pass |
| FuzzyPhraseThreeNamedList | 135.49 ms | 5.69 s | 0.02x | 0 | pass |
| FuzzyPhraseOneEnhanced | 46.96 ms | 2.01 s | 0.02x | 15358920 | pass |
| FuzzyPhraseOneMatch | 43.70 ms | 2.00 s | 0.02x | 12077736 | pass |
| FuzzyPhraseOneIsMatch | 43.23 ms | 2.01 s | 0.02x | 0 | pass |
| FuzzyPhraseThreeSeparatePasses | 121.85 ms | 5.93 s | 0.02x | 0 | pass |
| FuzzyBudgetThree | 982.9 ns | 68.14 us | 0.01x | 48 | pass |
| PathologicalNestedRepeat | 123.19 us | 14.51 ms | 0.01x | 0 | pass |
| PathologicalWordsToEnd | 140.20 us | 70.01 ms | 0.00x | 0 | pass |
| ConditionalPerToken | 162.81 us | 110.92 ms | 0.00x | 0 | pass |
| FuzzyNoMatchLong | 87.92 us | 127.96 ms | 0.00x | 0 | pass |
| FuzzyLong | 85.82 us | 129.03 ms | 0.00x | 32 | pass |
| ConditionalBesideBranch | 2.01 us | 17.52 ms | 0.00x | 0 | pass |
| PathologicalPrefixBranches | 2.66 us | 36.92 ms | 0.00x | 0 | pass |
| PathologicalSameBranches | 1.89 us | 162.71 ms | 0.00x | 0 | pass |
| MatchesFirstTwo | 344.3 ns | 48.87 ms | 0.00x | 2536 | pass |
| EnumerateMatchesSpanToEndDense | 1.40 ms | - | - | 0 | not paired |
| EmptyIterationRecord | 2.78 ms | - | - | 16000 | not paired |

40 paired; 1 slower (2%); worst 1.05x (TestedGroupRepeat)

## What is left

- **TestedGroupRepeat** `(a)(?:\1|b)*c`, failing: the backreference turns the failure memo off for
  the whole pattern, so the port does upstream's work at a slightly higher constant. The memo's
  backreference key ("BR", `docs/plan/2026-10-13-linear-nested-repeats-design.md`) is the
  algorithmic fix, after the test seam. ConditionalPerTokenNoMemo has the same cause and is now at
  parity (0.99x).
- **Next band, 0.49-0.71x:** MatchesToEnd, RedactDigits, ValidateEmails, ReverseFailedScan,
  ClassScan, ReplaceTemplate, ParseLogLines. Per-match object costs (MatchesToEnd allocates 27 MB a
  run) and the inner loop; the C2 remainder and S62b.
