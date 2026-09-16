# Optimisation techniques, judged against this engine

Research for Phase 7, gathered 2026-09-16. Every external claim carries the URL it came from and
the date it was fetched. **UNVERIFIED** marks anything no primary source confirmed.

The engine this is judged against: a bytecode-interpreting backtracking VM, `Matcher.cs` ~10.3k
lines, dispatching on `switch (node.Op)` over a graph of `sealed class Node` objects each holding
a `List<uint> Values`; per-match state in `MatchState` with three `ByteStack`s (the only
`ArrayPool` users in `src/`); Native AOT compatible (`IsAotCompatible=true`), so no
`Reflection.Emit` and no `Expression.Compile()`. Current usage in `src/FuzzyRegex`, counted
2026-09-16: `stackalloc` 18 sites, `ArrayPool` 5 (all in `ByteStack.cs`), `SearchValues` **0**,
`AggressiveInlining` **0**, `SkipLocalsInit` **0**, `AllowUnsafeBlocks` not set anywhere.

---

## 1. The techniques

### stackalloc
Allocates a block on the stack, exposed as `Span<T>` with no `unsafe` needed, reclaimed on return
with no GC involvement. **Pays** for small, short-lived scratch buffers in a method called
repeatedly - case-folding expansions, a few characters of lookahead. **Does not pay**, and is
actively dangerous, inside a loop: the docs say plainly *"Avoid using stackalloc inside loops.
Allocate the memory block outside a loop and reuse it inside the loop"*, because the stack is
about 1 MB per thread and an unbounded size is a `StackOverflowException` rather than an
exception you can catch. The content is undefined, unlike `new`. **AOT**: fine, pure IL.
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc (fetched
2026-09-16). *Here*: already the most-used technique in the engine (18 sites); the Phase 7 question
is not where to add it but whether any existing site is on a per-character path with a
size that depends on input.

### Span&lt;T&gt; / Memory&lt;T&gt;
`Span<T>` is a ref struct view over contiguous memory that avoids copying what it refers to;
`Memory<T>` is the heap-storable counterpart for when the buffer must outlive a stack frame.
**Pays** wherever a substring is taken only to be examined. **Does not pay** - cannot be used at
all - as a field of a normal class, across `await`/`yield`, or boxed, which is exactly the
constraint that blocks the obvious fix in this engine: the lazy walks (`EnumerateMatches`) yield,
so a `Span` cannot live across the yield, and `MatchState` is a class. **AOT**: fine.
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct
(fetched 2026-09-16). *Here*: `FuzzyRegex.cs:338` and `:873` copy the incoming
`ReadOnlySpan<char>` to a string because the engine indexes a `string`. OPTIMISATION-NOTES calls
this the *"Biggest allocation win available; decide early in Phase 7 because it touches
everything."* The `yield` constraint is the reason it is not a small change.

### CollectionsMarshal.AsSpan
Returns a `Span<T>` over a `List<T>`'s backing array, removing the indexer and enumerator from
the loop. **Pays** on a hot read-only loop over a list you own. **Does not pay** and is unsafe if
the list can change: *"Items should not be added or removed from the List&lt;T&gt; while the
Span&lt;T&gt; is in use"* - a resize silently invalidates the span, with no exception. **AOT**:
fine. https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal.asspan?view=net-10.0
(fetched 2026-09-16). *Here*: `Node.Values` is a `List<uint>` read on every set-membership test
(`Matcher.InSetUnion` and friends). It is appended to during compilation and frozen afterwards, so
the hazard is avoidable - but the better fix may be simpler: make it a `uint[]` once compilation
ends, which removes the indirection rather than working around it.

### string.Create
Allocates the final string once and hands you a mutable `Span<char>` to fill: *"the only heap
allocation that will occur"*. **Pays** when the final length is known and the alternative is
build-then-copy. **Does not pay** when the length is not known up front, or when the state needs
boxing to be passed in (use a ValueTuple or a custom struct). **AOT**: fine.
https://learn.microsoft.com/en-us/dotnet/api/system.string.create?view=net-8.0 (fetched
2026-09-16). *Here*: `Engine/Substitution.cs` builds replacement strings; a `Replace` over many
matches is the workload where this shows.

