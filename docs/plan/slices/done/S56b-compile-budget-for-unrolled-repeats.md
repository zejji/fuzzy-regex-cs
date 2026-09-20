---
slice: S56b
phase: 6
title: A compile budget on the node graph, so a pathological repeat fails fast instead of exhausting memory
delivers: []
---

# S56b - Bound the one super-linear allocation in the compile path

On 2026-09-18 a probe compiling `((a{1000}){1000}){1000}` grew past 15 GB and crashed the owner's
machine. Root cause, investigated the same day (Fable subagent, findings in
`docs/plan/2026-09-18-repeat-unrolling-investigation.md`): `Engine/NodeCompiler.cs` `BuildRepeat`
(line ~1351) unrolls the minimum count of every counted repeat into the node graph, so nested
repeats multiply and memory is linear in the product of the counts, about 250 bytes per body copy.
This is inherited line for line from upstream (`upstream/src/_regex.c` `build_REPEAT`, changelog
2018.11.22, "Hg issue 304": unrolling keeps the position-keyed repeat guard sound). Upstream has the
same growth, measured within 5% of the port. Everything else in the compile path is linear in
pattern length.

Owner decision 2026-09-18: bound it with a configurable node budget (option 1 of the
investigation). The structural fix, counted repeats without unrolling, is parked as a post-1.0
roadmap candidate because it changes the guard design and backtracking behaviour. Opus.

Runs on `main` after S55 lands; do not start while an S55 sitting is running.

## Scope

- **The budget.** Count nodes at the single creation point, `NodeCompiler.CreateNode`
  (`pattern.NodeList.Add(node)`, line ~216). When the count exceeds the budget, abandon the compile
  and throw `FuzzyRegexParseException` with a message that names the count, the budget and how to
  raise it. Default budget: 1,000,000 nodes (about 250 MB, about 2.5 s), which admits every pattern
  in the corpus (largest single count `{65535}`) and everything upstream compiles in reasonable
  memory. The compile must stop promptly when the budget trips: check in `CreateNode`, not after
  the loop, so the cost of a rejected pattern is bounded by the budget itself.
- **Configuration.** A `MaxCompiledNodes` (name to be confirmed against the public API conventions
  in `PublicAPI.Unshipped.txt`) on the `FuzzyRegex` constructor family, following how
  `matchTimeout` is threaded; static default in one place. Not a `FuzzyRegexOptions` flag, because
  it is a number. Document it in `<remarks>` on the constructor and in `docs/COMPARISON.md`
  ("Behaviour that differs and why").
- **Exception plumbing.** Today a builder failure surfaces as `NotSupportedException("invalid RE
  code")` from `PatternObject.Compile` (line ~244). The budget failure must not go that route: it is
  a parse-time refusal the caller can act on, so it is `FuzzyRegexParseException` with `Pattern` set
  and the budget in the message.
- **Tests, first.** (1) `((a{150}){150}){150}` throws `FuzzyRegexParseException` naming the budget
  within a small time bound and without the process exceeding a modest managed heap (assert on
  `GC.GetTotalMemory` delta or on elapsed time, whichever is stable; the investigation measured
  9 s to OutOfMemory unbounded). (2) `(a{1000}){1000}` still compiles at the default budget and
  matches as before (parity: compare with a pinned upstream answer). (3) A tiny budget (say 100)
  rejects `a{200}` and accepts `a{50}`, proving the knob works. (4) The message text is pinned. All
  under `Gaps/Engine/`, with provenance per the S52c rule.
- **DIVERGENCES.md row** under "Behaviour where the port answers differently on purpose":
  "Compile budget: a pattern needing more than the configured number of nodes throws
  `FuzzyRegexParseException` before matching; upstream unrolls without limit (measured 247 MB for
  `(a{1000}){1000}`, 877 MB for `((a{150}){150}){150}`, and a crash at `{1000}` cubed). Upstream
  behaviour available: raise the budget." Status SHIPPED, decided 2026-09-18.
- **Oracle check.** Run the oracle at the default budget: no row may change, because the budget
  only rejects patterns upstream would need more than about 250 MB to compile, and no such row
  exists (`grep -rhoE '\{[0-9]{4,}' tests` finds `{65535}` and `{1059}` at most).

## Verification

- The four tests fail before the change (the first by exhausting the 1 GB heap limit the test
  host must run under: set `DOTNET_GCHeapHardLimit` for the probe, never run it unbounded) and
  pass after; ratchet green; oracle unchanged at three seeds; AOT gate green (no reflection).
