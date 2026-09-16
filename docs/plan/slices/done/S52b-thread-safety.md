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

- [x] Structural and static immutability tests green with justified, proven allowlists.
- [x] Debug pool wrapper in the test project; suite green under it. **Narrowed - see closing note 4.**
- [x] Stress test across all families, deterministic, green three times.
- [x] Contract documented in XML docs and README; research quoted.
- [x] Ratchet GREEN, blind review, verifier, commit; tests marked PERMANENT for Phase 7.

## Closing notes (2026-09-16, one sitting)

**Ratchet GREEN: 6144 tests, 6144 passing, 6036 distinct ids, baseline 6036** - seventeen new tests
over S52's 6127/6019 (`ThreadSafetyTests` 9, `ThreadSafetyStressTests` 2, `PoolDisciplineTests` 6).
The default oracle wave is **GREEN at all three seeds**, the four `EXPECTED` rows being the standing
pins firing as the strict manifest requires.

**1. The slice found a real defect, and it was in `Match`, not in the pattern.** `Match.FuzzyChanges`
cached its answer in a `FuzzyChanges?` field - a `HasValue` flag plus three list references, four
fields wide - published with `??=`. That write is not atomic, so a second thread could see the flag
set and a list still null. It is now a `StrongBox<FuzzyChanges>`, one reference, published
atomically; the value is a pure function of the match's readonly change array, so two threads racing
to fill it compute the same answer and either may win. `Lazy<T>` was rejected because it allocates
on every match whether or not the property is read, and computing eagerly in the constructor was
rejected because `SplitFuzzyChanges` allocates three lists unconditionally, so every non-fuzzy match
would pay for them.

**2. The pattern graph itself was already right, and that is now measured rather than assumed.**
Forty fields reachable from a compiled `FuzzyRegex` are not `readonly` - all of `PatternObject`,
`Node`, `NextNode`, `GroupInfo`, `CallRefInfo`, `RepeatInfo` - because the engine builds its graph by
mutation, as upstream's C does. Every one is written inside `Engine.PatternObject.Compile`
(`PatternObject.cs:217`) and the `NodeCompiler`/`Optimiser` passes it calls, and `Compile` is invoked
from exactly one place, the `FuzzyRegex` constructor (`FuzzyRegex.cs:170`). They are allowlisted, and
the allowlist's claim is proved wholesale by a snapshot of the entire reachable graph taken before
any match and compared after two full workloads.

**3. What the blind review changed, and it was the most valuable thing in the slice.** Both findings
were the same mistake: my snapshot tests ran a warm-up *before* taking the baseline, reasoning that
one-time initialisation is legitimate. The effect was to hide every first-match-only write behind the
warm-up - which is exactly the shape a Phase 7 lazily computed cache takes, and so precisely what the
test existed to catch. The reviewer demonstrated it with `pattern.ReqString ??= new Node(0)` on the
match path and with an unsynchronised static `Dictionary` memo, both of which left the tests green. I
reproduced both before touching anything. The fixes: the instance snapshot now takes its first
reading before these patterns have matched anything, and the static snapshot runs its two workloads
over *differently salted subjects* so a per-subject memo keeps growing instead of saturating after
one pass (statics are process-wide, so unlike the instance graph a virgin reading cannot be
guaranteed in a shared test process - that limitation is written into the test).

**4. One scope item was narrowed deliberately, and the reason is this slice's own rule.** The file
asks for a debug pool wrapper the whole suite runs under. Reaching every `MatchState` the suite
creates needs a seam the engine consults, and the only such seam is a settable static on the
library - the exact shared mutable state
`Every_static_field_in_the_library_is_readonly_or_const` forbids. Not a trade worth making, because
every `ArrayPool` call in `src/FuzzyRegex` is the one `Rent` and two `Return`s inside `ByteStack`
(measured; the review re-confirmed it), so `ByteStack` took an `ArrayPool<byte>?` constructor
parameter defaulting to `Shared` and `PoolDisciplineTests` drives that class exhaustively against a
tracking pool. The leak half - a state that fails to release a stack - is covered by
`Disposing_a_match_state_releases_every_one_of_its_stacks`.

