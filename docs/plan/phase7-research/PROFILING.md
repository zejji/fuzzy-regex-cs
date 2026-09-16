# Profiling this engine on Windows, without a GUI

Research for Phase 7, gathered 2026-09-16. External claims carry a URL and the date fetched;
claims verified by running something on this machine say so and show the output. **UNVERIFIED**
marks anything that could not be confirmed from a primary source.

The constraint that decides everything below: **an agent can only act on a profile it can read as
text.** A `.dtp` snapshot that only opens in a GUI is, for an unattended slice, no profile at all.
So each option is judged on three things - can it be captured non-interactively, does it need
admin, and does a hot-path listing come out as text.

---

## 0. What is already installed on this machine (verified 2026-09-16)

`dotnet tool list --global`:

```
dotnet-dump      9.0.661903
dotnet-gcdump    9.0.661903
dotnet-stack     9.0.661903
dotnet-trace     9.0.661903
jetbrains.resharper.globaltools 2026.1.2   (jb - InspectCode etc., NOT a profiler)
```

Not installed / not on PATH: PerfView, `dottrace`, `dotmemory`, pyperf.

BenchmarkDotNet's diagnostic dependencies are **already restored** into the benchmark project's
output (`bench/FuzzyRegex.Benchmarks/bin/.../`): `Microsoft.Diagnostics.Tracing.TraceEvent.dll`,
`Microsoft.Diagnostics.NETCore.Client.dll`, `Iced.dll`, `Gee.External.Capstone.dll`,
`KernelTraceControl.dll`. So EtwProfiler/EventPipeProfiler/DisassemblyDiagnoser are a one-attribute
change away, not a packaging exercise.

Two text-report commands **verified by running them here on 2026-09-16**:

```
$ dotnet-trace report --help
  Generates a report into stdout from a previously generated trace.
  Commands: topN   Finds the top N methods that have been on the callstack the longest.

$ dotnet-trace report topN --help
  -n, --number     Gives the top N methods on the callstack. [default: 5]
  --inclusive      Output the top N methods based on inclusive time. If not specified,
                   exclusive time is used by default.

$ dotnet-gcdump report --help
  Generate report into stdout from a previously generated gcdump or from a running process.
  -t, --report-type <HeapStat>   [default: HeapStat]
```

Those two are the backbone of the recommendation, because they were not taken on trust.

---

## 1. BenchmarkDotNet diagnosers (capture attached to the benchmark itself)

| Diagnoser | What it gives | Windows/admin | Text out |
|---|---|---|---|
| `[MemoryDiagnoser]` | allocated bytes/op, Gen0/1/2 per 1000 ops | any, no admin | yes, in the results table and JSON |
| `[EventPipeProfiler(EventPipeProfile.CpuSampling)]` | CPU sample trace | cross-platform, **no admin** | `.speedscope.json` + `.nettrace` |
| `[EtwProfiler]` | ETW kernel session, native memory columns | Windows, **admin** | `.etl`, needs PerfView to read |
| `[DisassemblyDiagnoser]` / `--disasm` | ASM/IL/C# for the benchmarked methods | .NET Core disassembler is Windows-only | yes, a report file |
| `[ThreadingDiagnoser]` | lock contention, completed work items | .NET Core 3.0+ | yes |
| `[NativeMemoryProfiler]` | native allocations/leaks | Windows, **admin** (ETW) | via PerfView |

`EventPipeProfiler` profiles are CpuSampling (default), GcVerbose, GcCollect, Jit; it writes both
`.speedscope.json` and `.nettrace` into `BenchmarkDotNet.Artifacts`, named
`<Namespace.Class.Method>-<timestamp>.speedscope.json`, and is selectable on the command line as
`-p EP` / `--profiler EP` (https://benchmarkdotnet.org/articles/features/event-pipe-profiler.html,
fetched 2026-09-16). The rest of the table is from
https://benchmarkdotnet.org/articles/configs/diagnosers.html (fetched 2026-09-16).

**Why this matters more than it looks**: the profile then covers *exactly the workload iterations
BDN measured*, in the same process configuration, rather than a separately-launched run that may
differ. The measurement and the explanation come from the same event.

**UNVERIFIED**: whether `DisassemblyDiagnoser` works against a Native AOT job. No fetched source
addresses it either way. Probe it before a slice depends on reading AOT codegen.

---

## 2. dotnet-trace (+ dotnet-counters, dotnet-gcdump)

