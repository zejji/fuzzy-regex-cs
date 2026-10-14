# Gap table: this port against Python `regex` (2026-10-13, pinned)

Replaces `.claude/driver/perf-gap-table-2026-10-11.md`, which was taken unpinned and before the
D82, memo Option B, S62 and fold-screen merges.

- **Ours:** BenchmarkDotNet median, `--job medium`, pinned to the performance cores, main `a3d79986`,
  run A of the noise-floor pair (20:16-21:11). Committed as
  `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/net10.0.json`.
- **Python:** pyperf median, regex 2026.9.10, CPython 3.14.7, pyperf 2.10.0, `--affinity 0-15`
  (the same performance cores), same session straight after the .NET runs (22:07-22:50). Committed
  as `python-regex.json` in the same folder. All 37 rows gave the same full answer as C# before
  timing (`measure_python.py --check-only`). FuzzyPhrase rows run `V0|I` in Python (ledger 28).
- **Ratio** = ours / Python; below 1 is faster. **Noise floor** 1.10 (pinned A/B, `noise-floor.md`).
- **Gate:** at most 10% of rows slower, none slower by more than 1.25x.

**Verdict: the gate passes.** 3 of 37 paired rows are slower than Python (8%), and the worst is
1.22x. Allocation ("Ours alloc", bytes per operation) is shown beside the time; Python has no
comparable figure.

| Row | Ours | Python | Ratio | Ours alloc | Gate |
|---|---|---|---|---|---|
| TestedGroupRepeat | 2.73 ms | 2.24 ms | 1.22x | 0 | slower |
| ConditionalPerTokenNoMemo | 120.51 ms | 103.72 ms | 1.16x | 0 | slower |
| FuzzyBudgetThree | 68.75 us | 68.03 us | 1.01x | 48 | slower |
| EnhanceMatch | 68.86 us | 68.90 us | 1.00x | 120 | pass |
| BestMatch | 69.31 us | 70.32 us | 0.99x | 512 | pass |
| ReplaceTemplate | 38.57 ms | 40.63 ms | 0.95x | 19925074 | pass |
| MatchesToEndDense | 4.14 ms | 4.46 ms | 0.93x | 3207236 | pass |
| MatchesToEnd | 44.95 ms | 48.57 ms | 0.93x | 31650770 | pass |
| MatchesFirstTwo | 43.96 ms | 48.91 ms | 0.90x | 31650756 | pass |
| SplitLong | 27.76 ms | 35.60 ms | 0.78x | 6864163 | pass |
| ValidateEmails | 33.22 ms | 51.25 ms | 0.65x | 0 | pass |
| ClassScan | 25.96 ms | 40.10 ms | 0.65x | 0 | pass |
| RedactDigits | 33.54 ms | 52.91 ms | 0.63x | 32838152 | pass |
| CaseFoldedScan | 2.35 ms | 3.73 ms | 0.63x | 0 | pass |
| ParseLogLines | 50.45 ms | 81.54 ms | 0.62x | 45600000 | pass |
| ReverseFailedScan | 259.35 us | 456.56 us | 0.57x | 0 | pass |
| EnumerateMatchesToEndDense | 2.07 ms | 4.26 ms | 0.49x | 2682656 | pass |
| EnumerateMatchesFirstTwo | 246.0 ns | 662.9 ns | 0.37x | 472 | pass |
| PartialMatch | 101.39 us | 320.07 us | 0.32x | 0 | pass |
| CaseFoldAccented | 28.06 ms | 126.44 ms | 0.22x | 0 | pass |
| PathologicalNestedRepeat | 2.08 ms | 14.50 ms | 0.14x | 0 | pass |
| LiteralMatch | 51.89 us | 385.69 us | 0.13x | 0 | pass |
| CompileLargePattern | 247.30 us | 4.97 ms | 0.05x | 1016008 | pass |
| FuzzyShort | 283.7 ns | 7.42 us | 0.04x | 32 | pass |
| FuzzyPhraseOneEnhanced | 54.16 ms | 2.02 s | 0.03x | 16158920 | pass |
| FuzzyPhraseOneMatch | 52.30 ms | 2.00 s | 0.03x | 12877736 | pass |
| FuzzyPhraseThreeNamedList | 147.63 ms | 5.69 s | 0.03x | 0 | pass |
| FuzzyPhraseThreeSeparatePasses | 152.49 ms | 5.91 s | 0.03x | 0 | pass |
| FuzzyPhraseThreeAlternation | 145.10 ms | 5.67 s | 0.03x | 0 | pass |
| FuzzyPhraseOneIsMatch | 49.17 ms | 2.01 s | 0.02x | 0 | pass |
| PathologicalWordsToEnd | 504.21 us | 69.49 ms | 0.01x | 0 | pass |
| ConditionalPerToken | 203.51 us | 111.16 ms | 0.00x | 0 | pass |
| FuzzyLong | 91.24 us | 127.78 ms | 0.00x | 32 | pass |
| FuzzyNoMatchLong | 82.34 us | 128.96 ms | 0.00x | 0 | pass |
| PathologicalPrefixBranches | 7.87 us | 36.75 ms | 0.00x | 0 | pass |
| ConditionalBesideBranch | 2.32 us | 17.49 ms | 0.00x | 0 | pass |
| PathologicalSameBranches | 3.60 us | 163.50 ms | 0.00x | 0 | pass |
| EnumerateMatchesSpanToEndDense | 1.86 ms | - | - | None | not paired |
| EmptyIterationRecord | 3.19 ms | - | - | None | not paired |

37 paired; 3 slower (8%); worst 1.22x (TestedGroupRepeat)

## What the slower rows have in common

- **TestedGroupRepeat** `(a)(?:\1|b)*c` and **ConditionalPerTokenNoMemo**
  `(\w)(?:(?(?=\w)\w+|\s))*\1$`, both failing searches: each has a backreference, which turns the
  failure memo off for the whole pattern, so the port does the same exponential-in-practice work
  as upstream at a slightly higher constant. The memo's backreference key (C5's "BR" extension,
  `docs/plan/2026-10-13-linear-nested-repeats-design.md`) is the algorithmic fix; the cold-arm
  work on BasicMatchCore is the constant-factor one.
- **FuzzyBudgetThree** (1.01x), with **EnhanceMatch** (1.00x) and **BestMatch** (0.99x) at parity:
  `(?:haystack){e<=3}`, whose pieces are too short for the Navarro filter, so every start runs a
  full fuzzy attempt. C3 (`docs/plan/2026-10-13-fuzzy-literal-bitparallel-design.md`) targets these.
- Next band, 0.78-0.95x: ReplaceTemplate, MatchesToEndDense, MatchesToEnd, MatchesFirstTwo,
  SplitLong. These are per-match costs (Match and group objects, Replace's segment list), the C2
  items in the 2026-10-13 brief.
