# Roadmap

Phases, in order, from the design spec section 12. This file lists **what** each phase covers and
nothing about how far along it is: status lives in `docs/STATUS.md` (generated from the test
suite) and in `docs/plan/slices/` versus `docs/plan/slices/done/`. Duplicating status here would
just give it somewhere to rot.

Slice files are authored **one phase ahead only**. A detailed plan for phase 5 written today
would be wrong by the time phase 5 arrives, for the same reason a stale TODO list is.

| Phase | Content | Sessions (est.) | Main model |
|---|---|---:|---|
| 0 | Scaffolding: solution, build props, CI, skills, driver script, budget gate, slice files for phase 1 | 1-2 | Opus |
| 1 | Port the full upstream test suite, all skipped initially | 3-6 | Sonnet under Opus |
| 2 | **Compile-parity corpus first**, then parser and compiler (`_regex_core.py`), Unicode tables and case folding, pattern-level public API | 8 | Opus |
| 3 | **Differential oracle harness first**, then VM core: literals, classes, quantifiers, groups, backrefs, anchors - plus the Match object, substitution and the iteration API (`Matches`/`Split`/`Replace`), which nothing else claims and without which no group test can even run | 11-16 | Opus |
| 4 | Advanced: lookaround, atomic and possessive, recursion, branch reset, named lists, POSIX, partial matching | 8-12 | Opus |
| 5 | Fuzzy matching, `BESTMATCH`, `ENHANCEMATCH` | 5-8 | Opus |
| 6 | Oracle *hardening* (broader generators, all Unicode planes), gap tests, the native-AOT compatibility gate | 3-5 | Opus/Sonnet |
| 7 | Benchmarks and optimisation, every optimisation AOT-compatible | 5-10 | Opus |
| 8 | Docs, packaging, NuGet, 1.0 | 2-3 | Sonnet/Opus |
| 9 | Browser demo: Vue 3 page, the engine in a Web Worker, deployed to GitHub Pages | 2-3 | Opus |

Roughly 48-73 slice sessions in total, at plus or minus 50%. The generated status board makes the
real rate visible within the first two phases, which is when these numbers should be revised
against evidence rather than trusted.

**Phase 2's measured rate, recorded at its close (S13, 2026-08-31).** Phase 2 was re-estimated at
8 slices when it opened and landed in exactly 8 (S06-S13), so the *slice* estimate held. The
*driver session* count did not: `docs/plan/slice-log.jsonl` shows 8 slices took 11 driver sessions,
because S07 failed twice before it was parked and S10 failed once - about 35% more sessions than
slices. A completed slice cost 29M to 81M tokens, median around 48M, and a failed attempt costs
roughly the same as a successful one. **So read every phase estimate below as slices, and budget
1.35x that many driver sessions.** Phase 3's 11-16 slices is therefore 15-22 sessions. The estimate
itself is left alone: Phase 2 was parser work and Phase 3 is engine work, so its rate is not
evidence about Phase 3's, and revising a number on the strength of a different kind of work would
be worse than leaving it.

**Phase 3 as authored (2026-08-31) is 13 slices, S14-S26** - inside the 11-16 band - so at the
measured 1.35 sessions per slice, budget about 18 driver sessions. The Phase 3 content line above
was corrected the same day: the original omitted the Match object, substitution and the iteration
API, which are necessarily Phase 3 work (Phase 4/5 do not claim them, `Match.Result` was
constrained to Phase 3 at the S13 close, and group/backref tests cannot run without `m.Groups`);
four of the 13 slices are that unlisted work, and the band still held.

One number the owner should revisit before Phase 3 runs unattended: `docs/plan/budget.json` sets
`maxSlicesPerDay` to 5 and `maxSlicesPerWeek` to 12, and its own note flags that the weekly cap now
binds after two and a half busy days. Changing it is an owner decision, not a slice's.

Fuzzy matching - the reason this port exists - is usable at the end of phase 5, about two thirds
of the way through.

**Phase 3 opens with the differential oracle, not with the VM.** The oracle was originally phase 6
work, which left phases 3-5 - the entire engine - resting on the ported suite and the ratchet
alone. Those two cannot carry it: passing the original project's own tests is evidence of parity,
not proof of it, and the published figure for the closest analogue is that 72% of transpiled
functions were semantically equivalent *despite compiling and passing the existing tests*. The
infrastructure already exists from phase 0 (`tests/FuzzyRegex.OracleTests/`, `oracle.yml`,
Python `regex` installed), so this is a change of when the harness gets written, not of what has
to be built. Design spec amendment 10 has the evidence and the one caveat that does not apply
to us.

