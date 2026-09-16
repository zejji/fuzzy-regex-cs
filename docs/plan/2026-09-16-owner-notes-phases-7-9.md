# Owner's notes for Phases 7-9, checked and assessed (2026-09-16)

Source: the owner's notes of 2026-09-14, handed over 2026-09-16 with a note that they were only initial thoughts needing checking, assessing and augmenting. Each point below carries a verdict: **holds**
(already in the plan or confirmed), **adopt** (new, taken into the plan), **differs** (the plan
says otherwise; owner decision recorded or pending). Evidence for the technical claims is in
`phase7-research/` (Opus research, 2026-09-16, every external claim dated and linked).

## Phase 7: optimisation

| Owner's point | Verdict | Where it lands |
|---|---|---|
| Goal: speed at least at parity with upstream, not always achievable | **holds** | The v1.0 gate (spec section 11, `benchmark` skill): every workload's median at or below Python `regex`'s, at most 10% slower and none above 1.25x. Per workload, never an average. |
| Goal: minimise memory and allocations, or at least bound them | **adopt** | No allocation gate existed. S54's baselines record `MemoryDiagnoser` bytes per operation; `tools/compare-benchmarks.ps1` reports the allocation ratio beside the time ratio and fails on regression. A per-match allocation budget is set once the noise floor is known. |
| First step: decide how to benchmark and profile before changing anything | **holds, sharpened** | S58 (first Phase 7 slice) is measurement only: noise floor, pyperf check, profile route proven end to end, no engine change. Method in `phase7-research/BENCHMARKING-METHOD.md`. |
| Every change accurately benchmarked or it is not trusted | **holds, sharpened** | Per-slice checklist in `OPTIMISATION-TECHNIQUES.md` (14 steps): same machine, `--job medium` or longer, full JSON archived, before/after with the noise-floor threshold, oracle green at three seeds, AOT publish green, ratchet. `--job short` is banned for decisions (21% error bar measured on the existing backtracking benchmark). |
| BenchmarkDotNet is not a silver bullet | **holds** | Its own defaults were read from source; the method adds what BDN does not do: a measured noise floor, per-workload comparison, explicit thresholds. |
| Benchmark upstream reliably and compare | **holds, gap found** | pyperf is the tool (skill) but **is not installed**; `pyperf system tune` has no Windows procedure, so both sides are quieted by the same means and `pyperf check` gates every recorded baseline. S58 installs and records `pyperf system show`. |
| Profiling to find hot paths; research .NET options incl. JetBrains tools | **adopt** | `phase7-research/PROFILING.md`. Recommended CPU route: BDN `EventPipeProfiler` (`-p EP`) read as text with `dotnet-trace report topN` (both installed and verified 2026-09-16). Allocation attribution: dotTrace CLI snapshot read through the Rider MCP call tree (`filterEvent=memory`). dotMemory has no text export, so it stays an interactive tool for the owner. Profile on the JIT, verify wins on AOT (Microsoft rates AOT CPU profiling "partially supported"). |
| Technique list: stackalloc, Span/Memory, CollectionsMarshal.AsSpan, string.Create, ArrayPool, no boxing, inlining, SIMD, cache-friendly layout, SkipLocalsInit, ref structs, SearchValues, unsafe, ValueStringBuilder | **holds, augmented** | Sixteen techniques documented in `OPTIMISATION-TECHNIQUES.md` with when-it-pays, when-not, AOT status, dated reference. Spec order stands: allocations, `SearchValues` prefilters, layout and dispatch, `unsafe` last (`AllowUnsafeBlocks` is set nowhere; keep it so through 1.0 unless a measured case demands it). Zero `SearchValues`, zero `AggressiveInlining`, zero `SkipLocalsInit` in `src/` today. |
| Reference libraries: FuzzySharp, Cysharp, ValueStringBuilder | **holds** | Cited in the techniques file. The closest precedent is .NET's own `System.Text.RegularExpressions`: `RegexFindOptimizations` and `SearchValues` are exactly the prefilter swap this engine has waiting. |
| Detailed notes, summary table, subagent checklist | **adopt** | The three research files are the notes; the table and checklist are in `OPTIMISATION-TECHNIQUES.md`. The checklist becomes `.claude/skills/optimise/SKILL.md` when S58 lands, so every Phase 7 session loads it. |
| Tests, including manual or explicit ones if the suite would slow | **holds** | Oracle at three seeds and the permanent pins are the correctness net; slow cases go under an explicit category the ratchet skips, as `OptimiserTrapsTests` (S54) already plans. |
| See OPTIMISATION-NOTES.md | **holds** | It is the index; the research names its three best targets: the prefilter family, per-match allocation (span copy, one `MatchState` per lazy step), the per-character loop (`Node.Values` as `List<uint>`). |