### ArrayPool&lt;T&gt;
A shared pool for renting and returning arrays, to cut GC pressure *"in situations where arrays
are created and destroyed frequently"*. **Pays** for growable scratch larger than a safe
`stackalloc`. **Does not pay** when the lifetime is unclear: `Rent` may return a larger array than
asked for (track the logical length yourself), a missing `Return` quietly defeats the pool, and
`Return(array, clearArray: true)` costs a full clear. **AOT**: fine.
https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1?view=net-10.0 (fetched
2026-09-16). *Here*: `ByteStack` already does this. The known hazard is written down in
OPTIMISATION-NOTES: a `MatchState` held across a `yield return` owns rented buffers that an
abandoned iterator never returns. Any pooling of `MatchState` must solve release-on-`Dispose`
first, or it trades allocation for a leak.

### Avoiding boxing
Boxing wraps a value type in a heap object. *"A single boxing operation takes maybe 10-20
nanoseconds and allocates 12-24 bytes"* - trivial once, ruinous per character. **Pays** to remove
wherever value types pass through `object`, a non-generic collection, or `params object[]`.
**Does not pay** to chase outside hot paths. **AOT**: no AOT-specific concern.
https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing
(fetched 2026-09-16). *Here*: the engine is `uint`/`int`/`long`-heavy with enum opcodes, so the
place to check is exception and message formatting on paths that can run per match, and any
`List<object>`-shaped API on the public surface.

### [MethodImpl(AggressiveInlining)]
Hints the JIT to inline at the call site. **Pays** on tiny, extremely hot helpers - and .NET's own
`RegexInterpreter` marks its operator-decode helper exactly this way. **Does not pay** when
applied broadly; the docs are blunt: *"Unnecessary use of this attribute can reduce performance.
The attribute might cause implementation limits to be encountered that will result in slower
generated code"*, and *"Always measure performance to ensure it's helpful to apply this
attribute."* Code bloat costs icache. **AOT**: fine, honoured by the AOT compiler.
https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.methodimploptions?view=net-9.0
(fetched 2026-09-16). *Here*: zero uses today. Candidates are the per-character predicates
(`MatchesCharacter`, `InSetUnion`) and `MatchState.NextPos`/`StepBy`. One at a time, each with a
number, because this is the technique most likely to make things slower while looking like an
optimisation.

### SIMD (Vector128/256/512, Vector&lt;T&gt;)
Process many elements per instruction, via portable `Vector<T>` or fixed-width
`Vector128/256/512<T>`. **Pays** for scanning a span for a candidate start position - which is
precisely what .NET's own find-optimisations do. **Does not pay** below some span length where
setup dominates, and always needs an `IsHardwareAccelerated`/`IsSupported` check with a scalar
fallback. **AOT**: fine, specialised per target.
https://learn.microsoft.com/en-us/dotnet/standard/simd (fetched 2026-09-16). *Here*: do not
hand-write SIMD. `SearchValues` and `IndexOf`/`IndexOfAny` are vectorised already, and the engine
already uses `IndexOfAnyInRange` (`MatchState.cs:501`). Hand-rolled SIMD is a last resort after
the library primitives are exhausted.

### Cache-friendly struct layout
`[StructLayout(LayoutKind.Auto)]` lets the CLR reorder fields to remove padding; `Sequential` and
`Explicit` pin it. A worked example: 12 bytes with 4 bytes padding under `Sequential` becomes 8
bytes with none under `Auto` - *"we have reduced its size by 33%"*. **Pays** when a struct is
instantiated in bulk or scanned sequentially. **Does not pay** on a handful of instances, and .NET
7+ already handles the common small-wrapper case, so measure before hand-tuning. **AOT**: fine.
https://www.meziantou.net/optimize-struct-performances-using-structlayout.htm (fetched
2026-09-16; MVP blog, used because no Learn page gives the concrete numbers). *Here*: `BestEntry`
already carries `LayoutKind.Auto`. The bigger layout question is not attributes but shape: a graph
of `sealed class Node` with two `NextNode` objects each means several pointer hops per opcode. A
flat array of node records indexed by `Node.Index` (which already exists, for the backtracking
stack) is the structural version of this technique, and it is a large, sync-cost-bearing change -
see the warning in section 4.

