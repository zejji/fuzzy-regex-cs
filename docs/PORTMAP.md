# Port map: upstream symbol to C# type

This is what makes an upstream sync mechanical instead of archaeological. When upstream changes a
function, this file says which of our files to open.

Appended to as each slice lands - never written ahead of the code. An entry here means the symbol
is ported; absence means it is not (yet).

## File-level map

| Upstream | Ours |
|---|---|
| `regex/_regex_core.py` | `src/FuzzyRegex/Parsing/` |
| `src/_regex.c` | `src/FuzzyRegex/Engine/` |
| `src/_regex_unicode.c` | `src/FuzzyRegex/Unicode/*.g.cs` (transliterated by `tools/transliterate-unicode.py` in S09 - do not hand-edit) |
| `src/_regex_unicode.h` | `src/FuzzyRegex/Unicode/UnicodeConstants.g.cs` |
| `src/_regex.c`, the Unicode half (`unicode_has_property`, the encoding tables, the five module functions) | `src/FuzzyRegex/Unicode/` hand-written files. Not `Engine/`: they are table-facing and the parser calls them long before a VM exists |
| `tools/build_regex_unicode.py` | not ported: upstream commits its output and we transliterate that (`docs/plan/2026-08-30-phase2-decisions.md`, decision A) |
| Python's `unicodedata.lookup` (for `\N{...}`) | `src/FuzzyRegex/Unicode/CharacterNames.cs` over `UnicodeCharacterNames.g.cs`, generated from the Unicode 17.0.0 UCD by `tools/build-character-names.py` (decision B) |
| Python's `str.lower` (for `Sequence._fix_full_casefold`) | `Unicode.PythonStr.Lower` over `UnicodeLowercase.g.cs`, generated from the Unicode 17.0.0 UCD by `tools/build-lowercase.py`. The second and last place the port needs a UCD file: upstream's own tables carry no lowercase mapping, and .NET's is a different Unicode version (67 codepoints disagree with CPython, measured 2026-08-30) |
| `regex/_main.py` | `src/FuzzyRegex/` (namespace `Fuzzy.Text.RegularExpressions`) |
| `regex/tests/test_regex.py` | `tests/FuzzyRegex.Tests/Ported/` |

## The differential oracle harness (S14)

Not a port of anything - it is how the port is checked. The compile-parity corpus is the oracle for
what our *compiler* emits; this is the oracle for what our *matcher* answers, and from Phase 3 on
no slice that touches the engine commits without a local run (VERIFICATION.md rule 7).

| Piece | Where | What it does |
|---|---|---|
| Recorder | `tools/record-oracle.py` | Generates rows from a seed (or reads explicit ones), runs each through upstream, writes JSONL: pattern, flags, named lists, subject, operation, and the outcome - no match, the match with every group's span and captures, or the exception. |
| Consumer | `tests/FuzzyRegex.OracleTests/` | Reads the wave, runs `FuzzyRegex`, reports per row `agree`, `diverge` or `unsupported`. Any `diverge` fails the run; `unsupported` (a `NotImplementedException` seam) is informational. Writes `TestResults/oracle/report.txt`, which is what oracle.yml uploads. |
| One command | `tools/run-oracle.ps1` | Record, consume, verdict. Nonzero on any divergence. |
| Schedule | `.github/workflows/oracle.yml` | Weekly and on demand, never a merge gate (design spec amendment 7). |

Three properties of it are load-bearing, and each is pinned by a test in `OracleWaveTests`:

- **The seam is `NotImplementedException`, and *which call* threw it decides the verdict.** A seam
  hit while compiling means the port knows nothing: `unsupported`. A seam hit after the pattern
  compiled means the port has already answered whether the input is acceptable, so on a row
  upstream *rejected* that is a `diverge` - which is how the harness catches the five patterns S13
  left compiling here and rejected by upstream's `re_compile`, with no matcher in existence.
- **Any other exception is an *answer*, and agrees only if it is a rejection rather than a crash.**
  Two things separate them, because the types overlap: the exception must be one of a short
  allow-list, and it must have come from the constructor rather than from the matching call. The
  message is compared only for `regex.error`, whose text ports verbatim; a Python `KeyError`'s does
  not (`regex.compile('a', V0|V1)` says `regex.V0|V1`, where .NET decorates its own message with
  the parameter name and value), so comparing it there would fail a correct port.
- **Index translation happens in the recorder, once.** Python reports codepoint `(start, end)`; the
  file holds UTF-16 `(Index, Length)`. This is one of exactly two places the span convention is
  enforced - the other is `Match`/`Group`'s accessors - so a slip at the accessor shows up as a
  divergence rather than as a silent agreement (DECISIONS 2026-08-31).
- **A wave is generated, not committed.** A divergence is minimised by hand and pinned as an
  ordinary test in `tests/FuzzyRegex.Tests/Gaps/`; the recorder's file header has the workflow.
  Named lists are sorted before either engine sees them, for the reason the last row of "Where we
  diverge" gives.

## Symbol map

