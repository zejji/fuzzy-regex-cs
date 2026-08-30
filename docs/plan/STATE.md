# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06 to S12 done, **one pending, and it closes the phase**.

**Current slice:** none in flight. The tree is clean.

**Where S12 left the port:** ratchet GREEN, 1812 passing (baseline 1812), 3905 total, nothing
failing; overall parity 1.6%. **The parser is finished apart from S13's two constructs**, and the
replacement-template compiler and `Escape` are done. 1502 of the corpus's 1659 rows pass and none
fails; all 157 skips are S13's - 127 `fuzzy-syntax`, 30 `named-lists`. `escape-function` has left
the status board.

**Next action:** S13, `docs/plan/slices/S13-fuzzy-syntax-named-lists-phase-close.md`. Nothing
blocks it. Note two corrections to its text: the corpus is 1547 compiles / 50 errors / 62 templates
(not 1534/46/62), and `needs:parse-errors` is now 63 tests, not 72 - S12 un-skipped 8 of them.

**Blockers:** none.

**Worth knowing before the next slice:**

- **A wave beats the corpus for anything upstream's own suite is thin on.** S12's found nothing,
  but the corpus has no malformed template at all; the recipe is `.scratch/s12_wave_record.py`
  plus the scratch console in `.scratch/wave/` (`AssemblyName` `FuzzyRegex.Benchmarks`, so
  `InternalsVisibleTo` reaches the parser). Always run a negative control.
- **`Match.Result` is still unwired** and is the one piece of S12's scope not delivered: `Match`
  is a fieldless stub, so there is nothing to compile a template against until Phase 3.
- **`SubTemplateNumericEscapeTests`' 12 invalid-group-reference rows need a match**, so they stay
  skipped for Phase 3, unlike the seven `\g<...>` syntax errors S12 turned on.
- **`fuzzy = false` is hard-coded twice in `PatternCompiler.Compile`** and both become real in S13.
- **`\uXXXX` written through any agent tool - Write, Edit or a Bash heredoc - is decoded before it
  reaches disk.** A literal U+2028 in a C# comment is a source line terminator and will not
  compile. Build the text with `chr(0x5c) + 'u2028'` in a Python one-liner instead.
- **Regenerating anything Unicode:** `tools/transliterate-unicode.py`, then
  `tools/build-character-names.py`, then `tools/build-lowercase.py`, then
  `tools/record-unicode-fixtures.py` - in that order. `oracle.yml` runs all four with `--check`.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
