---
slice: S06
phase: 2
title: Compile-parity corpus - the oracle for the parser and compiler
delivers: []
---

# S06 - Compile-parity corpus

## Why this exists first

Phase 2 ports `_regex_core.py`: pattern text in, bytecode out. Nothing in Phase 2 can match a
string, so the ported suite cannot verify any of it, and the match-level differential oracle
(Phase 3) has nothing to compare. Without this slice, seven parser slices would rest on nothing
but their own reading of the Python.

The compiler's output is a list of integers, and upstream's is observable: `_main._compile`
(`upstream/regex/_main.py:460-686`) hands the finished code to `_regex.compile` as plain Python
values. Intercepting that call while upstream's own test suite runs records, for every pattern the
suite compiles, exactly what our compiler must produce. Measured on 2026-08-30 with the local
`regex` 2026.7.19 (behaviour-identical to the pin, DECISIONS 2026-08-29): the suite runs in
0.2 seconds and yields **1534 unique (pattern, flags, named lists) compiles, 46 unique parse
errors and 62 unique replacement templates**, 29,219 bytecode integers in all, over 32 distinct
flag combinations. No pattern contains a character above U+FFFF, so every offset is the same in
UTF-16.

This is a stronger check than the ported tests will ever give the parser: bit-exact output for
every construct the upstream suite exercises, before a single matching opcode exists.

## Scope

1. **Recorder**: `tools/record-compile-corpus.py`. Runs `upstream/regex/tests/test_regex.py`
   under `unittest` against the installed `regex` module with three hooks:
   - `regex._regex.compile` wrapped, recording the full argument tuple for `str` patterns:
     pattern, flags, `code`, `group_index`, `named_lists`, `named_list_indexes`, `req_offset`,
     `req_chars`, `req_flags`, `group_count`.
   - `regex._main._compile` wrapped with `cache_it=False`, recording `(msg, pos)` of every
     `regex.error` raised for a `str` pattern.
   - `regex._main._compile_replacement_helper` wrapped, recording the compiled template list
     (ints for group references, strings for literals). The C engine looks this function up by
     name at call time (`_regex.c:19918`, `:21835`), so the wrap is seen.

   The test file must be imported from a copy outside `upstream/` so `import regex` resolves to
   the installed module, not the uncompiled submodule package. Set `PYTHONDONTWRITEBYTECODE=1`
   and `PYTHONIOENCODING=utf-8` (DECISIONS 2026-08-29).

   Output: one JSON fixture, `tests/FuzzyRegex.Tests/Gaps/CompileParity/corpus.json`, sorted by
   key, LF line endings, integers as integers (the code is `uint32`: `UNLIMITED` is
   `0xFFFFFFFF`).

2. **The order hazard, which the recorder must solve.** 84 of the 1534 compiles produce
   different bytecode from one Python process to the next (measured with two `PYTHONHASHSEED`
   values; a fixed seed does not help, because `RegexBase.__hash__` hashes the node's class
   object, whose identity is per-process). Two sites turn a Python `set` into an ordered list:
   `_check_firstset` (`_regex_core.py:380-411`, `members` into `SetUnion(info, list(members))`)
   and `Branch._reduce_to_set` (`:2418`, `items` into `Branch._flush_set_members`). Set-member
   order has no matching semantics, so the fix is to **sort at exactly those points, on both
   sides**: the recorder wraps the two functions to sort by a key both languages can compute
   (class name, then the node's `_key` values), and the port sorts identically in S07 and S08.
   Record the deviation in the "Where we diverge" table of `docs/PORTMAP.md`. Do not
   canonicalise in the comparator instead: that needs a bytecode decoder, which is Phase 3 work
   and a second thing to get right.

   Proof, not assumption: after the fix, run the recorder twice under different
   `PYTHONHASHSEED` values and require byte-identical fixtures. If any row still differs, there
   is a third leak; find it before closing the slice.

3. **Internal seam** the corpus tests call. In `src/FuzzyRegex/Parsing/`, an
   `internal static` entry point shaped after the tail of `_main._compile`: given pattern text,
   options and named lists, return the same tuple upstream hands to `_regex.compile`. Name and
   shape are the slice's call; it throws `NotImplementedException` in this slice. Tests reach it
   through the existing `InternalsVisibleTo`.

4. **Corpus tests**, in namespace `Fuzzy.Text.RegularExpressions.Tests.Gaps.CompileParity`, one
   TUnit test per fixture row via a data source, with the pattern and flags in the test name so
   the ratchet baseline shows exactly which rows a slice turned on. Three test methods: compiles
   (compare every recorded field, not just `code`), errors (message text and offset; messages
   are ported verbatim), templates. Rows whose compile throws `NotImplementedException` **skip
   at runtime** with a `needs:` reason naming the unported construct when the exception says
   which; any other outcome fails. This is what lets the ratchet stay green while the parser is
   half built, and turn red the moment a ported construct emits the wrong bytes.

5. **Keep the fixture honest.** Add a step to `.github/workflows/oracle.yml`, after the oracle
   version assertion, that re-runs the recorder against the oracle built from `upstream/` and
   fails on any diff against the committed fixture. A stale fixture would make every later slice
   chase phantom divergences.

## Out of scope

The match-level differential harness in `tests/FuzzyRegex.OracleTests/`. That opens Phase 3, as
the roadmap says, because until the VM exists there is nothing to run it against.

## Done when

- [ ] The recorder produces the fixture, and two runs under different `PYTHONHASHSEED` values
      produce identical bytes. State the row counts in the closing notes.
- [ ] Every fixture row is a TUnit test; all skip (the seam is a stub); the Gaps table in
      `docs/STATUS.md` shows them.
- [ ] A mutation proves the tests have teeth: hand-edit one integer in a fixture row, point the
      seam at a hard-coded answer for that pattern, watch the row fail, revert both.
- [ ] `oracle.yml` regenerates the fixture and diffs it.
- [ ] `docs/PORTMAP.md` "Where we diverge" carries the set-order sorting.
- [ ] `tools/check-ratchet.ps1` GREEN, `-UpdateBaseline`, blind review, commit, slice moved to
      `done/`, STATE.md rewritten.

## Notes

- The `regex` on this machine is 2026.7.19 while the pin says 2026.8.12. DECISIONS 2026-08-29
  measured the two as identical in every file that matters; the oracle-job diff is the ongoing
  check.
- The corpus is what upstream's suite happens to compile. It is not exhaustive and it says
  nothing about matching. Both gaps belong to Phase 3 and Phase 6, deliberately.