Install: `dotnet tool install --global dotnet-trace` (already present here)
(https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace, fetched 2026-09-16).

**Important change**: the `--profile cpu-sampling` name has been **removed** - the name was
misleading because it sampled all threads, not only CPU-busy ones. The current default when no
profile or providers are given is `dotnet-common` + `dotnet-sampled-thread-time`; the old
behaviour is reproduced with
`--profile dotnet-sampled-thread-time,dotnet-common` (same URL, fetched 2026-09-16). Any Phase 7
script that copies an older recipe will silently use a different profile, so write the profile out
explicitly.

```powershell
# Launch the benchmark under a trace, non-interactively.
dotnet-trace collect --duration 00:00:00:30 -o artifacts/prof/run.nettrace `
  -- dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*FuzzyScan*' --job medium

# Read the hot paths as TEXT (verified locally 2026-09-16).
dotnet-trace report artifacts/prof/run.nettrace topN -n 30 --inclusive
dotnet-trace report artifacts/prof/run.nettrace topN -n 30            # exclusive (self) time

# Or convert for a flame view a human can open.
dotnet-trace convert artifacts/prof/run.nettrace --format Speedscope -o artifacts/prof/run
```

Flags confirmed at the Learn page above (fetched 2026-09-16): `--duration dd:hh:mm:ss`,
`-o/--output`, `--format Chromium|NetTrace|Speedscope`, `-- <command>` to launch a child (.NET 5+
targets), `--show-child-io`, `dotnet-trace ps`, `dotnet-trace list-profiles`.

Allocation and heap:

```powershell
dotnet-gcdump collect -p <pid> -o artifacts/prof/heap.gcdump   # triggers a gen2 GC - expensive
dotnet-gcdump report artifacts/prof/heap.gcdump                # plain-text Size/Count/Type table
dotnet-counters collect -p <pid> --format json -o diag.json --counters System.Runtime[gc-heap-size]
```

`dotnet-gcdump report` prints heap statistics to stdout without PerfView or Visual Studio
(https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump, fetched 2026-09-16, and
verified locally). Note what it is: a snapshot of **live objects**, not an allocation profile. It
answers "what is on the heap", not "what allocated". For a matcher whose garbage is short-lived by
design, that distinction matters - see section 5.

---

## 3. JetBrains dotTrace, driven from the command line

Install: `dotnet tool install --global JetBrains.dotTrace.GlobalTools`. Verified 2026-09-16 that
the package exists and is current: `dotnet tool search jetbrains` lists
`jetbrains.dottrace.globaltools 2026.2.2` (verified by JetBrains), and nuget.org's flat container
gives the newest version as `2026.3.0-eap02`. The older
`JetBrains.dotTrace.CommandLineTools` package id is **stale** - its newest version on nuget.org is
from 2019 (checked 2026-09-16 against `api.nuget.org/v3-flatcontainer/.../index.json`). Use the
global tool.

```powershell
dotTrace.exe start --profiling-type=Sampling --timeout=30s --save-to=artifacts/prof/run.dtp <exe> <args>
dotTrace.exe attach <PID> --profiling-type=Timeline --timeout=30s --save-to=artifacts/prof/run.dtt
```

`--profiling-type=<Sampling|Tracing|Line-by-Line|Timeline>`, `--timeout=<duration>`,
`--save-to=<path>` with the extension matching the type (`.dtp` for sampling, `.dtt` for timeline)
(https://www.jetbrains.com/help/profiler/Performance_Profiling__Profiling_Using_the_Command_Line.html,
fetched 2026-09-16).

**Getting text out, two ways:**

1. **`Reporter.exe`**, shipped with the CLI tools, converts a snapshot to XML:
   `Reporter.exe report <snapshot> --pattern=<methods.xml> --save-to=<report.xml>`, and
   `Reporter.exe compare <s1> <s2> --pattern=... --save-to=...`. It needs a pattern XML naming the
   methods to include, and it **does not work on Timeline snapshots** (sampling/tracing/
   line-by-line only) (same URL, fetched 2026-09-16). Workable but clumsy: you must know the
   method names before you can ask what is hot, which is backwards for discovery.

2. **The Rider MCP server, which this session already has configured.** Its tools read a snapshot
   file directly and return text:
   - `dotTraceGetSnapshotInfo(snapshotPath)` - duration, type, active filter. First call, because
     it tells you whether the snapshot is timeline or performance.
   - `dotTraceGetCallTree(snapshotPath, ...)` - the call tree in a compact form documented as
     `<Path> @<NodeId> (<OwnPayload>; <TotalPayload>; <%>)`, with `minOwnTimePercent` (default
     1.0), `maxNodes`, `mergeMode`, and `nodeId` to drill into a subtree. Works on **both**
     timeline and performance snapshots.
   - `dotTraceGetTimeline(snapshotPath, ...)` - per-bucket CPU, GC pauses, contention; timeline
     snapshots only.
   - The call tree's payload units follow the active event filter: **nanoseconds for
     time/gc/jit/fileio, bytes for `filterEvent: memory`** - so a Timeline snapshot read with
     `filterEvent=memory` is an allocation call tree, as text, per node.

   This is the single best hot-path-as-text route available here, and it is the one Reporter.exe
   cannot do (it excludes Timeline; the MCP includes it). **Verified**: the tool schemas are
   present and loadable in this session, and the compact output format and filter semantics above
   are quoted from those schemas. **Not verified**: an end-to-end capture-then-read against a real
   `.dtp`/`.dtt`, which needs the tool installed and Rider running. That probe is the first thing
   a profiling slice should do, and it is cheap.

---

## 4. dotMemory, and why it is not the allocation route

Install: NuGet `JetBrains.dotMemory.Console.windows-x64` (2026.1.4) or a standalone zip; no full
install needed to collect, but **the GUI is still needed to analyse**
(https://www.jetbrains.com/help/dotmemory/Working_with_dotMemory_Command-Line_Profiler.html,
fetched 2026-09-16). Note the same staleness trap as dotTrace: the plain
`JetBrains.dotMemory.Console` id returns nothing current on nuget.org (checked 2026-09-16), and
neither `jetbrains.dotmemory.globaltools` nor `jetbrains.dotmemory.commandlinetools` exists there
at all.

```powershell
dotMemory.exe get-snapshot <PID> --save-to-dir=artifacts/prof
dotMemory.exe start --trigger-timer=30s <exe>
```

Output is a `.dmw` workspace. **No CLI, text or JSON report export was found for dotMemory** - the
documentation is explicit that analysis happens in the GUI. `JetBrains.Profiler.Api`
(`MemoryProfiler.GetSnapshot(name)`, `CollectAllocations(bool)`, `ForceGc()`) lets you take a
snapshot from inside your own code at an exact point, but the snapshot still needs the GUI to read
(https://github.com/JetBrains/profiler-self-api, fetched 2026-09-16 via search summary - lower
confidence than a direct fetch). `JetBrains.Profiler.SelfApi` 2.5.18 and `JetBrains.Profiler.Api`
1.4.13 both exist on nuget.org (verified 2026-09-16).

So dotMemory is the owner's escalation path when a human is at the keyboard and the question is
"what is the shape of this retained graph", and it is **not** something an unattended slice can
use. That is a tooling fact, not a judgement on the tool.

---

## 5. PerfView

Not installed here. Verbs and qualifiers below were read directly from the source
(`CommandLineArgs.cs`): verbs `run` ("Starts data collection, runs a command and stops"),
`collect`, `HeapSnapshot`, `ForceGC`, `merge`, `abort`; qualifiers `/NoGui`, `/AcceptEULA`,
`/MaxCollectSec`, `/CircularMB`, `/LogFile`
(https://github.com/microsoft/perfview/blob/main/src/PerfView/CommandLineArgs.cs, fetched
2026-09-16). A worked unattended pattern:
`PerfView.exe /nogui collect /MaxCollectSec:30 /AcceptEULA /zip:true`
(https://techcommunity.microsoft.com/blog/iis-support-blog/perfview-command-for-capturing-automated-high-cpu-dumps/1501037,
fetched 2026-09-16 via search summary).

**The finding that decides it**: there is **no `/SaveStacks`-style CLI flag** exporting a stack
report to CSV or XML. It is not in `CommandLineArgs.cs` (checked directly) and no doc page
confirmed one. Text export of a PerfView trace appears to be GUI-only (File > Save As). Treat any
blog snippet claiming otherwise as wrong until someone shows the flag. PerfView therefore stays
the human's deep-dive tool - excellent for reading an `.etl` or a `.gcdump`, unusable as an
agent's text source.

---

## 6. Native AOT

Microsoft's own support table: **CPU profiling "Partially supported", heap analysis "Not
supported"** (https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/diagnostics,
fetched 2026-09-16 - the page states a .NET 8 baseline; not re-confirmed for .NET 10). Specifics
from that page:

- EventPipe (so `dotnet-trace`, `dotnet-counters`) **does** work with Native AOT, but it is
  opt-in: `<EventSourceSupport>true</EventSourceSupport>` in the project file. Coverage of
  well-known providers is partial.
- `dotnet-gcdump`, PerfView heap analysis and Visual Studio heap tools **do not** work for Native
  AOT managed-heap analysis.
- For CPU profiling an AOT binary the doc points at platform-native tools (PerfView, Linux
  `perf`); dotTrace is not mentioned.
- AOT publish emits a separate native symbol file; without it, profiling results degrade.

**UNVERIFIED**: dotTrace against a Native-AOT-published .NET binary. JetBrains added Timeline
profiling of native apps in 2021
(https://blog.jetbrains.com/dotnet/2021/08/17/profiling-native-apps-in-dottrace/, fetched
2026-09-16 via search summary), but nothing found states it covers AOT-published .NET.

**Consequence for Phase 7**: profile on the JIT, then *verify the win* on AOT with BenchmarkDotNet
timings and `tools/run-aot-smoke.ps1`, rather than expecting to profile the AOT binary. Setting
`EventSourceSupport=true` purely to profile would change what is published, so do it in a throwaway
copy of the sample, never in the shipping configuration.

---

## 7. Recommendation

**CPU: BenchmarkDotNet `EventPipeProfiler` for capture, `dotnet-trace report ... topN` for
reading.**

```powershell
dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- `
  --filter '*BestMatch*' --job medium -p EP --artifacts artifacts/prof/<slice>
dotnet-trace report artifacts/prof/<slice>/**/<Benchmark>-<ts>.nettrace topN -n 30 --inclusive
dotnet-trace report artifacts/prof/<slice>/**/<Benchmark>-<ts>.nettrace topN -n 30
```

