---
slice: S59
phase: 7
title: A bounded pattern cache behind FuzzyRegex.CacheSize, so the static conveniences stop compiling per call
delivers: []
---

# S59 - The pattern cache the static conveniences never had

Per-sitting progress, measurements and what is still open: `docs/plan/slices/notes/S59-sittings.md`.

Planned since 2026-09-16 as the first optimisation with an implementation
(`OPTIMISATION-NOTES.md`, API-level table; DIVERGENCES row `regex.purge`/`regex.cache_all`,
PLANNED). Upstream caches compiled patterns module-globally and exposes `purge` and `cache_all` to
control it; .NET keeps the fifteen most recently used static patterns behind `Regex.CacheSize`.
This port does neither: every static convenience calls `new FuzzyRegex(pattern, ...)` and pays a
full parse and compile per call, which is the single largest number on the compile-time workload
and is invisible on the instance API.

Small, self-contained, and the one Phase 7 slice whose win needs no profiler to predict - but it
still lands behind S58's numbers like every other, and it is where **shared mutable state** enters
a library whose thread-safety contract (S52b) is permanent.

## Scope

1. **A bounded MRU keyed on the raw flags.** An internal static cache in `FuzzyRegex`, keyed on the
   pattern string, the **raw `RegexFlags` integer** the options and the inline prefix produce (not
   `FuzzyRegexOptions`, and not `Options`, which reports flags the pattern itself set - S53b item
   2 is why this is stated: a key taken from a property that post-processes flags is a latent
   wrong-answer bug), the version, and the match timeout, as `Regex`'s key does. Most recently used
   wins; the least recently used entry is evicted at the bound.
2. **Only the overloads that can be cached.** Four of the static conveniences take a caller-supplied
   `namedLists` dictionary (`Match`, `MatchAtStart`, `FullMatch`, `Matches`, `EnumerateMatches`).
   That dictionary is mutable, caller-owned and unbounded, so it is not a cache key: those calls
   **bypass the cache** when `namedLists` is non-null and use it when it is null. Say so in the
   `<remarks>`, and pin it with a test that mutates a passed dictionary between two calls and gets
   the answer for the dictionary it passed, not a cached one.
3. **`public static int CacheSize { get; set; }`, default 15**, matching `Regex.CacheSize` - verify
   the default and the setter's trimming behaviour by running the built-in `Regex`, not by
   quoting it, and record the measurement in the slice notes. Setting it to 0 disables the cache
   and clears it, which is this port's `regex.purge`; a smaller value evicts down to the new bound
   immediately. A negative value throws `ArgumentOutOfRangeException`. The instance constructors
   never consult the cache, exactly as `new Regex(...)` does not.
4. **AOT-safe.** No reflection, no static initialisation that a trimmer can remove, nothing the
   `IsAotCompatible` analysers flag; `src/FuzzyRegex` stays at zero trim and zero AOT warnings, the
   state S53 left it in.
5. **Concurrency under S52b's contract.** The cache is the first shared mutable state in the
   library, so it is tested as such: a lock or `ConcurrentDictionary` plus an explicit MRU order,
   with a deterministic stress test in `tests/FuzzyRegex.Tests/Gaps/Api/` alongside
   `ThreadSafetyStressTests.cs`, hammering the same and different patterns from many threads and
   asserting every answer and that the bound is never exceeded. `ThreadSafetyTests.cs`,
   `ThreadSafetyStressTests.cs` and `PoolDisciplineTests.cs` stay green unchanged - a slice that
   widens one of their allowlists has introduced the defect they exist to catch (ROADMAP: their
   tests are PERMANENT and constrain Phase 7 directly).
6. **Records.** `docs/DIVERGENCES.md` row `regex.purge`/`regex.cache_all`: PLANNED becomes
   SHIPPED, naming `CacheSize` and the `namedLists` bypass. `docs/PORTMAP.md` row for the cache.
   DECISIONS entry with the measured default. `OPTIMISATION-NOTES.md`: delete the static-conveniences
   row and any `ponytail:`/`Phase 7` comment at the sites in the same commit.

## Verification

- Before and after, same machine, same session:
  `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*' --job medium
  --exporters json --artifacts artifacts/bench/<date>-S59-<before|after>`, compared with
  `pwsh -File tools/compare-benchmarks.ps1` against the committed baseline at S58's noise floor.
  Report the per-workload time and allocation ratios, never an average. The compile-time workload
  and the static-convenience workloads are where the win must show; anything else moving is a
  finding.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict.
  A cache that changes an answer has a key bug, not an optimisation.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN; binary size
  recorded against 6,972,928 bytes.
- `pwsh -File tools/update-public-api.ps1`, because `CacheSize` is new public surface;
  `PublicAPI.Unshipped.txt` committed with the diff reviewed, not just regenerated.

## Done when

- [ ] Bounded MRU behind `CacheSize` (default 15), keyed on the raw flags, with `namedLists` calls
      bypassing it and a test proving the bypass.
- [ ] `CacheSize = 0` clears and disables; a reduced bound evicts immediately; negative throws.
- [ ] Deterministic concurrency test added; every S52b test green unchanged, no allowlist widened.
- [ ] AOT publish green, zero trim and AOT warnings in `src/FuzzyRegex`.
- [ ] Measured before and after in the commit message; DIVERGENCES row flipped to SHIPPED, PORTMAP
      and DECISIONS updated, the OPTIMISATION-NOTES row and its source comment deleted.
- [ ] `update-public-api.ps1` run and `PublicAPI.Unshipped.txt` committed.
- [ ] Anything deferred carries a `ponytail:`/`Phase 7` comment and a new OPTIMISATION-NOTES row.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a key that omits the
      timeout, the version or a flag the inline prefix set; an entry mutated after publication; an
      MRU order updated without the lock; a cached instance shared across threads while a caller's
      `namedLists` is mutated; `CacheSize = 0` leaving entries alive), commit.
