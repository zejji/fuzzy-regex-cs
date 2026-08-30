---
slice: S07
phase: 2
title: Parser and compiler skeleton - literals, groups, the compile pipeline
delivers: [pattern-properties]
---

# S07 - Parser and compiler skeleton

## Why a vertical slice

Upstream's file is horizontal: 800 lines of parse functions, then 2,200 lines of node classes.
Porting it in that order would leave the parse functions unverifiable until the node classes
exist. Instead every Phase 2 slice is vertical - parse function, node class, bytecode - for one
family of constructs, so the S06 corpus verifies each slice as it lands. This first one builds
the spine everything else hangs on: read a pattern of literals and groups, produce upstream's
exact bytecode.

## Scope

All in `src/FuzzyRegex/Parsing/`, mirroring `upstream/regex/_regex_core.py` name for name.
Keep upstream's function and variable names recognisable and quote the upstream line above
anything non-obvious (AGENTS.md house rules).

- **Constants**: `RegexFlag` values (`:73-90`) as internal constants carrying **every** upstream
  bit, including `ASCII`, `LOCALE`, `UNICODE`, `WORD`, `DEBUG` and `TEMPLATE`, which the public
  `FuzzyRegexOptions` does not expose but the parser needs internally. `DEFAULT_VERSION`,
  `DEFAULT_FLAGS`, `GLOBAL_FLAGS`, `SCOPED_FLAGS`, `ALPHA`, `DIGITS`, `ALNUM`, `OCT_DIGITS`,
  `HEX_DIGITS`, `SPECIAL_CHARS`, `UNLIMITED` (`uint.MaxValue`: `_regex.get_code_size()` is 4),
  `HEX_ESCAPES`, the `OPCODES` table (`:212-296`) as an enum whose numeric values are the
  table positions, and `POSITIVE_OP` and friends (`:1923-1933`).
- **`Source`** (`:4110-4356`) and **`Info`** (`:4356-4420`), complete. `char_type` and the
  bytes branches drop; this port is `char`-based. `ignore_space` is wired even though `(?x)`
  parsing arrives in S10.
- **`error`** (`:30-60`) maps onto `FuzzyRegexParseException`: message text verbatim, `pos` as
  `Offset`. `ParseError`, `_UnscopedFlagSet` and `_FirstSetError` become internal exceptions.
- **Node classes**: `RegexBase` (`:1941-2010`, including `__eq__` and `__hash__` - `Branch`
  optimisation in S08 relies on structural equality, so implement `Equals` and `GetHashCode` on
  every node from the start), `Sequence` (`:3502-3715` including `pack_characters`,
  `_flush_characters`, `_merge_chunks`; `_fix_full_casefold` may throw `NotImplementedException`
  until S10), `Character` (`:2581`), `String` and `Literal` (`:4007-4069`), `Any`, `AnyAll`,
  `AnyU` (`:2040-2074`), `Group` (`:3060-3142`).
- **Parse functions**: `_parse_pattern`, `parse_sequence` (`:452-548`) with every branch that is
  not this slice's construct throwing `NotImplementedException` naming the construct (that
  message is what the corpus tests turn into a `needs:` skip reason); `parse_paren` for `(...)`,
  `(?:...)`, `(?P<name>...)`, `(?<name>...)` (`:850-942`); `parse_name` (`:1225`);
  `parse_escape` for the literal escapes only - `\n \t \r \f \v \a`, `\xhh`, `\uhhhh`,
  `\Uhhhhhhhh`, octal, `\0`, and the "any other character escapes itself" default
  (`:1256-1416`).
- **Pipeline**: the tail of `_main._compile` (`_main.py:460-686`) minus caching and bytes:
  the `_UnscopedFlagSet` retry loop, the unbalanced-parenthesis check, version and encoding
  checks, `fix_groups`, `optimise`, `pack_characters`, `_get_required_string` (`:4460`),
  `_check_group_features` (`:4421`, trivially empty until S11), `compile`, `_flatten_code`,
  `_compile_firstset` and `_check_firstset` (`:370-411`, **with the sorted set order S06
  recorded**), the `SUCCESS` opcode, and the `index_group` mapping. The result feeds the S06
  seam.
- **Public API**: `FuzzyRegex`'s constructor compiles for real. `Pattern`, `Options` (upstream's
  `info.flags | version`, so `(?i)` in the pattern shows up, as `test_getattr#2` expects),
  `GroupNames`, `GroupNumbers`, `GroupNameFromNumber`, `GroupNumberFromName`, `MatchTimeout`,
  `NamedLists` (empty until S13), `ToString`. Matching members keep throwing
  `NotImplementedException` until Phase 3.

## Tests

- Un-skip `needs:pattern-properties` (9 tests). Read each skip's prose first.
- Rewrite `tests/FuzzyRegex.Tests/Gaps/Api/ApiSurfaceStubTests.cs`: the "throws
  NotImplementedException" assertions flip to real assertions on the compiled pattern, as S01
  intended. Retire the old ids with `check-ratchet.ps1 -AcceptRemovals`, never by hand-editing
  the baseline.
- **The thread-safety gate DECISIONS 2026-08-29 requires is due now**: this slice adds the first
  instance fields to `FuzzyRegex`, so add the reflection test asserting every instance field of
  `FuzzyRegex` is `initonly`. Mutate one field to prove it fires.
- Corpus rows for pure literals, dots, and plain or named groups must pass; report the count.
  Any row that fails rather than skips is a defect in this slice.

## Done when

- [ ] Corpus rows within scope pass, the rest skip with a `needs:` reason; no row fails.
- [ ] `pattern-properties` tests pass; `ApiSurfaceStubTests` rewritten; `initonly` test in.
- [ ] `docs/PORTMAP.md` lists every symbol ported, with `_regex_core.py` line numbers.
- [ ] Ratchet GREEN, baseline updated, blind review (brief it to hunt: a Python `dict` iterated
      where insertion order matters, `str` indexing that is codepoint-based upstream, an
      off-by-one between `Source.pos` and `FuzzyRegexParseException.Offset`), commit.

## Notes

- `DEFAULT_VERSION` is `VERSION0` in `_main.py:443`, overriding `_regex_core.py:161`. Our
  default is `Version0` too; `test_getattr#2` pins it.
- **Remove `FuzzyRegexOptions.ExplicitCapture`** in this slice (owner decision C, 2026-08-30,
  `docs/plan/2026-08-30-phase2-decisions.md`). It has no upstream counterpart, no test uses it,
  and the corpus cannot verify a construct upstream does not have. Update the enum's doc comment
  and `ApiSurfaceStubTests`, which lists the option values.
- The decisions document above is the background for anything in this phase that looks like a
  departure from the spec as first written.
