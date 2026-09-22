---
slice: S68
phase: 8
title: <remarks> divergence notes on every affected public member - after Phase 7, because it edits src/
delivers: []
---

# S68 - The documentation that travels in the package

XML doc comments reach IntelliSense, Copilot and the `.xml` in every consumer's package cache with
no site and no crawler, which the research (2026-09-16) rated the highest-leverage artefact. This
slice adds a `<remarks>` divergence note to every public member whose behaviour differs from
Python `regex` or `System.Text.RegularExpressions`, each one a short restatement of the
COMPARISON.md section it links to, never new prose.

**Blocked until Phase 7 is done** (owner notes, 2026-09-16): this is the one Phase 8 slice that
edits `src/`, and it must not collide with the optimisation stream's edits. Do not start while any
S58-S63 slice is pending. Runs in the `docs` worktree after rebasing onto `main`.

## Scope

- For every SHIPPED row in `docs/DIVERGENCES.md`, find the public members it touches (the row
  names them; `PublicAPI.Shipped.txt` is the list) and add a `<remarks>` paragraph: one or two
  sentences stating the difference and a `<see href>` to the COMPARISON.md anchor. Members already
  carrying such a note are checked, not duplicated.
- The fuzzy-specific members (`FuzzyRegexOptions.BestMatch`, `EnhanceMatch`, the `{e<=n}` family
  documented on `FuzzyRegex` itself, `\L<name>` lists) get the worked example from COMPARISON.md
  in `<example>`, copied verbatim so the S65 sample tests remain the proof.
- A convention test extending S65's: every member named by a SHIPPED row has a `<remarks>`
  containing a COMPARISON.md link. Non-vacuity floor as always.

## Verification

- Build emits no CS1573/CS1574/CS1591; the generated `.xml` contains each link; the new
  convention test fails when one `<remarks>` is removed (prove once, revert).
- Ratchet green, oracle unchanged (no behaviour change is possible from comments, but the ratchet
  runs anyway).

## Done when

- Every SHIPPED divergence is visible from IntelliSense on the member it affects, with a link to the
  one page that explains it.
