# Two decisions for the owner: span threading, and the lazy walk's per-step state

S58 measures; it changes no engine code. This document is what the measurement is for. Both items
are deferrals the port has carried since Phase 2, both are named in `OPTIMISATION-NOTES.md` as
"biggest allocation win available" and "the largest number in this file", and both have an obvious
implementation that does not work. The numbers below say what they are worth; the analysis says what
each would cost; each section ends with a recommendation. **The owner decides. S61 implements
whichever shapes are signed off.**

Measurements: BenchmarkDotNet 0.15.8, `--job medium` (`IterationCount=15, LaunchCount=2,
WarmupCount=10`), Release, .NET 10, this machine, on a quiet machine on 2026-09-19. Every figure is
reproducible from the committed tree:

```
pwsh -File tools/compare-benchmarks.ps1 -Job medium -Filter '*SpanOverload*' -UseExisting:$false
pwsh -File tools/compare-benchmarks.ps1 -Job medium -Filter '*StateBenchmarks*'
```

Ratios are read against this machine's noise floor (`bench/baselines/<machine-id>/noise-floor.md`);
anything inside it is not a difference.

---

## 1. Span threading

### What the code does today

`FuzzyRegex.cs:461` and `:1117`:

```csharp
public bool IsMatch(ReadOnlySpan<char> input, ...) => IsMatch(input.ToString(), ...);
public int Count(ReadOnlySpan<char> input, ...) => Count(input.ToString(), ...);
```

The `ponytail:` comments beside both say what that costs: the overloads spare the caller a
conversion, not the allocation. A caller who already holds a span - a slice of a buffer, a parsed
field - pays one full copy of the subject per call.

### Why the obvious fix does not compile

`MatchState` is `internal sealed class MatchState : IDisposable` (`MatchState.cs:125`) and holds the
subject as `internal readonly string Text` (`:143`). A **class cannot have a `ReadOnlySpan<char>`
field, and cannot have a `ref char` field either**: ref fields exist only in `ref struct`s. So
"store the span on the state instead of the string" is not a smaller version of this change, it is
not a version of it at all.

That leaves two real shapes.

**(a) Make `MatchState` a `ref struct`.** Blocked, and not marginally. A state is held across a
`yield return` in `Iteration.cs` (`:216`, `:336`), a `ref struct` cannot be a field of the iterator
class the compiler generates, and the lazy walk is public API (`EnumerateMatches`,
`EnumerateSplits`, `Match.NextMatch`). It is also `IDisposable` and returns rented buffers.

**(b) Pass the subject as a parameter instead of storing it.** This is what upstream does - the C
`RE_State` holds a `void* text` and every `try_match_*` receives the state - and what
`System.Text.RegularExpressions` does, threading `ReadOnlySpan<char> inputSpan` down through its
matching methods. Concretely, here: 96 methods in `Matcher.cs` take a `MatchState state` parameter
today, and each would gain a `scoped ReadOnlySpan<char> text`, as would `CharacterIndex`,
`MatchState`'s own character readers (`CharAt`, `NextPos`, `PrevPos`, the surrogate pairing at
`:667-711`) and `Substitution`. `MatchState.Text` becomes `TextLength` plus whatever the callers
pass.

### What it could not buy, whatever we do

Only the entry points that do not return a `Match` can ever be span-native. `Capture._subject` is a
`string` field and `Capture.Value` is `_subject[_start.._end]` (`Match.cs:26`, `:48`), so a
`Match`-returning span overload has to materialise a string regardless; there is nowhere else for
`Value` to come from short of making the whole `Match` family generic over a buffer lifetime, which
is a different library. **So the ceiling on this work is exactly the two overloads that exist:
`IsMatch` and `Count`.** (Section 2's recommendation is independent of this one and can be taken
on its own.) That is worth saying plainly, because "the biggest allocation win
available" reads like it applies to the whole API and it does not.

### The measurement

`SpanOverloadBenchmarks`. The pattern is `new FuzzyRegex("the quick")`, which matches at index 0 of
every corpus subject, so the engine work either side of the copy is as near to nothing as a real
public call can be and the string/span pairs differ by the copy and by nothing else. A full-scan
pattern would bury the copy under the scan - which is how this measurement gets taken and reported
as "free".

Medians from run A, `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json`:

