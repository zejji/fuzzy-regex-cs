# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06, S07 and S08 done, five pending.

**Current slice:** none in flight. The tree is clean.

**Where S08 left the port:** ratchet GREEN, 786 passing tests (baseline 786), 3688 total.
Compile-parity corpus at 693 of 1547 compiles and 23 of 50 errors, nothing failing. `quantifiers`
fell from 460 waiting tests to 179, `anchors` from 261 to 87, `alternation` off the board.
`character-classes` is now the biggest block at 573, which is what S10 delivers.

**Next action:** S09, `docs/plan/slices/S09-unicode-tables.md` - transliterate
`upstream/src/_regex_unicode.c` into `src/FuzzyRegex/Unicode/`, port the C helpers the parser
calls, and build the `\N{name}` table. It is the gate on S10 and on 573 waiting tests.

**Blockers:** none.

**Worth knowing before the next slice:**

- **S09 deletes four ASCII-only seams**: `is_cased_i`, `str.isdigit`, `str.isidentifier` and
  `str.isalpha` all answer only below U+0080 today and throw `needs:unicode-tables` above it.
  PORTMAP's "Where we diverge" carries the rows to remove. S08 added two more seams S09 owns:
  `Branch._is_folded` (needs `fold_case` and `get_expand_on_folding`) and, from S07,
  `Sequence._flush_characters`.
- **A green corpus does not cover a boundary the corpus never reaches.** `CompiledPattern.ReqOffset`
  was an `int` that silently truncated any offset above 2^31, and 693 rows passed with the bug in
  place; it was found only by writing a gap test for a pattern upstream's own compiler cannot
  compile. Prefer a `Gaps/` test over a corpus row for anything at a numeric limit.
- **Upstream's bytecode for a pattern its C compiler chokes on** is still readable: monkeypatch
  `regex._regex.compile` to capture its arguments and raise instead of calling through. Calling
  through is what made the dead S08 session look hung. Recipe in DECISIONS, 2026-08-30.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