**5. A latent hang was removed on the way.** The first stress probe used `Parallel.For` with a
`Barrier(ProcessorCount * 4)`. `Parallel.For` does not promise that many concurrent threads, so
`SignalAndWait` waited on participants the pool injected at roughly one per half-second. It was both
a potential hang and a feeble test: against the unfixed field it found the tear once in about 57
seconds and not at all inside a 15-second budget. Rewritten onto 64 dedicated `Thread`s running
20,000 barrier rounds it takes about two seconds and caught the same defect on three runs of three.
**The lesson generalises: a barrier needs real threads, never a thread pool.**

**6. The contract is now written where callers read it** - XML docs on `FuzzyRegex` and `Match`, and
a README section - and it is deliberately *stronger* than the built-in `Regex`'s. Quoted from
`learn.microsoft.com/dotnet/standard/base-types/best-practices-regex` (fetched 2026-09-16): "The
Regex class itself is thread safe and immutable (read-only) ... However, result objects (Match and
MatchCollection) returned by Regex should be used on a single thread. Although many of these objects
are logically immutable, their implementations could delay computation of some results to improve
performance, and as a result, callers must serialize access to them." This port promises a `Match`
is readable from any thread, and finding 1 is what that promise cost. Enumerators remain the
exception, as they are throughout .NET. From `managed-threading-best-practices` (same date): "Make
static data (`Shared` in Visual Basic) thread safe by default", "Do not make instance data thread
safe by default. Adding locks to create thread-safe code decreases performance", and "Avoid
providing static methods that alter static state." The apparent tension with the second is resolved
the way `Regex` resolves it - by immutability, not by locking - and the docs say so. A stale
paragraph on `FuzzyRegex` claiming the matching members still throw `NotImplementedException` was
removed while there.

### Review

The blind pass raised **two** findings, both with runnable reproductions and no prose, as briefed. I
reproduced **both** independently before changing anything; **both** were real and **both** are
fixed, and I then re-ran the reviewer's own two probes against the fixed tests and confirmed each now
fails as it should. Nothing was rejected. The reviewer additionally ran four checks that produced no
finding (structural probe, static probe, the reverted `Match` field, and five repeat runs of the
stress tests for flakiness) and confirmed `ArrayPool` appears nowhere in `src/` but `ByteStack`.

**No second blind pass was run, and here is the judgement.** The fixes touched test code only - the
snapshot ordering, a `salt` parameter and a `NonLazy` filter, all inside `ThreadSafetyTests.cs`,
which the reviewer had already read. No public API, no tooling, no `src/` change. What replaces a
second opinion is stronger than one: each fix was verified against the reviewer's own failing probe,
which is a reproduction rather than a judgement.

### Controls - how to re-run every one

All are test-based rather than oracle waves, so they take a source edit and one filtered test run
rather than a seed. Re-run command in each case:
`dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/<Class>/*"`.

**Control A, structural ratchet.** In `src/FuzzyRegex/Engine/Node.cs`, after
`    internal bool Match;` add `    internal int ProbeField;`. Result: `ThreadSafetyTests` **1 failed /
9**, `Every_field_reachable_from_a_compiled_pattern_is_write_once_or_allowlisted` reporting
`items {"Node.ProbeField"} are not part of the superset`. (The first draft of this note said 2 of 9,
which was wrong: that figure came from a run with controls A and B applied at the same time. The
verifier caught it.)

**Control B, static audit.** In `src/FuzzyRegex/Engine/PatternObject.cs`, after
`    internal bool DoSearchStart;` add `    private static int _probeStatic;`, and in `Compile` after
`        ArgumentNullException.ThrowIfNull(compiled);` add `        _probeStatic++;` followed by
`        if (_probeStatic < 0) { throw new InvalidOperationException("probe"); }` so the field is
both written and read. Result: `Every_static_field_in_the_library_is_readonly_or_const` fails with
`{"Fuzzy.Text.RegularExpressions.Engine.PatternObject._probeStatic"}`. **Worth knowing: the probe has
to be `private` and has to be read.** A non-private mutable static is refused by the analyzers at
build time (S2223), and an unwritten or unread private one by S1144/S4487/IDE0052 - so the build is
itself a first line of defence here, and the test covers the private-and-used case it cannot see.