| Benchmark | Median | Allocated | What the span overload adds |
|---|---:|---:|---|
| `StringShort` | 138.00 ns | 904 B | |
| `SpanShort` | 148.31 ns | 1,056 B | +10.3 ns, +152 B |
| `StringKilobyte` | 168.11 ns | 904 B | |
| `SpanKilobyte` | 248.55 ns | 3,088 B | +80.4 ns, +2,184 B |
| `StringMegabyte` | 34,478.0 ns | 904 B | |
| `SpanMegabyte` | 332,567.8 ns | 2,098,456 B | +298,090 ns (**9.6x**), +2,097,552 B |
| `CountStringMegabyte` | 14,052,282.8 ns | 760 B | |
| `CountSpanMegabyte` | 14,958,806.3 ns | 2,098,263 B | +906,523 ns (**+6.5%**), +2,097,503 B |

Read off those:

- **Allocation: two bytes per character, exactly.** (2,098,456 - 904) / 1,048,576 = 2.0002. At a
  megabyte that is a Large Object Heap allocation on every call, and the ratio against the string
  overload is 2,321x.
- **Time: cache-dependent, not a constant per byte.** 0.075 ns/char at a kilobyte (the copy is in
  L1) against 0.284 ns/char at a megabyte - about 27 GB/s against 7 GB/s.
- **How big it looks depends entirely on how much matching the call does.** On `IsMatch` with a
  pattern that hits at index 0, the copy is 9.6x the whole call. On `Count` over the same subject,
  where the scan is real, the same copy is 6.5%. Both numbers are honest and they differ by 150x;
  quoting either alone would mislead, which is why the suite measures both.
- `CountSpanMegabyte` carried BenchmarkDotNet's multimodality warning in run A, so its 6.5% is
  worth about one significant figure.

### What the deferral got wrong, and it matters

Both `ponytail:` comments say the lift "touches every opcode" and means "threading a
ReadOnlySpan through MatchState and every `try_match_*`". **That is not what the code says today.**
`Text[` appears at five sites in the whole of `src/FuzzyRegex`, all inside `MatchState`; the engine
reads the subject through one accessor, `MatchState.CharAt` (`:665`), which `Matcher.cs` calls 37
times and never indexes around. The plumbing is small.

What blocks the change is not the plumbing, it is the type system, and no amount of plumbing moves
it: a sealed class cannot hold a `ReadOnlySpan<char>` or a `ref char`, and `ReadOnlyMemory<char>`
cannot be made from a `ReadOnlySpan<char>` at all. **A span the caller passes cannot be stored
anywhere that outlives the call frame, and that is the whole of the problem.**

### The three shapes, then

**(a) `MatchState.Text` becomes `ReadOnlyMemory<char>`, plus new `ReadOnlyMemory<char>` overloads.**
Safe, small - the five indexing sites and whatever hands the subject out. It gives zero copy to a
caller who can produce a `Memory` (a `string`, an array, a pooled buffer: most of them). It gives
**nothing** to a caller holding a span, whose overload must still copy. Risk to measure before
adopting: `CharAt` would go through `Text.Span`, which is a few instructions on the hottest path in
the engine, 37 call sites deep. That has to be benchmarked, not assumed.

**(b) Pin the span and hold `char*` + length, under `AllowUnsafeBlocks`.** The only shape that makes
the *span* overloads allocation-free, and structurally it is the closest to upstream, whose
`RE_State` holds a `void* text`. Three costs: `AllowUnsafeBlocks` is currently unset and
`OPTIMISATION-TECHNIQUES` puts `unsafe` at "last resort, after the proof"; it works only where the
state cannot escape the `fixed` block, so it is confined to `IsMatch` and `Count`; and `IsMatch`
today is `Run(...).Success`, which builds a `Match` holding the subject **as a string**
(`Match.cs:26`), so that path needs re-plumbing to a bool before the pin buys anything.

**(c) Do nothing, and say so in the API documentation.** The overloads stay a convenience over
`.ToString()` at the call site, and the XML doc says plainly that they allocate a copy.

### Recommendation

**(c) now, (a) in S61 if the `Text.Span` measurement comes out flat; not (b).**

The reasoning is the 6.5% figure. On any call that does real matching work the copy is a few per
cent of the time - and time is what a user notices. The 2 MB per call is the real cost, and it is
paid only by a caller passing a megabyte span, who is also the caller most likely to be able to hand
over a `ReadOnlyMemory` instead. So (a) serves that caller safely, and (b) buys the remaining case -
a caller who has a span and cannot produce a memory - at the price of turning on `unsafe` in a
library whose selling points include being AOT-safe and boring. That trade needs a user asking for
it, not a benchmark suggesting it.

