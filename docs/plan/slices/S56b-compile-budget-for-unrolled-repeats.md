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

- Budget in, configurable, documented, tested; DIVERGENCES row and COMPARISON section written;
  `PublicAPI.Unshipped.txt` updated; the investigation document committed alongside; closing notes
  name the post-1.0 candidate (counted repeats) with the guard-soundness caveat.

## Safety rule for this slice

No probe of a pathological pattern without `DOTNET_GCHeapHardLimit=0x40000000`, a wall-clock
timeout and a subject under 100 characters, and no probe delegated to a subagent without those
limits written into its prompt. The machine crashed once already.
