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

- [x] Corpus rows within scope pass, the rest skip with a `needs:` reason; no row fails.
- [x] `pattern-properties` tests pass; `ApiSurfaceStubTests` rewritten; `initonly` test in.
- [x] `docs/PORTMAP.md` lists every symbol ported, with `_regex_core.py` line numbers.
- [x] Ratchet GREEN, baseline updated, blind review (brief it to hunt: a Python `dict` iterated
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

## Closing notes (2026-08-30)

**Landed.** `src/FuzzyRegex/Parsing/` now holds the spine of `_regex_core.py`: `RegexFlags`,
`Opcode` (all 81, generated from upstream's table), `Source`, `Info`, the node classes
(`RegexBase`, `Any`/`AnyAll`/`AnyU`, `Character`, `String`/`Literal`, `Sequence`, `Group`,
`PrecompiledCode`), `ParseFunctions`, and `PatternCompiler.Compile` as the real port of
`_main._compile`'s tail. `FuzzyRegex`'s constructor compiles for real and the pattern properties
are live.

**Parity.** 514 tests passing, up from 37. Compile-parity corpus: **436 of 1547 compile rows** and
**16 of 50 error rows** match upstream's bytecode and messages exactly; the remaining rows skip
with a `needs:` tag. **No row fails.** Templates (62) are S12 and skip as a block.

**Plan corrections**, all recorded in DECISIONS: `is_cased_i` is a hard S09 dependency and is
answered ASCII-only here, which is what let `(?i)` patterns compile a phase early; `FlagsTests`
was retagged from `needs:pattern-properties` to `needs:anchors` because it compiles `^pattern$`;
and `test_getattr#2` was ported wrong in S02 - it dropped upstream's `regex.U`, proved against the
oracle rather than papered over. A fourth, found late: `Gaps/Parsing` as a namespace shadows
`Fuzzy.Text.RegularExpressions.Parsing`, fixed by qualifying the one clashing reference.

**Review.** Two blind passes ran. The first raised three findings, all reproduced and all fixed -
the important one being that `\p`, `\P`, `\N` and `\g` threw their `needs:` tags on sight, when
upstream reaches an ordinary literal through all four when the delimiter is absent; that had six
corpus rows and two ported tests skipping for capabilities they do not need. The fixes added ~130
lines the first reviewer never saw, so a second pass ran over that delta alone, as VERIFICATION
rule 4 requires. It raised two findings and confirmed both against upstream:

- `\g<99999999999` - Python's `int()` is arbitrary precision, so upstream parses the number, fails
  on the missing `>`, and degrades the escape to literals. Our `int.Parse` threw
  `OverflowException` straight through `parse_escape`'s catch. **Fixed** with `BigInteger.Parse`.
- `\g<` + a non-ASCII name - upstream degrades to literals here too, but reaching that path means
  getting past `IsDigitName`, which throws its `needs:unicode-tables` seam on any non-ASCII input.
  **Not fixed, deliberately**: upstream's answer does not depend on the Unicode tables, but
  deciding that in general is S09's job and widening the seam on one example is how a port
  acquires a guess. Recorded as a skipped test that S09 turns on.

Both are pinned in `Gaps/Parsing/GroupReferenceFallbackTests.cs` with the oracle output that
proves them.

A **third pass** then ran over that fix delta - the `BigInteger` change, the new test file and the
two tooling patches - because rule 4 counts unreviewed changes, not slices. It raised four
findings and all four survived reproduction, which is well above the usual one-in-five:

1. **The `BigInteger` fix was incomplete.** `Info.IsOpenGroup` parsed the same name with
   `int.Parse` one call later, so the *delimited* `\g<99999999999>` still threw
   `OverflowException` where upstream raises "invalid group reference". Fixed at the second site;
   the delimited path now reaches its `needs:backrefs` seam like every other `\g<N>`. Pinned by a
   test proven red against the unfixed file.
2. **Two allowlist entries were dead and the diagnosis behind them was wrong.** The denial came
   from the `PowerShell` tool, not Bash - this machine sets `CLAUDE_CODE_USE_POWERSHELL_TOOL=1`
   globally and the child inherits it - and `Bash(...)` rules never govern it. Corrected to carry
   both families; verified by driving `claude -p` with the committed array, zero denials.
3. **"Python's `int()` never overflows" is false above 4300 digits.** CPython 3.11+ caps
   `int(str)` and raises `ValueError`. Recorded as a deliberate divergence rather than ported: it
   is an interpreter setting with no .NET equivalent, not regex grammar.
4. **A prose claim contradicted by its own commit** - the skill said the driver deletes the work
   with `git reset --hard`, which the stash added in the same commit had already made false.

Findings 1 and 2 were code; 3 and 4 were corrections to claims this slice itself wrote. The
lesson worth carrying: a fix that changes one call site of a ported Python builtin should be
grepped for its siblings before it is called done.

**For the next slice.** The `needs:` placement rule from the first review is the general one for
the rest of Phase 2: port the whole function's control flow and throw only where upstream consults
a table or builds a node this slice does not have. Throwing at the top of a branch silently
over-skips.

**Process.** This slice took three attempts. The first two were unattended driver runs that both
reached a green ratchet and then died without committing, because the session ended its turn while
a review subagent was still running and `claude -p` ends the process when the turn ends; the
driver's rollback then deleted the work. `port-slice` and `tools/run-slices.ps1` were both fixed
in this commit - see DECISIONS.
