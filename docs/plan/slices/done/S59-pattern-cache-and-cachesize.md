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

- [x] Bounded MRU behind `CacheSize` (default 15), keyed on the raw flags, with `namedLists` calls
      bypassing it and a test proving the bypass.
- [x] `CacheSize = 0` clears and disables; a reduced bound evicts immediately; negative throws.
- [x] Deterministic concurrency test added; every S52b test green unchanged, no allowlist widened.
- [x] AOT publish green, zero trim and AOT warnings in `src/FuzzyRegex`. (The `src` half: yes -
      `run-aot-smoke.ps1` GREEN, 29 cases, 0 misses, 6,982,144 bytes. **`run-aot-tests.ps1` is
      RED and was already red before this slice**, on one `IL2065` in the TEST project that S65
      introduced and S58 recorded; reproduced here from a deleted `obj` and `bin`. Not fixed here
      - fixing it is a real choice and a `.cs` change outside this slice - and now owned by S57
      with a scope bullet and a box of its own.)
- [x] Measured before and after in the commit message; DIVERGENCES row flipped to SHIPPED, PORTMAP
      and DECISIONS updated, the OPTIMISATION-NOTES row and its source comment deleted.
- [x] `update-public-api.ps1` run and `PublicAPI.Unshipped.txt` committed. (Re-run by the second
      reviewer: no diff.)
- [x] Anything deferred carries a `ponytail:`/`Phase 7` comment and a new OPTIMISATION-NOTES row.
      (Nothing deferred; the row deleted, and no orphaned source comment survives it.)
- [x] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a key that omits the
      timeout, the version or a flag the inline prefix set; an entry mutated after publication; an
      MRU order updated without the lock; a cached instance shared across threads while a caller's
      `namedLists` is mutated; `CacheSize = 0` leaving entries alive), commit. (Ratchet GREEN
      6432/6432; oracle clean at seeds 7 and 4242 and 1 divergence at 20260919 on the one
      pre-existing triaged row; AOT as qualified above. Nothing on the hunt list reproduced.)

## Closing notes

### What landed

A bounded most-recently-used cache of compiled patterns (`src/FuzzyRegex/PatternCache.cs`, 210
lines) and `FuzzyRegex.CacheSize` over it, default **15** - which is what `Regex.CacheSize` is,
measured on .NET 10.0.10 rather than read off a page (`tools/probes/bcl-regex-cachesize.ps1`), as
are the `0`-clears-and-disables, evict-on-reduction and negative-throws semantics.

**Twelve static conveniences consult the cache**: seven that take no `namedLists` argument, and
five that take one and read the cache when it is null, which is their default. The bypass is per
CALL, not per overload - a call that actually carries a caller-supplied dictionary compiles per
call, because that dictionary is mutable, caller-owned and unbounded and so cannot be part of a
key. The constructors never consult it, exactly as `new Regex(...)` does not.

The key is the pattern text, the **raw** options integer the caller passed, the default version and
the instance match timeout - never the `Options` property, which folds in what the pattern's own
inline prefix set and masks off the bits this port does not name, so `"(?i)a"` with `None` and
`"(?i)a"` with `IgnoreCase` have equal `Options` and would share one entry while compiling to
different graphs. That is the S53b item-2 trap, and a test pins both halves of it.

Measured per workload, `--job medium`, **in one process**: each `Static*` row has an `Uncached*`
twin whose body is `b6e82db`'s own convenience verbatim, so the pair differs in the cache and in
nothing else, and an `Instance*` row gives the ceiling. `StaticIsMatch` 634.9 ns / 1,816 B against
`UncachedIsMatch` 7,315.3 ns / 29,840 B - **11.5x faster, 16.4x less**; `StaticMatch` 11.4x and
16.4x; the fuzzy pair 1.35x and 9.7x, moving least because matching dominates it. Two readings
matter more than the headline: a cached static call now costs what a pre-compiled instance costs
(1.02x, inside the noise floor), so the convenience has stopped being the slow way to do it; and
the always-missing row - 20 distinct patterns cycled against a bound of 15, so every call evicts
and misses - lands at 1.07x of having no cache at all, under S58's 1.13 time floor, so the cache
costs nothing when it never hits. Every one of those fourteen numbers was re-checked against the
raw artifact by the independent verifier.