### [SkipLocalsInit]
Suppresses `.locals init`, so the JIT does not zero a method's locals. **Pays** on a hot method
with a `stackalloc` that is always fully written before being read. **Does not pay** anywhere the
buffer may be read before written: *"This attribute is unsafe, because it may reveal uninitialized
memory to the application in certain instances"*. It is inherited by nested lambdas and local
functions, so scope it narrowly. **AOT**: fine.
https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.skiplocalsinitattribute?view=net-10.0
(fetched 2026-09-16). *Here*: method-level only, on specific `stackalloc` sites, never at module
level. In an engine that indexes buffers by computed positions, a module-wide `SkipLocalsInit`
turns a latent bug into leaked memory contents.

### ref struct / ref fields / scoped
Stack-only structs that can hold `ref` fields pointing at data without copying; `scoped`
constrains escape lifetime. **Pays** for a `Span`-like cursor or a scratch builder that must never
reach the heap. **Does not pay** where the type must be an array element, a field of a class,
captured in a lambda, or live across `await`/`yield`. **AOT**: fine, compile-time only.
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct
(fetched 2026-09-16). *Here*: this is the shape of the answer OPTIMISATION-NOTES asks for at
`Iteration.cs:216`/`:336` - *"a pooled state released on Dispose, or a struct enumerator"*. A ref
struct enumerator solves the per-step-state cost without the abandoned-iterator leak, at the price
of not being an `IEnumerable<T>`. That is an API decision, not just an optimisation.

### SearchValues&lt;T&gt;
Precomputes a search-optimised representation of a fixed set of chars, bytes or strings; then
`Contains`/`IndexOfAny` beats `HashSet<T>.Contains` or `IndexOfAny(char[])`. *"SearchValues&lt;T&gt;
instances are optimized for situations where the same set of values is frequently used for
searching at run time."* **Pays** when the set is built once and searched many times - a compiled
pattern's leading character class is the textbook case. **Does not pay** for a one-off search,
where `Create` is pure overhead. **AOT**: fine, plain sealed BCL class.
https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1?view=net-10.0 (fetched
2026-09-16). *Here*: zero uses, and the highest-value gap in the list. A `PatternObject` is built
once and matched many times, which is exactly the shape `SearchValues` wants, and it is the
natural implementation of the deferred `search_start` prefilter family.

### unsafe / fixed / Unsafe.Add
Raw pointer arithmetic, pinning, and bounds-check-free reference advancement. **Pays** only at the
innermost indexing loop when profiling proves the JIT is not eliminating the bounds check.
**Does not pay** before that proof: `Unsafe.Add` warns that *"If elementOffset is a calculated
value rather than a hardcoded literal, callers should consider the possibility of integer
overflow"*, and pointers reintroduce every C memory hazard. **AOT**: fine.
https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe.add?view=net-7.0
(fetched 2026-09-16). *Here*: `AllowUnsafeBlocks` is not set in any project file, so this needs a
csproj change and the repo's analyser settings (`TreatWarningsAsErrors`, `AnalysisModeSecurity=All`)
reviewed. The `benchmark` skill already rules it last: *"only with a benchmark in the commit
message proving it earned its place."* Keep it there.

### ValueStringBuilder
dotnet/runtime's internal pattern: a `ref struct` builder starting from a caller-supplied
(usually `stackalloc`ed) `Span<char>`, falling back to `ArrayPool<char>.Shared` only when it
overflows. **Pays** when most results are short and a `StringBuilder` would dominate. **Does not
pay** where a ref struct cannot go - it *"cannot be used in a generic context"*, which is why the
BCL keeps it internal. **AOT**: fine.
https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Text/ValueStringBuilder.cs
(fetched 2026-09-16). *Here*: `Substitution.cs` is the only consumer. It is a copy-in-the-repo
pattern, not a dependency; if it lands, it lands as an internal file with its provenance in the
comment, the way this port records every other borrowing.

