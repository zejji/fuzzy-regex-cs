---
slice: S52b
phase: 6
title: Thread safety proven structurally, under load, and in the documented contract - before Phase 7 adds a single cache
delivers: []
---

# S52b - Thread safety

The design spec's "runtime discipline" promises what upstream and .NET `Regex` promise: a compiled
pattern is immutable and safe to share across threads, and everything mutable lives in per-call
state. Nothing in the repository proves it. There is no test that uses a second thread, and no check
that a field on the pattern graph is not written after construction. Owner request 2026-09-14:
cover it in the plan (spec amendment 23).

Why the .NET side is harder than upstream's. Upstream's engine already runs on several OS threads
against one pattern (`_regex.c` releases the GIL inside the match loop when `concurrent=True`,
`release_GIL` at :2198 of the pinned 2026.9.10), so pattern immutability is upstream's own rule and
the engine loop has a real precedent. But everything OUTSIDE upstream's match loop is protected by
the GIL for free - the pattern's Python attributes, `groupindex`, the `Match` object - and the port
has no such umbrella. .NET callers hit concurrency by default (request threads, `Parallel.ForEach`,
`async`), so the guarantee is table stakes here where most Python users never test it. And Phase 7
is where caches appear (start optimisations, required-string prefilters, the `Regex`-style
interpreter state), which is exactly where .NET's own `Regex` had to make thread safety deliberate.
Hence this slice sits in Phase 6 and its tests are PERMANENT: a Phase 7 slice that turns one red has
introduced shared mutable state, and the fix is to remove it, not to widen the allowlist.

## Scope, in order of weight

1. **Structural immutability test** (`tests/FuzzyRegex.Tests/Gaps/Api/ThreadSafetyTests.cs`).
   Reflection over the object graph reachable from a compiled `FuzzyRegex` - `PatternObject`, every
   `Node`, the Unicode tables, named lists, the group index - asserts every instance field is
   `readonly` or init-only and every collection is a read-only type, with a justified allowlist for
   fields the compiler writes before the pattern is published (each entry names the writer and the
   line, and a test proves no write happens after construction). The reflection walk is the test
   that fails the moment a Phase 7 slice adds a lazily-computed cache without `Lazy<T>` or an
   `Interlocked` publication. Test-first: plant a mutable `int` on a node in a scratch branch and
   watch the test go red before removing it.
2. **Static state audit.** Every `static` field in `src/FuzzyRegex` is `readonly` and holds an
   immutable or thread-safe type, or is a `ReadOnlySpan<byte>` property over a constant. One test,
   one allowlist, same rule as above.
3. **Pool discipline.** `MatchState` rents from `ArrayPool<T>.Shared`. A double return hands one
   buffer to two threads. A debug pool wrapper, enabled in the test project only, throws on a
   return of a buffer not currently rented and on a rented buffer not returned when the state is
   disposed; the whole suite runs under it once, and the fuzzy, BESTMATCH, verb and partial families
   are exercised explicitly because they own the deepest backtracking stacks.
4. **Stress test under real parallelism.** One compiled pattern per family - plain, fuzzy,
   `(?e)`, `(?b)`, POSIX, partial, `(*SKIP)`, named lists, `sub`, `split`, `finditer` - matched from
   `Environment.ProcessorCount * 4` threads over a few hundred subjects drawn from the recorded
   waves, every result compared to the sequential answer computed first. Deterministic engine, so
   any mismatch or exception is a race. Bounded in time with the per-test `[Timeout]`. Run three
   times in the slice to shake out a flaky race, then kept as one ordinary test.
5. **`Match` objects** are immutable value holders and safe to read from any thread: same reflection
   rule, applied to the `Match`/`Group`/capture types.
6. **The contract, written where callers read it.** XML docs on `FuzzyRegex` and `Match` state the
   guarantee the way `System.Text.RegularExpressions.Regex` does: "instances are immutable and
   thread-safe; a `Match` may be read from any thread; do not share a mutable enumerator". The
   README's API section gets the same sentence. Any `concurrent` parameter upstream exposes is
   documented as having no port equivalent, because the port always matches without a global lock.
7. **Research, quoted in the notes**: how `System.Text.RegularExpressions.Regex` guarantees this in
   .NET 10 (its cache, its `RegexRunner` rental) and what the Framework Design Guidelines say about
   documenting thread safety - real sources fetched during the slice, not recollection.

## Verification

- Ratchet GREEN; the new tests red-first as described; the stress test green three times in a row;
  a scratch-branch mutable field demonstrated red.
- Blind review (hunt: an allowlist entry with no proof of construction-time-only writes; a stress
  test whose sequential baseline is computed on the same shared object it is testing; a pool wrapper
  that only wraps one of the rent sites), then the verifier pass re-running the stress test.

## Done when

- [ ] Structural and static immutability tests green with justified, proven allowlists.
- [ ] Debug pool wrapper in the test project; suite green under it.
- [ ] Stress test across all families, deterministic, green three times.
- [ ] Contract documented in XML docs and README; research quoted.
- [ ] Ratchet GREEN, blind review, verifier, commit; tests marked PERMANENT for Phase 7.
