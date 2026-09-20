# Findability

How coding assistants discover this library. Both steps below need the owner's accounts and are
not automatable from a session; `context7.json` (repo root) is the machine-readable half.

**Prerequisite: the repo must be public first.** Checked 2026-09-18:
`curl https://api.github.com/repos/zejji/fuzzy-regex-cs` returns 404, which GitHub gives for both
a private repo and one that doesn't exist there yet. Both steps below need a public GitHub repo at
that URL - check that before assuming either failed.

## Owner step 1: Context7

Submit at https://context7.com/add-library?tab=github with the repo URL
`https://github.com/zejji/fuzzy-regex-cs`. Context7 reads `context7.json` from the repo root
automatically once indexed - no extra config to paste in.

**Proof it worked:** query an assistant with Context7 MCP enabled, e.g. "use context7: what does
fuzzy-regex-cs say about differences from Python's `regex` module?" A working index answers from
`docs/COMPARISON.md` (the file `context7.json`'s `rules` field points at), not from a generic
regex answer.

## Owner step 2: DeepWiki

Visit `https://deepwiki.com/zejji/fuzzy-regex-cs` (replacing `github.com` with `deepwiki.com` in
the repo URL is enough to trigger indexing for a public repo; no account needed). Indexing takes a
few minutes.

**Proof it worked:** the page renders a generated wiki instead of an "index this repo" prompt, and
answering a question there about upstream differences cites `docs/COMPARISON.md`.

## Deferred

`llms.txt` is deferred until a docs site exists to serve it from (the 2026-09-16 research rated it
"skip" for now) - there is no canonical single-file summary to point it at yet, and a docs site is
its own slice.
