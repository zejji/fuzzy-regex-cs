---
slice: S12
phase: 2
title: Replacement templates, Escape, and the deferred API decisions
delivers: [escape-function]
---

# S12 - Replacement templates, `Escape`, and the deferred API decisions

## Scope

- **Replacement templates**: `_compile_replacement`, `parse_repl_hex_escape`,
  `parse_repl_named_char`, `compile_repl_group` (`_regex_core.py:1801-1923`) and
  `_compile_replacement_helper` (`_main.py:687-754`) minus its cache. The template language is
  upstream's (`\1`, `\g<name>`, `\n`, `\x41`, `\N{...}`; `$` is literal), settled in DECISIONS
  2026-08-29. Wire it into `Replace`, `Replace(..., out int)`, `Match.Result` and the static
  `Replace` **ahead of matching**, exactly as upstream does: measured 2026-08-30,
  `regex.sub('x', r'\g<bad', 'abc')` raises `missing >` and a trailing backslash raises
  `bad escape (end of pattern)` even though nothing matches, whereas `regex.sub('x', r'\2',
  'abc')` returns `'abc'` - the group-number check happens at expansion. So `Replace` compiles
  the template, then throws `NotImplementedException` where matching would begin.
- **`Escape`** (`_main.py:388-420`, `_METACHARS` at `:447`): both flags, all four combinations,
  as the S01 doc comment already specifies.
- **Remove `FuzzyRegex.Unescape`** (owner decision C, 2026-08-30). S01 added it to mirror
  `Regex.Unescape`; upstream has no such function, no test uses it, and the corpus cannot check
  it. Remove its `ApiSurfaceStubTests` reference if S07 left one.
- **Deferred API decisions are all "do not add in Phase 2"** (owner decision D, 2026-08-30,
  already in DECISIONS). This slice only makes the record consistent: update the `Match.pos`
  row of PORTMAP's "Deliberately not ported" table from "Phase 2 decides" to decided, and add
  rows for `allcaptures`/`allspans`/`groupdict`/`capturesdict` and for public `Word`/`Ascii`
  options if they are not already there. Nothing is added to the public surface. Background:
  `docs/plan/2026-08-30-phase2-decisions.md`, section 6.

## Tests

- Un-skip `needs:escape-function` (12 tests).
- Corpus template rows (62) pass. Corpus rows are keyed by pattern, flags and template; the
  seam for templates takes the compiled pattern's `group_index`, as `compile_repl_group` does.
- The template subset of `needs:parse-errors` whose prose says the template itself is malformed
  (`\g<name` without `>`, trailing backslash: 8 skip attributes, `SubTemplateNumericEscapeTests`,
  `SymbolicRefsTests`, `BasicRegexSubTests` and the `Regressions` file) may be un-skipped **only
  after checking against the oracle that upstream raises with a non-matching subject** for that
  exact template, as above. An "invalid group reference" assertion needs a match and stays
  skipped for Phase 3. The tag is delivered as a whole by S13; this slice un-skips by prose,
  which DECISIONS 2026-08-29 allows and requires reading first.

## Done when

- [ ] `escape-function` tests pass; template corpus rows pass; template parse-error tests
      handled as above.
- [ ] `Unescape` removed; PORTMAP rows for the deferred members current.
- [ ] `docs/PORTMAP.md` updated; the public surface diff reviewed as its own pass (rule 4 of
      `docs/VERIFICATION.md`: API changes are unreviewed until a reviewer has seen them).
- [ ] Ratchet GREEN, baseline updated, commit.