### switch dispatch vs delegate / function-pointer dispatch
How a bytecode VM gets from an opcode to its arm. **Pays**: .NET's own `RegexInterpreter` uses
`switch (_operator)` in `TryMatchAtCurrentPosition()`, with the decode helper `SetOperator`
marked `AggressiveInlining`, the input threaded as `ReadOnlySpan<char>`, and the track stack
mutated through `ref int` locals - no delegate table, no `stackalloc`.
https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/RegexInterpreter.cs
(fetched 2026-09-16). **Does not pay**: **UNVERIFIED** - no .NET-authoritative source was found
comparing switch against delegate or function-pointer dispatch for a C# interpreter. The common
claim that a dense switch compiles to a jump table roughly as fast as a function-pointer table is
a hypothesis here, not a fact. **AOT**: both fine; delegates too, as long as they are not
constructed by reflection. *Here*: this port already does what the BCL does, so the lesson is
"you are on the right structure" - and any proposal to replace the switch with a table needs its
own measurement before it is believed.

### RegexOptions.Compiled vs the source generator (the AOT constraint)
`Compiled` reflection-emits specialised IL; `[GeneratedRegex]` emits equivalent C# at compile
time. The source generator gives *"all the throughput performance benefits of RegexOptions.Compiled
(more, in fact) and the start-up benefits of not having to do all the regex parsing, analysis, and
compilation at runtime"*. `Compiled` *"is costly to construct"* and reflection-emit *"inhibits the
use of RegexOptions.Compiled in certain environments; some operating systems don't permit
dynamically generated code to be executed, and on such systems, Compiled becomes a no-op."*
**AOT**: `Compiled` is a silent no-op; the source generator is the correct route.
https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-source-generators
(fetched 2026-09-16). *Here*: this confirms ROADMAP's Phase 7 note, including the sibling trap
that `Expression.Compile()` silently falls back to the LINQ interpreter under AOT. Source-generated
patterns stay post-1.0 per spec section 11; nothing in this research changes that.

---

## 2. Summary table

| Technique | Use when | Avoid when | AOT-safe | Measure with |
|---|---|---|---|---|
| `stackalloc` | small fixed scratch, once per call | inside a loop; input-dependent size | yes | MemoryDiagnoser, time |
| `Span`/`Memory` | slicing without copying | across `yield`/`await`; as a class field | yes | MemoryDiagnoser |
| `CollectionsMarshal.AsSpan` | hot read loop over a frozen `List<T>` | list may resize while the span lives | yes | time; `--disasm` |
| `string.Create` | final length known | length unknown; state would box | yes | MemoryDiagnoser |
| `ArrayPool<T>` | growable scratch reused across calls | lifetime unclear (leak risk on abandon) | yes | MemoryDiagnoser |
| avoid boxing | value types crossing `object` in hot paths | cold paths | yes | MemoryDiagnoser |
| `AggressiveInlining` | tiny, provably hot helper | broadly; large bodies | yes | time, both JIT and AOT |
| SIMD | scanning for candidate positions | short spans; before trying `SearchValues` | yes | time, with a scalar control |
| struct layout | bulk-instantiated or scanned structs | a handful of instances | yes | time; struct size probe |
| `SkipLocalsInit` | hot method, buffer always written first | any read-before-write path; module scope | yes | time (small effect) |
| `ref struct`/`scoped` | stack-only cursor or enumerator | must be a field, boxed, or cross `yield` | yes | MemoryDiagnoser |
| `SearchValues<T>` | fixed set built once, searched often | one-off search | yes | time, on scan workloads |
| `unsafe`/`Unsafe.Add` | proven bounds-check cost, last resort | before the proof; `AllowUnsafeBlocks` unset | yes | `--disasm` + time |
| `ValueStringBuilder` | short results, hot formatting | generic contexts | yes | MemoryDiagnoser |
| switch dispatch | already what we and the BCL do | replacing it without a measurement | yes | time (both shapes) |
| `RegexOptions.Compiled` | never here | always - no-op under AOT | **no** | n/a |