**Phase 2 opens with a compile-parity corpus, and carries the Unicode tables.** Both changes were
made at the Phase 1 checkpoint (2026-08-30). The parser's output is bytecode, and upstream's
bytecode is observable by intercepting `_regex.compile` while upstream's own suite runs: 1,534
patterns, 46 parse errors and 62 replacement templates, bit-exact. That is the oracle for a phase
in which nothing can match yet, so it is slice S06 and every later slice is verified against it.
The Unicode tables were parked in Phase 6, but the parser consults them for `\p{...}`, for every
case-insensitive pattern and for `\N{...}`, so they cannot wait; they are S09, transliterated
from upstream's generated C rather than regenerated. The reasoning and the measurements are in
`docs/plan/2026-08-30-phase2-decisions.md`. The estimate moved from 5-8 to 8 sessions.

**Native AOT compatibility is a requirement, enforced in two places (owner decision, 2026-08-31).**
Static analysis and a real published binary answer different questions, so both are in the plan and
neither replaces the other.

The static half is `<IsAotCompatible>true</IsAotCompatible>` on `src/FuzzyRegex`, set now rather
than at the end. On net8 and later that one property turns on the trim, AOT and single-file
analyzers, and because `Directory.Build.props` already sets `TreatWarningsAsErrors`, the first
slice that introduces a hazard fails its own build instead of leaving it for phase 8 to find.
It is cheap today: the library source contains no reflection at all - checked 2026-08-31, every
`System.Reflection` hit under `src/` was in generated `obj/` assembly-info or in a compiled binary,
and the only `System.Type` use is `typeof(...)` and `GetType()` inside `Equals`/`GetHashCode`,
which is statically known and trim-safe. Retrofitting the same property across a finished 30k-line
engine is the expensive version of this decision.

The dynamic half is a tiny consumer app published with `PublishAot=true` and run in CI, asserting
real matches. Static analysis is necessary and not sufficient - it cannot see a runtime-only
failure - so this is the test that actually proves the claim, and it belongs in phase 6 alongside
the rest of the hardening.

**This is why it constrains phase 7 specifically.** The classic .NET regex optimisation is
`RegexOptions.Compiled`: emit IL at run time through `Reflection.Emit` and `DynamicMethod`. That is
fundamentally incompatible with native AOT, which has no JIT to emit into. Phase 7 has to know the
constraint before it plans, because the alternative is discovering the conflict after the fast path
is built. Waiting until phase 8 is worse still - a packaging phase is the worst moment to find a
design problem.

**Phase 6 has an exit gate, in this order.** "Sweep for coverage gaps" without criteria produces a
number nobody acts on, so the phase closes against these, biggest signal first.

1. **Skips remaining in the ported suite.** This is the real coverage metric for a port, and it
   already exists: 1,880 skipped of 5,492 as of 2026-08-31, about 34%, each carrying a
   machine-readable `needs:` reason that `tools/PortTools.psm1` aggregates into `docs/STATUS.md`.
   That is an enumerated, per-capability gap list of upstream behaviour this port does not yet
   have, which is more than any coverage tool can tell us.
2. **Oracle waves across every generator, with zero divergences.** Real ground truth, and unlike
   coverage it finds behaviour that was never tested at all - S16's leading-anchor bug was found
   by a wave and by no ported test.
3. **Mutation testing**, already parked for phase 6 below, scoped to the public API layer and the
   parse-error paths. It is the only instrument that answers "would a regression actually fail a
   test?", and those are the two places the oracle cannot reach.
4. **Line coverage last, and only as a backstop** - to find a file or a branch with no test at all.
   Never as a percentage target.

Phase 6 also has to leave phase 7 something to regress against, because optimisation is where
silent behaviour change is likeliest. Two things get pinned before phase 7 starts: the benchmark
baselines, measured and committed per `.claude/skills/benchmark/SKILL.md` (the skill defines them;
nothing is committed under `bench/baselines/` yet, so this is work, not a tick), and the edge cases
an optimiser is tempted to special-case - zero-width and empty matches, anchors, `MatchTimeout`,
large inputs, and pathological backtracking.

**Phase 9 is a browser demo, and it comes after 1.0 (2026-08-31).** It depends on a public API that
has stopped moving and on the phase 6 trim and AOT gate, and demo polish must not hold up the
release, so it sits at the end. The alternative is yours to take: if the README has to carry a
working "try it in your browser" link *at* 1.0, the demo moves into phase 8 and phase 8's estimate
goes up accordingly. The 2-3 sessions in the table are a guess with no measured basis - nothing
comparable has been built here.

**The engine runs in a Web Worker, and that is the whole safety design.** A public demo invites
strangers to type pathological patterns. Regex matching is unbounded in the worst case, and this is
a fuzzy engine, so approximate matching is combinatorially worse than the exact case. `MatchTimeout`
only bounds a freeze if the engine polls the deadline in its inner loop, which means one bug at one
missed check point turns a bounded stutter into a frozen tab that the user cannot close cleanly. The
worker makes "the page never freezes" a property of the browser's scheduler rather than of the
engine's diligence: a runaway is killed with `worker.terminate()`, and a warm spare worker is
spawned in advance so the respawn is not felt. `MatchTimeout` stays in the demo as the fast
common-case exit and as defence in depth, never as the only safety net.

