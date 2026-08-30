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
| `src/_regex_unicode.c` | `src/FuzzyRegex/Unicode/` (transliterated by `tools/transliterate-unicode.py` in S09 - do not hand-edit) |
| `tools/build_regex_unicode.py` | not ported: upstream commits its output and we transliterate that (`docs/plan/2026-08-30-phase2-decisions.md`, decision A) |
| `regex/_main.py` | `src/FuzzyRegex/` (namespace `Fuzzy.Text.RegularExpressions`) |
| `regex/tests/test_regex.py` | `tests/FuzzyRegex.Tests/Ported/` |

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
| `regex.escape` | `_main.py:388` | `FuzzyRegex.Escape(input, specialOnly, literalSpaces)` - both upstream flags are carried; all four combinations differ | S01 |
| `pos` / `endpos` arguments | `_main.py:252` onwards | `beginning` / `length` (`endpos` = `beginning + length`) | S01 |
| `partial=True` argument | `_main.py:252` onwards | `partial` parameter | S01 |
| `overlapped=True` argument | `_main.py:341` onwards | `overlapped` parameter | S01 |
| `**kwargs` named lists (`regex.compile(p, name=[...])`) | `_main.py:460` (`_compile`) | `namedLists` parameter on the `FuzzyRegex` constructor and on the static `Match`, `MatchAtStart`, `FullMatch` and `Matches`, typed `IReadOnlyDictionary<string, IReadOnlyCollection<string>>` | S04 |
| `Pattern.named_lists` | `_main.py` | `FuzzyRegex.NamedLists`, typed `IReadOnlyDictionary<string, IReadOnlySet<string>>` because upstream returns each list as a `frozenset` | S04 |
| `_main._compile` (the tail: parse, optimise, compile) | `_main.py:460-686` | `Parsing.PatternCompiler.Compile` - **shape only in S06**, still throws; the pattern cache is not ported | S06 |
| The `_regex.compile(...)` argument tuple | `_main.py:660-663` | `Parsing.CompiledPattern` (record). `index_group` omitted: it is `GroupIndex` inverted | S06 |
| `_main._compile_replacement_helper` | `_main.py:687-741` | `Parsing.PatternCompiler.CompileReplacement` - **shape only in S06**. Takes the group count and group index rather than a pattern, because `compile_repl_group` (`_regex_core.py:1902-1918`) reads nothing else from it | S06 |
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
| `apply_constraint`, `parse_fuzzy` | `_regex_core.py:590-602`, `:655-677` | `Parsing.ParseFunctions.ParseFuzzy` - **shape only**, throws `needs:fuzzy-syntax` until S13 | S08 |

