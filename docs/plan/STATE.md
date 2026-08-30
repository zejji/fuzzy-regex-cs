# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06 to S09 done, four pending.

**Current slice:** none in flight. The tree is clean.

**Where S09 left the port:** ratchet GREEN, 851 passing tests (baseline 851), 3749 total, nothing
failing. The whole of `_regex_unicode.c` is transliterated into `src/FuzzyRegex/Unicode/*.g.cs`
(253 tables, 104 functions), the `_regex.c` helpers over it are hand-ported beside them, and
`\N{...}` has a Unicode 17.0.0 name table. `unicode-tables` is off the waiting board and the four
ASCII-only seams S07 left are deleted. `character-classes` is still the biggest block at 573.

**Next action:** S10, `docs/plan/slices/S10-character-classes-and-case-folding.md` - sets,
properties, case folding and inline flags. It is the biggest slice of the phase and everything it
needs from S09 is in place and proved.

**Blockers:** none.

**Worth knowing before the next slice:**

- **S10's five entry points are `RegexModule.FoldCase`, `GetAllCases`, `GetExpandOnFolding`,
  `HasPropertyValue` and `GetProperties`**, plus `UnicodeCharacterNames.TryLookup` for
  `parse_named_char`. The `needs:case-folding` seams in `Nodes.cs` - `Character`'s constructor,
  `Branch._is_folded`, `Sequence` - are S10's, as is `is_cased_i`'s remaining `LOCALE` seam.
- **Suspect the parser before the tables.** The tables are checked against upstream exhaustively
  (every codepoint, every property/value pair); the corpus is not. A row that disagrees only in
  set-member order is a third set-order leak, not a folding bug.
- **Regenerating anything Unicode:** `tools/transliterate-unicode.py`, then
  `tools/build-character-names.py` (downloads the UCD, cached in `.scratch/`), then
  `tools/record-unicode-fixtures.py` - in that order, because the last reads the second's output.
  `oracle.yml` runs all three with `--check`.
- **The ratchet now keys test ids ordinally.** S09 found that `Update-Baseline` and `Test-Ratchet`
  used PowerShell's case-insensitive defaults, so two tests whose names differ only in case
  collapsed into one and the gate stopped watching the other. S10 writes case-folding tests, so
  keep an eye on the baseline count matching the reported passing count.
- **Local `regex` is 2026.7.19; upstream is pinned at 2026.8.12.** The Unicode tables have not
  changed since 2025-10-20, so the fixtures agree either way, but CI regenerates them against a
  build of the pinned submodule.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