### Surprises

**The `namedLists` bypass is per call, and three records said otherwise.** The code was right and
the public XML docs were right; DECISIONS, DIVERGENCES and the sitting notes had all flattened "a
call carrying a dictionary is not cached" into "those five conveniences are never cached", which is
false for the defaulted call - the common one. Only the second blind pass caught it, because the
first reviewer was looking at the code, where it is correct.

**Two pre-rebase duplicate SHAs, in one day.** DECISIONS recorded the `dfa8767` trap on 2026-09-19;
hours later this slice's S57 bullet cited `3b09b76` for the same kind of commit and the verifier
rejected it with `git merge-base --is-ancestor`. The right SHA is `148c3bf`. Chasing it found the
triaged-divergence doc resting on `dfa8767` and `e30a4e8` too, so its "empty" diff was not empty
and its "28 commits" was a count over a branch nobody is on - re-derived as 41 commits, 0 touching
`src/`. **Check a SHA is an ancestor before quoting it**, and do not cite `HEAD` in a comment that
outlives the tree: a reviewer resolved `HEAD` against a grown file and raised two findings that
were wrong for exactly that reason.

### For the next slice

`tools/run-aot-tests.ps1` is RED and has been since S65, on one `IL2065` in
`tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs(62)`. **S57 now owns it**, with
the reproduction, the three options and a "Done when" box. It is in the test project only;
`src/FuzzyRegex` is clean under AOT. S58 recorded this failure in its closing notes and S59 still
spent a full AOT publish re-deriving it, which is why it is now a scope bullet rather than a note.

### Review

**Two blind passes and one independent verifier.**

The first pass (sitting 1, Sonnet, reproduction-only) covered `PatternCache.cs`, the `FuzzyRegex.cs`
call sites and both cache test files: **1 finding raised, 1 reproduced, 1 fixed** - a stale test
name in `tests/parity-baseline.json` that took the ratchet RED on a missing baselined id.

The second pass (sitting 2, Sonnet, reproduction-only, `.scratch/s59-review2-brief.md`) covered the
delta the first reviewer never saw - `PatternCacheBenchmarks.cs`, the `ThreadSafetyTests` edits,
DIVERGENCES, PORTMAP, DECISIONS, OPTIMISATION-NOTES, `PublicAPI.Unshipped.txt`, the `CacheSize` XML
docs and both probes: **3 findings raised, 1 reproduced, 1 fixed**. The one that survived is the
per-call bypass claim above, fixed in three records. The two that did not both rested on resolving
`HEAD` against the current tree instead of the commit the comment names; the comments now name
`b6e82db`. A third pass was **not** needed: the sitting-2 fixes are documentation, comments and one
new probe, and they went to the verifier, which re-ran the probe.

The verifier (fresh Opus, amendment 16 limb (d), no-git-revert clause verbatim,
`.scratch/s59-verifier-brief.md`) re-derived ten items: **7 CONFIRMED, 2 DIFFERENT, 4 sub-items
COULD NOT RUN**. Both DIFFERENT were reproduced here and fixed - the `3b09b76` SHA and the
"byte-identical" row claim. Of the COULD NOT RUN, two gap-test assertions genuinely lacked
provenance and now carry it from a real upstream run
(`tools/probes/s59-cache-answers-upstream.py`); the third needed none, being this port's own
`Options` semantics; and the deleted-`obj` precondition is now written out as the two commands that
produced it, since the log alone cannot show it.

No control was run: this slice changes no engine behaviour, and the oracle is the check that it
does not.
