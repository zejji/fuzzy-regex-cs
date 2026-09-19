# State

**S58 is DONE** (four sittings). Phase 7's measurement slice: no `src/` change, and
`git diff 2c1e747 -- src` is empty. Closing notes and review record:
`docs/plan/slices/done/S58-measurement-method-and-noise-floor.md`. Ratchet GREEN (6,399/6,291).

**Next slice: S59** (`docs/plan/slices/S59-pattern-cache-and-cachesize.md`); S57 is Phase 6's close.

## What S58 leaves Phase 7

- Noise floor committed (`bench/baselines/<machine-id>/noise-floor.md`): time 1.13, allocation
  1.0001. `tools/compare-benchmarks.ps1` defaults to it and reports both ratios per workload.
- The allocation **profiler** route is closed - nothing installed reads a captured trace back to a
  source site. The arithmetic route is demonstrated:
  `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- attribution`. A capture group
  costs 264.00 B in a `Match`: 40.00 B state, 224.00 B outside. State's line: 984 B + 40 B/group.
- **S60 owes a `ponytail:` comment** at `FuzzyRegex.cs:444` and `:568` - `IsMatch` allocates
  byte-for-byte what `Match` does (`Run` passes `visibleCaptures: true` unconditionally). The
  `OPTIMISATION-NOTES.md` row exists; the source half does not, because S58 may not touch `src/`.
- Two decisions wait on the owner in `docs/plan/2026-09-19-span-threading-decision.md`: hoist the
  per-subject work out of the per-step state, then pool the state; decline the `ref struct`.

## Two gates are RED, neither S58's

1. **Oracle, 1 row of 6,380 at seed `20260919`** (7 and 4242 green). Triage:
   `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`. Not pinned on purpose;
   needs a slice allowed into `src/`.
2. **AOT publish: `IL2065`** at `tests/.../Conventions/PublicApiDocumentationTests.cs:62`, from S65
   (`3b09b76`). AOT smoke GREEN at 6,977,536 bytes.

## Open

- `.claude/skills/optimise/SKILL.md` needs the owner; body parked in
  `docs/plan/phase7-research/optimise-skill-pending.md`. Unattended sessions cannot write there.
- **Stryker is paused for benchmarking** - the floor is in, it can resume. The after-a-reboot floor
  repeat is parked on the owner.
- `docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is
  absent (`tools/check-ratchet.ps1`, `$upstreamCommit`); it should fail loudly.
- **Owner: push `phase9-demo`, set Settings > Pages > Source = GitHub Actions.**
- Running, nothing killed per the owner's rule: `python -m http.server` on 8090/8092/8137, a Vite
  dev server (PID 34120) holding `demo/web/node_modules`, a hung `VBCSCompiler.exe`.