What must not survive this document either way is the claim in the deferral comments and in
`OPTIMISATION-NOTES.md` that this is "the biggest allocation win available" and that it "touches
every opcode". It is bounded to two methods, because `Capture.Value` needs a string, and the
plumbing is five sites plus one accessor.

---

## 2. The lazy walk's per-step state

### What the code does today

`Iteration.cs:216` and `:336`: one `MatchState` per step, not one per walk. Deliberate, and the
comment says why - a state owns rented buffers, and a state held across a `yield return` is one an
abandoned iterator never returns. The consequence is in `OPTIMISATION-NOTES.md` and it is the
largest number there: a full lazy walk of `\w+` over 1 MB costs **12,643 ms against 111 ms** for
the eager `Matches`, and the cost is quadratic (10.2x the subject cost 108x the time).

### The measurement, and the thing it changes

`GroupCountStateBenchmarks` and `SubjectLengthStateBenchmarks` split the per-state cost into the
part the pattern sizes and the part the subject sizes, because a single figure for "a state" cannot
be projected onto a different pattern or a different subject.

Medians from the same run A baseline:

| Groups | Median | Allocated | | Subject length | Median | Allocated |
|---:|---:|---:|---|---:|---:|---:|
| 1 | 273.76 ns | 1,296 B | | 64 | 274.78 ns | 1,296 B |
| 2 | 324.97 ns | 1,560 B | | 1,024 | 309.79 ns | 1,296 B |
| 4 | 452.74 ns | 2,088 B | | 16,384 | 697.27 ns | 1,296 B |
| 8 | 647.14 ns | 3,144 B | | 262,144 | 8,686.87 ns | 1,296 B |
| 16 | 1,030.27 ns | 5,256 B | | | | |
| 32 | 1,821.43 ns | 9,480 B | | | | |

The group-count side is exactly linear, to the byte: **1,032 bytes fixed plus 264 bytes per
capture group** reproduces all six rows with no residual (1,032 + 264x32 = 9,480), and time is
about 50 ns per group.

**The subject-length side is the finding.** Allocation is flat at 1,296 bytes from 64 characters to
262,144 while time goes 274.78 ns to 8,686.87 ns - a slope of **0.0321 ns per character**, about 62
GB/s, which is the shape of a vectorised scan and not of an allocation.

One caveat stated rather than glossed: `MemoryDiagnoser` measures managed GC allocation, so a warm
`ArrayPool` rental is invisible to it. "Flat" here means "not GC-allocating per call", not "not
using memory". It does not weaken the finding, because the finding is about the time slope.

### How much of that 1,296 bytes is the state, measured

Added 2026-09-19, after the tables above were written, because step 2 of the recommendation below
turns on it and the sweeps alone cannot answer it: a benchmark cannot call `MatchState.Create`,
which is `internal`. `bench/FuzzyRegex.Benchmarks/Attribution.cs` can, and
`dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- attribution` measures the same two
sweeps at three levels - `Create` alone, `IsMatch`, and `Match`:

| Groups | `Create` | `Match` | state's share |
|---:|---:|---:|---:|
| 1 | 1,024 B | 1,392 B | 74% |
| 32 | 2,264 B | 9,576 B | 24% |

The `Match` column is 1,392 B where the tables above say 1,296 B: the probe's absolutes sit exactly
96 B above `MemoryDiagnoser`'s at every group count, a constant that is measured and not explained
(`phase7-research/profiles/README.md`). Every share here is taken inside the probe's own numbers,
so the offset cancels; against the 1,296 B figure the one-group share would read 79% instead of
74%, and no step of the recommendation turns on which of the two is quoted.

Per group the 264 B splits **40 B in the state** (`MatchState.cs:544-548`: an array slot plus one
`GroupData`) and **224 B outside it**, at all five steps of the sweep with no residual. The state's
own line is **984 B fixed plus 40 B per group** - that intercept fits all six measured rows
exactly, and a one-group state's 1,024 B is 984 + 40, not a fixed 1,024 with the first group free.
So the state is most of a one-group match and a quarter of a 32-group one, and **pooling the state
removes 984 B plus 40 B per group, not the whole 264**. The profiles note
(`phase7-research/profiles/README.md`) has the full run and the `IsMatch` finding that came with it.

### Where the 0.0321 ns per character comes from

`MatchState.cs:501`, in the constructor:

```csharp
OneUnitPerCharacter = text.AsSpan().IndexOfAnyInRange('\uD800', '\uDBFF') < 0;
```

