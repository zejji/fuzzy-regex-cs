# State

**Slice in flight: S59 - pattern cache behind `FuzzyRegex.CacheSize`. CHECKPOINT, sitting 1 of 2.**
The code is done and green; three finishing steps are left. Sitting notes, with every number and
the full open list, are in `docs/plan/slices/notes/S59-sittings.md`. Read that first.

Green at this commit: ratchet 6432/6432 (baseline 6324 ids), oracle clean at three seeds bar the
already-triaged row 3655, benchmarks GREEN against the machine baseline, `run-aot-smoke.ps1` GREEN.

## Next actions, in order

1. **Re-run the AOT test gate from a clean intermediate directory.** Delete
   `tests/FuzzyRegex.Tests/obj` and `tests/FuzzyRegex.Tests/bin`, then
   `pwsh -File tools/run-aot-tests.ps1`. It failed this sitting with `MSB3077` out of ilc and one
   `IL2065` trim error in `Conventions/PublicApiDocumentationTests.cs`, a file S59 never touched;
   the likely cause is that I built that project Release/win-x64 WITHOUT `PublishAot` first and
   polluted `obj`. If a clean run still fails, the trim error is real and predates S59 - find out
   why S58's gate was green before changing anything.
2. Second blind pass over the delta the first reviewer never saw: `PatternCacheBenchmarks.cs`, the
   `ThreadSafetyTests` edits, DIVERGENCES, PORTMAP, DECISIONS, the `CacheSize` XML docs.
3. Fresh-Opus independent verifier (amendment 16 limb (d)), verbatim no-git-revert clause from
   `docs/VERIFICATION.md`.
4. `git mv docs/plan/slices/S59-*.md docs/plan/slices/done/`, closing notes from the sitting notes,
   tick the "Done when" boxes, commit.

## Blockers

Two wedged processes from this sitting's first AOT attempt are still running and were NOT killed
(owner rule): `dotnet publish` PID 33360 and its child `csc.exe` PID 37192, both started 18:47,
csc flat at 105.5 s of CPU across a 60 s sample. They do not block other builds - a fresh Release
build finished in 17 s beside them - but they should be cleared before the next AOT attempt, and
only the owner can clear them.