- A bounded manual probe (`DOTNET_GCHeapHardLimit=0x40000000`, `timeout 60`) shows the cubed
  pattern rejected in well under a second with a flat heap. Record the printed line in the notes.

## Done when

- [x] Budget in, configurable, documented, tested; DIVERGENCES row and COMPARISON section written;
  `PublicAPI.Unshipped.txt` updated; the investigation document committed alongside (it landed with
  the investigation on 2026-09-18, `docs/plan/2026-09-18-repeat-unrolling-investigation.md`);
  closing notes name the post-1.0 candidate (counted repeats) with the guard-soundness caveat.

## Safety rule for this slice

No probe of a pathological pattern without `DOTNET_GCHeapHardLimit=0x40000000`, a wall-clock
timeout and a subject under 100 characters, and no probe delegated to a subagent without those
limits written into its prompt. The machine crashed once already.

---

## Closing notes (2026-09-18)

### What landed

- **The budget is checked at the single node-creation point.** `NodeCompiler.CreateNode`
  (`src/FuzzyRegex/Engine/NodeCompiler.cs`) refuses before `pattern.NodeList.Add(node)` when the
  list has already reached `pattern.MaxNodes`, throwing `FuzzyRegexParseException` with `Pattern`
  set and `Offset == -1`. `PatternObject` carries the two new fields the check needs
  (`MaxNodes`, `PatternText`), and `PatternObject.Compile` takes the budget as a parameter
  defaulting to `FuzzyRegex.DefaultMaxCompiledNodes`. No `catch` sits between `CreateNode` and the
  constructor, so the exception reaches the caller unchanged - confirmed independently by the
  blind reviewer.
- **The knob is `maxCompiledNodes`, threaded exactly like `matchTimeout`**: a trailing optional
  parameter on the widest public `FuzzyRegex` constructor, validated with
  `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`, surfaced as the read-only property
  `MaxCompiledNodes`, with the default in one place as
  `public const int FuzzyRegex.DefaultMaxCompiledNodes = 1_000_000`. There is deliberately no
  "unlimited" value: the whole point of the slice is that unbounded is what crashed the machine.
- **The budget counts nodes CREATED, not nodes kept.** `Optimiser.DiscardUnusedNodes` prunes about
  half the graph, but it runs after the memory has been spent. Measured by bisecting the budget on
  2026-09-18: `(a{100}){100}` creates 20,913 nodes and keeps 10,509; a budget of 10,509 does not
  compile it. That asymmetry is pinned by a test and documented on the property and in
  `docs/COMPARISON.md`, because it is the one thing a caller sizing the number will get wrong.
- Docs: a "Compile budget" row in `docs/DIVERGENCES.md` (SHIPPED, decided 2026-09-18), a
  `### Compile budget` section in `docs/COMPARISON.md` with both a raise-it and a lower-it example,
  a `docs/PORTMAP.md` row under "Where we diverge from upstream's structure", and a
  `docs/plan/OPTIMISATION-NOTES.md` row paired with the `ponytail:` comment at
  `Engine/NodeCompiler.cs:1430`.
- Tests: `tests/FuzzyRegex.Tests/Gaps/Engine/CompileBudgetTests.cs`, 7 methods / 8 cases. The
  matching answers they assert come from `tools/probes/s56b-compile-budget-upstream-answers.py`,
  run against regex 2026.9.10 on 2026-09-18 and quoted beside each assertion; the budget's own
  behaviour has no upstream answer and cites the DIVERGENCES row instead.

### Measurements, all from this sitting (Debug build, 2026-09-18)

Re-runnable with the scratch harness `.scratch/probe` (gitignored; source is in the session
transcript) or, more cheaply, by bisecting the public knob as `CompileBudgetTests` does:

```
a{450000} budget=1000000 allocated=241 MB retained=107 MB time=757 ms   (reviewer's run: 724 ms)
((a{150}){150}){150} refused in 763 ms, allocated=249 MB                (reviewer's run: 681 ms)
created: a{50}=106, a{200}=406, (a{100}){100}=20,913, (a{1000}){100}=202,713
kept:    (a{100}){100}=10,509, (a{1000}){100}=101,409
```

The one kept figure NOT measured here is `(a{1000}){1000}`'s 1,005,009: it comes from
`docs/plan/2026-09-18-repeat-unrolling-investigation.md` (the run that preceded this slice), and
compiling it again is exactly what the safety rule forbids. The verifier reached it a second way,
from `kept = XY + X + 4Y + 9` fitted to the measured points, and confirmed the pattern is refused
at the default budget.