### Decisions the owner still has to make (from the research)

1. Span threading through `MatchState`: opening design slice, or later? It touches everything
   and a `Span` cannot cross a `yield`, so it interacts with the lazy walks.
2. Lazy-walk shape: pooled state released on `Dispose`, or a ref struct enumerator that is not
   `IEnumerable<T>`? Public surface, so not a slice's call.
3. Structural divergence budget: may Phase 7 flatten the node graph or replace the dispatch
   switch at all, and above what measured win? Each such change taxes `sync-upstream` forever.
4. AOT measurement policy: both JIT and `nativeaot` for every optimisation, or JIT with an AOT
   verification only where the mechanism is JIT-dependent (the research's recommendation)?

## Phase 8: documentation

The owner's notes did not cover Phase 8; the plan is `2026-09-16-llm-friendly-docs-research.md`
(Do: `<remarks>` divergence notes, `docs/COMPARISON.md`, README, convention test, snupkg;
Consider: Context7, DeepWiki; Skip: llms.txt). Runs in the `docs` worktree, Junie-driven
(Sonnet, medium), one file per task, every diff reviewed before merge; the `<remarks>` pass runs
last, after Phase 7, so it does not collide with `src/` edits.

## Phase 9: demo web app

| Owner's point | Verdict | Where it lands |
|---|---|---|
| Whether Blazor would be best for ease of including the WASM-compiled C# library | **differs; owner decided 2026-09-16: keep the roadmap design** | ROADMAP (2026-08-31) chose Vue 3 + a Web Worker hosting the .NET WebAssembly runtime, and rejected Blazor: the worker is the safety design (a runaway pattern is `terminate()`d, the page never freezes), Blazor adds Components assemblies and boot machinery for a three-input UI, and Microsoft's Blazor-on-a-worker sample needs more files than the plain route. Blazor buys nothing for including the library: the worker hosts the runtime directly via `[JSExport]`. Owner confirmed. |
| Elegant front end, researched UI/UX practice, quality libraries where apt | **adopt for v2** | v1 is the roadmap's deliberately small page. v2 adds design polish; regex101 is the reference the owner named. |
| All major features experimentable; editable samples per feature | **adopt, staged** | v1: three inputs, highlighted spans, group table, about eight worked examples, URL-fragment sharing. v2: a sample per major feature (fuzzy budgets, BESTMATCH/ENHANCEMATCH, named lists, POSIX, partial, reverse, timeouts), all editable. |
| Contextual help; an interactive documentation section linked from the app | **adopt for v2** | Generated from the same `docs/COMPARISON.md` sections Phase 8 writes, so one source. |
| Timeouts and error handling against catastrophic errors | **holds** | Worker termination plus a warm spare, `MatchTimeout` as defence in depth, subject length and match count capped (ROADMAP). |
| As small and optimised as possible | **holds** | The WASM publish is a trimming and AOT proof (ROADMAP); size baseline recorded at S53b and re-measured after Phase 7. |
| Build in stages; 1.0 need not wait for every demo feature | **holds** | ROADMAP puts the demo after 1.0 unless a working link is wanted *at* 1.0; the staging above lets v1 ship with 1.0 if the owner wants it. |

Prerequisite (owner action, elevated prompt): `dotnet workload install wasm-tools wasm-experimental`.
No workloads are installed today (`dotnet workload list`, 2026-09-16).

## Running the three streams in parallel

Decided 2026-09-16: Phase 7 (optimisation), Phase 8 (docs) and Phase 9 (demo) run in parallel
worktrees under `.claude/worktrees/` as soon as S53b lands, while main finishes S54-S57.
Mechanisms: the public API is frozen by `Microsoft.CodeAnalysis.PublicApiAnalyzers`
(`PublicAPI.Unshipped.txt` is the change log the other streams read); the driver takes `-Phase`
so a worktree driver skips main's remaining Phase 6 slices; the docs stream treats
`DIVERGENCES.md`, `PORTMAP.md` and `src/` as read-only until the final `<remarks>` pass.
Phase 7 starts after S54 (it needs the baselines) and rebases onto main at the start of every
slice; S56's engine fixes are small and merge under it.
