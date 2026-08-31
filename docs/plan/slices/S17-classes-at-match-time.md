---
slice: S17
phase: 3
title: Character classes, ranges, sets and properties at match time
delivers: [character-classes, escapes, unicode-properties, set-operations, ascii-flag]
---

# S17 - Character classes, ranges, sets and properties at match time

Needs S16. Membership tests only - single-position opcodes over the S09/S10 tables the parser
already consults - so it is the cheapest big win after the spine: `escapes` (134), `character-
classes` (114), `unicode-properties` (95) all sit behind it. Forward, case-sensitive variants
only; `_IGN`/`_FLD` are S22 and `_REV` is S23.

## Scope

All line references are `upstream/src/_regex.c`.

- **Predicates**: `matches_PROPERTY` (`:2924`), `matches_RANGE` (`:3003`), `in_range` (`:2816`),
  `matches_member` (`:3025`), `in_set_diff` / `in_set_inter` / `in_set_sym_diff` / `in_set_union`
  (`:3155`, `:3201`, `:3236`, `:3278`), `matches_SET` (`:3313`).
- **Property lookup by encoding**: `unicode_has_property` (`:1362`) over the transliterated
  tables (`src/FuzzyRegex/Unicode/UnicodeProperties.g.cs` etc., landed S09), and
  `ascii_has_property` (`:822`) - the ASCII flag's whole matching story is that the encoding
  table swaps, which is why `ascii-flag` is deliverable here. The locale encoding stays not
  ported (PORTMAP).
- **Main-switch cases**: `PROPERTY` (`:13804`), `RANGE` (`:13914`), `SET_DIFF` / `SET_INTER` /
  `SET_SYM_DIFF` / `SET_UNION` (`:14446-14449`); their entries in the shared one-character
  backtrack block (`:15210-15243`).
- The `match_many_*` bulk variants for these ops (`:4045`, `:4485`, `:4737`) only if S16's spine
  already routes single steps through a `match_many`-shaped helper; otherwise they arrive with
  the repeat opcodes in S19, which is their only caller upstream.

## Verification

- **Un-skip** `needs:character-classes`, `escapes`, `unicode-properties`, `set-operations` and
  `ascii-flag`, reading each skip's prose first; tests that also need quantifiers, groups or
  case-insensitivity move to those tags with prose saying so.
- **Oracle wave**: generated classes - single chars, ranges, negations, nested sets with all four
  operators under V1, POSIX classes, `\p{...}` properties (reuse S10's property-name blocks),
  `\d\D\w\W\s\S` under plain and ASCII flags - over subjects spanning ASCII, Latin-1, BMP and
  astral. This is where a wrong table *lookup* first becomes observable (S10 verified the tables'
  content; this verifies the engine reads them at the right codepoint). Zero divergences; quote
  counts; negative control.
- A corpus-style caution from S10 applies: if a set behaves wrongly, check the S09 fixture tests
  are still green before suspecting the engine.

## Done when

- [ ] The five tags delivered or their stragglers retagged with prose; counts in the closing
      notes.
- [ ] Oracle wave green, counts quoted, any divergence minimised into a permanent test.
- [ ] `docs/PORTMAP.md` updated with every symbol ported here.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a property lookup by `char` instead of
      by codepoint on an astral subject, a set operator evaluated left-to-right where upstream
      nests, a negated set that forgets the newline/ANY interaction), commit.
