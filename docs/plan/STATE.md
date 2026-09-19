# State

**S73 is in flight** (started 2026-09-19), chunks 1 and 2 of 5 committed and green. Phase 9 reopened
for it: the owner's first look at the live demo judged it as a product and found seven faults. Spec:
`docs/plan/slices/S73-demo-as-a-product.md`. Chunk plan, review record and numbers:
`docs/plan/slices/notes/S73-sittings.md`.

Chunk 1 landed the copy linter and the rewrite of every user-facing string (1,185 words to 921).
Chunk 2 landed the shell: a header/two-pane/footer frame gated on width AND height, the examples and
help behind an ARIA tab set, two one-column disclosures, the ink-and-white palette with its 22
measured contrast pairs, and `prefers-color-scheme: dark` removed. Sitting 3 fixed five of its seven
review findings: the shell is `min-h-dvh` (the built CSS now holds no `100vh` at all), both tab
panels are in the page with the unselected one `hidden` so every `aria-controls` resolves, the
disclosures name the regions they open, the Help panel takes `tabindex="0"` while it is empty, and
Tailwind no longer scans `tests/` for classes. `checks.html` still wants re-running whole: chunk 2
saw eight of nine, and the red one was its own stale literal, since fixed.
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

**The two open chunk-2 findings, then chunk 3.** Both are scoped in the sittings notes under
"Chunk 2's review findings, fixed":

- **Finding 6**: four `layout.test.ts` assertions still read the stylesheet SOURCE. The route is
  proven - an in-process `vite build` of `src/styles.css` (`configFile: false`, `write: false`) gave
  byte-identical output to `npm run build`, same hash, in 127 ms - but `tests/built-css.ts` is not
  written.
- **Finding 5**: focus drops to `<body>` when the media gate narrows with a tab focused. A
  `watch(wide, ...)` opening whichever disclosure holds the focus is the cheap shape.

**Then chunk 3**, from the spec's remaining deliverables. It inherits two things:

- **The per-edit underlay is still not buildable.** `Match.FuzzyChanges` has to be threaded through
  `DemoEngine.cs`, `types.ts` and `highlight.ts` first; chunk 2 delivered the edit hues on chips.
- **`docs/demo/*.png` are the old page** and want re-taking from the published build.

**The independent verifier still owes chunk 2**, and **chunk 5's blind pass must cover chunk 1's fix
delta**, which no reviewer has seen.

After S73: `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`.
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
  S71's one open box; nothing in the repository can do it, nothing is live.
- **v3 polish after S73.** Kept: a formatter for `demo/web`, and a saved-case list beside the
  samples. Dropped, reasons in the S72 notes: the explanation pane, the step debugger, the code
  generator.
- **`docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out** (`tools/check-ratchet.ps1:94`); it should fail loudly. First maintenance job.
- `.github/workflows/pages.yml`: the MAIN checkout has a stray copy of it.
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8092 (PID 14996),
  8090, 8137, and 8181 (serving `.scratch/demo-publish/wwwroot`).
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8090/8092/8137, a
  Vite dev server (PID 34120) holding `demo/web/node_modules`, and a hung `VBCSCompiler.exe`.