**Control C, the defect this slice fixed.** In `src/FuzzyRegex/Match.cs` change
`    private System.Runtime.CompilerServices.StrongBox<FuzzyChanges>? _splitChanges;` back to
`    private FuzzyChanges? _splitChanges;`, and
`    public FuzzyChanges FuzzyChanges => (_splitChanges ??= new(SplitFuzzyChanges())).Value;` back to
`    public FuzzyChanges FuzzyChanges => _splitChanges ??= SplitFuzzyChanges();`. Two tests fail, for
two different reasons, which is why both exist:
`ThreadSafetyTests.Every_field_a_match_writes_after_construction_is_a_single_reference` fails
immediately and deterministically with `{"Match._splitChanges : Nullable`1"}`, and
`ThreadSafetyStressTests.One_match_can_be_read_from_many_threads_at_once` fails with
`{"a torn read: one of the three lists was null"}` - **on three runs out of three**, each about two
seconds.

**Control D, pool discipline.** In `src/FuzzyRegex/Engine/ByteStack.cs`'s `Dispose`, delete the line
`        _storage = [];`. Result: `PoolDisciplineTests` 2 failed / 6 -
`Disposing_a_stack_twice_does_not_return_its_buffer_twice` and
`Disposing_a_match_state_releases_every_one_of_its_stacks`.

**Control E, the instance snapshot (the blind review's finding 1).** In
`src/FuzzyRegex/Engine/MatchState.cs`, in `Create`, immediately before
`        state.Overlapped = overlapped;` add `        pattern.ReqString ??= new Node(0);`. Result:
`Matching_writes_nothing_reachable_from_a_compiled_pattern` fails. **This is the control that was
green before the review and is red after it** - it is the finding and its fix in one.

**Control F, the static snapshot (finding 2).** In `src/FuzzyRegex/Engine/MatchState.cs`, in the
body of `GroupData`, add
`    internal static readonly Dictionary<string, int> ProbeMemo = new(StringComparer.Ordinal);`, and
in `Create` immediately before `        state.Overlapped = overlapped;` add
`        GroupData.ProbeMemo[text] = text.Length;`. Result:
`Matching_writes_nothing_in_the_librarys_static_tables` fails. **Also green before the review fix and
red after**, and it is the salted second workload that makes it red - an unsalted one lets the memo
saturate after a single pass.

### The verifier, and a process lesson that cost real work

The independent verifier ran all six controls and both count claims. **Everything reproduced except
Control A's failure count**, which said 2 of 9 and is 1 of 9; the notes above are corrected and the
cause was a run with two probes applied at once. It also confirmed the ratchet numbers, the oracle's
three seeds, the per-class test counts, that `ArrayPool` appears in `src/` only inside `ByteStack`,
and that the forty-name allowlist is exactly the observed set (the two allowlist tests assert
subset in both directions, so together they pin equality).

**The lesson, and it is mine to own: never instruct a verifier to `git checkout -- <file>` while the
slice's own work is uncommitted.** My brief told it to restore tracked files that way after each
control. For Controls C and D the file being restored was one this slice had *modified but not
committed*, so the checkout discarded the slice's work rather than just the control edit. The
verifier noticed, reconstructed `Match.cs` from the doc XML of a build made before the damage, and
restored `ByteStack.cs` by re-editing. I checked both diffs line by line rather than taking its word:
`ByteStack.cs` is byte-identical to what I wrote, and `Match.cs` differs only in one `<see cref>`
having been written fully qualified, which I normalised back. **Next time: commit a checkpoint before
the verifier runs, or brief it to stash-and-restore instead of checkout.**

### For the next slice

- **These tests are PERMANENT and they are aimed at Phase 7.** A slice that adds a start
  optimisation, a required-string prefilter or any other cache reachable from a compiled pattern will
  turn Control A's test red on the day it lands. The answer is to remove the shared state or publish
  it as a single reference, never to add a line to the allowlist. `PatternObject.ReqString` and
  `DoSearchStart` already exist and are written only by the compiler; Phase 7 is where something will
  want to fill them lazily, and that is the moment to stop.
- **`ObjectGraph.cs` is reusable.** It walks instances rather than declared types and takes any root,
  so a future slice wanting "did this operation write anything reachable from X" has the instrument.
- **The workload gained two families the slice file did not list** - `recursion` and `repeat` -
  because `PatternObject`'s `CallRefInfoList` and `RepeatInfoList` stay empty unless a pattern calls a
  group or repeats one a bounded number of times, and a field the walk never reaches is a field the
  allowlist cannot pin.
- Carried items from S52's STATE are untouched by this slice and still open.