| Upstream symbol | Upstream file:line | Ours | Slice |
|---|---|---|---|
| `Pattern` (the compiled pattern object) | `_main.py` (returned by `_compile`, 460) | `FuzzyRegex` | S01 |
| `RegexFlag` | `_regex_core.py:73-90` | `FuzzyRegexOptions` | S01 |
| `error` | `_regex_core.py` (`class error`) | `FuzzyRegexParseException` | S01 |
| `Match` | C type in `_regex.c`, surfaced by `_main.py` | `Match` : `Group` : `Capture` | S01 |
| `Match.fuzzy_counts` | `_regex.c` | `Match.FuzzyCounts` (`FuzzyCounts` record struct) | S01 |
| `Match.fuzzy_changes` | `_regex.c` | `Match.FuzzyChanges` (`FuzzyChanges` record struct) | S01 |
| `Match.partial` | `_regex.c` | `Match.PartialMatch` | S01 |
| `Match.captures(g)` | `_regex.c` | `Group.Captures` (`CaptureCollection`) | S01 |
| `Match.expand` | `_main.py:687` (`_compile_replacement_helper`) | `Match.Result(string)` - the template language is upstream's (`\1`, `\g<name>`, `\n`), not `Regex`'s `$1` | S01, language settled S02 |
| `Match.expandf` | `_main.py:687` | `Match.ResultFormat(string)` | S01 |
| `Match.lastindex` | `_regex.c` | `Match.LastGroupNumber` | S01 |
| `Match.lastgroup` | `_regex.c` | `Match.LastGroupName` | S01 |
| `Pattern.search` / `regex.search` | `_main.py:273` | `FuzzyRegex.Match` (.NET meaning of Match) | S01 |
| `Pattern.match` / `regex.match` | `_main.py:252` | `FuzzyRegex.MatchAtStart` | S01 |
| `Pattern.fullmatch` / `regex.fullmatch` | `_main.py:266` | `FuzzyRegex.FullMatch` | S01 |
| `Pattern.finditer` / `regex.finditer` | `_main.py:350` | `FuzzyRegex.Matches` (`MatchCollection`) | S01 |
| `Pattern.sub` / `regex.sub` | `_main.py:280` | `FuzzyRegex.Replace` | S01 |
| `Pattern.subn` / `regex.subn` | `_main.py:300` | `FuzzyRegex.Replace(..., out int)` | S01 |
| `Pattern.subf` / `regex.subf` | `_main.py:290` | `FuzzyRegex.ReplaceFormat` | S01 |
| `Pattern.subfn` / `regex.subfn` | `_main.py:312` | `FuzzyRegex.ReplaceFormat(..., out int)` | S01 |
| `Pattern.split` / `regex.split` | `_main.py:324` | `FuzzyRegex.Split`, returning `string?[]` - `null` where a group did not take part, as upstream puts `None` | S01, signature revised S02 |
| `Pattern.groupindex` | `_main.py` | `FuzzyRegex.GroupNumberFromName` / `GroupNames` | S01 |
| `regex.escape`, `_METACHARS` | `_main.py:388-423`, `:445` | `FuzzyRegex.Escape(input, specialOnly, literalSpaces)` and its `_metachars` constant - **implemented in S12**. Both upstream flags are carried; all four combinations differ. One loop over whole codepoints rather than upstream's two loops over a `str`, so an astral character takes one backslash and a lone surrogate survives | S01, S12 |
| `pos` / `endpos` arguments | `_main.py:252` onwards | `beginning` / `length` (`endpos` = `beginning + length`) | S01 |
| `partial=True` argument | `_main.py:252` onwards | `partial` parameter | S01 |
| `overlapped=True` argument | `_main.py:341` onwards | `overlapped` parameter | S01 |
| `**kwargs` named lists (`regex.compile(p, name=[...])`) | `_main.py:460` (`_compile`) | `namedLists` parameter on the `FuzzyRegex` constructor and on the static `Match`, `MatchAtStart`, `FullMatch` and `Matches`, typed `IReadOnlyDictionary<string, IReadOnlyCollection<string>>` | S04 |
| `Pattern.named_lists` | `_main.py` | `FuzzyRegex.NamedLists`, typed `IReadOnlyDictionary<string, IReadOnlySet<string>>` because upstream returns each list as a `frozenset` | S04 |
| `_main._compile` (the tail: parse, optimise, compile) | `_main.py:460-686` | `Parsing.PatternCompiler.Compile` - **shape only in S06**, still throws; the pattern cache is not ported | S06 |
| The `_regex.compile(...)` argument tuple | `_main.py:660-663` | `Parsing.CompiledPattern` (record). `index_group` omitted: it is `GroupIndex` inverted | S06 |
| `_main._compile_replacement_helper` | `_main.py:687-737` | `Parsing.PatternCompiler.CompileReplacement` - **implemented in S12**. Takes the group count and group index rather than a pattern, because `compile_repl_group` (`_regex_core.py:1902-1918`) reads nothing else from it. The replacement cache is not ported, for the same reason the pattern cache is not; `make_string` is a private helper of the same class | S06, S12 |
| `RegexFlag` bit values, `_ALL_VERSIONS`, `_ALL_ENCODINGS`, `DEFAULT_FLAGS`, `GLOBAL_FLAGS`, `SCOPED_FLAGS`, `ALPHA`, `DIGITS`, `ALNUM`, `OCT_DIGITS`, `HEX_DIGITS`, `SPECIAL_CHARS`, `UNLIMITED`, `REGEX_FLAGS`, `CASE_FLAGS`, `NOCASE`, `FULLIGNORECASE`, `FULL_CASE_FOLDING`, `CASE_FLAGS_COMBINATIONS`, `HEX_ESCAPES`, `CHARACTER_ESCAPES`, `ASCII_ENCODING`, `UNICODE_ENCODING` | `_regex_core.py:73-209`, `:4592-4603` | `Parsing.RegexFlags` | S07 |
| `OPCODES` / `OP` | `_regex_core.py:212-302` | `Parsing.Opcode` (all 81, numbered by table position) | S07 |
| `POSITIVE_OP`, `ZEROWIDTH_OP`, `FUZZY_OP`, `REVERSE_OP`, `REQUIRED_OP`, `ENCODING_OP_SHIFT` | `_regex_core.py:1924-1929` | `Parsing.NodeFlags` | S07 |
| `_UnscopedFlagSet`, `ParseError`, `_FirstSetError` | `_regex_core.py:59-70` | `Parsing.UnscopedFlagSetException`, `ParseErrorException`, `FirstSetErrorException` | S07 |
| `Source` | `_regex_core.py:4110-4354` | `Parsing.Source`. `match` is `MatchText` (`Match` is a public type in the parent namespace); `char_type` and the bytes branch are not ported | S07 |
| `Info` | `_regex_core.py:4356-4419` | `Parsing.Info`. `char_type` not ported; `DEFAULT_VERSION` is a constructor argument rather than a module global | S07 |
| `RegexBase` | `_regex_core.py:1941-2008` | `Parsing.RegexBase`. `_key` becomes `Equals`/`GetHashCode` per subclass; `positive`, `case_flags` and `zerowidth` become virtual properties | S07 |
| `Any`, `AnyAll`, `AnyU` | `_regex_core.py:2040-2072` | `Parsing.Any`, `AnyAll`, `AnyU` | S07 |
| `Character` | `_regex_core.py:2581-2653` | `Parsing.Character`. `folded` is an `int[]` of codepoints rather than a `str`, so `max_width` stays a codepoint count | S07 |
| `PrecompiledCode` | `_regex_core.py:3303-3308` | `Parsing.PrecompiledCode` | S07 |
| `String`, `Literal` | `_regex_core.py:4007-4067` | `Parsing.String`, `Parsing.Literal` | S07 |
| `Sequence` (with `pack_characters`, `_flush_characters`) | `_regex_core.py:3502-3713` | `Parsing.Sequence`. `_fix_full_casefold` and `_merge_chunks` are not ported: they need the Unicode folding tables (S09) | S07 |
| `Group` | `_regex_core.py:3060-3140` | `Parsing.Group` | S07 |
| `make_sequence` | `_regex_core.py:1935-1938` | `Parsing.Sequence.MakeSequence` | S07 |
| `is_cased_i`, `make_case_flags`, `make_character` | `_regex_core.py:362-435` | `Parsing.ParseFunctions.IsCasedI` (**ASCII only in S07**, see "Where we diverge"), `MakeCaseFlags`, `MakeCharacter` | S07 |
| `_parse_pattern`, `parse_sequence`, `parse_paren`, `parse_extension`, `parse_flag_set`, `parse_flags`, `parse_subpattern`, `parse_flags_subpattern`, `parse_positional_flags`, `parse_name`, `parse_escape`, `parse_numeric_escape`, `parse_octal_escape`, `parse_hex_escape` | `_regex_core.py:452-546`, `:850-976`, `:1133-1242`, `:1256-1414` | `Parsing.ParseFunctions`, same names. Every branch this slice does not deliver throws `NotImplementedException("needs:<tag> ...")` | S07 |
| `_compile_firstset`, `_check_firstset`, `_flatten_code`, `_check_group_features`, `_get_required_string` | `_regex_core.py:370-417`, `:4421-4479` | `Parsing.ParseFunctions.CompileFirstset`, `CheckFirstset`, `FlattenCode`, `CheckGroupFeatures`, `GetRequiredString` | S07 |
| `_main._compile` (the tail) | `_main.py:460-685` | `Parsing.PatternCompiler.Compile` - **implemented in S07**. The pattern cache, `_locale_sensitive`, the `bytes` branches, `ignore_unused` and the `DEBUG` dump are not ported | S07 |
| `Pattern.pattern`, `Pattern.flags`, `Pattern.groupindex`, `Pattern.groups`, `Pattern.named_lists` | `_main.py` | `FuzzyRegex.Pattern`, `.Options`, `.GroupNumberFromName` / `.GroupNames` / `.GroupNameFromNumber`, `.GroupNumbers`, `.NamedLists` - **implemented in S07** | S07 |
| `ZeroWidthBase` | `_regex_core.py:2011-2038` | `Parsing.ZeroWidthBase`. `_opcode` becomes the abstract `ZeroWidthOpcode` property | S08 |
| `Boundary`, `DefaultBoundary`, `DefaultEndOfWord`, `DefaultStartOfWord`, `EndOfLine`, `EndOfLineU`, `EndOfString`, `EndOfStringLine`, `EndOfStringLineU`, `EndOfWord`, `Keep`, `SearchAnchor`, `StartOfLine`, `StartOfLineU`, `StartOfString`, `StartOfWord` | `_regex_core.py:2130`, `:2744-2778`, `:3142`, `:3498`, `:3991-4005` | `Parsing.*`, same names. `Failure`, `Prune` and `Skip` are the `(*VERB)` nodes and wait for S11 | S08 |
| `GreedyRepeat`, `LazyRepeat`, `PossessiveRepeat` | `_regex_core.py:2938-3058`, `:3146-3148` | `Parsing.GreedyRepeat`, `LazyRepeat`, `PossessiveRepeat`. `type(self)(...)` in `optimise` becomes the virtual `Recreate` | S08 |
| `Branch` | `_regex_core.py:2134-2524` | `Parsing.Branch`. `_merge_common_prefixes`, `_is_simple_character` and `_flush_char_prefix` are **not ported** - see "Deliberately not ported". `_flush_set_members` throws `needs:character-classes` for more than one member, which is where `SetUnion` (S10) belongs | S08 |
| `apply_quantifier`, `_QUANTIFIERS`, `parse_quantifier`, `is_above_limit`, `parse_limited_quantifier`, `parse_count`, the `^`/`$` and `?*+{` branches of `parse_sequence`, the positional escapes in `parse_escape` | `_regex_core.py:558-588`, `:604-653`, `:846-848`, `:495-537`, `:1304-1315` | `Parsing.ParseFunctions.ApplyQuantifier`, `ParseQuantifier`, `IsAboveLimit`, `ParseLimitedQuantifier`, `ParseCount`, `ParseSequence`, `PositionEscape` | S08 |
| `POSITION_ESCAPES`, `ASCII_POSITION_ESCAPES`, `UNICODE_POSITION_ESCAPES`, `WORD_POSITION_ESCAPES` | `_regex_core.py:4635-4668` | `Parsing.ParseFunctions.PositionEscape`, one function rather than four tables: they differ only in the four word-related entries | S08 |
| `apply_constraint`, `parse_fuzzy` | `_regex_core.py:590-602`, `:655-677` | `Parsing.ParseFunctions.ParseFuzzy` - **shape only** in S08, throwing `needs:fuzzy-syntax`; implemented in S13, see the rows below | S08, S13 |
| All 253 tables and both typedef struct arrays of `_regex_unicode.c` | `_regex_unicode.c` (whole file) | `Unicode.UnicodeTables` (partial, across `UnicodeProperties.g.cs`, `UnicodeCasing.g.cs`, `UnicodeScriptExtensions.g.cs`, `UnicodePropertyNames.g.cs`). A struct array becomes one flat array per field - `AllCasesTable4Delta`, `AllCasesTable4Others`, `FullFoldingTable4Data`, `PropertiesName`/`Id`/`ValueSet` - so a field-order transposition cannot be written | S09 |
| All 104 `re_get_*` lookup functions | `_regex_unicode.c:3746-31481` | `Unicode.UnicodeTables.Get*`. 100 are transliterated; `re_get_all_cases`, `re_get_simple_case_folding`, `re_get_full_case_folding` and `re_get_script_extensions` are hand-ported in `Unicode/UnicodeCasing.cs` and their C bodies pinned by digest in the transliterator | S09 |
| `re_get_property[]` (the function-pointer table) | `_regex_unicode.c:31484` | `Unicode.UnicodeTables.GetProperty(prop, codepoint)`, a switch. Upstream's NULL at index 86 (`RE_PROP_SCX`) throws rather than dereferencing | S09 |
| Every `#define` of `_regex_unicode.h`, plus `RE_BLANK_MASK`, `RE_GRAPH_MASK`, `RE_WORD_MASK` | `_regex_unicode.h:1-207`, `_regex_unicode.c:3-5` | `Unicode.UnicodeTables` constants, `RE_` stripped and PascalCased (`RE_PROP_GC` is `PropGc`). All `int`: they all fit, and `int` keeps `1 << PropZl` legal | S09 |
| `unicode_has_property` | `_regex.c:1362` | `Unicode.Encodings.HasProperty` | S09 |
| `unicode_all_cases`, `unicode_simple_case_fold`, `unicode_full_case_fold`, `unicode_possible_turkic` and their `ascii_` twins; the `RE_EncodingTable` choice by flag | `_regex.c:941-1020`, `:1984-2062`, `:26176-26184` | `Unicode.Encodings` with a `CaseEncoding` enum. Upstream dispatches through a struct of function pointers; two encodings and four operations do not justify one here | S09 |
| `fold_case`, `get_expand_on_folding`, `has_property_value`, `get_all_cases`, `get_properties` (the module's exports) | `_regex.c:26133`, `:26285`, `:26327`, `:26347`, `:26126` | `Unicode.RegexModule`, same names. `FoldCase` takes and returns codepoints rather than a `string`, matching `Character.Folded` | S09 |
| `munge_name`, `init_property_dict` | `_regex.c:26418`, `:26433` | `Unicode.RegexModule.MungeName` and the lazy `GetProperties` | S09 |
| `str.isdigit`, `str.isidentifier`, `str.isalpha` (CPython's, which upstream calls) | `_regex_core.py:1229`, `:1236`, `:918` | `Unicode.PythonStr`. Measured equivalences: `isalpha` is `\p{gc=L}`, `isdigit` is `Numeric_Type` of `Decimal` or `Digit`, `isidentifier` is `XID_Start`-or-`_` then `XID_Continue` | S09 |
| `unicodedata.lookup` | `_regex_core.py:1444`, `:1893` | `Unicode.UnicodeCharacterNames.TryLookup`. `parse_named_char` itself is S10 | S09 |
| `Property` | `_regex_core.py:3310-3364` | `Parsing.Property`. `encoding` is deliberately outside equality, as upstream's `_key` leaves it out | S10 |
| `Range` | `_regex_core.py:3372-3447` | `Parsing.Range`, including the `optimise` that expands a range over the full-case-folding characters | S10 |
| `SetBase`, `SetDiff`, `SetInter`, `SetSymDiff`, `SetUnion` | `_regex_core.py:3715-3985` | `Parsing.SetBase` and the four subclasses, including `_handle_case_folding` and `max_width`. `char_width` and `__del__` are not ported: the first is engine-only, the second is Python's collector | S10 |
| `parse_set`, `parse_set_union`, `parse_set_symm_diff`, `parse_set_inter`, `parse_set_diff`, `parse_set_imp_union`, `parse_set_member`, `parse_set_item`, `parse_posix_class`, `SET_OPS` | `_regex_core.py:1511-1686`, `:183` | `Parsing.ParseFunctions.ParseSet` and friends, same names | S10 |
| `parse_property`, `parse_property_name`, `lookup_property`, `standardise_name`, `numeric_to_rational`, `float_to_rational`, `_POSIX_CLASSES`, `_BINARY_VALUES`, `make_property` | `_regex_core.py:1453-1509`, `:1688-1799`, `:445` | `Parsing.ParseFunctions`, same names. `numeric_to_rational` becomes `TryNumericToRational` and `float_to_rational` returns a nullable, because upstream signals "not a number" by raising `ValueError` from `float()` or `int()`; the counts are `BigInteger`, since Python's `int` has no width and `\p{1e300}` standardises to a 301-digit string | S10 |
| `CHARSET_ESCAPES`, `ASCII_CHARSET_ESCAPES`, `UNICODE_CHARSET_ESCAPES` | `_regex_core.py:4605-4633` | `Parsing.ParseFunctions.CharsetEscape`, one function rather than three tables: they differ only in the encoding tag on six of their seven entries, and `\h` keeps the base entry in all three | S10 |
| `parse_named_char` | `_regex_core.py:1437-1451` | `Parsing.ParseFunctions.ParseNamedChar` - **implemented in S10** over S09's name table | S10 |
| `parse_comment` | `_regex_core.py:978-993` | `Parsing.ParseFunctions.ParseComment` | S10 |
| `Sequence._fix_full_casefold`, `Sequence._merge_chunks`, and the `FULLIGNORECASE` arm of `_flush_characters` | `_regex_core.py:3608-3689` | `Parsing.Sequence.FixFullCasefold`, `MergeChunks`, `FlushCharacters` | S10 |
| `Branch._is_folded`, `Branch._flush_set_members` | `_regex_core.py:2471-2515` | `Parsing.Branch.IsFolded`, `FlushSetMembers` - **implemented in S10**; the set-member sort lands here | S10 |
| `Character.folded`, `Character._compile`'s expansion branch, `String.folded_characters` | `_regex_core.py:2596-2634`, `:4018-4026` | `Parsing.Character.Folded`, `String.FoldedCharacters` - **implemented in S10** over S09's `fold_case` | S10 |
| `str.lower` (CPython's, which `_fix_full_casefold` calls) | `_regex_core.py:3643` | `Unicode.PythonStr.Lower` over `Unicode.UnicodeLowercase`. One codepoint at a time; CPython's one context-sensitive rule, final sigma, is unreachable because nothing folds to U+03A3 | S10 |
| `Atomic` | `_regex_core.py:2074-2128` | `Parsing.Atomic` | S11 |
| `RefGroup`, `make_ref_group` | `_regex_core.py:3449-3496`, `:437-439` | `Parsing.RefGroup`, `ParseFunctions.MakeRefGroup`. Upstream's `self.group` starts as the text the pattern wrote and becomes an `int` in `fix_groups`; ours keeps the text and fills `GroupNumber` | S11 |
| `CallGroup`, `CallRef` | `_regex_core.py:2526-2579` | `Parsing.CallGroup`, `Parsing.CallRef`. `CallGroup.call_ref` is `CallRefIndex`; `__del__` is Python's collector | S11 |
| `Conditional`, `LookAroundConditional` | `_regex_core.py:2655-2742`, `:3218-3301` | `Parsing.Conditional`, `Parsing.LookAroundConditional` | S11 |
| `LookAround` | `_regex_core.py:3150-3216` | `Parsing.LookAround` | S11 |
| `Grapheme`, `GraphemeBoundary` | `_regex_core.py:2919-2936` | `Parsing.Grapheme`, `Parsing.GraphemeBoundary`. Upstream's `GraphemeBoundary` is not a `RegexBase` at all - see "Where we diverge" | S11 |
| `Failure`, `Prune`, `Skip`, `VERBS` | `_regex_core.py:2780-2784`, `:3366-3370`, `:3987-3989`, `:4671-4676` | `Parsing.Failure`, `Prune`, `Skip`; `ParseFunctions.Verbs`, factories rather than upstream's four shared singletons, as `PositionEscape` is | S11 |
| `parse_lookaround`, `parse_conditional`, `parse_lookaround_conditional`, `parse_atomic`, `parse_common`, `parse_call_group`, `parse_rel_call_group`, `parse_call_named_group` | `_regex_core.py:995-1131` | `Parsing.ParseFunctions`, same names | S11 |
| `parse_paren`'s `(?`, `(*` and `(?P` branches; `parse_group_ref`; the backreference arm of `parse_numeric_escape`; the `\R` and `\X` arms of `parse_escape` | `_regex_core.py:850-976`, `:1365-1370`, `:1416-1425`, `:1290-1301` | `Parsing.ParseFunctions.ParseParen`, `ParseExtension`, `ParseGroupRef`, `ParseNumericEscape`, `ParseEscape` - **completed in S11**; every branch but fuzzy constraints and `\L` now dispatches | S11 |
| `_check_group_features` | `_regex_core.py:4421-4458` | `Parsing.ParseFunctions.CheckGroupFeatures` - **implemented in S11**, with the `isinstance(parsed, Fuzzy)` test left `false` until S13 wired it | S11, S13 |
| `_compile_replacement`, `parse_repl_hex_escape`, `parse_repl_named_char`, `compile_repl_group` | `_regex_core.py:1801-1918` | `Parsing.ParseFunctions.CompileReplacement`, `ParseReplHexEscape`, `ParseReplNamedChar`, `CompileReplGroup`. `is_unicode` is always true and `source.sep` is always a `str` (no bytes templates), so the octal mask is always `0x1FF`. The `\N{...}` name alphabet here is `ALPHA \| {" "}`, narrower than the pattern side's `NAMED_CHAR_PART`, and a `\N` that finds no name is an error rather than the literal `N` - both measured, both pinned by `Gaps/Parsing/ReplacementTemplateTests.cs` | S12 |
| Python's `int(s)` as `parse_name`, `is_open_group` and the three group-resolving nodes call it | `_regex_core.py:1229-1236`, `:4409-4415`, `:2537`, `:2666`, `:3467` | `Unicode.PythonStr.TryParseInt` over `PythonStr.DecimalValue`, reached through `Parsing.ParseFunctions.TryParseGroupNumber` (saturating to `int`) and `ParsePythonInt` (throwing where upstream's `ValueError` escapes). `BigInteger`: Python's `int` has no width, and narrowing early would send `\g<99999999999999999999>` down the name-lookup path and change the error message. **Not** `BigInteger.Parse`: Python's `int()` takes any Unicode decimal digit, so `(?P=١)` is a reference to group 1 | S11 |

| `Fuzzy` | `_regex_core.py:2786-2917` | `Parsing.Fuzzy`, with the constraints dict as `Parsing.FuzzyConstraints` (its `Limits`, `Cost` and `Test` are upstream's `"d"`/`"i"`/`"s"`/`"e"`, `"cost"` and `"test"` keys). The constructor mutates the constraints it is handed, as `Fuzzy.__init__` does, which `apply_constraint` relies on | S13 |
| `is_actually_fuzzy`, `apply_constraint` | `_regex_core.py:548-556`, `:590-602` | `Parsing.FuzzyConstraints.IsActuallyFuzzy`, `ParseFunctions.ApplyConstraint`. `IsActuallyFuzzy` is a method on the constraints rather than a free function, because it only reads them | S13 |
| `parse_fuzzy`, `parse_fuzzy_item`, `parse_cost_constraint`, `parse_cost_limit`, `parse_constraint`, `parse_fuzzy_compare`, `parse_cost_equation`, `parse_cost_term`, `parse_fuzzy_test` | `_regex_core.py:655-844` | `Parsing.ParseFunctions`, same names. `parse_constraint` drops upstream's unused `source` argument. `parse_cost_limit` saturates at `UNLIMITED` where upstream's `int()` has no width - see "Where we diverge" | S13 |
| `StringSet`, `make_string_set`, `parse_string_set` | `_regex_core.py:4069-4108`, `:441-443`, `:1427-1435` | `Parsing.StringSet` (a `Branch` subclass, as upstream), `ParseFunctions.MakeStringSet`, `ParseFunctions.ParseStringSet`. Upstream's `index`, `encoding` and `fold_flags` locals are computed and never read, so they are not ported; `__del__` is Python's collector. `parse_string_set`'s `name is None` guard is dead - `parse_name` raises rather than returning `None` | S13 |
| `_fold_case` | `_regex_core.py:354-360` | `Parsing.PatternCompiler.FoldCase`, over whole codepoints rather than a `str`. Its only caller is the named-list build | S13 |
| The named-list build and `complain_unused_args` (`_main.py:604-619`, `:482-490`) | `_main.py:604-619` | `Parsing.PatternCompiler.Compile`'s named-list loop and `ComplainUnusedArgs` - **implemented in S13**. `args_needed` is not ported: it feeds only the pattern cache and the unused-argument check, and `ComplainUnusedArgs` reads `Info.NamedListsUsed` directly | S13 |
| `parse_sequence`'s fuzzy-constraint arm; the `\L` arm of `parse_escape` | `_regex_core.py:516-537`, `:1288-1290` | `Parsing.ParseFunctions.ParseSequence`, `ParseEscape` - **completed in S13**. Every branch of the parser now dispatches | S13 |
| `fuzzy = isinstance(parsed, _Fuzzy)`, **both** places upstream asks it | `_main.py:577`, `_regex_core.py:4436` | `Parsing.PatternCompiler.Compile` and `ParseFunctions.CheckGroupFeatures` - **wired in S13**. Both feed the group-0 call lookup, and wiring only the first appends a spurious `CALL_REF` copy of the whole pattern; pinned by `Gaps/Parsing/FuzzySectionTests.cs` | S13 |

Signatures only in S01. Every member throws `NotImplementedException`; phase 2 puts a parser and
an engine behind them.

### The node compiler (`src/_regex.c`), S15

Everything between receiving the code list and having a runnable node graph. All line references
are `upstream/src/_regex.c` unless the row says otherwise.

| Upstream symbol | Upstream line | Ours |
|---|---|---|
| `RE_OP_*` (all 98, including the seventeen the parser's table does not have) | `_regex.h:19-117` | `Parsing.Opcode`, extended past `FuzzyExt` to `TailStart`. One enum, because upstream keeps one numbering across both files |
| `RE_Node` | `:290-310` | `Engine.Node`. The `nonstring`/`string` union becomes separate fields: the search offsets are allocated lazily at match time, so the union reads as all-zero throughout compilation whichever arm the code picks |
| `RE_NextNode` | `:283-288` | `Engine.NextNode` |
| `RE_POSITIVE_OP` … `RE_REQUIRED_OP` | `:124-129` | `Engine.NodeFlags` |
| `RE_STATUS_*`, `max_status_2`/`_3`/`_4` | `:131-174`, `:746-760` | `Engine.NodeStatus`, `NodeStatus.Max2`/`Max3`/`Max4` |
| `RE_FUZZY_*`, `RE_FUZZY_VAL_*` | `:176-200` | `Engine.FuzzyValue` |
| `node_matches_one_character`, `locate_test_start` | `:3433`, `:3476` | `Engine.NodeQueries.MatchesOneCharacter`, `LocateTestStart`. Declared beside the matcher upstream; here beside the compiler, which needs them first |
| `possible_unfolded_length` | `:6908` | `Engine.NodeCompiler.PossibleUnfoldedLength` |
| `RE_GroupInfo`, `RE_CallRefInfo`, `RE_RepeatInfo` | `:352-369` | `Engine.GroupInfo`, `CallRefInfo`, `RepeatInfo` |
| `PatternObject` (the compiler's half) | `:539-592` | `Engine.PatternObject` |
| `RE_CompileArgs` | `:643-661` | `Engine.CompileArgs`, a mutable struct: upstream's `subargs = *args` fork is a C# struct assignment |
| `re_compile` | `:25863-26121` | `Engine.PatternObject.Compile`. The `PyArg_ParseTuple` half drops - the arguments arrive as a `Parsing.CompiledPattern` |
| `get_required_chars`, `make_STRING_node` | `:25756`, `:25802` | `PatternObject.GetRequiredChars`, `NodeCompiler.MakeStringNode` |
| `compile_to_nodes`, `build_sequence` | `:25701`, `:25490` | `NodeCompiler.CompileToNodes`, `BuildSequence` |
| `create_node`, `add_node`, `get_step` | `:23864`, `:23918`, `:24104` | `NodeCompiler.CreateNode`, `AddNode`, `GetStep` |
| `ensure_group`, `record_ref_group`, `record_group`, `record_group_end` | `:23926`-`:23990` | `NodeCompiler.EnsureGroup`, `RecordRefGroup`, `RecordGroup`, `RecordGroupEnd`, over `PatternObject.GroupInfoAt` |
| `ensure_call_ref`, `record_call_ref_defined`, `record_call_ref_used` | `:23996`-`:24045` | `PatternObject.CallRefInfoAt`, `NodeCompiler.RecordCallRefDefined`, `RecordCallRefUsed` |
| `sequence_matches_one`, `record_repeat` | `:24056`, `:24067` | `NodeCompiler.SequenceMatchesOne`, `RecordRepeat`, over `PatternObject.RepeatInfoAt` |
| `build_ANY`, `build_FUZZY`, `build_ATOMIC`, `build_BOUNDARY`, `build_BRANCH`, `build_CALL_REF`, `build_CHARACTER_or_PROPERTY`, `build_CONDITIONAL`, `build_GROUP`, `build_GROUP_CALL`, `build_GROUP_EXISTS`, `build_LOOKAROUND`, `build_RANGE`, `build_REF_GROUP`, `build_REPEAT`, `build_STRING`, `build_SET`, `build_SUCCESS`, `build_zerowidth`, `build_charset_equiv` | `:24157`-`:25487` | `NodeCompiler.BuildAny` … `BuildCharsetEquiv`, one method per builder, same order |
| `optimise_pattern` | `:23821` | `Engine.Optimiser.OptimisePattern` |
| `skip_one_way_branches` | `:23136` | `Optimiser.SkipOneWayBranches` |
| `add_repeat_guards` | `:23237` | `Optimiser.AddRepeatGuards` |
| `add_index`, `record_subpattern_repeats_and_fuzzy_sections` | `:23482`, `:23517` | `Optimiser.AddIndex`, `RecordSubpatternRepeatsAndFuzzySections`. **Vestigial upstream:** both call sites pass a null parent (`:23837`, `:23845`), so `add_index` never fires and the walk's only lasting effect is the `VISITED_REP` mark. Ported with the parameter intact so a release that restores the atomic and lookaround call sites still diffs onto this file |
| `use_nodes`, `discard_unused_nodes` | `:23617`, `:23641` | `Optimiser.UseNodes`, `DiscardUnusedNodes` |
| `mark_named_groups` | `:23672` | `Optimiser.MarkNamedGroups`. Upstream asks `PyDict_Contains(indexgroup, i + 1)`; `indexgroup` is `groupindex` inverted, so ours asks whether the number is a value of `GroupIndex` |
| `can_test_past`, `set_test_node`, `set_test_nodes` | `:23697`-`:23805` | `Optimiser.CanTestPast`, `SetTestNode`, `SetTestNodes`. The marking is done now; the match loop's fast path that consults it is Phase 7 (DECISIONS 2026-08-31) |
| `RE_CheckStack`, `RE_NodeStack` and their `_init`/`_fini`/`_push`/`_pop` | `:23188-23236`, `:23573-23616` | **Not ported as types.** Hand-rolled growable stacks of `PyMem_Realloc`'d arrays; `Stack<T>` is the same structure with the same push/pop order |
| `RE_ERROR_MEMORY` and every `if (!node) return RE_ERROR_MEMORY` | throughout the builders | **Not ported.** In .NET `new` throws, so there is nothing to guard |

### The matcher (`src/_regex.c`), S16

The engine spine: match state, the backtracking stacks, the dispatch and backtrack switches, and the
public entry points. Only this slice's opcodes are real; every other case throws a seam exception
naming its capability tag (`Engine.Seam`), so an unported construct is a skipped test or an
`unsupported` oracle row rather than a wrong answer. All line references are `upstream/src/_regex.c`.

| Upstream symbol | Upstream line | Ours |
|---|---|---|
| `RE_ERROR_*`, `bool_as_status` | `:103-122`, `:2225` | `Engine.MatchStatus`. Only the codes this port can produce; the rest describe CPython argument errors our own signatures make unrepresentable |
| `ByteStack`, `ByteStack_init`/`_fini`/`_reset`/`_push`/`_push_block`/`_pop`/`_pop_block`/`_drop`/`_drop_block`/`_top_block` | `:2285-2433` | `Engine.ByteStack`. Backed by an `ArrayPool<byte>` rental, which is where `PatternObject.stack_storage`'s per-pattern cache and its 64KB cap go (`state_fini`, `:18676-18693`) - the pool does the same job across all patterns and needs no lock |
| `push_uint8`, `pop_uint8`, `push_size`, `pop_size`, `push_bstack`, `push_sstack` | `:2440`-`:2601` | `ByteStack.PushUInt8`/`PopUInt8`/`PushSize`/`PopSize`, called directly at the two sites upstream wraps. `RE_MEMORY_LIMIT` is kept; it is the guard against an unbounded backtracking stack |
| `push_bool`, `pop_bool`, `push_ssize`, `pop_ssize`, `drop_ssize` | `:2446`, `:2617`, `:2451`, `:2621`, `:2785` | `ByteStack.PushBool`/`PopBool`/`DropSize`. `push_ssize` and `push_size` are one method here: `Py_ssize_t` and `size_t` are both 64-bit on the platforms this port targets and C# has one type for both |
| `push_pointer`, `pop_pointer` | `:2472`, `:2645` | `ByteStack.PushNode`/`PopNode`, which push an **index into `PatternObject.NodeList`** - upstream's own `node_list` - rather than a reference: a managed reference cannot go in a byte array, and the alternative of a second parallel stack of nodes would stop the byte layout matching upstream's. 8 bytes on the stack either way. `Node.Index` is assigned once at the end of `PatternObject.Compile`, after the optimiser has pruned the list. DECISIONS 2026-08-31 |
| `push_int8`, `push_code`, `push_int`, `push_groups`, `push_captures`, `push_guard_data`, `push_repeat_data`, `push_repeats` and their pops | `:2434-2815` | **Not ported yet - they arrive with their first caller, and none of them was S19's.** `push_groups`/`pop_groups` and `push_captures`/`pop_captures` have seventeen call sites, every one Phase 4 and split across both switches. Forward: `ATOMIC` (`:12040`), `CONDITIONAL` (`:12226`), `END_CONDITIONAL` (`:12433`), `END_LOOKAROUND` (`:12998`), `GROUP_CALL` (`:13405`), `GROUP_RETURN` (`:13500`, `:13516`), `LOOKAROUND` (`:13774`). Backtrack: `ATOMIC` (`:15279`), `CONDITIONAL` (`:15425`), `END_ATOMIC` (`:15457`), `END_CONDITIONAL` (`:15477`), `END_LOOKAROUND` (`:15669`), `GROUP_CALL` (`:16371`), `GROUP_RETURN` (`:16400` - a *push*, not a pop - and `:16418`), `LOOKAROUND` (`:17159`). The backtrack half is what an opcode-by-opcode port misses: it is where group state is put back when a call or a lookaround is unwound, so omitting it is wrong only on backtracking. The S19 slice file listed the guard and repeat pushes as its own; they are not - `grep -n 'push_repeats\|pop_repeats'` gives ten call sites (`:12228`, `:12431`, `:13407`, `:13502`, `:13514`, `:15423`, `:15475`, `:16369`, `:16400`, `:16416`) and every one is inside `CONDITIONAL`, `END_CONDITIONAL`, `GROUP_CALL` or `GROUP_RETURN`. The repeat opcodes push their own state through `RE_RepeatStateData` and friends instead. `push_code` is `PushSize` here, 8 bytes where upstream pushes 4 |
| `RE_GroupSpan`, `RE_GroupData` | `:277-280`, `:329-334` | `Engine.GroupSpan` (a readonly record struct) and `Engine.GroupData`. Upstream's `capacity` is the array's `Length` |
| `save_capture`, `unsave_capture` | `:9249`, `:9282` | `MatchState.SaveCapture`, `UnsaveCapture`. Upstream's asymmetry is kept verbatim: both index `state->groups` by the **public** group number while `START_GROUP`/`END_GROUP` read and write `group->current` by the **private** one. The two differ only for a branch-reset group, so tidying it would silently change behaviour Phase 4 has yet to test |
| `same_span`, `same_span_as_group` | `:11634`, `:11639` | `Matcher.SameSpan`, `SameSpanAsGroup`. This is where a group that matched empty - a real `(pos, pos)` span - is told apart from one that took no part in the match |
| `same_span_of_group` | `:11646` | **Not ported.** Its only callers are the branch-reset and `GROUP_EXISTS` paths, which are Phase 4's |
| `RE_GroupStateData` | `:416-422` | `Matcher.GroupStateData`, pushed and popped field by field (`PushGroupStateData`/`PopGroupStateData`) rather than as a struct block: the stack is internal and the only requirement on it is that the pop mirrors the push, so the difference is 8 bytes per entry |
| `RE_Position` | `:271-274` | `Engine.Position`, only ever `try_match`'s out-parameter |
| `save_captures`, `restore_groups`, `discard_groups` | `:17403`, `:17468`, `:17500` | **Not ported yet - Phase 5.** Their only callers are `do_best_fuzzy_match` (`:17760`, `:17789`) and `do_enhanced_fuzzy_match` (`:17957`, `:18000`, `:18008`). The S18 slice file listed them on the belief that a repeated capture group exercises them; it does not - a repeat uses `push_captures`/`pop_captures`, and those are Phase 4 callers |
| `RE_State` | `:463-529` | `Engine.MatchState`, a class with internal fields (see DECISIONS 2026-08-31). Dropped: the `view`/`charsize`/`is_unicode`/`should_release` buffer fields and the `char_at`/`set_char_at`/`point_to` pointers they select, because this port matches `string` only; `thread_state`, `lock` and `is_multithreaded`, because the state is per call and the pattern is immutable; `search_positions`, which only `search_start` reads |
| `state_init`, `state_init_2`, `state_fini` | `:18598`, `:18275`, `:18662` | `MatchState.Create` and `MatchState.Dispose`. `get_string` (`:18217`) and `check_compatible` (`:18573`) drop with the bytes support they exist for |
| `init_match` | `:3404` | `MatchState.InitMatch` |
| `clear_groups` | `:3369` | `MatchState.ClearGroups`, called from `InitMatch` and from the `FAILURE` backtrack case. The capture arrays are kept and only the counts go to zero, exactly as upstream does |
| `reset_guards`, `reset_guard_list` | `:3383`, `:3363` | `MatchState.ResetGuards` and `GuardList.Reset`. The fuzzy-section (`:3392`) and group-call (`:3398`) halves are still out: no fuzzy guards until Phase 5, no group-call guards until Phase 4, and a pattern needing either throws at its own opcode first |
| `check_timed_out`, `safe_check_cancel` | `:2253`, `:2266` | `MatchState.CheckTimedOut` and `Matcher.SafeCheckCancel`. `Stopwatch` ticks rather than `clock()` ticks; `PyErr_CheckSignals` has no counterpart in a library call. `RE_ERROR_TIMED_OUT` surfaces as `RegexMatchTimeoutException`, thrown by `FuzzyRegex.Run` because that is where the pattern text the exception carries lives |
| `decode_timeout` | `:21056` | The `_timeoutTicks` conversion in `FuzzyRegex`'s constructor. A negative value means "no timeout" upstream, which is exactly what `InfiniteMatchTimeout` is |
| `char_at` (the `bytesN_char_at` family) | `:763-800`, selected at `:18408` | `MatchState.CharAt`, which decodes a surrogate pair inline so a non-BMP character is one character to every opcode. `NextPos`, `PrevPos` and `CharBefore` are the stepping half; upstream needs none of them because a Python `str` is indexed by codepoint |
| `same_char`, `matches_ANY`, `matches_ANY_U`, `matches_CHARACTER` | `:2838`, `:2900`, `:2906`, `:2912` | `Matcher.SameChar`, `MatchesAny`, `MatchesAnyU`, `MatchesCharacter` |
| `ascii_is_line_sep`, `unicode_is_line_sep` | `:894`, `:1936` | `Unicode.Encodings.IsLineSep`, beside the rest of the encoding table's codepoint-only half |
| `ascii_at_line_start`/`_end`, `unicode_at_line_start`/`_end` | `:899`, `:919`, `:1942`, `:1963` | `Matcher.AtLineStart`, `AtLineEnd`. In `Engine/`, not `Unicode/`, because they take the match state |
| `in_range`, `matches_RANGE`, `matches_PROPERTY`, `matches_member`, `matches_SET` | `:2816`, `:3003`, `:2924`, `:3025`, `:3313` | `Matcher.InRange`, `MatchesRange`, `MatchesProperty`, `MatchesMember`, `MatchesSet` |
| `in_set_diff`, `in_set_inter`, `in_set_sym_diff`, `in_set_union` | `:3155`, `:3201`, `:3236`, `:3278` | `Matcher.InSetDiff`, `InSetInter`, `InSetSymDiff`, `InSetUnion`. Each walks the out-of-line member list `build_SET` hangs off `next_2` |
| `ENCODING_KIND`, `ASCII_ENCODING`, `UNICODE_ENCODING`, `RE_ENCODING_SHIFT` | `:164`-`:167` | `NodeStatus.EncodingKind` and the two constants beside it, read through `Matcher.NodeEncoding`. These are the same two status bits as `RE_STATUS_HAS_GROUPS`/`HAS_REPEATS`, which is safe because no node ever carries both meanings; the whole of the scoped `(?a:...)`/`(?u:...)` flag at match time is this lookup |
| `ascii_has_property`, `ascii_has_property_ign` | `:822`, `:832` | `Unicode.Encodings.HasProperty(CaseEncoding, …)`. The `_ign` form is the same function - "the property is case-insensitive" - so it is one method, and its `_IGN` opcodes are S22's anyway |
| `in_range_ign`, `matches_RANGE_IGN`, `matches_PROPERTY_IGN`, `matches_member_ign`, `matches_SET_IGN`, `in_set_*_ign` | `:2821`, `:3009`, `:2937`, `:3085`, `:3334`, `:3177`-`:3295` | **Not ported yet - S22.** Their opcodes throw `needs:ignore-case` |
| `match_many_ANY`, `match_many_ANY_U`, `match_many_CHARACTER`, `match_many_PROPERTY`, `match_many_RANGE`, `match_many_SET` | `:3537`, `:3647`, `:3803`, `:4045`, `:4485`, `:4737` | `Matcher.MatchesMany` **as the predicate only**; `Matcher.CountOne` is the loop. Each upstream function is the same loop written out for three character widths with a different predicate, and its two bounds - the caller's `limit` and `count_one`'s clamp to the slice - only ever stop the same walk, so one loop carrying both says the same thing in one pass. Upstream's ANY and ANY_U compare against the caller's `match` argument where the rest fold `node->match` in first (`:3809`); the distinction is kept even though `.` is never negated. The reverse and case-insensitive members are S23's and S22's |
| `try_match_ANY`, `_ANY_ALL`, `_ANY_U`, `_START_OF_LINE`, `_START_OF_LINE_U`, `_START_OF_STRING`, `_END_OF_LINE`, `_END_OF_LINE_U`, `_END_OF_STRING`, `_END_OF_STRING_LINE`, `_END_OF_STRING_LINE_U` | `:6919`-`:7371` | `Matcher.TryMatchAny` … `TryMatchStartOfString`, same names |
| `basic_match` | `:11714-17403` | `Matcher.BasicMatch`, with upstream's `goto` labels at upstream's places. Real cases: `SUCCESS`, `FAILURE`, `CHARACTER`, `STRING`, `ANY`/`ANY_ALL`/`ANY_U`, `START_OF_STRING`, `END_OF_STRING`, `START_OF_LINE`/`_U`, `END_OF_LINE`/`_U`, `END_OF_STRING_LINE`/`_U`, `SEARCH_ANCHOR`, `PROPERTY` (`:13804`), `RANGE` (`:13914`), `SET_DIFF`/`SET_INTER`/`SET_SYM_DIFF`/`SET_UNION` (`:14446`-`:14449`), `BRANCH` (`:12079`), `START_GROUP` (`:14569`), `END_GROUP` (`:12686`), and `FAILURE`, `BRANCH` (`:15354`), `START_GROUP` (`:17307`) and `END_GROUP` (`:15596`) in the backtrack switch. Upstream's nine separate zero-width cases become one case group with the predicate chosen by a switch, and so do its seven separate one-character cases and each of its two `START_GROUP`/`END_GROUP` pairs - one arm per upstream case in each. Upstream's shared one-character backtrack block (`:15210-15243`) is nothing but `retry_fuzzy_match_item`, so it is still the seam the `default` arm throws. `advance:` is back, as `BRANCH`'s backtrack case jumps to it |
| `locate_required_string`, `search_start`, the `string_search`/`fast_string_search` family | `:11082`, `:5231-6918` | **Deferred to Phase 7** (DECISIONS 2026-08-31). All are semantically transparent prefilters; without them the search tries the pattern at every position, which is slower and answers the same |
| `try_match` (the test-node fast path) | `:7671` | `Matcher.TryMatch`, **reduced to its default arm** (`:7843`): it reports success and leaves the position alone, so `BRANCH` and the repeat opcodes enter the alternative or the body and the dispatch loop tests its first node in the ordinary way. Semantically transparent - where upstream would refuse the branch, this port enters it and backtracks straight out again. The predicate dispatch is Phase 7's. **S19 ported the full version, measured it and reverted it**: it was written to fix an exponential blowup this turned out not to cause, and it fixed nothing measurable. DECISIONS 2026-08-31 |
| `dealloc_groups` | `:18261` | **Nothing to port.** It frees the `captures` arrays and the group block; both are ordinary managed arrays here |
| `do_exact_match`, `do_match_2`, `do_match` | `:18064`, `:18099`, `:18121` | `Matcher.DoExactMatch`, `DoMatch2`, `DoMatch`. The GIL release and re-acquire drop; `do_simple_fuzzy_match`, `do_enhanced_fuzzy_match` and `do_best_fuzzy_match` (`:18027`, `:17862`, `:17584`) are seams throwing their fuzzy tags |
| `pattern_search_or_match`, `pattern_match`, `pattern_fullmatch`, `pattern_search` | `:21522`-`:21646` | `FuzzyRegex.Run` and the six public entry points over it. The `PyArg_ParseTupleAndKeywords` half drops - our own overloads have parsed the arguments |
| `get_limits`, `limited_range` | `:21627`, `:18794` | `MatchState.ClampIndex`, one method: `state_init_2` (`:18376`) and `get_limits` spell the same clamping out twice |
| `pattern_new_match` | `:20738` | `FuzzyRegex.NewMatch`, including the rule that a reverse match reports its two ends the other way round (`:20795`). Upstream returns `None` for no match where this returns an unsuccessful `Match` whose groups are all absent, which is `Regex`'s shape and what S01 committed to. The `fuzzy_counts`/`fuzzy_changes` half and the `pos`/`endpos` fields are Phase 5's and unreported respectively |
| `copy_groups` | `:20621` | `GroupData.CopyGroups` and `GroupData.Copy`, less the single-block allocation arithmetic a garbage-collected heap does not need. Load-bearing, not cosmetic: the state's arrays are reused across start positions and across calls, so a `Match` sharing them would change under its owner |
| `match_get_group_by_index`, `match_get_start_by_index`, `match_get_end_by_index`, `match_get_span_by_index`, `match_get_spans_by_index`, `match_get_captures_by_index` | `:18847`-`:19180` | `Match.GroupAt`, which answers all six with one `Group`: `Success`, `Index`, `Length`, `Value` and `Captures`. Index 0 is special-cased to the match itself, as upstream special-cases it. An absent group reports `Success == false` and a `(0, 0)` span - the built-in `Regex`'s shape, and what the oracle recorder writes where Python's span is `(-1, -1)` |
| `match_lastindex`, `match_lastgroup` | `:20430`, `:20442` | `Match.LastGroupNumber` and `Match.LastGroupName`; the `do_match` loop that computes them (`:18186-18200`) is in `Matcher.DoMatch`. `lastindex` is the group that closed **last** (`group_info[g].end_index`), not the highest-numbered one, and only a named group is ever recorded as `lastgroup`, so upstream's `indexgroup` lookup is `FuzzyRegex.GroupNameFromNumber` and never falls back to the number |
| `state_get_group` | `:20818` | **Not ported.** It reads a group's text straight off a live `RE_State`, and its only callers are the substitution and split paths (S24, S25), which have a `Match` in hand here |
| `make_match_copy` | `:20669` | **Not ported.** It exists for `Match.__copy__`/`__deepcopy__` and for the detached-string case, neither of which this surface has |
| `check_posix_match`, `restore_best_match` | `:11602`, `:11565` | **Not ported.** Both branches are present in `basic_match` and throw `needs:posix-matching` |

### The repeat opcodes and the position guards (`src/_regex.c`), S19

Quantifiers: the `GREEDY_REPEAT`/`LAZY_REPEAT` pair, their `*_ONE` fast paths for a repeat of a
single character, the `BODY_*`/`MATCH_*`/`TAIL_START` backtrack markers that sequence body against
tail, and the guard lists that stop a repeat re-entering its own body at a position it has already
failed at. All line references are `upstream/src/_regex.c`.

| Upstream symbol | Upstream line | Ours |
|---|---|---|
| `RE_GuardSpan`, `RE_GuardList` | `:312-317`, `:319-326` | `Engine.GuardSpan` (a readonly record struct) and `Engine.GuardList`. Upstream's `capacity` is the array's `Length`. **`last_text_pos` and `last_low` are not ported**: `grep -n 'last_low\|last_text_pos' upstream/src/_regex.c` gives eight sites and every one assigns, none reads |
| `insert_guard_span`, `delete_guard_span`, `is_guarded`, `guard`, `guard_range` | `:9296`, `:9328`, `:9340`, `:9378`, `:9464` | `GuardList.InsertSpan`, `DeleteSpan`, `IsGuarded`, `Guard`, `GuardRange`. `safe_realloc`'s failure path has nothing to port: the list holds at most one span per position, so it is bounded by the subject rather than by an allocation limit |
| `guard_repeat`, `guard_repeat_range`, `is_repeat_guarded` | `:9446`, `:9534`, `:9559` | `MatchState.GuardRepeat`, `GuardRepeatRange`, `IsRepeatGuarded`, over a shared `ActiveGuardList` helper - upstream spells the same "is a guard active, and which list" three lines out in each of the three. `is_repeat_guarded` tests `guard_type == RE_STATUS_BODY` where the other two test `&`; every call site passes exactly one bit, so `&` is the one kept |
| `RE_RepeatData` | `:336-343` | `Engine.RepeatData`, a class for the same reason `GroupData` is one: `basic_match` reaches into it through `rp_data` and mutates it in place all over |
| `state->repeats` allocation | `:18493-18505` | `MatchState.Repeats`, allocated in `Create`. `repeats_storage`'s per-pattern cache has nothing to port on a garbage-collected heap |
| `dealloc_repeats` | `:18630` | **Nothing to port.** Three calls to `free` |
| `RE_BodyEndStateData`, `RE_RepeatStateData`, `RE_MatchBodyTailStateData`, `RE_RepeatOneStateData` | `:433-438`, `:440-446`, `:424-431`, `:448-453` | `Matcher.BodyEndStateData`, `RepeatStateData`, `MatchBodyTailStateData`, `RepeatOneStateData`, each pushed and popped field by field like `GroupStateData`. `RE_RepeatOneStateData.node` and `RE_MatchBodyTailStateData.position.node` go on the stack as `Node.Index`, through `ByteStack.PushNode` |
| `count_one` | `:4989` | `Matcher.CountOne`, with an extra `endPos` out-parameter. Upstream's callers do `text_pos += (Py_ssize_t)count * node->step`, which works because it indexes the subject by codepoint; ours are UTF-16 code unit indices, so the walk's end position is returned rather than recomputed by multiplication. Upstream's switch has no `default` at all - an opcode it does not list falls off the end with `count` uninitialised - so this throws a seam for the reverse (S23) and case-insensitive (S22) members instead |
| `text_pos + (Py_ssize_t)count * step`, `abs_ssize_t(pos - text_pos)`, `slice_end - text_pos` as a character count | `:13390`, `:15851`, `:16297`, `:16486`, `:17058` and the rest | `Matcher.StepBy` and `Matcher.CountBetween`. **Not upstream functions.** They exist only because upstream can multiply a character count by a step to get a subject offset and this port cannot: an astral character is one character and two code units |
| `match_one` | `:11373` | `Matcher.MatchOne`, forward and case-sensitive only. Upstream's `default` answers `FALSE` for an opcode it has no `try_match_*` for, which here would turn a construct a later slice delivers into a silent "no repeat"; the leaf throws instead (the S07 rule) |
| `try_match_CHARACTER`, `try_match_PROPERTY`, `try_match_RANGE`, `try_match_SET` | `:7026`, `:7154`, `:7220`, `:7292` | `Matcher.TryMatchOne`, one method: the four are the same six lines with a different `matches_*` predicate, which is the switch `Matcher.MatchesOne` already makes |
| `at_end` | `:11628` | `Matcher.AtEnd`. Its only reader is `END_GREEDY_REPEAT`'s partial-to-failure conversion (`:12575`), which is unreachable until Phase 7 restores `try_match`'s test-node arm |
| `GREEDY_REPEAT` (`:13176`), `LAZY_REPEAT` (`:13557`), `GREEDY_REPEAT_ONE` (`:13313`), `LAZY_REPEAT_ONE` (`:13693`), `END_GREEDY_REPEAT` (`:12525`), `END_LAZY_REPEAT` (`:12760`) | as listed | Six cases in `Matcher.BasicMatch`'s dispatch switch, written out separately as upstream writes them: the greedy and lazy members differ by which of body and tail takes precedence, which is a swap of two roles rather than a shared body with a flag. Two asymmetries of upstream's are kept and commented rather than tidied - `END_GREEDY_REPEAT` converts a `PARTIAL` body status to `FAILURE` at the minimum where `END_LAZY_REPEAT` does not (`:12575`), and `GREEDY_REPEAT` parks the repeat's own `start`/`capture_change` in a `MATCH_TAIL` record where `END_GREEDY_REPEAT` parks the state's (`:13265` against `:12635`) |
| `BODY_END` (`:15282`), `BODY_START` (`:15312`), `GREEDY_REPEAT`/`LAZY_REPEAT` (`:15778`), `GREEDY_REPEAT_ONE` (`:15815`), `LAZY_REPEAT_ONE` (`:16445`), `MATCH_BODY` (`:17177`), `MATCH_TAIL` (`:17223`), `TAIL_START` (`:17377`) | as listed | Eight cases in the backtrack switch. `GREEDY_REPEAT` and `LAZY_REPEAT` share one, as upstream does |
| The `GREEDY_REPEAT_ONE` retreat sub-switch (`:15907-16270`) and the `LAZY_REPEAT_ONE` advance sub-switch (`:16533-17023`) | as listed | **Only the `default` arms are ported** (`:16271`, `:17024`). Upstream's `CHARACTER*` and `STRING*` arms are optimisations of those - "a repeated single-character match is often followed by a literal, so checking specially for it can be a good optimisation when working with long strings" - and the string ones are built on `string_search_rev`, the Phase 7 deferral. The character ones read `test` themselves, which is the test-node fast path already deferred. The default arm retreats or advances one character and re-enters the dispatch loop, which tests the tail in the ordinary way. One thing the string arms do that is **not** an optimisation: they answer `PARTIAL` for a partial string match at the moved position, so a partial-matching test that reaches here is `needs:partial`, not this deferral's fault |
| The fuzzy retreat and advance loops (`:15881`, `:16500`) | as listed | Seams throwing `needs:fuzzy-matching`. `skip_pos` (`:16461`) drops with the string arms that are its only writers |
| `add_repeat_guards`'s `RE_STATUS_BODY`/`RE_STATUS_TAIL` output | `:23322-23326` | Read for the first time here, through `MatchState.ActiveGuardList`. S15 computed it |

## Deliberately not ported

Recorded so the omissions are visible and countable rather than silently missing.

| Upstream symbol or test | Why not | Decided |
|---|---|---|
| `concurrent=` argument | Releases the GIL. .NET has no GIL, so there is nothing to release. | S01 |
| `regex.purge`, `regex.cache_all` | Control upstream's pattern cache, a Python-module-global. Not referenced by any upstream test. | S01 |
| `Pattern.splititer` | Lazy `split`. `Split` returns the same pieces; a caller who wants laziness can stream the array. Revisit if a ported test needs the laziness itself. | S01 |
| `Pattern.scanner`, `regex.Scanner` | Public (both are in `__all__`), but a stateful lexer-style API with no `Regex` counterpart, used by 11 lines of the upstream suite. Deferred to the slice that ports `test_scanner`, which is where its shape can be chosen against real tests. | S01 |
| `Match.detach_string` | Drops the match's reference to the subject so Python can free it. .NET's GC needs no such hint. | S01 |
| `regex.template`, `TEMPLATE`/`T` flag | Present upstream only because Python's `re` has it; upstream does not implement behaviour for it. | S01 |
| `ASCII`, `LOCALE`, `UNICODE`, `WORD`, `DEBUG` flags | Not yet surfaced on `FuzzyRegexOptions`. `WORD` in particular changes `\b` semantics and needs the engine before it means anything. Add in the slice that needs them. | S01 |
| `Match.pos`, `Match.endpos`, `Match.string`, `Match.re` | No counterpart on our `Match`, and none on `System.Text.RegularExpressions.Match` either - it keeps the subject in an internal field. Upstream's `pos`/`endpos` are already expressed as the `beginning`/`length` *arguments*; these are the read-back properties. Not added in S02 because a test-porting slice must not widen the public surface. **Phase 2 decides**, and the evidence points at "do not add": the whole upstream suite reads these properties four times, all four inside `test_getattr` (lines 473-477). The 22/14 figures in `DECISIONS.md` count the `pos`/`endpos` *arguments*, which are already ported as `beginning`/`length`, and do not apply here. **Phase 2 decided: do not add** (owner decision D, `docs/plan/2026-08-30-phase2-decisions.md` section 6). Phase 3 is the first point at which a real `Match` exists to design against, and all four are additive. | S02, decided S12 |
| `Match.regs` | The tuple of every group's span. A Python-ism; `Groups[n].Index`/`.Length` already carries the same information. | S02 |
| `FuzzyRegex.Unescape` (ours, not upstream's) | **Removed in S12.** An S01 analogy with `Regex.Unescape`. Upstream has no such function, so it has no defined semantics against upstream's escape rules, no test uses it, and the compile-parity corpus cannot check it - which would have made it the only unverified logic on the public surface. Owner decision C, `docs/plan/2026-08-30-phase2-decisions.md`. | S12 |
| `ASCII` and `WORD` as public `FuzzyRegexOptions` members | **Phase 2 decided: do not add** (owner decision D). Every ported assertion that needs either uses the inline form (`(?a)`, `(?w)`), which the parser handles; the 28 `flags=regex.A`/`regex.U` sites were proven equivalent to a leading inline flag against the oracle (DECISIONS 2026-08-30). Adding a public member would widen the surface without enabling a single test. | S12 |
| `FuzzyRegexOptions.ExplicitCapture` (ours, not upstream's) | **Removed in S07.** No upstream counterpart, so the compile-parity corpus could not verify it and it would have been the only unverified logic in the parser. Owner decision C, `docs/plan/2026-08-30-phase2-decisions.md`. | S07 |
| `RegexBase.dump`, and the `DEBUG` flag's `parsed.dump()` call (`_main.py:594-595`) | Prints the parse tree to stdout. No bytecode effect, so the corpus cannot see it, and no ported test asserts on it (`test_hg_bugs` #106 only checks that compiling with `DEBUG` succeeds, and is already recorded as not ported). | S07 |
| `_shrink_cache`, `_cache`, `_named_args`, `_locale_sensitive`, `_replacement_cache` | Upstream's pattern and replacement caches, all module globals. Not referenced by any upstream test; a .NET caller keeps its own `FuzzyRegex` instance. | S07 |
| `_compile`'s `ignore_unused` argument (`_main.py:460`) | Only `subf`/`subfn` pass it true, and only for a pattern that has already compiled once. Our `Compile` always complains. | S07 |
| `get_code_size` (`_regex.c:26121`) | Returns `sizeof(RE_CODE)`, whose only use is `UNLIMITED = (1 << (BYTES_PER_CODE * 8)) - 1` (`_regex_core.py:186-190`). S07 already measured it as 4 and baked `0xFFFFFFFF` into `RegexFlags.Unlimited`, so porting the function would add a member with no caller. | S09 |
| `locale_all_cases`, `locale_simple_case_fold`, `locale_full_case_fold`, `scan_locale_chars`, `locale_encoding` (`_regex.c:1057-1360`) | The `LOCALE` encoding reads the process's C locale, which has no .NET equivalent and is not surfaced on `FuzzyRegexOptions`. `Unicode.Encodings.Select` throws `needs:locale-flag` for it. **Settled against the oracle in S10:** `(?L)` on a `str` pattern does nothing until casing is consulted - it resolves to `LOCALE|VERSION0` (upstream stops OR-ing `UNICODE` in once an encoding flag is set, `_main.py:570-574`), gives the property escapes encoding tag 0, and compiles identically to no flag at all otherwise. The only locale-sensitive call the parser makes is `is_cased_i`, reached only from `Sequence._flush_characters`, so a *run* of two or more case-insensitive characters hits the seam and a single character does not. `Character.folded` is not locale-sensitive: it folds with the constant `FULL_CASE_FOLDING`, whose `UNICODE` bit wins. Ten measured patterns are pinned by `Gaps/Parsing/LocaleFlagTests.cs`. | S09, settled S10 |
| `RE_ScriptExt` (`_regex_unicode.c:7-9`) | Declared and never used - `script_extensions_table_5` is a flat `RE_UINT8` run table, not an array of the struct. Transliterating a dead typedef would put a type nobody constructs in the assembly. | S09 |
| `Branch._merge_common_prefixes`, `Branch._is_simple_character`, `Branch._flush_char_prefix` (`_regex_core.py:2385-2415`, `:2445-2469`) | **Dead code upstream, and broken.** Nothing in the `regex` package calls `_merge_common_prefixes` (`Branch.optimise` calls `_flatten_branches`, `_split_common_*`, `_reduce_to_set` and `_add_precheck`, and nothing else), and it could not work if anything did: line 2409 calls the five-parameter `_flush_char_prefix` with four arguments, so any call raises `TypeError`. Its two helpers have no other caller. Porting it would add ~60 lines the compile-parity corpus cannot reach and the ratchet cannot protect. Re-check on the next upstream sync: if a release wires it up, port it then. | S08 |

### Every remaining `_regex_core.py` and `_main.py` symbol, accounted for (S13)

S13 closes the parser, so every `class` and `def` in both files was listed and checked against
this document. Seventy-two names had no entry of their own. Sixty-two of them are **methods of a
class that already has a row**, and a class row covers its members: `RegexBase`'s `with_flags`,
`is_atomic`, `can_be_affix`, `contains_group`, `has_simple_start`, `is_empty`, `__ne__` and every
node's override of them; `Branch._split_common_prefix`, `_split_common_suffix`, `_can_split`,
`_can_split_rev`, `_is_full_case`; `Source.peek`, `get_many`, `get_while`, `skip_while`,
`expect`, `at_end`; `Info.open_group`, `close_group`. `Scanner.scan` is covered by the
`regex.Scanner` row above. The other nine are these:

| Upstream symbol | Upstream file:line | Why not, or where | Decided |
|---|---|---|---|
| `Namespace` | `_regex_core.py:297-299` | An attribute bag whose only use is building `OP` out of `OPCODES`. `Parsing.Opcode` is a C# enum, so there is nothing to build. | S13 |
| `is_cased_f` | `_regex_core.py:366-368` | **Dead upstream.** Nothing in the package calls it; the only live caller of either function is `is_cased_i`, from `Sequence._flush_characters` and `_fix_full_casefold`. | S13 |
| `is_decimal`, `is_hexadecimal` | `_regex_core.py:1248-1254` | **Dead upstream.** Neither has a caller anywhere in the package. | S13 |
| `is_octal` | `_regex_core.py:1244-1246` | Ported, but inlined at both its call sites (`parse_numeric_escape:1354`, `_compile_replacement:1854`) rather than as a function: it is a one-line `all(ch in OCT_DIGITS ...)` over a string this port already has as a `char` list. | S13 |
| `Fuzzy._constraints_to_string` | `_regex_core.py:2886-2917` | Renders the constraints for `Fuzzy.dump`, which is not ported (see the `RegexBase.dump` row above). No bytecode effect and no other caller. | S13 |
| `RegexFlag.__repr__` | `_regex_core.py:93-98` | Python's display convention for a flag enum. C#'s `enum` has `ToString`. | S13 |
| `regex.prefixmatch` | `_main.py:259-264` | An exact alias of `regex.match`, added upstream so the name reads better; identical body. `FuzzyRegex.MatchAtStart` is that one method. A second name for it would be surface with no behaviour. | S13 |
| `_pickle` and the `copyreg` registration | `_main.py:756-759` | Registers `Pattern` with Python's pickle protocol via `pattern._pickled_data`. .NET serialisation is opt-in and unrelated; no ported test touches it. | S13 |

### Upstream test methods in lines 1-1007 not ported (S02)

63 methods sit in S02's range; 53 are ported and these 10 are not. The range is fully accounted
for, so a future reader can tell an omission from an oversight.

| Upstream test | Why not |
|---|---|
| `test_weakref` | Python object model: `weakref.proxy` over a compiled pattern. |
| `test_bug_1661` | Asserts that passing flags *alongside an already-compiled pattern* raises. Our static overloads take a `string` pattern, so the situation cannot arise. |
| `test_re_escape_byte` | `bytes` patterns; this port is `char`-based. |
| `test_bytes_str_mixing` | `bytes` patterns. |
| `test_bug_926075` | `bytes` versus `str` pattern identity. |
| `test_constants` | Asserts Python-level flag integers (`regex.I is regex.IGNORECASE`). |
| `test_scanner` | The `Scanner` API, deferred in S01. |
| `test_bug_764548` | A `str` subclass used as a pattern; Python object model. |
| `test_empty_array` | Python buffer protocol (`array.array` as a subject). |
| `test_ascii_and_unicode_flag` | Contrasts `ASCII` against `UNICODE`, neither surfaced on `FuzzyRegexOptions`. Its one flag-independent assertion - `\w` matches a non-ASCII letter - **is** ported, as `Ported/CharacterClasses/WordClassIsUnicodeByDefaultTests.cs`, because nothing else in the range pins it. |

### Assertions omitted from methods that are otherwise ported (S02)

| Where | Why not |
|---|---|
| `bytes` assertions in `test_basic_regex_sub` (#18), `test_sub_template_numeric_escape` (#17-18), `test_special_escapes`, `test_ignore_case`, `test_getattr` | `char`-based port. |
| `regex.splititer` assertions in `test_re_split` | `splititer` deferred in S01. |
| The `pat.scanner(...)` half of `test_bug_581080` | `Scanner` deferred in S01. |
| `Match.pos`, `endpos`, `string`, `re`, `regs` in `test_getattr` | See the row above; phase 2 decides. |
| `p.groupindex["n"] = 0` in `test_getattr` | Upstream asserts the mapping is a copy by mutating it. `GroupNames` is read-only, so the mutation does not compile. |
| The `regex.L` (LOCALE) row of the loop in `test_flags` | `LOCALE` not surfaced on `FuzzyRegexOptions`. |
| The `\L<options>` assertions in `test_case_folding` (#37-38) | Named lists deferred in S01. |

### Upstream test methods in lines 1008-1740 not ported (S03)

None. All 19 methods in the range - `test_properties`, `test_word_class`, `test_search_anchor`,
`test_search_reverse`, `test_atomic`, `test_possessive`, `test_zerowidth`,
`test_scoped_and_inline_flags`, `test_repeated_repeats`, `test_lookbehind`,
`test_unmatched_in_sub`, `test_bug_10328`, `test_overlapped`, `test_splititer`, `test_grapheme`,
`test_word_boundary`, `test_line_boundary`, `test_branch_reset`, `test_set` - are ported. The
assertions dropped from inside them are in the next table.

### Assertions omitted from methods that are otherwise ported (S03)

| Where | Why not |
|---|---|
| `test_properties` #1-3 and #5-16, and the 28 `(?L)`/`(?a)` rows of the table at lines 1146-1176 | `bytes` patterns; this port is `char`-based. 43 assertions in all, the largest single omission in the range. |
| `test_properties` #64-68, the `\X` block at lines 1112-1120 | Duplicated verbatim in `test_grapheme` (lines 1533-1542) and ported there. Confirmed identical apart from one blank line. |
| `test_search_reverse` #27, #29, #31, #33 | `endpos=-1` is Python's index-from-the-end convention. Our API takes a `length`, so `endpos=-1` and `endpos=3` are the same call on a four-character subject; the `endpos=3` form is ported. |
| `test_zerowidth` #2, #14, #15, #18, #19; `test_unmatched_in_sub` #2, #5, #8; `test_bug_10328` #2 | The pre-3.7 branch of a `sys.version_info` guard. Only the `>= 3.7` branch is a useful oracle. |
| `test_zerowidth` #13, #17, #21, #23; `test_splititer` #2 | `regex.splititer`, deferred in S01. `Split` returns the same pieces. |
| `test_lookbehind` #26; the `(?V0)([][-])` assertion in `test_set` (lines 1734-1735) | `repr(type(regex.compile(...)))`, a Python type-identity check. |

`word_set` at `test_properties` line 1125 is assigned and never read upstream, so there is nothing
to port from it.

### Upstream test methods in lines 1741-3083 not ported (S04)

| Upstream method | Why not |
|---|---|
| `test_copy` (2876-2918) | Python's copy protocol: `copy.copy`/`copy.deepcopy` on a pattern, a match and an iterator, plus `detach_string`. Patterns are immutable so upstream returns the same object; .NET has no equivalent protocol and no `detach_string` (see the row above). 15 assertions. |

Every other method in the range is ported: `test_various`, `test_replacement`,
`test_common_prefix`, `test_captures`, `test_guards`, `test_turkic`, `test_named_lists`,
`test_fuzzy`, `test_recursive`, `test_format`, `test_fullmatch`, `test_issue_18468` and
`test_partial`.

### Assertions omitted from methods that are otherwise ported (S04)

| Where | Why not |
|---|---|
| `test_named_lists` #4-6 | `bytes` patterns; the `str` forms are #1-3 and are ported. |
| `test_fuzzy` #57-62, #65 | `bytes` patterns. #57-62 repeat the `\L<words>{e<=1}` findall block of #51-56 and #65 repeats #63. |
| `test_recursive` #29 | Commented out in the upstream source itself (`#self.assertEqual(...)`, line 2869, with the note "The next regex should and does match. Perl 5.14 agrees."). Never executed upstream either. The index is still counted so the numbering stays aligned with the file. |
| `test_issue_18468` #2, #23 (the `StrSubclass` halves), #3-6, #12-16, #20-22, #28-32 | `str`/`bytes` subclasses, `bytearray` and `memoryview`. The method exists to check that `sub`, `split`, `findall` and `group` return plain `str`/`bytes` whatever subclass went in - a Python typing question with no C# equivalent, since `string` is sealed. Each distinct behaviour is ported once from its `str` form. |
| `test_issue_18468` #10-11 | The pre-3.7 branch of a `sys.version_info` guard; only the `>= 3.7` branch (#8-9) is a useful oracle. |

### Upstream test methods in lines 3084-4540 not ported (S05)

Six methods sit in S05's range. Five are ported - `test_hg_bugs`, `test_fuzzy_ext`,
`test_subscripted_captures`, `test_more_zerowidth` and `test_line_ending` - and one is not.

| Upstream method | Why not |
|---|---|
| `test_main` (4536-4537) | The `unittest.main(verbosity=2)` runner entry point, not a test. It is the 102nd method only because it sits at module level rather than in the class. |

With this slice every one of upstream's 102 test methods is accounted for: 91 ported, 11 recorded
here as not ported. Verified by listing the methods out of the Python source and matching them
against the `[Property("Upstream", ...)]` attributes actually present in `tests/`, not by assuming.

### Assertions omitted from methods that are otherwise ported (S05)

`test_hg_bugs` holds 498 assertions by the counting rule above. 475 are ported and these 23 are
not.

| Where | Why not |
|---|---|
| `test_hg_bugs` #68-70 | `bytes` patterns; the `str` forms are #65-67 and are ported. |
| `test_hg_bugs` #128-141 | `bytes` patterns - the "Posix in ASCII" half of Issue 23692. The "Posix in Unicode" half (#114-127) is ported and covers the same fourteen POSIX bracket classes. |
| `test_hg_bugs` #234-235 | `bytes` patterns (Hg issues 197 and 198), both `assertRaises` on a bad pattern. |
| `test_hg_bugs` #83, #86 | The pre-3.7 branch of a `sys.version_info` guard; only the `>= 3.7` branch (#82, #85) is a useful oracle. |
| `test_hg_bugs` #106 | Needs the `DEBUG` flag, which is not surfaced on `FuzzyRegexOptions`. The assertion only checks that compiling with `DEBUG` succeeds; upstream's own comment says it exists because Python 2 had no `ascii` builtin. |
| `test_hg_bugs` #227 | Hg issue 195: `pickle.dumps`/`pickle.loads` round-trip of a compiled pattern. Python's serialisation protocol, with no C# counterpart. |
| `test_line_ending` #2 | `bytes` pattern; the `str` form is #1 and is ported. |

`test_hg_bugs` #58 **is** ported, but not as the flag test it looks like. Upstream writes
`regex.sub(r"(\w+)", r"[\1]", subject, regex.WORD)`, and the fourth positional parameter of
`regex.sub` is `count`, not `flags` - so `regex.WORD` is used as a replacement count and the WORD
flag is never applied. Measured against the local oracle on 2026-08-30: the call with no fourth
argument, and the call with `flags=regex.WORD`, both give the identical result. It is ported as a
plain `Replace` with no options.

### Not ported: API upstream has and we do not (S05)

| Upstream symbol | Why not | Decided |
|---|---|---|
| `Match.allcaptures`, `Match.allspans` | Convenience tuples over every group's captures and spans. `Groups` and `Group.Captures` already carry the same information, and a test-porting slice must not widen the public surface. `test_hg_bugs` #435-436 (Git issue 474) is ported through `m.Groups.Select(g => g.Captures...)`, with a comment saying so. Revisit in phase 2 if the shape is wanted for its own sake. | S05 |
| `Match.groupdict`, `Match.capturesdict` | Python dictionaries keyed by group name. Expressed through `Groups["name"]` and `Groups["name"].Captures` at each of the nine `(?(DEFINE)...)` assertions that use them. | S05 |
| `Match.allcaptures`, `Match.allspans`, `Match.groupdict`, `Match.capturesdict` (the phase-2 review of the four rows above) | **Phase 2 decided: do not add** (owner decision D, `docs/plan/2026-08-30-phase2-decisions.md` section 6). The fifteen lines that use them are already ported through `Groups` and `Captures`, so unlike S04's named lists nothing is unwriteable without them. Phase 3 may add them for their own sake. | S12 |

## Where we diverge from upstream's structure

A faithful port keeps upstream's shape so diffs map across. Anywhere we have moved away from it -
an optimisation in phase 7, say - belongs here, with the reason, because it makes every future
sync of that area more expensive.

| Area | How it differs | Why | Slice |
|---|---|---|---|
| Set-member order in `_check_firstset` (`_regex_core.py:380-411`) and `Branch._flush_set_members` (`:2470-2483`) | Both turn a Python `set` of parse nodes into a list. **We sort at both points, and so does the recorder**, by the node's `_key` rendered with the class *name* in place of the class *object*, recursing into nested nodes and tuples (`_render_key` in `tools/record-compile-corpus.py`). | `RegexBase.__hash__` hashes `self._key`, whose first element is the class object, whose hash is its address - so upstream's bytecode for these two paths differs from one Python process to the next. Measured 2026-08-30 with the sort disabled: eight runs disagreed on 111 of 1547 compiles, and no two runs disagreed on the same rows (51 for one pair of runs, 82 for another), so any single pair undercounts. The seed is not what varies - a class object's hash is its address, so every fresh process is a fresh sample. Set-member order carries no matching semantics, so sorting is safe, and it is the only way the corpus can be a byte-exact oracle. Canonicalising in the comparator instead would need a bytecode decoder - Phase 3 work, and a second thing to get right. | S06 |
| Pattern offsets (`Source.pos`, `_regex_core.py:4120`) | Upstream counts codepoints; we count **UTF-16 code units**, so `FuzzyRegexParseException.Offset` differs from upstream's `error.pos` for a pattern containing a non-BMP character before the failure. `Source.Get` still returns whole codepoints, so the *bytecode* is identical. Verified against the local oracle 2026-08-30: `'\U0001F63A'` compiles to `[12, 1, 128570, 1]` (one `CHARACTER`, not two surrogates) and `'\U0001F63A('` fails with `pos=2` upstream where we report 3. Pinned by `Gaps/Parsing/SourceScannerTests.cs`. | AGENTS.md's UTF-16 rule: every public index in this port is a UTF-16 code unit, matching `System.Text.RegularExpressions`. No corpus row contains a non-BMP character, so this costs no parity. | S07 |
| The "unused keyword argument" message (`_main.py:490`) | Upstream formats the name with `{!a}`, Python's `ascii()` repr: it escapes non-ASCII and switches to double quotes around an apostrophe. We interpolate the name as it is. Measured 2026-08-30: for a named list called `é` upstream says `unused keyword argument '\xe9'` and we say `unused keyword argument 'é'`; for `a'b` upstream says `unused keyword argument "a'b"` and we say `unused keyword argument 'a'b'`. ASCII names agree exactly. | `ascii()` is a Python terminal convention, not behaviour. No test pins this message - not upstream's suite, not ours, and no compile-parity row reaches it - so implementing Python's `repr` for one message would be unverified code, which is the same objection that removed `ExplicitCapture`. Raised by the S07 blind review and left deliberately. | S07 |
| Set-member order at `_check_firstset` | S06's PORTMAP rule says S07 and S08 must sort the first set's members. **Neither does**, because both throw `needs:character-classes` at the line that would build the `SetUnion` instead. S08 narrows that throw to first sets of *two or more* members: `SetUnion.optimise` hands a one-member set straight back (`_regex_core.py:3939-3943`), so a single-character first set needs no set node and no order. **S10 lands the sort** at both points, as `RegexBase.RenderKey` - upstream's `_key` with the class *name* in place of the class *object* - ordered ordinally, which is what `_render_key` in `tools/record-compile-corpus.py` computes. Every rendered key is ASCII, so an ordinal sort is Python's `sorted`. | The sort only matters once the members are turned into an ordered list, which is the `SetUnion` construction itself - and a one-member list has only one order. | S07, narrowed S08, landed S10 |
| The three patterns on which **upstream itself** raises a Python internal error: `[^\s\S]` (`AttributeError: 'AnyAll' object has no attribute 'rebuild'`), `(?V1)[a--[\s\S]]` (`TypeError: RegexBase.optimise() got an unexpected keyword argument 'in_set'`) and an inline `(?V1)` under the `V0` flag (`KeyError: regex.V0|V1`) | All three are rejected here too, but with a .NET exception - `NotSupportedException` for the first two, `ArgumentOutOfRangeException` for the third - whose message names the same missing member rather than reproducing Python's text. | These are upstream bugs, not specified behaviour: its optimiser reduces a set to an `AnyAll` and then calls a method `AnyAll` does not have. There is no right answer to port, and reproducing a Python `AttributeError` message would be unverified code. What matters is that the pattern is **rejected rather than quietly compiled**, and it very nearly was not: an earlier draft of this slice gave every node an ignored `inSet` parameter and compiled `(?V1)[a--[\s\S]]`. Found by the S10 differential wave, pinned by `Gaps/Parsing/UpstreamInternalErrorTests.cs`. | S10 |
| The two replacement-template failures upstream does **not** raise `regex.error` for: `\g<name>` naming a group the pattern has not got (`IndexError("unknown group")`, `_regex_core.py:1918`) and an escape whose value is above U+10FFFF (`ValueError` from `chr()` inside `make_string`, `_main.py:704`) | The first becomes `ArgumentException("unknown group", "replacement")`, the second `NotSupportedException`. Both messages are ours; upstream's `IndexError` text is reproduced, its `ValueError` text is not. | An unknown group name in a *template* is a caller's argument being wrong, not pattern syntax, which is why upstream declines to use its own error type - and `System.Text.RegularExpressions` raises `ArgumentException` for exactly this mistake, so a .NET caller needs no new vocabulary. `NotSupportedException` follows the rule already set for the patterns in `UpstreamInternalErrorTests`: where upstream lets a Python internal error escape there is no specified behaviour to port, only the requirement that the input be rejected. The `paramName` names the public parameter (`FuzzyRegex.Replace`, `Match.Result`), not one of `CompileReplGroup`'s own, so CA2208, S3928 and MA0015 are disapplied at that one line with the reason in a comment. Both pinned by `Gaps/Parsing/ReplacementTemplateTests.cs`, and both agreed with upstream on all 4,230 rows of the S12 differential wave. | S12 |
| `GraphemeBoundary` (`_regex_core.py:2934-2936`) | Upstream's is a bare class with one `compile` method and no base; ours derives from `RegexBase` and throws `NotSupportedException` from `MaxWidth`, naming the attribute upstream lacks. | Duck typing: upstream drops it into a `Sequence` that only ever calls `compile` on its items. C# needs it in the hierarchy to sit in a `Sequence` at all. It is built and compiled inside `Grapheme._compile` and never optimised, packed or compared, so the throwing members are unreachable. | S11 |
| `CallRef.__eq__`/`__hash__`, `GraphemeBoundary.__eq__`/`__hash__` (`_regex_core.py:2572`, `:2934`) | Neither upstream class calls `RegexBase.__init__`, so neither has a `_key` and both raise `AttributeError` if compared or hashed. Ours use **reference equality** and the identity hash. | Throwing from `Equals` or `GetHashCode` is the defect S3877 exists to catch: it breaks any collection or debugger that touches the node, and buys nothing, because nothing compares either node. Reference equality is the honest .NET rendering of "this node has no identity beyond itself". `MaxWidth` still throws - that one *would* be a real bug if it were ever reached. | S11 |
| `Conditional.remove_captures`, `LookAroundConditional.remove_captures` (`_regex_core.py:2695-2697`, `:3248-3251`) | Upstream's bodies fall off the end and return `None`, which the caller would then store in place of the node. Ours perform the same mutations and return `this`. | An upstream bug, and unreachable in both: `remove_captures` is only called from `Scanner` (`_regex_core.py:4498`), which is not ported. Returning a null the type system forbids would need `RegexBase?` on every override for no caller's benefit. | S11 |
| A group name written in decimal digits Unicode 17.0 added (`Numeric_Type=Decimal` since 17.0, e.g. U+11DE0-U+11DE9 TOLONG SIKI) | Ours treats it as a group **number**; the local oracle treats it as neither a number nor an identifier. `'(x)\g<𑷡>'` degrades to literals upstream and compiles to `REF_GROUP 1` here; `'(x)(?P=𑷡)'` is "bad character in group name" upstream and compiles here. | The **same** deliberate choice as the `\N{...}` row below, and not new to S11: `PythonStr` rebuilds `str.isdigit` from upstream's tables, which are Unicode 17.0.0, while the host CPython's `unicodedata` is 16.0.0 and does not know the codepoint exists at all (measured 2026-08-31: `unicodedata.unidata_version` is 16.0.0 and `unicodedata.name('\U00011DE1')` raises). `PythonStrTests` already excludes those 4,803 codepoints for this reason. What S11 changed is only that the divergence is now *reachable*: before it, the port threw `FormatException` on any non-ASCII digit name, which was neither answer. Found by the second S11 review pass. | S11 |
| The decimal *value* of a digit (CPython's `Py_UNICODE_TODECIMAL`, which `int()` reads) | Derived by `Unicode.PythonStr.DecimalValue` from the codepoint's offset within its own maximal run of `Numeric_Type=Decimal` codepoints, modulo ten - not read from a table. | Upstream's tables carry the `Numeric_Type` property but no numeric value, and reading .NET's `CharUnicodeInfo` instead makes the port internally inconsistent: its table is an older Unicode version, so the ten codepoints above would be a digit to the `isdigit` gate and not a decimal digit to the conversion, and the port would **throw** where upstream compiles. The modulo is not cosmetic: runs abut, and U+1D7CE-U+1D7FF is one 50-long run of five aligned blocks. Verified against `CharUnicodeInfo` for every codepoint it knows, by `Gaps/Unicode/PythonStrTests`. | S11 |
| `CallGroup`, `RefGroup` and `Conditional`'s `__hash__` | Upstream hashes `_key`, which includes the resolved group number. Ours leave the group number out: `CallGroup` and `Conditional` hash the type alone, `RefGroup` the type and its case flags. | The group number is only known after `fix_groups`, so hashing it would let a node's hash change while it sat in a set. Equal nodes still hash equal, which is all a hash has to promise, and this is the rule `Branch` already follows (row above). The three analyzers that flag it - SS008, S2328 - are right about the hazard. | S11 |
| Python's `str.lower()` inside `Sequence._fix_full_casefold` | Per codepoint, from a table generated from the UCD, with no final-sigma rule. | CPython's `str.lower` is context-sensitive in exactly one place - a final U+03A3 lower-cases to U+03C2 - and this is only ever applied to text that has already been through `fold_case`. No codepoint folds to U+03A3, measured over all 1,114,112 codepoints on 2026-08-30 and pinned by `Gaps/Unicode/UnicodeLowercaseTests.cs`, so the rule cannot fire. Per-codepoint lowering was measured to agree with CPython's `str.lower` for every codepoint. | S10 |
| `max_width` arithmetic (`_regex_core.py:3011-3015`, `:3017-3028`, `:3665-3669`) | Upstream's widths are unbounded Python `int`s. Ours are `long`, multiplied and added through `Parsing.Widths`, which **saturates at `long.MaxValue`** instead of overflowing. `(?:a{4294967294}){4294967294}` is a legal pattern whose product is 1.8e19 and does not fit. | Every consumer either compares the width against `UNLIMITED` (0xFFFFFFFF) or takes `min(w, UNLIMITED)`, and saturation can only happen above `long.MaxValue`, which is itself far above `UNLIMITED` - so a saturated value is always on the same side of that comparison as the true one. Making every width a `BigInteger` would cost far more. Measured against upstream on 2026-08-30: `(?:a{4294967294}){0,4294967294}a` gives `req_offset = -1` (the true product, 1.8e19, is above `UNLIMITED`, so `_get_required_string` replaces it) and the non-saturating `(?:a{65535}){0,65535}a` gives `req_offset = 4294836225`; our port agrees on both. Upstream's own `regex.compile` cannot reach either - the C compiler is O(repeat count) and dies with `MemoryError` - so the evidence comes from intercepting `regex._regex.compile` without calling through, and no corpus row can cover it. Pinned by `Gaps/Parsing/RepeatWidthOverflowTests.cs`. | S08 |
| Fuzzy cost limits and coefficients above `UNLIMITED` (`_regex_core.py:750-760`, `:808-818`) | Upstream's `int(digits)` has no width and puts the value straight into the code list, so `(?:abc){i<=99999999999}` compiles to a code word of 99999999999. Ours are `uint` code words (S06), so `Fuzzy.CodeWord` **clamps each code word at `UNLIMITED`** - and clamps *only* there. Every parse-time value is a `BigInteger`, as unbounded as upstream's `int`. | A fuzzy cost is the one number upstream range-checks nowhere: `is_above_limit` guards repeat counts (`:646`) and nothing guards these, so any ceiling below the code word is observable as a pattern this port rejects and upstream compiles. Two drafts proved it: a ceiling at `UNLIMITED` made `{i<4294967296}` subtract from the ceiling and cap at 4294967294 and made `{4294967296<=i<=4294967295}` compile; a ceiling at `long.MaxValue` only moved the fault upwards, overflowing `min_cost += 1` to a negative minimum on `{9223372036854775807<i<=…}` and collapsing two distinct values on `{9223372036854775807<=i<9223372036854775808}` - all three accepted upstream, measured 2026-08-31. The remaining difference is not observable in matching - measured against `regex` 2026.7.19 on 2026-08-31 (`.scratch/s13_bigcost.py`), `{i<=99999999999}` and `{i<=4294967295}` both match `'axbxc'` with `fuzzy_counts=(0, 2, 0)`, and `{99999999999i<=1}` and `{4294967295i<=1}` both refuse it - and it could not be, because a code word is an `RE_CODE`, an `RE_UINT32` (`_regex.c:58`), so the value the C engine matches with can never exceed `UNLIMITED` whatever the Python list holds. Only the intermediate list the corpus records differs, and upstream's own suite writes no cost this large, so no corpus row covers it. Found by the S13 differential wave; both bad ceilings were found by S13's two blind review passes. Pinned by `Gaps/Parsing/FuzzyCostLimitOverflowTests.cs`, controls included. | S13 |
| `{e<=1:\X}`, `{e<=1:\b}`, `{e<=1:\A}`, `{e<=1:\Z}`, `{e<=1:\L<a>}` - a zero-width or multi-character node as a fuzzy test | Upstream's *parser* accepts all five and emits exactly the bytecode this port emits, word for word; its *C compiler* then answers `RuntimeError: invalid RE code`. **No longer a divergence: S15 rejects them too**, with `NotSupportedException("invalid RE code")` - the type `UpstreamInternalErrorTests` already sets for an upstream rejection that is not `regex.error` (DECISIONS 2026-08-31). | The rejection is the C engine's opcode validator, which is the `re_compile` port (`_regex.c:25863-26121`) and was therefore Phase 3's, not the parser's. Adding a validator to `PatternCompiler` would have been a second implementation of it in the wrong layer. Fourteen rows of the S13 wave; S15's own 1,684-row accept/reject wave agrees with upstream on all 198 rows it refuses this way. Pinned by `Gaps/Engine/NodeGraphTests.cs`. Contrast the `\1` row below, where the divergence is not deferrable because our `uint` code list cannot hold upstream's value at all. | S13, closed S15 |
| A cost equation with no digits after its comparator (`_regex_core.py:796`) | Upstream's `parse_cost_equation` calls a bare `int(parse_count(source))` rather than `parse_cost_limit`, so `a{1i<=}` escapes `regex.compile` as a plain `ValueError: invalid literal for int() with base 10: ''`. This port raises `FuzzyRegexParseException("bad fuzzy cost limit")` at the same offset. | Both reject the pattern, which is the requirement. Unlike the `UpstreamInternalErrorTests` cases, this is not upstream calling a method that does not exist - it is a malformed pattern upstream simply forgot to route through its own error function, one line before it does exactly that for the identical input in `parse_cost_constraint`. A caller catching parse errors should get one. Found by the S13 blind review; pinned by `Gaps/Parsing/FuzzySectionTests.cs` for four offsets. | S13 |
| A backreference inside a fuzzy test, `(a)(?:abc){e<=1:\1}` (`_regex_core.py:2822`) | `Fuzzy.fix_groups` descends into `subpattern` and not into `constraints["test"]`, so upstream's `RefGroup.group` stays the *string* `'1'` and `_regex.compile` answers `RuntimeError: invalid RE code`. This port cannot put a string in a `uint` code list, so `RefGroup._compile` throws `NotSupportedException` naming that error. | The rule already set by `Gaps/Parsing/UpstreamInternalErrorTests`: where upstream lets an internal error escape there is no specified behaviour to port, only the requirement that the input be **rejected rather than quietly compiled**. It very nearly was not - `RefGroup.GroupNumber` defaults to 0, a valid code word, so before the guard this compiled to a reference to group 0 and would have matched the whole subject once the engine landed. Found by the S13 blind review; pinned by `Gaps/Parsing/FuzzySectionTests.cs`, control included. | S13 |
| `Fuzzy.__hash__` (`_regex_core.py:2879-2881`) | `Fuzzy` defines `__eq__` without `__hash__`, so it is **unhashable** in Python; ours returns a constant, for the reason the `Branch` row below gives. | A `Fuzzy` cannot reach a set upstream either: it does not override `get_firstset`, so `RegexBase.get_firstset` raises `_FirstSetError` first. Everything `Fuzzy.Equals` compares by is filled in or replaced by the constructor - it adds the default limits and the default cost equation - so hashing any of it would let a hash change while the node sat in a set. | S13 |
| `Branch.__hash__`, `GreedyRepeat.__hash__` | Both define `__eq__` without `__hash__`, so both are **unhashable** in Python. Ours implement `GetHashCode`: `Branch` returns a constant, `GreedyRepeat` hashes its type and counts but not its mutable `Subpattern`. | `RegexBase.GetHashCode` is abstract here precisely so no node inherits reference equality by accident, and nothing upstream ever puts a `Branch` or a repeat in a set - `get_firstset` returns only characters and set nodes. Throwing instead would be a trap for a later slice that adds a legitimate set. | S08 |
| The four `re_get_*` functions that walk a struct table (`_regex_unicode.c:26045`, `:30627`, `:30948`, `:31452`) | Hand-written in `Unicode/UnicodeCasing.cs` instead of transliterated, and reading flat per-field arrays rather than a struct array. | Twenty single-use translation rules to auto-generate sixty lines of C# is a worse trade than sixty lines of reviewable C#, and a flat array named after its field cannot be transposed. The risk of drift is closed by pinning each function's normalised C body to a SHA-256 in `tools/transliterate-unicode.py`: an upstream edit to any of them fails the script rather than leaving the C# quietly stale. | S09 |
| `RE_EncodingTable` (`_regex.c:1003`, `:2046`) | Upstream is a struct of function pointers per encoding; ours is a `CaseEncoding` enum and a switch in `Unicode.Encodings`, carrying only the four casing operations and `has_property`. | The rest of the struct is engine machinery (character reading, position tests) that phase 3 needs and phase 2 cannot use. Building the whole table now would be seven unverified members; the enum is replaced or widened when the VM needs it. The `locale_` encoding is not ported at all: `LOCALE` is not surfaced, and `Encodings.Select` throws `needs:locale-flag`. | S09 |
| `\N{...}` named sequences (`unicodedata.lookup`) | `unicodedata.lookup("KEYCAP DIGIT ZERO")` returns three codepoints; our table holds single-codepoint names only, so the same input reports "undefined character name". | Upstream cannot use a named sequence either: `parse_named_char` calls `ord()` on the result (`_regex_core.py:1445`), which raises `TypeError` on a three-character string. So the only observable difference is which error the caller gets, and carrying ~1,000 more names to change the error text is not worth the size. Measured 2026-08-30. | S09 |
| Character names are Unicode **17.0.0** | Upstream resolves `\N{...}` through whatever `unicodedata` the host CPython ships - 16.0.0 on the machine this was built on. Ours is 17.0.0, the version `_regex_unicode.h` declares, so 4,803 codepoints resolve here that raise upstream. | Upstream's tables *are* 17.0.0; its reliance on the host's `unicodedata` is an accident of implementation, not intent, and a port that copied the accident would be pinned to whichever Python it happened to run under. Owner decision B, `docs/plan/2026-08-30-phase2-decisions.md`. Every 16.0.0 name is still a 17.0.0 name (Unicode's name stability policy), and `tools/build-character-names.py` proves it for all 148,853 of them on every run. | S09 |
| Named-list member order (`StringSet.__init__`, `_regex_core.py:4088-4100`) | **Not a port divergence**: the port keeps whatever order its caller gave. The *recorder* sorts each named list before handing it to upstream, and the fixture stores it sorted, so the fixture's order is the order upstream compiled in. | `StringSet` sorts its branches by length only, a stable sort, so two equal-length members keep the caller's iteration order - and upstream's suite passes a `set` (`test_regex.py:2578-2579`). The leak is in the caller's container, not in upstream's logic, so it is fixed at the input rather than in the compiler. | S06 |
