# State

**S58 is IN FLIGHT (checkpoint, sitting 2).** Phase 7's measurement slice. Spec:
`docs/plan/slices/S58-measurement-method-and-noise-floor.md`. Working notes, both sittings:
`docs/plan/slices/notes/S58-sittings.md`. **No `src/` change, and none is allowed in this slice.**

## The floor is measured. No benchmark run is in flight.

Run **E** (11:56:31-12:29:55) is A's partner and the first attempt to pass its own gate: 0 of 49
rows below 0.85, sampler log clean. **Time floor 1.13, allocation floor 1.0001**, both now the
defaults in `tools/compare-benchmarks.ps1`; the derivation, the row each came from and how to
tighten the time floor are in `bench/baselines/<machine-id>/noise-floor.md`. Self-test passes: A
against E reads `same` for all 49 rows. Tool tests 113/0 - three of them had never run and were
failing on a fixture defect, now fixed.

## Next action

Verification for the slice so far: `tools/check-ratchet.ps1`, `tools/run-oracle.ps1` at its three
default seeds, `tools/run-aot-tests.ps1` + `run-aot-smoke.ps1`. `src/` is untouched in this slice,
so all three prove it changed nothing, which is the claim. Then blind review and the independent
verifier. **Builds may need `-p:UseSharedCompilation=false`** while `VBCSCompiler` is hung.

## Open items

- S58's unstarted scope: pyperf install + `system show`/`check` archive, the EventPipe topN route,
  the dotTrace route (**the `rider` MCP server would not connect this session: ConnectionRefused**,
  so the fallback in PROFILING section 7 is the likely answer), and BDN's affinity/GC-mode question,
  which needs one short `--keepFiles` run. Verification not yet run: ratchet, oracle at three seeds,
  AOT, tool tests (the two new Pester files have never executed), blind review, verifier.
- **Stryker is paused for this slice's benchmarks**; tell the orchestrator when the floor is in.
- `docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out (`tools/check-ratchet.ps1`, the `$upstreamCommit` line); it should fail loudly.
  First between-slice maintenance job.
- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8090/8092/8137, a
  Vite dev server (PID 34120) holding `demo/web/node_modules`, and a hung `VBCSCompiler.exe`
  (PID 30204) - builds need `-p:UseSharedCompilation=false` until cleared.
