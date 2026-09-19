# State

**S72 (demo v2: one sample per feature, plus generated help) is IN FLIGHT** on branch `phase9-demo`,
checkpointed at the end of sitting 1 (2026-09-19). Spec:
`docs/plan/slices/S72-demo-v2-features-and-help.md`; per-sitting record and the review:
`docs/plan/slices/notes/S72-sittings.md`. S71 is done.

## Where it is

Chunks 1-3 of 5 landed: the engine contract v2 (mode, replacement, named lists in; `replaced` and
`partialMatch` out; three new caps), `tools/build-demo-help.ps1` generating `wwwroot/help.json` from
`docs/COMPARISON.md` and failing on a renamed heading, and `examples.json` at 18 rows with every
expectation taken from a real `regex 2026.9.10` run. Verified on this tree: **ratchet GREEN** (6393
passing, 0 failing, baseline 6285), **doc examples GREEN** (all 37 blocks), the help generator's
failure path reproduced with its output quoted in the notes. One blind pass, six findings, all six
reproduced, five fixed.

## Next action

Sitting 2 takes chunk 4 (the page) and then chunk 5 (verification and close), in this order:

1. **A blind pass over sitting 1's five review fixes**, which no reviewer has seen (see the notes).
2. `demo/web`: `types.ts` and `shapes.ts` still describe the S71 contract, and `demo.ts` still posts
   only pattern, flags and subject - so four sidebar rows would answer wrongly if this tree were
   deployed. Nothing is live (`pages.yml` deploys from `main`), but the page must land before this
   branch merges.
3. Then the help disclosures, the five borrowed regex101 interactions, the accessibility items, and
   chunk 5: wasm smoke, served page, keyboard pass, screenshots at 390 and 1280, blind review,
   independent verifier, close.

## Open items

- **The owner still has to push `phase9-demo` and set Settings > Pages > Source = GitHub Actions.**
  S71's one open box; nothing in the repository can do it.
- **`tools/check-doc-examples.ps1` was red on `main`** from dfa8767 (S56b, 2026-09-18) until this
  commit: the `maxCompiledNodes` example used an undeclared variable and expected an output ending in
  `...`. Worth knowing why CI on main was failing.
- **`dotnet csharpier check .` was also red on `main`** from e38f270 (S71): two project files were
  committed unformatted. Whitespace only, fixed in this commit for the same reason.
- **`docs/STATUS.md`'s upstream-commit line is generated wrong when the `upstream` submodule is not
  checked out** (`tools/check-ratchet.ps1:94`); the generator should fail loudly rather than fall back
  to our own HEAD. First between-slice maintenance job.
- `.github/workflows/pages.yml` is committed here and the MAIN checkout also has a stray copy.
- Left running, nothing killed per the owner's rule: `python -m http.server 8092` (PID 14996), a Vite
  dev server on port 5199, `python -m http.server 8090`, and PID 34120 (another session's Node).
