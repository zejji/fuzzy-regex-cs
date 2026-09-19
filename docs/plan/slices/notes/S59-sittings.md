# S59 sittings

## Sitting 1, 2026-09-19 (checkpoint, allowance window closing at 93%)

### What landed, all green

`src/FuzzyRegex/PatternCache.cs` - a bounded most-recently-used cache of compiled patterns, and
`FuzzyRegex.CacheSize` over it, default 15. The twelve static conveniences that do not take named
lists go through it; the five that take a `namedLists` dictionary bypass it and compile per call, as
every convenience did before; the constructors never consult it.

The key is the pattern text, the RAW options integer the caller passed, the default version and the
instance match timeout - raw, never the `Options` property, which folds in what the pattern's own
inline prefix set and masks off the bits this port does not name, so two different compiles would
share one entry (the S53b item-2 trap). `"(?i)a"` with `None` against `"(?i)a"` with `IgnoreCase` is
the worked example, pinned in `PatternCacheTests`.

Tests: `PatternCacheTests` (20 tests), `PatternCacheStressTests` (2, four threads per core), and two
edits to `ThreadSafetyTests` - `PatternCache` is classified thread-safe under the rule's own third
category, and `StaticTableSnapshot` skips it EXPLICITLY rather than letting the `Convert.ToString`
fall-through render it as a line that can never move. Ratchet GREEN, 6432/6432, baseline 6324.

Oracle: 1 divergence of 6380 at seed 20260919, row 3655, byte-identical to the already-triaged
`docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`; the other two seeds clean.

### Measured, per workload, never an average

`bench/FuzzyRegex.Benchmarks/PatternCacheBenchmarks.cs`, `--job medium` (15 iterations, 2 launches,
10 warmups), 19:05-19:46, artifacts at `artifacts/bench/2026-09-19-S59-after`.

The before and after were taken IN ONE PROCESS rather than across two trees: each `Static*` row has
an `Uncached*` twin whose body is HEAD's own convenience verbatim, so the pair differs in the cache
and in nothing else - no second build, no cross-run drift - and an `Instance*` row gives the ceiling.

| Workload | Time | Allocated | Against its twin |
| --- | ---: | ---: | --- |
| `StaticIsMatch` vs `UncachedIsMatch` | 634.9 ns vs 7,315.3 ns | 1,816 B vs 29,840 B | **11.5x faster, 16.4x less** |
| `StaticMatch` vs `UncachedMatch` | 648.9 ns vs 7,398.2 ns | 1,816 B vs 29,840 B | **11.4x faster, 16.4x less** |
| `StaticFuzzyIsMatch` vs `UncachedFuzzyIsMatch` | 8,744.8 ns vs 11,775.7 ns | 992 B vs 9,656 B | **1.35x faster, 9.7x less** |
| `StaticIsMatch` vs `InstanceIsMatch` | 634.9 ns vs 621.4 ns | 1,816 B vs 1,816 B | 1.02x - the same, to the noise floor |
| `StaticFuzzyIsMatch` vs `InstanceFuzzyIsMatch` | 8,744.8 ns vs 8,386.9 ns | 992 B vs 992 B | 1.04x - the same |
| `StaticIsMatchAlwaysMissing` vs `UncachedIsMatch` | 7,858.1 ns vs 7,315.3 ns | 29,784 B vs 29,840 B | 1.07x, under S58's 1.13 time floor: a pure miss costs what no cache cost |
| `StaticMatchWithNamedLists` vs `UncachedMatch` | 8,062.9 ns vs 7,398.2 ns | 31,920 B vs 29,840 B | the bypass, compiling per call as before; the extra 2,080 B is the named list itself |

Two readings beyond the headline. A cached static call now costs what a pre-compiled instance costs
(1.02x): the convenience has stopped being the slow way to do it. And the always-missing row - 20
distinct patterns cycled against a bound of 15, so every call evicts and misses - lands inside the
noise floor of having no cache at all, so the cache costs nothing when it never hits. The fuzzy rows
move least because matching dominates them: `(?:needle){e<=1}` spends about 8.4 us matching and 3 us
compiling.

