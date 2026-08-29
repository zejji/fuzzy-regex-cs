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
| `src/_regex_unicode.c` | `src/FuzzyRegex/Unicode/` (generated - do not hand-edit) |
| `tools/build_regex_unicode.py` | `src/FuzzyRegex.UnicodeGenerator/` |
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
| `Pattern.named_lists` | One upstream test uses it. Deferred to the slice that ports named lists. | S01 |
| `Match.detach_string` | Drops the match's reference to the subject so Python can free it. .NET's GC needs no such hint. | S01 |
| `regex.template`, `TEMPLATE`/`T` flag | Present upstream only because Python's `re` has it; upstream does not implement behaviour for it. | S01 |
| `ASCII`, `LOCALE`, `UNICODE`, `WORD`, `DEBUG` flags | Not yet surfaced on `FuzzyRegexOptions`. `WORD` in particular changes `\b` semantics and needs the engine before it means anything. Add in the slice that needs them. | S01 |
| `Match.pos`, `Match.endpos`, `Match.string`, `Match.re` | No counterpart on our `Match`, and none on `System.Text.RegularExpressions.Match` either - it keeps the subject in an internal field. Upstream's `pos`/`endpos` are already expressed as the `beginning`/`length` *arguments*; these are the read-back properties. Not added in S02 because a test-porting slice must not widen the public surface. **Phase 2 decides**, and the evidence points at "do not add": the whole upstream suite reads these properties four times, all four inside `test_getattr` (lines 473-477). The 22/14 figures in `DECISIONS.md` count the `pos`/`endpos` *arguments*, which are already ported as `beginning`/`length`, and do not apply here. | S02 |
| `Match.regs` | The tuple of every group's span. A Python-ism; `Groups[n].Index`/`.Length` already carries the same information. | S02 |

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

## Where we diverge from upstream's structure

A faithful port keeps upstream's shape so diffs map across. Anywhere we have moved away from it -
an optimisation in phase 7, say - belongs here, with the reason, because it makes every future
sync of that area more expensive.

| Area | How it differs | Why | Slice |
|---|---|---|---|
| _(nothing yet)_ | | | |
