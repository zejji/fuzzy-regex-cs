---
name: benchmark
description: Use to measure FuzzyRegex performance, refresh the committed Python regex and System.Text.RegularExpressions baselines, or check the v1.0 performance gate. Covers running BenchmarkDotNet, measuring the Python side with pyperf on the same machine, and the per-workload rule the gate applies.
---

# Benchmarking and the v1.0 performance gate

Correctness gates come first; this is Phase 7 work and it is benchmark-driven throughout. Never
optimise on a hunch - measure, change, measure again, and keep the change only if the number
moved.

## The gate, exactly

For every workload in the suite, our median (BenchmarkDotNet) must be at or below the Python
`regex` median (pyperf) for the equivalent operation, with one tolerance:

- no more than **10% of workloads** may be slower, and
- **none by more than 1.25x**.

A "workload" is one named benchmark case - pattern plus input corpus plus operation - not an
average. An overall win cannot hide one badly regressed case. Workloads Python cannot express
(Span APIs and the like) are measured but excluded from the gate.

Both sides must be measured **on the same machine, in the same session**. A baseline carried over
from different hardware means nothing.

## Running

```powershell
dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*'
dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --list flat   # what exists
dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*Fuzzy*'
```

Release only, and nothing else running on the machine. BenchmarkDotNet refuses a Debug build for
good reason; do not work around it.

## Refreshing the Python baseline

`bench/baselines/measure_python.py` times one pyperf benchmark per `WorkloadBenchmarks` and
`ManyInputsBenchmarks` row, named as the BenchmarkDotNet method so the tables join. It reads the
C# side's inputs and answers from a directory that its `--prepare` step writes (it runs the
benchmark project's `usage-corpus`, `workload-answers` and `usage-answers` commands):

```powershell
python -m pip install regex pyperf
python bench/baselines/measure_python.py --prepare .scratch/gate-inputs
python bench/baselines/measure_python.py --inputs .scratch/gate-inputs --check-only
python bench/baselines/measure_python.py --inputs .scratch/gate-inputs -o bench/baselines/<machine-id>/python-regex.json
python -m pyperf check bench/baselines/<machine-id>/python-regex.json
```

Before timing, every row's answer is compared with the C# answer: the returned number and the full
answer behind it (every span, the substituted text, which inputs matched). A row that differs is
printed and recorded as "answer differs" and still timed; `--check-only` exits 1 if any row
differs. Rows Python cannot express, and why, are in the script's `EXCLUDED` table.

Flags: the rows run under `regex.V1`, the port's default, where IgnoreCase means full case
folding. The fuzzy `FuzzyPhrase` rows run under `V0 | I` (simple folding) instead, because upstream
drops matches the port finds under full folding, in V1 and in `V0 | FULLCASE` alike (ledger entry
28). On this all-ASCII data simple folding gives the port's answers. Each row's flags, operation and
answer-check result are in the JSON metadata, with the `regex`, Python and pyperf versions and the
git SHA. Record the machine and the date beside the file. A baseline without those is not
evidence. Commit the JSON.

## What the suite must cover

- literal-heavy patterns,
- character-class-heavy patterns,
- backtracking-heavy patterns,
- fuzzy matching: short and long inputs, and varying error budgets,
- `BESTMATCH` workloads specifically - they explore far more of the search space than a plain
  fuzzy match and are the likeliest place to regress.

Where a feature overlaps `System.Text.RegularExpressions`, measure that too. It is not part of
the gate, but it is the number a .NET user will actually compare against.

## Optimising, in order

Work down this list. Stop as soon as the gate passes - an optimisation with no measured win is
just a bug you have not found yet.

1. **Allocation elimination** - `Span<T>`, `stackalloc`, `ArrayPool`. Usually the whole story.
2. **`SearchValues<char>` literal prefilters** - skipping non-candidate positions beats making
   the inner loop faster.
3. **Struct layout and devirtualising the VM dispatch.**
4. **`unsafe`** - last, and only with a benchmark in the commit message proving it earned its
   place.

Source-generated compiled patterns are explicitly post-1.0. The bytecode design does not preclude
them; building them now would be speculative.

## After any optimisation

An optimisation that moves the port away from upstream's structure makes every future sync more
expensive, so it has to pay for itself:

```powershell
tools/check-ratchet.ps1     # must be GREEN - performance work never costs correctness
```

Record in `docs/PORTMAP.md` where our structure now diverges from upstream and why, and remove
the matching `.editorconfig` port relaxation if the code no longer needs it. Put the measured
before-and-after in the commit message.