Why this one: no admin, nothing to install (both pieces are already here), the profile covers
exactly the iterations BDN measured, and the hot list comes out of stdout where an agent can read
it. The `.speedscope.json` written alongside gives the owner a flame graph for the same capture
with no second run. Read inclusive first to find the responsible subsystem, then exclusive to find
the method actually burning the time.

Escalate to **dotTrace CLI + the Rider MCP call tree** when topN is too coarse - when the answer is
"it is all in `Matcher.TryMatch`" and you need the tree beneath it, with own/total percentages per
node and drill-down by `nodeId`. That is a genuinely better tool for a 10k-line interpreter, and it
is the reason the owner's JetBrains licence is worth using here. Install and probe it once, early,
so the route is known-good before a slice needs it.

**Allocation: `MemoryDiagnoser` for the number, a dotTrace Timeline snapshot read with
`filterEvent=memory` for the attribution.**

`MemoryDiagnoser` is the ratchet - allocated bytes/op is exact, stable and already in the results
table. It says *whether* a change helped. For *where* the allocations are, the honest ranking of
what an agent can actually read:

1. dotTrace Timeline + `dotTraceGetCallTree(filterEvent: "memory")` - allocation bytes per call
   tree node, as text. Best answer, needs the one-off install-and-probe above.
2. `EventPipeProfiler(EventPipeProfile.GcVerbose)` - writes a `.speedscope.json` that is machine
   readable, so a small script can attribute allocation ticks without a GUI.
3. `dotnet-gcdump report` - text, but it shows the **live** heap, not the allocation stream. For
   this engine, whose garbage is per-match and short-lived, it will mostly show what survived,
   which is the less interesting half.
4. dotMemory - the best analysis, but GUI-only, so it is the owner's tool and not a slice's.

And the cheapest technique of all, which for this codebase may beat every profiler: the
allocation sites are few and already documented. `MatchState.Create` allocates a `GroupData[]`
plus one `GroupData` per group, a `RepeatData[]` plus one per repeat, three `ByteStack`s, two
`long[FuzzyValue.Count]`s, and does one vectorised `IndexOfAnyInRange` pass over the subject
(`src/FuzzyRegex/Engine/MatchState.cs:501`, :544-:568). A benchmark that varies group count and
match count against `MemoryDiagnoser` will attribute the bytes arithmetically, with no profiler in
the loop at all. Reach for a profiler when the arithmetic stops explaining the number.
