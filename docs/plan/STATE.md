# State

**S58 is IN FLIGHT (checkpoint, sitting 2).** Phase 7's measurement slice. Spec:
`docs/plan/slices/S58-measurement-method-and-noise-floor.md`. Working notes, both sittings:
`docs/plan/slices/notes/S58-sittings.md`. **No `src/` change, and none is allowed in this slice.**

## A benchmark run is in flight - do not orient, block

Noise run **E**, launched detached by `.scratch/s58-run-e.ps1`. Its PID and its sampler's are in
`.scratch/s58-run-e.pid`; it writes `.scratch/s58-run-e.done` when it finishes, and takes about
**36 minutes**. Load log: `bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-E-load.log`.

If you are reading this while that run is alive: **stop reading and block on the PID.** Reading
state files and sampling the machine costs 3.6-4.3 cores through the Headroom proxy, which is what
ruined run D. Rules: `bench/baselines/<machine-id>/noise-floor.md`, "Taking a run so it counts".

## Next action

When E is done: read its load log and its own min/median line. If clean, compare against the
committed run A with `tools/probes/compare-two-baselines.ps1`, set `-NoiseFloor` and
`-AllocationNoiseFloor` in `tools/compare-benchmarks.ps1` from the largest time and allocation
ratio, and replace noise-floor.md's "The floor" section with those two numbers and their rows.
Runs B, C and D were discarded, each with its cause measured and committed; A needs no re-take and
the tree is unchanged since it (`src/` last touched at `dfa8767`, `bench/` sources at 08:00-08:02,
both before A started at 08:22).

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