---

## 3. The checklist for every optimisation slice

Run it in order. A step skipped is a result nobody has to believe.

**Before**
1. **Read the deferral, not just the code.** `grep -rn -i 'ponytail:\|Phase 7' src/FuzzyRegex
   --include=*.cs` and read the comment at the line. OPTIMISATION-NOTES is the index; the source is
   the record.
2. **Know the noise floor.** If this machine's floor has not been measured, measure it first
   (BENCHMARKING-METHOD section 4). A delta smaller than the floor is not a delta.
3. **Measure before.** Full suite, Release, `--job medium` or longer, `--exporters json`, into
   `artifacts/bench/<date>-<slice>-before`. Nothing else running - in particular, not the slice
   driver.
4. **Write the hypothesis down** before touching code: which workload, which direction, roughly
   how much, and *why* - which mechanism in section 1 does the work. "It should be faster" is not a
   hypothesis. A profile (PROFILING section 7) is how the hypothesis earns its place.

**Change**
5. **One technique per slice**, at the smallest site that tests the hypothesis. Two changes in one
   measurement means two unattributed numbers.
6. **Correctness first**: if the change can alter an answer (anything in the prefilter family),
   write the test that pins the answer before the change, and remember that the pinned answers in
   `BacktrackingVerbTests`, `PartialMatchingTests` and `ReverseMatchingTests` are **permanent** -
   a Phase 7 slice that turns one red has ported an upstream bug (ROADMAP, owner rule 2026-09-12).

**After**
7. **Measure after**, same machine, same job, same filter, same session if possible; compare with
   the same script both times. Report the per-workload ratio, never an average.
8. **Allocation delta**: report allocated bytes/op before and after alongside the timings. A
   speed win that allocates more is a trade to declare, not to hide.
9. **Ratchet GREEN**: `tools/check-ratchet.ps1`.
10. **Oracle GREEN at three seeds**, `ExpectedDivergences` strict: `tools/run-oracle.ps1`. An
    optimisation that changes an answer has ported a bug.
11. **AOT still green**: `tools/run-aot-tests.ps1` and `tools/run-aot-smoke.ps1`, and record the
    binary size against the 6,972,928-byte baseline. If the technique's benefit is JIT-dependent
    (inlining, layout, devirtualisation), **also record the benchmark under the `nativeaot`
    runtime** - a win that exists only under the JIT is half a win for a library that ships AOT.
12. **Ratchet the number**: update the committed baseline under `bench/baselines/<machine-id>/` so
    the next slice regresses against the new floor, not the old one.
13. **Record**: measured before-and-after in the commit message (house style, commit 58977bb);
    delete the `ponytail:`/`Phase 7` comment and its OPTIMISATION-NOTES row in the same commit;
    note any new divergence from upstream's structure in `docs/PORTMAP.md` with why, and drop the
    matching `.editorconfig` port relaxation if the code no longer needs it.
14. **If the number did not move**, revert. *"An optimisation with no measured win is just a bug
    you have not found yet"* (`benchmark` skill). Record the negative result in
    OPTIMISATION-NOTES so it is not re-attempted - S19's reverted fast path
    (`Matcher.cs:8894`) is the precedent.

---

## 4. Two standing cautions

**Divergence from upstream has a recurring cost.** Every structural change makes the next
`sync-upstream` more expensive. The `benchmark` skill already says an optimisation that moves the
port away from upstream's structure *"has to pay for itself"*. Flattening the `Node` graph or
replacing the dispatch switch is exactly that kind of change: plausible, and expensive forever.
Prefer the wins that keep the shape - `SearchValues` prefilters, allocation removal, a pooled or
struct enumerator - and take a structural change only with a number large enough to justify a
permanent sync tax, agreed with the owner first.

**Do not simplify away the constraints.** Not timeouts and cancellation (`SafeCheckCancel` is
polled once per 256 loop turns and is provably load-bearing - dropping it leaves all 17
cancellation cases running after two minutes), not the `ExpectedDivergences` strictness, not the
permanent pins. Those are the things an optimiser is most tempted to shave, and they are the ones
the owner's rules protect.