Signatures only in S01. Every member throws `NotImplementedException`; phase 2 puts a parser and
an engine behind them.

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
| `Match.pos`, `Match.endpos`, `Match.string`, `Match.re` | No counterpart on our `Match`, and none on `System.Text.RegularExpressions.Match` either - it keeps the subject in an internal field. Upstream's `pos`/`endpos` are already expressed as the `beginning`/`length` *arguments*; these are the read-back properties. Not added in S02 because a test-porting slice must not widen the public surface. **Phase 2 decides**, and the evidence points at "do not add": the whole upstream suite reads these properties four times, all four inside `test_getattr` (lines 473-477). The 22/14 figures in `DECISIONS.md` count the `pos`/`endpos` *arguments*, which are already ported as `beginning`/`length`, and do not apply here. | S02 |
| `Match.regs` | The tuple of every group's span. A Python-ism; `Groups[n].Index`/`.Length` already carries the same information. | S02 |
| `FuzzyRegexOptions.ExplicitCapture` (ours, not upstream's) | **Removed in S07.** No upstream counterpart, so the compile-parity corpus could not verify it and it would have been the only unverified logic in the parser. Owner decision C, `docs/plan/2026-08-30-phase2-decisions.md`. | S07 |
| `RegexBase.dump`, and the `DEBUG` flag's `parsed.dump()` call (`_main.py:594-595`) | Prints the parse tree to stdout. No bytecode effect, so the corpus cannot see it, and no ported test asserts on it (`test_hg_bugs` #106 only checks that compiling with `DEBUG` succeeds, and is already recorded as not ported). | S07 |
| `_shrink_cache`, `_cache`, `_named_args`, `_locale_sensitive`, `_replacement_cache` | Upstream's pattern and replacement caches, all module globals. Not referenced by any upstream test; a .NET caller keeps its own `FuzzyRegex` instance. | S07 |
| `_compile`'s `ignore_unused` argument (`_main.py:460`) | Only `subf`/`subfn` pass it true, and only for a pattern that has already compiled once. Our `Compile` always complains. | S07 |
| `Branch._merge_common_prefixes`, `Branch._is_simple_character`, `Branch._flush_char_prefix` (`_regex_core.py:2385-2415`, `:2445-2469`) | **Dead code upstream, and broken.** Nothing in the `regex` package calls `_merge_common_prefixes` (`Branch.optimise` calls `_flatten_branches`, `_split_common_*`, `_reduce_to_set` and `_add_precheck`, and nothing else), and it could not work if anything did: line 2409 calls the five-parameter `_flush_char_prefix` with four arguments, so any call raises `TypeError`. Its two helpers have no other caller. Porting it would add ~60 lines the compile-parity corpus cannot reach and the ratchet cannot protect. Re-check on the next upstream sync: if a release wires it up, port it then. | S08 |

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

## Where we diverge from upstream's structure

A faithful port keeps upstream's shape so diffs map across. Anywhere we have moved away from it -
an optimisation in phase 7, say - belongs here, with the reason, because it makes every future
sync of that area more expensive.

| Area | How it differs | Why | Slice |
|---|---|---|---|
| Set-member order in `_check_firstset` (`_regex_core.py:380-411`) and `Branch._flush_set_members` (`:2470-2483`) | Both turn a Python `set` of parse nodes into a list. **We sort at both points, and so does the recorder**, by the node's `_key` rendered with the class *name* in place of the class *object*, recursing into nested nodes and tuples (`_render_key` in `tools/record-compile-corpus.py`). | `RegexBase.__hash__` hashes `self._key`, whose first element is the class object, whose hash is its address - so upstream's bytecode for these two paths differs from one Python process to the next. Measured 2026-08-30 with the sort disabled: eight runs disagreed on 111 of 1547 compiles, and no two runs disagreed on the same rows (51 for one pair of runs, 82 for another), so any single pair undercounts. The seed is not what varies - a class object's hash is its address, so every fresh process is a fresh sample. Set-member order carries no matching semantics, so sorting is safe, and it is the only way the corpus can be a byte-exact oracle. Canonicalising in the comparator instead would need a bytecode decoder - Phase 3 work, and a second thing to get right. | S06 |
| Pattern offsets (`Source.pos`, `_regex_core.py:4120`) | Upstream counts codepoints; we count **UTF-16 code units**, so `FuzzyRegexParseException.Offset` differs from upstream's `error.pos` for a pattern containing a non-BMP character before the failure. `Source.Get` still returns whole codepoints, so the *bytecode* is identical. Verified against the local oracle 2026-08-30: `'\U0001F63A'` compiles to `[12, 1, 128570, 1]` (one `CHARACTER`, not two surrogates) and `'\U0001F63A('` fails with `pos=2` upstream where we report 3. Pinned by `Gaps/Parsing/SourceScannerTests.cs`. | AGENTS.md's UTF-16 rule: every public index in this port is a UTF-16 code unit, matching `System.Text.RegularExpressions`. No corpus row contains a non-BMP character, so this costs no parity. | S07 |
| `is_cased_i` (`_regex_core.py:362-364`) | **Temporary.** Answers only for codepoints below U+0080, where it is exactly "is this an ASCII letter"; anything above throws `needs:unicode-tables`, as does the `LOCALE` encoding. | Upstream asks `_regex.get_all_cases`, a four-level lookup into the generated Unicode tables (`_regex_unicode.c:30627`), which S09 transliterates. Measured against the built oracle 2026-08-30: over codepoints 0-127 both the `UNICODE` and `ASCII` encodings report exactly A-Z and a-z as cased, and they first disagree at U+00B5, so 0x80 is the exact point at which guessing would start. Without this, every `(?i)` pattern - including the five `test_getattr` assertions S07 turns on - would have had to wait for S09. **Delete this row when S09 lands.** | S07 |
| The "unused keyword argument" message (`_main.py:490`) | Upstream formats the name with `{!a}`, Python's `ascii()` repr: it escapes non-ASCII and switches to double quotes around an apostrophe. We interpolate the name as it is. Measured 2026-08-30: for a named list called `é` upstream says `unused keyword argument '\xe9'` and we say `unused keyword argument 'é'`; for `a'b` upstream says `unused keyword argument "a'b"` and we say `unused keyword argument 'a'b'`. ASCII names agree exactly. | `ascii()` is a Python terminal convention, not behaviour. No test pins this message - not upstream's suite, not ours, and no compile-parity row reaches it - so implementing Python's `repr` for one message would be unverified code, which is the same objection that removed `ExplicitCapture`. Raised by the S07 blind review and left deliberately. | S07 |
| `word[:1].isalpha()` in the `(*VERB)` branch (`_regex_core.py:918`) | Python's `isalpha` is Unicode-aware. We answer for ASCII and throw `needs:unicode-tables` above U+007F rather than guess, so `(*é)` - which upstream rejects with "unknown verb" - skips here. | Same reason and the same upgrade path as `is_cased_i` above: S09's tables. Measured 2026-08-30: `(*é)` and `(*Аb)` give "unknown verb" at position 2, while `(*😀)` falls through to "nothing to repeat" at 1, so the branch really does turn on letterhood. Raised by the S07 blind review. | S07 |
| Set-member order at `_check_firstset` | S06's PORTMAP rule says S07 and S08 must sort the first set's members. **Neither does**, because both throw `needs:character-classes` at the line that would build the `SetUnion` instead. S08 narrows that throw to first sets of *two or more* members: `SetUnion.optimise` hands a one-member set straight back (`_regex_core.py:3939-3943`), so a single-character first set needs no set node and no order. The sort lands with the set node in S10. | The sort only matters once the members are turned into an ordered list, which is the `SetUnion` construction itself - and a one-member list has only one order. | S07, narrowed S08 |
| `max_width` arithmetic (`_regex_core.py:3011-3015`, `:3017-3028`, `:3665-3669`) | Upstream's widths are unbounded Python `int`s. Ours are `long`, multiplied and added through `Parsing.Widths`, which **saturates at `long.MaxValue`** instead of overflowing. `(?:a{4294967294}){4294967294}` is a legal pattern whose product is 1.8e19 and does not fit. | Every consumer either compares the width against `UNLIMITED` (0xFFFFFFFF) or takes `min(w, UNLIMITED)`, and saturation can only happen above `long.MaxValue`, which is itself far above `UNLIMITED` - so a saturated value is always on the same side of that comparison as the true one. Making every width a `BigInteger` would cost far more. Measured against upstream on 2026-08-30: `(?:a{4294967294}){0,4294967294}a` gives `req_offset = -1` (the true product, 1.8e19, is above `UNLIMITED`, so `_get_required_string` replaces it) and the non-saturating `(?:a{65535}){0,65535}a` gives `req_offset = 4294836225`; our port agrees on both. Upstream's own `regex.compile` cannot reach either - the C compiler is O(repeat count) and dies with `MemoryError` - so the evidence comes from intercepting `regex._regex.compile` without calling through, and no corpus row can cover it. Pinned by `Gaps/Parsing/RepeatWidthOverflowTests.cs`. | S08 |
| `Branch.__hash__`, `GreedyRepeat.__hash__` | Both define `__eq__` without `__hash__`, so both are **unhashable** in Python. Ours implement `GetHashCode`: `Branch` returns a constant, `GreedyRepeat` hashes its type and counts but not its mutable `Subpattern`. | `RegexBase.GetHashCode` is abstract here precisely so no node inherits reference equality by accident, and nothing upstream ever puts a `Branch` or a repeat in a set - `get_firstset` returns only characters and set nodes. Throwing instead would be a trap for a later slice that adds a legitimate set. | S08 |
| Named-list member order (`StringSet.__init__`, `_regex_core.py:4088-4100`) | **Not a port divergence**: the port keeps whatever order its caller gave. The *recorder* sorts each named list before handing it to upstream, and the fixture stores it sorted, so the fixture's order is the order upstream compiled in. | `StringSet` sorts its branches by length only, a stable sort, so two equal-length members keep the caller's iteration order - and upstream's suite passes a `set` (`test_regex.py:2578-2579`). The leak is in the caller's container, not in upstream's logic, so it is fixed at the input rather than in the compiler. | S06 |
