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

- [x] The two files exist, owner steps are listed with their proof checks, and the closing notes record
  the documentation URLs and the date read.

## Closing notes (2026-09-18)

Landed `context7.json` (repo root) and `docs/plan/FINDABILITY.md`. Nothing submitted - both
registrations need the owner's accounts and are listed as owner steps with proof checks.

**Documentation read, 2026-09-18:**
- Context7 config format: `https://context7.com/docs/api-reference/add-library/add-a-github-repository`
  (field descriptions, example) and the schema itself,
  `https://context7.com/schema/context7.json` (JSON Schema draft-07: `projectTitle` 1-100 chars,
  `description` 10-200 chars, `folders`/`excludeFolders`/`excludeFiles`/`rules` string arrays,
  `additionalProperties: false`). Submission form: `https://context7.com/add-library?tab=github`.
- DeepWiki: public repos index by visiting `deepwiki.com/<owner>/<repo>` with no account; private
  repos need Devin. Source: search summary of `docs.devin.ai/work-with-devin/deepwiki` and related
  pages (2026-09-18).

`context7.json` validates against the schema's documented constraints (checked by hand against the
field list above, not machine-validated - no local schema validator was worth adding for one file):
`projectTitle` 10 chars, `description` 156 chars, one `rules` entry, `additionalProperties: false`
respected (no extra keys).

**Review:** one blind pass (Sonnet subagent) over the two-file diff. Two findings, both real:
(1) FINDABILITY.md claimed `context7.json` was "already committed" while still only staged - fixed
by dropping the claim; (2) the more material one - `curl https://api.github.com/repos/zejji/fuzzy-regex-cs`
returns 404 unauthenticated, meaning the repo is currently private or not yet pushed to that URL,
which both Context7's public-repo submission and DeepWiki's no-account public-repo flow depend on.
Reproduced myself (same 404) before fixing; added a prerequisite note to FINDABILITY.md rather than
guessing which of "private" or "not pushed" is true. Both fixes are doc-text only, touched nothing
the reviewer hadn't already seen, so no second blind pass was needed. No probes, second-engine
commands or oracle waves in this slice, so the independent-verifier step (amendment 16(d)) does not
apply - nothing for it to reproduce.
