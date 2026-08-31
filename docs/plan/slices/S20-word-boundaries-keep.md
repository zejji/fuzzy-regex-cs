---
slice: S20
phase: 3
title: Word, default and grapheme boundaries, and the KEEP marker
delivers: [anchors, line-boundaries, word-flag, keep-marker, grapheme]
---

# S20 - Word, default and grapheme boundaries, and the KEEP marker

Needs S17 (word boundaries are property lookups). The zero-width predicates S16 did not cover:
everything that asks "is this position at a boundary" rather than "does this character match".

## Scope

All line references are `upstream/src/_regex.c`.

- **Main-switch cases**: `BOUNDARY` (`:12060`), `DEFAULT_BOUNDARY` (`:12255`),
  `DEFAULT_START_OF_WORD` / `DEFAULT_END_OF_WORD` (`:12294`, `:12274`), `START_OF_WORD` /
  `END_OF_WORD` (`:14700`, `:13111`), `GRAPHEME_BOUNDARY` (`:13157`), `KEEP` (`:13543`) with its
  backtrack case (`:16439`); the zero-width entries in the shared backtrack block
  (`:15330-15344`) that S16 did not already land.
- **Word predicates, per encoding**: `unicode_word_left` / `unicode_word_right` (`:1447`,
  `:1453`), `unicode_at_boundary` (`:1460`), `unicode_at_word_start` / `_end` (`:1471`,
  `:1482`); the ASCII counterparts (`:849-893`). The WORD flag (`(?w)`) selects the DEFAULT_
  opcodes at parse time (already done), whose predicates are the Unicode word-break rules:
  `unicode_at_default_boundary` (`:1531-1751`, WB5-WB16 with `is_unicode_vowel` `:1496` and the
  apostrophe special case `:1514`), `unicode_at_default_word_start_or_end` (`:1752-1785`).
- **Grapheme**: `unicode_at_grapheme_boundary` (`:1786-1935`, the GB rules over the GCB
  property, which the transliterated tables already carry - `"GCB"` is in
  `UnicodePropertyNames.g.cs`). This is what makes `\X` match a full cluster, so the `grapheme`
  tests un-skip here.
- **KEEP** (`\K`): moves the reported match start; its interaction with what `Match.Index`
  reports is exactly the two-place span convention again - the engine records `match_pos`,
  the accessor converts.

## Verification

- **Un-skip** the residual `needs:anchors` tests S16 left (word-boundary ones), plus
  `line-boundaries`, `word-flag`, `keep-marker` and `grapheme`, reading each skip's prose first;
  stragglers retag with prose.
- **Oracle wave**: `\b \B \m \M` (word start/end) over scripts with and without cased letters,
  under plain, ASCII and WORD flags; `\X` over combining sequences, ZWJ emoji, regional-indicator
  pairs and CRLF; `\K` in and out of alternations and repeats, comparing the reported span. The
  WB/GB rule tables are the risk - upstream implements a specific Unicode version's rules by
  hand, so probe the boundary cases its code comments name. Zero divergences; negative control.

## Done when

- [ ] The five tags delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green including the emoji/ZWJ/regional-indicator grapheme probes; counts
      quoted.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a word-break rule ported with `>=`
      where upstream chains lookups asymmetrically, a grapheme rule consulting the property of
      the wrong side's codepoint, `\K` inside a lookaround changing the span when upstream says
      it must not - and if lookaround is not portable yet, pin that probe as skipped for Phase
      4), commit.