One vectorised pass over the whole subject, per state, to learn a single bool - and that bool is a
pure function of the subject. It is not waste: the comment at `:471-477` says it "buys back the
per-position walks in the repeat opcodes' backtrack arms that made a lazy scan quadratic", and it
does. But it is computed **per step** in a lazy walk, where it is the same answer every time.

That is the quadratic term, and the arithmetic closes:

- Per step over a 1 MB subject: 0.0321 ns x 1,048,576 = **33.6 microseconds**, against roughly 275 ns
  of fixed per-state cost. The subject pass is over **100x** everything else a state does.
- `OPTIMISATION-NOTES` records the consequence measured by S54: a full lazy walk of `\w+` costs
  12,643 ms over 1 MB against 111 ms eager, and 10.2x the subject cost 108x the time. A per-step
  O(n) term predicts exactly that squaring (10.2 squared is 104), and the per-step figure above
  accounts for the bulk of the 12,643 ms at the match counts that subject produces.

### The two shapes

**(a) Pool the state and release it on `Dispose`.** Keeps `IEnumerable<Match>`, keeps every existing
call site, and solves the abandoned-iterator problem the honest way: `foreach` compiles to a
`try/finally` that disposes the enumerator even on `break` or an exception, so the rental comes back.
The cost is that the contract becomes real - an enumerator obtained by hand and dropped without
`Dispose` leaks a rental until the finalizer or not at all.

**(b) A `ref struct` enumerator.** No rental lifetime problem at all, because the state never
outlives the stack frame. The API consequence is the whole of the decision: **a `ref struct`
enumerator is not an `IEnumerable<T>`.** It cannot be LINQ'd, cannot be stored in a field, cannot
cross an `async` boundary, and cannot be returned from an iterator method. `EnumerateMatches` and
`EnumerateSplits` are shipped public API (`PublicAPI.Shipped.txt`), so this is a breaking change
unless it is added alongside.

**And the measurement says neither one fixes the problem.** Both (a) and (b) address the *fixed*
per-step cost: 1,296 bytes and about 275 ns. Neither touches the 33.6 microseconds per step that
the subject pass costs, because both still build a state per step and a state's constructor scans
the subject. On the 1 MB `\w+` walk, pooling removes on the order of a tenth of a second from
12,643 ms. Anything that claims a lazy walk stops being quadratic because the state is pooled is
wrong, and this document exists partly to stop that claim being made.

### The change that does fix it

**Hoist the subject-derived, pattern-independent work out of the per-step state.**
`OneUnitPerCharacter` is a function of the subject alone, so a walk computes it once and every step
after the first is handed the answer - a state-creation overload taking the known flag, and one
field on the iterator. No public API changes, no lifetime contract, no `ref struct`. That deletes
the whole quadratic term; what remains per step is the 1,296 bytes and the ~275 ns, which is what
(a) and (b) are actually for.

Two things to check while implementing, neither of which changes the shape of the recommendation:
`GetCharacterIndex()` builds a subject-sized table lazily (`:488`) and is the second per-subject
value a walk should carry; and the slice must confirm no walk ever changes the subject under the
cached flag - `Iteration` walks one subject, so this should be a read of the code, not a hope.

### Recommendation

**In this order, and stop when the number stops moving.**

1. **Hoist the per-subject work out of the per-step state.** Small, safe, no API consequence, and it
   is where about 99% of the 12,643 ms lives. S61 measures the walk before and after against the
   committed baseline; `EnumerateMatchesToEndDense` is the row to read.
2. **Then, if allocation still shows up, pool the state and release it on `Dispose`** - shape (a).
   It is worth removing once it is no longer hiding behind 33.6 microseconds, and `foreach`'s
   `try/finally` makes the contract real for every normal caller. Size it honestly first: pooling
   removes the state, which the attribution above measures at 984 B a step plus 40 B per group -
   74% of a one-group match's bytes but 24% of a 32-group one - and not the other 224 B per group,
   which is the capture copy and lives outside the state.
3. **Not the `ref struct` enumerator.** It buys the same fixed cost as pooling and costs
   `IEnumerable<T>`: no LINQ, no field, no `async`, and either a breaking change to shipped API or a
   second parallel enumeration surface to document and test forever. That is a large permanent price
   for the smaller half of a cost that step 1 has already made small.

The owner signs step 1 and step 2 off separately; step 3 is a recommendation to decline.

---

## What S58 did not do

Neither shape is implemented here, by the slice's own scope: this is a measurement slice and
`src/` is untouched, which the green ratchet and the oracle at three seeds prove. The `ponytail:`
comments and the `OPTIMISATION-NOTES.md` rows stay until S61 deletes them with the implementation.
