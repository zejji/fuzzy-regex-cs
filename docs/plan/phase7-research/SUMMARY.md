# Phase 7 research: the short version (2026-09-16)
## Recommended method
1. **Measure this machine's noise floor first** - run the unchanged Release build twice and compare.
   The largest per-workload ratio is the threshold below which no Phase 7 claim counts; it costs no
   code change and makes every later number defensible.
2. **BenchmarkDotNet, `--job medium` or longer, never `--job short`** for a decision (short gave a 21%
   error bar on the existing backtracking benchmark); `[MemoryDiagnoser]` always; full JSON archived
   per slice and compared per workload, never averaged.
3. **pyperf on the same machine and session**, `pyperf check` run on every baseline and its verdict
   recorded; `pyperf system tune` has no Windows procedure in its docs, so both sides are quieted by
   the same means and nothing else.
4. **CPU profile**: `-p EP` on the benchmark run (EventPipeProfiler, no admin), read as text with
   `dotnet-trace report <trace> topN -n 30 [--inclusive]` - both pieces already installed here and
   verified working today. Escalate to dotTrace CLI + the Rider MCP call tree when topN is too coarse.
5. **Allocation**: `MemoryDiagnoser` for the number, dotTrace Timeline with `filterEvent=memory` for
   attribution; dotMemory has no text export, so it stays the owner's tool.
## The three most promising targets
1. **The prefilter family** (`Matcher.cs:197`, `:388`, `:4718`, `:7149-7583`) - algorithmic, not
   micro: it skips positions instead of speeding the loop, six arms are ported and waiting, and
   `SearchValues<T>` plus vectorised `IndexOf` are its natural implementation - the swap .NET's own
   engine made. Zero `SearchValues` in `src/` today; the permanent pins must stay green.
2. **Per-match allocation** - the span overloads that copy to a string (`FuzzyRegex.cs:338`, `:873`)
   and one `MatchState` per step in the lazy walks (`Iteration.cs:216`, `:336`); each state costs a
   `GroupData[]` plus an object per group, a `RepeatData[]` plus one per repeat, three `ByteStack`s,
   two `long[]`s and a vectorised subject pass. A rented buffer held across a `yield` is one an
   abandoned iterator never returns, so the API decision comes first.
3. **The per-character inner loop** - `Node.Values` is a `List<uint>` read on every set test, and
   `src/` has zero `AggressiveInlining` and zero `SkipLocalsInit`. Cheap and shape-preserving: freeze
   `Values` to a `uint[]`, inline the hottest predicates one at a time. Flattening the node graph is
   the expensive version - a permanent sync tax needing its own number.
## Owner decisions needed
- **The span question, early** - threading `ReadOnlySpan<char>` through `MatchState` touches
  everything and interacts with the lazy walks.
- **Lazy-walk API shape** - pooled state released on `Dispose`, or a ref struct enumerator that is
  not `IEnumerable<T>`; it changes the public surface.
- **Structural divergence budget** - is flattening the node graph or replacing the dispatch switch on
  the table at all, and above what measured win? Every such change taxes `sync-upstream` forever.
- **AOT policy** - both JIT and `nativeaot` for every optimisation, or only JIT-dependent ones? And
  confirm `unsafe` stays off (`AllowUnsafeBlocks` is set nowhere) through 1.0.