The rest of the suite against this machine's committed baseline: GREEN, nothing more than 1.25x
slower or allocating 1.25x more. **No baseline was updated**, deliberately: the machine was shared
for the run. `tools/probes/sample-machine-load.ps1` ran beside it, 162 samples; the busy samples are
the benchmark's own children bar six foreign ones (`python` 8.0-8.5 s in three, `msedge` and
`VBCSCompiler` in three more).

### Review

One blind pass (Sonnet, reproduction-only brief at `.scratch/s59-review-brief.md`, hunt list: a key
omitting the timeout, the version or an inline-prefix flag; an entry mutated after publication; the
MRU order touched without the lock; a cached instance shared while a caller's `namedLists` mutates;
`CacheSize = 0` leaving entries alive; any change to a non-convenience path).

Findings raised: 1. Reproduced: 1. Fixed: 1 - `tests/parity-baseline.json` still carried the old
name of the test renamed to `Raising_the_size_from_zero_starts_caching_again`, so the ratchet went
RED on a missing baselined id; re-run with `-AcceptRemovals -UpdateBaseline`, GREEN at 6324 ids. The
reviewer ran `PatternCacheTests` (31 cases), `PatternCacheStressTests` (2) and `ThreadSafetyTests`
(10) green and reproduced nothing against hunt items 1-7. A second pass over unreviewed delta is
still owed for the docs and the bench file (see below).

### Still open, in order, for the next sitting

1. **The AOT test gate is RED and the failure is very likely mine, not the slice's.**
   `tools/run-aot-smoke.ps1` is GREEN - 29 cases, 0 misses, binary 6,982,144 bytes against S58's
   6,972,928, so the cache costs 9,216 bytes of native image (0.13%) and `src/FuzzyRegex` is clean.
   `tools/run-aot-tests.ps1` then failed in ilc: `error MSB3077 ... ilc ... exited with return value
   0, but errors were detected`, with exactly one trim analysis error in the log, `IL2065` at
   `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs(62)` - a file S59 does not
   touch, in the TEST project, not the library. Before the gate ran I had built that project with
   `dotnet build tests/FuzzyRegex.Tests -c Release -r win-x64` (to prove a wedged compiler was not
   blocking builds), WITHOUT `PublishAot`, into the same `obj/Release/net10.0/win-x64`. Start the
   next sitting by deleting `tests/FuzzyRegex.Tests/obj` and `bin` and re-running the gate clean; if
   it still fails, the IL2065 is real and predates this slice, and the question is why S58 was green.
2. The second blind pass over what the first reviewer never saw: `PatternCacheBenchmarks.cs`, the
   `ThreadSafetyTests` edits, DIVERGENCES, PORTMAP, DECISIONS, and the `CacheSize` XML docs.
3. The fresh-Opus independent verifier (amendment 16 limb (d)), with the verbatim no-git-revert
   clause from `docs/VERIFICATION.md`.
4. `git mv` the slice file to `done/`, closing notes (this file is the draft of them), tick the
   "Done when" boxes.

### Two wedged processes, NOT killed (owner rule: report and wait)

`dotnet publish tests/FuzzyRegex.Tests` PID 33360, started 18:47:10, and its child `csc.exe` PID
37192, started 18:47:28. The csc had burned 105.5 s of CPU and then stopped: two samples 60 s apart
read 105.46875 both times. They do NOT block other builds - a fresh Release build of the same
project completed in 17 s beside them - but they are the first sitting's attempt at the AOT gate and
they are still there. The script's own hang fix (`MSBUILDDISABLENODEREUSE=1`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0`, `tools/run-aot-tests.ps1:46-56`) was in force, so this is a NEW
failure mode: the hang moved from the MSBuild node to `csc.exe` itself.

### Probes run this sitting

- `tools/probes/bcl-regex-cachesize.ps1` - `Regex.CacheSize` is 15 on .NET 10.0.10; reducing it
  evicts at once; 0 clears and disables; -1 throws `ArgumentOutOfRangeException` on `value` and
  leaves the value unchanged. That is where the default of 15 and the `0` semantics come from.
- `tools/probes/s59-named-lists-rebind.py` - regex 2026.9.10 printed `no-match: None` and
  `match: <regex.Match object; span=(0, 4), match='beta'>`, which are the two expected answers in
  `PatternCacheTests.A_caller_s_named_lists_dictionary_bypasses_the_cache`.
