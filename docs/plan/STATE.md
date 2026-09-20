# State

**S73 is closed** (2026-09-20, eight sittings). The demo is a product: the answer in the first
viewport at 1366x768 and 1440x900, every string through a copy linter that runs in the suite, a
light identity pinned to what Chrome paints, the C# snippet panel, two-way match linking, the
per-edit underlay, a skip link, and scripted reference layouts. Spec and closing notes:
`docs/plan/slices/done/S73-demo-as-a-product.md`; the measurements:
`docs/plan/slices/notes/S73-sittings.md`. Ratchet GREEN, 6,406 tests, **baseline updated to 6,298**.
259 tests in `demo/web`, all green; `tools/build-demo-web.ps1` green.

**Next slice: the queue's lowest number, S57** (`docs/plan/slices/`). Phase 9's remaining demo
polish is in the S72 notes, not a slice.

**Waiting on the owner, both from S73.** The two reference layouts in `docs/demo/` need the owner's
eye - the only Done-when box this slice could not close itself, and it is ticked with that said.
And the demo is unpublished until the owner pushes `phase9-demo` and sets Pages > Source = GitHub
Actions.

**Maintenance, small and greppable.** Four comments cite `_regex.c:20535-20537` for upstream's
deletion shift, which is the top of `match_fuzzy_changes`; the shift is at `:20555-20558`. They are
`src/FuzzyRegex/Match.cs:389`, `src/FuzzyRegex/Engine/MatchState.cs:32`,
`tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyMatchingTests.cs:75` and `tools/record-oracle.py:1366`.
S73 fixed the four in its own files. Also open: `tools/check-ratchet.ps1:94` writes the
upstream-commit line wrongly when there is no submodule, and MAIN has a stray
`.github/workflows/pages.yml`.

**Two gates are RED, neither from S73** (both need engine code): the oracle at 1 row of 6380 at seed
`20260919` (triage in `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`,
deliberately not pinned), and the AOT publish, `IL2065` at `PublicApiDocumentationTests.cs:62`.
Stryker is paused for S58's benchmarks.

**Left running:** four `python -m http.server` (8090, 8092, 8137, 8199) and Vite (PID 34120), all
from earlier sittings. The Roslyn compiler-server stall cleared on its own.
