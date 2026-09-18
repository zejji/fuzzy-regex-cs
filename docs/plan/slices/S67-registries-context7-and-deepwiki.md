---
slice: S67
phase: 8
title: Registries that make the library findable by assistants - prepared files, owner-performed submissions
delivers: []
---

# S67 - Findability, the cheap half only

The research (2026-09-16) rated Context7 and DeepWiki "consider" (fifteen minutes and five minutes,
one-off) and everything else in that space "skip" (llms.txt as strategy, consumer SKILL.md, a
DocFX site for 1.0). This slice prepares what a submission needs and stops; the submissions
themselves need the owner's accounts and are listed as owner steps.

Runs in the `docs` worktree. Small: half a sitting.

## Scope

- **`context7.json`** at the repo root, per the current Context7 documentation fetched on the day
  (record the URL and date in the closing notes): project name, description, the folders to index
  (`docs/`, `README.md`), excludes (`docs/plan/`, `docs/superpowers/`, `upstream/`, `bench/`,
  `TestResults/`), and the rules field pointing at `docs/COMPARISON.md` as the canonical
  difference list.
- **A short `docs/plan/FINDABILITY.md`** with the two owner steps (Context7 add-library form,
  DeepWiki submit), the check that proves each worked (a query that returns a COMPARISON.md fact),
  and the note that llms.txt is deferred until a docs site exists.
- **Nothing else**: no llms.txt, no docs site, no consumer skill (research, "Skip").

## Verification

- `context7.json` validates against the schema the documentation publishes (or parses as the
  documented shape if no schema is published; say which).
- Ratchet unaffected (docs only).

## Done when

- The two files exist, owner steps are listed with their proof checks, and the closing notes record
  the documentation URLs and the date read.
