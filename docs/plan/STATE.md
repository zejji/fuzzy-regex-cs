# State

**S58 is IN FLIGHT (checkpoint, sitting 1 of 2).** Phase 7's measurement slice, taken ahead of S57
on the orchestrator's instruction because it needed a quiet machine. Spec:
`docs/plan/slices/S58-measurement-method-and-noise-floor.md`. Everything this sitting learned:
`docs/plan/slices/notes/S58-sittings.md`. **No `src/` change, and none is allowed in this slice.**

Landed: the benchmarks the floor must cover (span overloads, two `MatchState` sweeps, the fuzzy
no-match workload); `compare-benchmarks.ps1`'s allocation ratio, `-Job`, `-BaselinePath` and the two
floor parameters; `SYNC-DIVERGENCE.md` + `check-sync-divergence.ps1` wired into the ratchet;
two Pester files; both owner decisions written up with run A's numbers in
`docs/plan/2026-09-19-span-threading-decision.md`; the optimise checklist parked in
`docs/plan/phase7-research/optimise-skill-pending.md` (this session was refused write under
`.claude/`).

## Next action

**Take the one missing noise run**, then set the floor. Read
`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/noise-floor.md` section "Taking a run so
it counts" first and obey all five rules - in particular, **the session must be silent while the run
is in flight** (its own Headroom proxy traffic costs 2.6 cores) and **nothing may build in any other
worktree**. Run A is sound, committed and needs no re-take; the tree is unchanged since it. Runs B
and C were discarded, with the evidence, for exactly these two causes.

## Open items

- S58's unstarted scope: pyperf install + `system show`/`check` archive, the EventPipe topN route,
  the dotTrace route (**the `rider` MCP server would not connect this session: ConnectionRefused**,
  so the fallback in PROFILING section 7 is the likely answer), and BDN's affinity/GC-mode question,
  which needs one short `--keepFiles` run. Verification not yet run: ratchet, oracle at three seeds,
  AOT, tool tests (the two new Pester files have never executed), blind review, verifier.
- **Stryker is paused for this slice's benchmarks**; tell the orchestrator when the floor is in.
- `docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out (`tools/check-ratchet.ps1`, the `$upstreamCommit` line, now near :104 after this
  slice's pre-flight insertion); it should fail loudly. First between-slice maintenance job.
- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8090/8092/8137, a
  Vite dev server (PID 34120) holding `demo/web/node_modules`, and a hung `VBCSCompiler.exe`
  (PID 16364 earlier, PID 30204 now) - builds need `-p:UseSharedCompilation=false` until cleared.
