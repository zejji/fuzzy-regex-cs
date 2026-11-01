# FuzzyRegex

@AGENTS.md

@docs/plan/STATE.md

On "continue", follow AGENTS.md "Checkpoint and resume". Claude Code reads AGENTS.md by itself only
when no CLAUDE.md applies (code.claude.com/docs/en/memory, checked 2026-11-01), hence the import.

<!-- headroom:serena-instructions -->
# Serena — Symbol-First Code Navigation

Serena's MCP tools expose this project's code as a symbol graph backed by a
language server. **Prefer these tools over reading whole files** — they return
only the code you need, cutting context usage sharply. Read a file end-to-end
only when a symbol view is insufficient (non-code files, or when you need the
surrounding glue).

## Preferred workflow
- `get_symbols_overview(<file>)` — list a file's top-level symbols before opening it.
- `find_symbol(<name>)` — fetch a symbol's definition/body instead of reading the file.
- `find_referencing_symbols(<name>)` — find call sites / usages instead of grepping.
- `find_declaration(<name>)` — jump to where a symbol is defined.

## Rule
Reach for a symbol tool first; fall back to reading a whole file only when the
symbol view does not answer the question.
<!-- /headroom:serena-instructions -->
