# State

**S74 is closed** (2026-09-20). The demo's flags text box is a collapsible checkbox panel: a summary
row, ten checkboxes, two radio groups for the only two pairs the library refuses, and a per-flag help
sentence that is the enum's own `<summary>`. The flags string stays the state, so old shared links
and every `examples.json` row load unchanged. Spec, deviations, evidence and the three blind passes:
`docs/plan/slices/done/S74-flags-control.md`. Ratchet GREEN, 6,461 tests, **baseline updated to
6,353**; 297 web tests in 14 files; typecheck and `npm run build` green.

**Next slice: the queue's lowest number, S57** (`docs/plan/slices/`). Phase 9's remaining demo polish
is in the S72 notes, not a slice.

**Two slices are checkpointed and still open.**
- **S57 - Phase 6 close-out.** Order in `notes/S57-sittings.md`: items 5, 7-11 untouched, 911
  uncovered lines unclassified, **no blind review and no verifier over the S57 diff**.
- **S60 - prefilter family (Phase 7), after sitting 3.** Route at the head of
  `notes/S60-sittings.md`. Next: blind review and verifier over sitting 3's diff (it has had
  neither), a pinning test for row 3633, the benchmark on a quiet machine, and moving items 2, 3, 6,
  8-14, 16, 17 into a successor slice file - a phase-plan change, so spec amendment plus a ROADMAP
  row in the same commit.

**Waiting on the owner.** The two reference layouts in `docs/demo/` want the owner's eye, and the
demo is unpublished until `phase9-demo` is pushed and Pages > Source is set to GitHub Actions.

**Two gates are RED**, both needing engine code: the oracle at 1 row of 6380 at seed `20260919`
(`docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`, deliberately not pinned), and
the AOT publish, `IL2065` at `PublicApiDocumentationTests.cs:62`. Stryker is paused for S58's
benchmarks.

**Maintenance, small and greppable.** `tools/check-ratchet.ps1:94` writes the upstream-commit line
wrongly when there is no submodule, and MAIN has a stray `.github/workflows/pages.yml`.

Standing: `git merge-base --is-ancestor <sha> HEAD` before quoting a SHA. `-Count` on
`run-oracle.ps1` is PER GENERATOR; `-Seeds` takes a comma-separated string, and the third default
seed is today's date, so a date-seeded red row blocks the gate only until midnight.

**Left running:** four `python -m http.server` (8090, 8092, 8137, 8199) and Vite (PID 34120), from
earlier sittings.
