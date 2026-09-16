# Junie guidelines for this worktree (Phase 8: documentation)

Read `AGENTS.md` at the repo root first; it holds the repo's conventions and they apply here.
This file adds only what is specific to the documentation stream.

## What this worktree is for

Phase 8 of the port: user documentation, packaging and the 1.0 release notes. The code is not
yours to change. Edit only `README.md`, `docs/COMPARISON.md`, `CHANGELOG.md`, and files under
`docs/` that the task names. Never edit `src/`, `tests/`, `docs/DIVERGENCES.md`,
`docs/PORTMAP.md` or anything under `docs/plan/`; those are the sources you read from.

## Sources of truth, in order

1. `docs/DIVERGENCES.md`: every deliberate difference from Python's `regex` module, with a Status
   column. Document only rows whose Status is SHIPPED. A PLANNED row is not yet true of the code.
2. `src/FuzzyRegex/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`: the exact public surface.
   A member not listed there does not exist; do not invent one.
3. The XML doc comments in `src/FuzzyRegex/*.cs` for a member's meaning.
4. `upstream/` holds the Python source being ported, for the Python side of any comparison.

When a source and your memory of Python `regex` or .NET `Regex` disagree, the source wins, and
say so in the text if the difference is one a reader would trip over.

## Writing rules

- Every code example is complete, compiles against the public API as listed, and shows its
  expected output in a comment on the next line. Do not show an example whose output you have not
  derived from the sources above.
- Each section stands alone: name the member, state what it does, state the difference from
  Python `regex` and from .NET `Regex` where one exists, then the example. Readers (and language
  models) arrive at a section without the ones before it.
- Plain English, short sentences, no hedging, no marketing. British spelling. A normal hyphen,
  never an em-dash.
- Do not paraphrase a DIVERGENCES row loosely; quote its behaviour exactly and link the row by
  its heading.
- Leave a `<!-- TODO(owner): ... -->` comment where a source is missing or ambiguous rather than
  guessing.

## What done looks like

The task text names the file(s) and the acceptance check. Run `git diff --stat` at the end and
list every file you touched in your final message, with one line per file on what changed.
