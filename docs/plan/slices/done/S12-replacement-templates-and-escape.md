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

- [x] `escape-function` tests pass; template corpus rows pass; template parse-error tests
      handled as above.
- [x] `Unescape` removed; PORTMAP rows for the deferred members current.
- [x] `docs/PORTMAP.md` updated; the public surface diff reviewed as its own pass (rule 4 of
      `docs/VERIFICATION.md`: API changes are unreviewed until a reviewer has seen them).
- [x] Ratchet GREEN, baseline updated, commit.

## Closing notes (2026-08-31)

**What landed.** `_compile_replacement`, `parse_repl_hex_escape`, `parse_repl_named_char` and
`compile_repl_group` in `ParseFunctions`; `_compile_replacement_helper` and its `make_string`
closure as `PatternCompiler.CompileReplacement`; `escape` and `_METACHARS` as `FuzzyRegex.Escape`.
`Unescape` is gone. `Replace`, `Replace(..., out int)` and the static `Replace` compile the
template and then throw `NotImplementedException` where matching would start. Ratchet GREEN,
1812 passing against a baseline of 1692 - **+120**: 10 `escape-function`, 7 `SymbolicRefsTests`,
1 `RegressionsParseErrorTests`, all 62 template corpus rows, and 40 new gap tests.

**Surprises, all measured rather than reasoned.**

- **A bare `\N` in a template is an error, not the literal `N`.** The pattern side rewinds and
  makes a literal; `_compile_replacement` rewinds and then falls through to `bad escape \N`. And
  the name alphabet is `ALPHA | {" "}`, not the pattern side's `ALNUM | {" ", "-"}`, so `\N{1}`
  and `\N{LATIN-SMALL-LETTER-A}` are errors too. Both pinned by `ReplacementTemplateTests`.
- **`parse_repl_hex_escape` has no range check** where `parse_hex_escape` does, so `\UFFFFFFFF`
  is a well-formed escape that fails later in `chr()`. The ported items are therefore `long`, not
  `int`, and `MakeString` raises `NotSupportedException` above U+10FFFF.
- **`Match.Result` could not be wired** as the slice asked. `Match` is a stub with no fields and
  nothing constructs it, so there is no pattern to compile a template against; wiring it would
  mean inventing `Match`'s internals, which is Phase 3's design work. Left throwing, and this is
  the one part of the slice's scope not delivered.
- **Which template errors can be tested before the engine exists was measured, not guessed.**
  `.scratch/s12_repl_errors.py` ran each template against a subject that cannot match: all seven
  `\g<...>` syntax errors and the trailing backslash raise, while the whole
  invalid-group-reference family (`\1`, `\8`, `\118`, `\800`, ...) returns the subject unchanged,
  because that check happens during expansion. So `SubTemplateNumericEscapeTests`' 12-row
  parse-error test stays skipped and is Phase 3's.

**Retagged, not un-skipped.** Two of the twelve `needs:escape-function` tests
(`test_re_escape` #2-3 and #4) call `MatchAtStart` as well as `Escape`, so they now carry
`needs:basic-matching` with prose naming the combination - the rule DECISIONS 2026-08-29 set after
S02's `splitting` mis-tag. `escape-function` has left the status board.

**Analyzers.** CA2208, S3928 and MA0015 all demand that an `ArgumentException`'s `paramName` name
a parameter of the throwing method. Obeying that in `CompileReplGroup` would mean either a false
name or an extra parameter carrying a string the `Source` already holds, so all three are
disapplied at that one line with the reason in a comment - narrower than the `.editorconfig`
per-directory block, which would have turned them off for the whole parser. Nothing else was
suppressed; the other four findings (S3241, S4136, S127, MA0015 on the missing name) were fixed.

**Verification.** Beyond the corpus, a differential wave (`.scratch/s12_wave_record.py` plus the
scratch console in `.scratch/wave/`): 1,410 generated templates x 3 group contexts = 4,230 rows,
2,618 successes and 1,612 errors, compared on result, exception class, message and offset -
**0 failures**. Negative control: forcing the octal mask to `0xFF` reported the `\400`/`\777`
rows. `Escape` was compared over **all 1,114,112 codepoints x 4 flag combinations**, both as a
per-codepoint decision bitmap (0 differences) and as a SHA-256 of the concatenated output (all
four digests equal). Two mutations were run against the new gap tests and both went red.

**Review.** One blind pass over the whole diff, briefed per `docs/VERIFICATION.md` and read in the
same turn. **Findings raised: 0. Reproduced: 0. Fixed: 0.** The reviewer built its own independent
wave rather than reading only - 105,148 templates x 4 group contexts = 420,592 rows, plus 40,000
multi-character `Escape` strings x 4 flag combinations = 160,000 rows - and reported
`failures=0` on both, re-measured every oracle claim in the diff's comments, checked every
upstream line reference, and mutation-tested one of the new tests to prove it can fail. **No
second pass was needed: nothing changed after the review**, so there is no unreviewed delta.
