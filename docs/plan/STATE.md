# State

**S58 is IN FLIGHT (checkpoint, sitting 3).** Phase 7's measurement slice. Spec:
`docs/plan/slices/S58-measurement-method-and-noise-floor.md`. Working notes, all sittings:
`docs/plan/slices/notes/S58-sittings.md`. **No `src/` change, and none is allowed in this slice.**
Sitting 3 ended on the allowance (94% of the window), not on a problem. Ratchet GREEN (6399).

## Scope items 1-4 are done. The measurement question that remains is item 4's tail

Sittings 1-2 measured the floor (time 1.13, allocation 1.0001), answered BDN's affinity/GC
question from a real run's artifacts, measured the Python floor at 1.11x with pyperf, and proved
the EventPipe topN CPU route. Sitting 3 settled the allocation route:
`docs/plan/phase7-research/profiles/README.md` has it with commands and output. Short form: an
unattended session **can capture** an allocation profile (dotTrace Timeline, 54 MB; `dotnet-trace
--profile gc-verbose` by attaching, never by launching, which deadlocks) and **cannot read one**
(no `Reporter.exe` in the package, no report verb in the CLI, Rider MCP `ConnectionRefused`, and
the speedscope conversion carries milliseconds, not bytes).

**Next, and it is small:** demonstrate the arithmetic attribution the fallback rests on, from a
medium run's `Allocated` column - `FuzzyShort` against `FuzzyLong` for subject length,
`MatchesFirstTwo` against `MatchesToEnd` for match count, read against the allocation sites at
`src/FuzzyRegex/Engine/MatchState.cs:544-568`. Then re-probe whether `.claude/skills/` is writable
(the optimise checklist is parked in `phase7-research/`), and run the finish sequence: oracle at
three seeds, AOT, tool tests, blind review, second pass, verifier, closing notes.

## Two gates are RED, and neither is S58's - triage these first

This slice changed **no `.cs` file at all**, so neither can be its doing. Both need a slice that is
allowed to touch code; S58 is not.

1. **Oracle, 1 row of 6380 at seed `20260919`** (seeds 7 and 4242 green). `git diff dfa8767 -- src`
   is empty. Reproduction and triage:
   `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`. **Not pinned on purpose.**
2. **AOT publish fails**: `Trim analysis error IL2065` at
   `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs:62`, a convention test added
   by S65 (`3b09b76`).

## Open items

- **Stryker is paused for this slice's benchmarks**; tell the orchestrator the floor is in.
- Scope item 1's after-a-reboot floor repeat is parked - the owner's call.
- `docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out (`tools/check-ratchet.ps1`, the `$upstreamCommit` line); it should fail loudly.
- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8090/8092/8137, a
  Vite dev server (PID 34120) holding `demo/web/node_modules`, and a hung `VBCSCompiler.exe`.