**The shape.** The main thread is a Vue 3 UI, vendored as an ESM build so there is no build step and
no CDN dependency. The worker hosts its own .NET WebAssembly runtime (a `wasmbrowser` project) and
exposes exactly one `[JSExport]` method: pattern, flags and subject in as strings, a JSON result
out. SolidJS was considered and rejected - the UI is three inputs, a result pane and an examples
list, so fine-grained reactivity would optimise the part that was never the bottleneck, and the
owner already knows Vue. Framework choice is orthogonal to responsiveness here, because all the
expensive work is behind `postMessage`. Blazor was the original recommendation and was dropped once
the UI no longer had to be .NET: it brings the Components assemblies and boot machinery for no
benefit at this size, and Microsoft's own Blazor-on-a-worker sample needs more files than the plain
route, not fewer.

**No cross-origin isolation is needed, and that distinction is worth recording.** The worker boots a
second, independent runtime and talks to the page over `postMessage`; COOP and COEP headers are
required only by `WasmEnableThreads`, which is an unrelated mechanism this design does not use.
GitHub Pages cannot set custom response headers at all, so without this note someone will later read
"WebAssembly in a worker" as "impossible on Pages" and redesign around a constraint that was never
there.

**Known risks, stated at the precision the evidence supports.** The `wasmbrowser` and `wasmconsole`
project templates are documented as experimental - "the developer workflow for the templates is
evolving" - but the `[JSImport]`/`[JSExport]` interop the worker actually depends on has been
supported since .NET 8. So expect template ergonomics to shift and the API surface to hold. Respawn
latency after `terminate()` has no published figure and none has been measured here; the warm spare
is the documented mitigation, and the compiled module is browser-cached, so a respawn hits cache
rather than re-downloading. `[JSExport]` requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in
the demo project, which fails at build time rather than at run time, so it cannot reach a user.

**Deployment mechanics, all of which have bitten someone before.** GitHub Actions publishes to
Pages. A `.nojekyll` file is mandatory: without it Jekyll drops the underscore-prefixed `_framework`
folder and the app serves a 404 for its own runtime. The base href is rewritten to the repository
subpath. `*.js binary` goes in `.gitattributes`, because this repository sets `* text=auto eol=lf`
and any line-ending conversion of the framework JavaScript breaks its SRI integrity check and the
app will not boot. `InvariantGlobalization` is already true repo-wide in `Directory.Build.props`, so
the demo inherits it.

**Scope, deliberately small.** Three inputs; a result pane showing highlighted spans and a group
table of index, length and captures; a sidebar of about eight worked examples loaded from a JSON
array, which *is* the guided tour; and the three inputs encoded in the URL fragment so a case can be
shared in a bug report. Subject length and the number of displayed matches are capped. Explicitly
out: accounts, persistence, analytics, anything server-side.

**It also earns its keep as a test.** Publishing the library into a live WebAssembly host is a real
trimming and AOT proof under a different runtime from the CI gate, which is why phase 9 is tied to
the phase 6 gate rather than being pure marketing.

Precedent for both halves: PeachPDF keeps its demo as a project in the same repository and deploys
it to Pages alongside the docs (it uses Blazor, which this does not), and `dotnet/blazor-samples`
ships `DotNetOnWebWorkersReact`, available for .NET 10 and later, as a non-Blazor host driving a
worker-hosted runtime.

## Candidates parked for later

- **Mutation testing (Stryker.NET), phase 6, scoped and on demand.** It answers "do our tests
  actually pin this behaviour?", which is worth asking of a port. But it is the wrong tool for
  most of this codebase: the differential oracle answers the same question with real ground
  truth and also finds behaviour we never tested at all, whereas mutation testing only finds
  code paths existing tests fail to pin. Stryker reruns the suite per mutant, so a 30k-line
  engine would take hours to days per run - never a merge gate. Where it would earn its keep is
  the small, bounded parts the oracle cannot reach: the public API layer (option handling,
  argument validation, `MatchTimeout`, Span overloads) and the parse-error paths. Revisit in
  phase 6 with real code and real timings; any estimate made now would be a guess. It cannot
  test the PowerShell tooling at all.

## Phase boundaries are human checkpoints

`tools/run-slices.ps1` refuses to cross one. When the pending queue empties, the driver stops and
the owner reviews progress, then authors and approves the next phase's slices. See
`docs/plan/OPERATIONS.md`.

## Where the work queue actually is

- `docs/plan/slices/` - pending, lowest number first. Listing this directory **is** the todo list.
- `docs/plan/slices/done/` - completed, with closing notes appended.
- `docs/plan/STATE.md` - current slice, blockers, next action. Thirty lines maximum, rewritten
  each session.
- `docs/plan/DECISIONS.md` - append-only dated one-liners.