The slice text's "about 2.5 s" for a million nodes was wrong by about 3.5x; the blind review caught
it. The timing is now quoted as "about 0.7 s", with both measured runs beside it, rather than as a
single number a different machine would fail to reproduce. The reviewer also checked the created
counts against a formula: created fits `2nm+2n+7m+13` on 8 of 8 measured points and kept fits
`nm+n+4m+9` at both quoted points.

### Deviation from the slice text

Test (2) as written expected `(a{1000}){1000}` to "still compile at the default budget". It does
not: it KEEPS 1,005,009 nodes and creates about twice that, so at a default of 1,000,000 it is
refused. The choice was to raise the default or to change the example. The default stays - a
million nodes is already about 250 MB of graph for one pattern, and a pattern that keeps a million
nodes is exactly the shape the slice exists to refuse - and the test uses `(a{100000})?b` and
`(a{1000}){100}` instead, both with pinned upstream answers. Recorded in `docs/plan/DECISIONS.md`.

### The thing to know before touching the constructors

Adding the trailing `int maxCompiledNodes` to the public constructor silently captured the two
call sites that were calling the INTERNAL constructor's fifth parameter, `defaultVersion`. C#
overload resolution prefers a candidate that omits no optional parameters over one that omits
some, so `new FuzzyRegex(pattern, options, timeout, namedLists, RegexFlags.Version0)` rebound from
"compile under version 0" to "compile with a budget of 8,192 nodes, under the default version".
It compiled, and it surfaced as two ratchet failures and three oracle DIVERGE rows that all looked
like ordinary behaviour regressions. The fix is structural: the internal seam is now a named
factory, `internal static FuzzyRegex.WithDefaultVersion(...)`, and the six-parameter constructor is
private. **A name cannot be captured by an overload.** If a future slice adds another optional
parameter to this family, check every call site that passes a positional argument in the last slot.

### Post-1.0 candidate: counted repeats without unrolling

`BuildRepeat` writes one copy of the body per repetition of the minimum count (upstream
`build_REPEAT`, `upstream/src/_regex.c:25166-25197`), which is where all of this comes from. The
lift is a counting loop instead of unrolled copies. It is **not** a transparent rewrite: upstream
unrolled deliberately in 2018.11.22 ("Hg issue 304") because the position-keyed repeat guard is
only sound when each repetition is its own node, and the equivalence would have to be PROVEN, not
assumed, for captures per repetition, the fuzzy sections and the group-call guard. Parked as its
own post-1.0 slice; the ceiling and the lift are recorded at `Engine/NodeCompiler.cs:1430` and in
`docs/plan/OPTIMISATION-NOTES.md`.

### Oracle controls

None. This slice changed no generator and added no oracle wave, so there is no control to
reproduce; the oracle's role here was the regression check the slice asked for, run at three seeds
(7, 4242, 20260918) with `diverge 0` in each. It earned its keep: it was RED at all three before
the `WithDefaultVersion` fix.

### Review

One blind pass over the whole diff (fresh Opus subagent, reproduction-only brief: findings only,
each with the command and its decisive output; no explanations, no rewrites, no style opinions).
**Findings raised: 1. Reproduced: 1. Fixed: 1.** The finding was the "about 2.5 s" timing claim
inherited from the slice text - about 3.5x too high, and self-contradictory in the DIVERGENCES row,
which said "about 2.5 s" and "refused inside a second" in the same cell. Reproduced here at
757 ms / 763 ms against the reviewer's 724 ms / 681 ms, and corrected in `FuzzyRegex.cs`,
`CompileBudgetTests.cs` (two places) and the DIVERGENCES row.

The reviewer also reported, unprompted, five checks that found no defect, and they are the ones
worth keeping: every node count quoted here reproduced; the boundary is exactly N created nodes
admitted at budget N, so a `>` mutant of the check fails `oneBelow`; `CreateNode` is the only
`NodeList.Add`/`new Node(` site in `src/` and no `catch` sits between it and the constructor
(`PatternCompiler`'s catches all run before node building); no call site rebinds after the
`WithDefaultVersion` change, and the solution builds with 0 warnings; the full suite is
`total: 6351 failed: 0` with 8 added baseline ids and 0 removed.

**No second blind pass was needed**: the post-review edits touched only comment and documentation
text the reviewer had already read, and added no public API, no tooling and no logic. The
independent verifier pass then ran over the commit-ready tree; its report is in
`docs/plan/slices/notes/S56b-sittings.md`.
