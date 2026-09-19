# State

**S58 is IN FLIGHT (checkpoint, sitting 2).** Phase 7's measurement slice. Spec:
`docs/plan/slices/S58-measurement-method-and-noise-floor.md`. Working notes, both sittings:
`docs/plan/slices/notes/S58-sittings.md`. **No `src/` change, and none is allowed in this slice.**

## The floor is measured. No benchmark run is in flight.

Run **E** (11:56:31-12:29:55) is A's partner and the first attempt to pass its own gate: 0 of 49
rows below 0.85, sampler log clean. **Time floor 1.13, allocation floor 1.0001**, both now the
defaults in `tools/compare-benchmarks.ps1`; the derivation and the row each came from are in
`bench/baselines/<machine-id>/noise-floor.md`. Self-test passes: A against E reads `same` for all 49
rows. Ratchet GREEN (6399), tool tests 114/0. Two blind passes, nine findings, all reproduced and
fixed; the verifier ran over the commit-ready tree.

**The floors move no verdict, and that is deliberate.** `-Threshold` (1.25) fails a run and governs
both axes; a floor only forgives a ratio already over `-Threshold`, so a floor below it forgives
nothing. A row allocating 1.20x its baseline is GREEN. Pinned as a test, and handed to **S63 scope
item 7** as a gate decision rather than improvised here.

## Two gates are RED, and neither is S58's - triage these first

This slice changed **no `.cs` file at all**, so neither can be its doing. Both need a slice that is
allowed to touch code; S58 is not.

1. **Oracle, 1 row of 6380 at seed `20260919`** (seeds 7 and 4242 green). `git diff dfa8767 -- src`
   is empty. Reproduction and triage questions:
   `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`. **Not pinned on purpose.**
2. **AOT publish fails**: `Trim analysis error IL2065` at
   `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs:62`, a convention test added
   by S65 (`3b09b76`). Annotate, suppress with a reason, or keep it out of the native publish.

## Then: the rest of S58

pyperf install + `system show`/`check` archive, the EventPipe topN route, the dotTrace route (the
`rider` MCP server refused connection this session), BDN's affinity/GC question (one short
`--keepFiles` run), and scope item 1's after-a-reboot repeat, which is the owner's call.

## Open items

- **Stryker is paused for this slice's benchmarks**; tell the orchestrator the floor is in.
- `docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out (`tools/check-ratchet.ps1`, the `$upstreamCommit` line); it should fail loudly.
  First between-slice maintenance job.
- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8090/8092/8137, a
  Vite dev server (PID 34120) holding `demo/web/node_modules`, and a hung `VBCSCompiler.exe` -
  builds may need `-p:UseSharedCompilation=false` until it is cleared.
