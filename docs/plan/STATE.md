# State

**S73 is in flight** (started 2026-09-19), chunks 1 and 2 of 5 committed, green and now closed.
Spec: `docs/plan/slices/S73-demo-as-a-product.md`. Chunk plan, review record and numbers:
`docs/plan/slices/notes/S73-sittings.md`.

Chunk 1 landed the copy linter and the rewrite of every user-facing string (1,185 words to 921).
Chunk 2 landed the shell: a header/two-pane/footer frame gated on width AND height, the examples and
help behind an ARIA tab set, two one-column disclosures, the ink-and-white palette with its 22
measured contrast pairs, and `prefers-color-scheme: dark` removed. Sittings 3 and 4 fixed all seven
of its review findings. Sitting 4 took the last two: `tests/built-css.ts` compiles the stylesheet
in process (byte-identical to `npm run build`, md5 `ebc4e84e...`) so four layout assertions read
what the browser receives, and a pre-flush `watch(wide, ...)` opens whichever disclosure holds the
focus when the gate narrows, so focus is never dropped to `<body>`. Three blind passes; the third
found the ownership rules tested for one region only, so they are now `test.each` over both and five
mutants that survived are dead. 193 tests in `demo/web`.

**Chunk 3 is next: the C# snippet.** `demo/web/src/lib/snippet.ts`, `snippet.test.ts`, the revealed
panel at the foot of the results region, and the clipboard fallback. One snippet must be compiled
for real and its output quoted. The spec's section "The C# snippet" is the whole brief.

Chunk 3 also inherits two things from chunk 2:

- **The per-edit underlay is still not buildable.** `Match.FuzzyChanges` has to be threaded through
  `DemoEngine.cs`, `types.ts` and `highlight.ts` first; chunk 2 delivered the edit hues on chips.
- **`docs/demo/*.png` are the old page** and want re-taking from the published build.

Then chunk 4 (two-way match linking, the header GitHub link) and chunk 5 (verification and close).
**Chunk 5 owes four things:** the independent verifier over chunks 2, 3 and 4's numbers; a blind
pass covering chunk 1's fix delta, which no reviewer has seen; `checks.html` re-run whole; and the
mirror of sitting 4's focus fix - the disclosure buttons are `v-if="!wide"`, so WIDENING drops focus
to `<body>` (measured). Pre-existing, same WCAG 3.2.2 case, needs a focus destination chosen.

After S73: `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`. S58 remains a checkpoint
(`docs/plan/slices/S58-measurement-method-and-noise-floor.md`, notes in `notes/S58-sittings.md`).

## Two gates are RED, and neither belongs to S73

Both need a slice that is allowed to touch engine code; S73 is a demo slice and has touched no `.cs`
file since chunk 1.

1. **Oracle, 1 row of 6380 at seed `20260919`** (seeds 7 and 4242 green). Reproduction and triage:
   `docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`. **Not pinned on purpose.**
2. **AOT publish fails**: `Trim analysis error IL2065` at
   `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs:62`, a convention test added
   by S65 (`3b09b76`).

## Open items

- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
  S71's one open box; nothing in the repository can do it, nothing is live.
- **Stryker is paused for S58's benchmarks**; tell the orchestrator the floor is in.
- `docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out (`tools/check-ratchet.ps1:94`); it should fail loudly. First maintenance job.
- `.github/workflows/pages.yml`: the MAIN checkout has a stray copy of it.
- **v3 polish after S73.** Kept: a formatter for `demo/web`, and a saved-case list beside the
  samples. Dropped, reasons in the S72 notes: the explanation pane, the step debugger, the code
  generator.
- Left running, nothing killed per the owner's rule: `python -m http.server` on 8090, 8092, 8137
  and 8181 (serving `.scratch/demo-publish/wwwroot`), a Vite dev server (PID 34120) holding
  `demo/web/node_modules`, and a hung `VBCSCompiler.exe`.
