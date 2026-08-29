---
slice: S01
phase: 1
title: Public API surface stub
delivers: []
---

# S01 - Public API surface stub

## Why this exists

Phase 1 ports ~1,544 upstream assertions into TUnit tests. Those tests call the public API, so
the API types must exist before a single one of them can compile. This slice writes the surface
and nothing behind it: signatures, XML docs, and members that throw. No parsing, no matching, no
cleverness.

Phase 2 implements the parser and compiler behind exactly this surface. If Phase 2 finds a shape
here that is wrong, it changes it - a stub is not a commitment, it is a compilation target.

## Scope

In `src/FuzzyRegex/`, root namespace `FuzzyRegex`. Shapes mirror
`System.Text.RegularExpressions` so a .NET user needs no new vocabulary, plus the mrab-only
members (design spec section 4). Upstream reference: `upstream/regex/_main.py` for the API layer
and `upstream/regex/_regex_core.py` for the option flags.

- **`FuzzyRegex`** - the compiled pattern. Constructors taking a pattern and
  `FuzzyRegexOptions`; instance `IsMatch` / `Match` / `Matches` / `Replace` / `Split` /
  `Count`; the matching static conveniences; `MatchTimeout`; `Pattern`, `Options`,
  `GroupNames`, `GroupNumbers`. `ReadOnlySpan<char>` overloads where `Regex` has them.
- **`Match`** (derives from `Group`) - `Success`, `Index`, `Length`, `Value`, `ValueSpan`,
  `Groups`, `NextMatch()`, plus mrab-only `FuzzyCounts` and `PartialMatch`.
- **`Group`** (derives from `Capture`) - `Success`, `Name`, `Captures` as the **full** capture
  list, which is an mrab feature .NET's `Regex` does not have.
- **`Capture`** - `Index`, `Length`, `Value`, `ValueSpan`.
- **`GroupCollection`, `CaptureCollection`, `MatchCollection`** - indexed by number and by name.
- **`FuzzyCounts`** - a readonly record struct of substitutions, insertions, deletions.
- **`FuzzyRegexOptions`** - `[Flags]`. The `Regex` overlaps (`IgnoreCase`, `Multiline`,
  `Singleline`, `IgnorePatternWhitespace`, `RightToLeft`, `ExplicitCapture`) plus mrab's
  `BestMatch`, `EnhanceMatch`, `Posix`, `Reverse`, `FullCase`, `Version0`, `Version1`.
- **`FuzzyRegexParseException`** - what a bad pattern throws, carrying the pattern and the
  offset.

Every member: `throw new NotImplementedException()`. Every public member: XML docs, because
`GenerateDocumentationFile` is on for the shipping project and CS1591 is an error there.

## Out of scope

Anything that runs. If you find yourself writing a parser, stop - that is S06.

Do not design the Span-based API surface beyond mirroring what `Regex` already offers; the
Span contracts get their own gap tests in Phase 6 and their shapes can follow the engine.

## Done when

- [ ] `dotnet build` is clean: no warnings, analyzers satisfied, XML docs on every public member.
- [ ] A smoke test in `tests/FuzzyRegex.Tests/Gaps/Api/` asserts that constructing a
      `FuzzyRegex` throws `NotImplementedException` - so the stub is provably a stub, and the
      test flips to a real one in Phase 2.
- [ ] `docs/PORTMAP.md` maps each public type to its upstream counterpart in `_main.py`.
- [ ] `tools/check-ratchet.ps1` GREEN, then `-UpdateBaseline`.
- [ ] Slice file moved to `done/` with closing notes; `STATE.md` rewritten; committed.

## Notes for the next slice

Record in the closing notes any API shape you were unsure about, so S02-S05 know which
expressions in the ported tests may need revisiting when Phase 2 firms the surface up.

---

## Closing notes (2026-08-29)

### What landed

Six files under `src/FuzzyRegex/`: `FuzzyRegex.cs` (plus the `MatchEvaluator` delegate),
`Match.cs` (`Capture` -> `Group` -> `Match`), `MatchCollections.cs`, `FuzzyRegexOptions.cs`,
`FuzzyCounts.cs` (`FuzzyCounts` and `FuzzyChanges`) and `FuzzyRegexParseException.cs`. Signatures
and XML docs only; every member throws. Eleven gap tests in
`tests/FuzzyRegex.Tests/Gaps/Api/ApiSurfaceStubTests.cs`. Build clean, suite green, ratchet green.

### The namespace had to move - `Fuzzy.Text.RegularExpressions`

The biggest thing this slice changed, and it was not in the plan.

MA0049 fired on `FuzzyRegex.FuzzyRegex`. Rather than suppress it, the claim was measured with a
throwaway project referencing the built library. A consumer in an unrelated namespace who writes
`using FuzzyRegex;` gets:

```
error CS0234: The type or namespace name 'IsMatch' does not exist in the namespace 'FuzzyRegex'
error CS0118: 'FuzzyRegex' is a namespace but is used like a type
```

The entry type was unreachable. The owner chose to move the namespace rather than rename the
type, mirroring `System.Text.RegularExpressions`. Directories, project files, assembly names and
the NuGet id are all unchanged - only C# namespaces moved, and test/bench namespaces moved with
them because `FuzzyRegex.Tests.*` declared the colliding `FuzzyRegex` namespace itself.

Re-measured after the move: `using Fuzzy.Text.RegularExpressions;` alone gives unqualified access
to `FuzzyRegex`, `Match`, `Group` and `FuzzyCounts`. Importing `System.Text.RegularExpressions`
as well makes seven names ambiguous (CS0104), measured, not estimated: `Match`, `Group`,
`Capture`, `MatchCollection`, `GroupCollection`, `CaptureCollection` and `MatchEvaluator`. One
type alias does not clear that; use a namespace alias
(`using FR = Fuzzy.Text.RegularExpressions;`). Still an explicit, diagnosable clash rather than
the hard block it replaced.

Ported tests in `Fuzzy.Text.RegularExpressions.Tests.Ported.<Area>` need **no using directive at
all** to name `FuzzyRegex`, `Match` or `Group` - the enclosing namespace finds them. Worth
knowing for S02-S05.

### The surface is wider than this file specified, deliberately

This file listed a surface that could not express what the ported suite calls. Counted in
`upstream/regex/tests/test_regex.py`: `fullmatch` 71 uses, `partial=True` 91, `pos=`/`endpos=`
22/14, `overlapped=True` 15, `subn` 17, `expand` 8, `regex.escape` 24. The owner approved
covering them, since S01's stated purpose is to be a compilation target for S02-S05.

Added beyond the list: `FullMatch`/`IsFullMatch`, `MatchAtStart`/`IsMatchAtStart`, the `partial`
and `overlapped` parameters, `Replace(..., out int replacements)`, `Match.Result`,
`Match.FuzzyChanges`, `GroupNameFromNumber`/`GroupNumberFromName`, `Escape`/`Unescape`.

The blind review found four more the same counting exercise had missed, all added:
`Match.LastGroupNumber`/`LastGroupName` (see below), the `specialOnly` and `literalSpaces` flags
on `Escape` (all four combinations differ: `escape('foo!?')` is `foo!\?` but with
`special_only=False` it is `foo\!\?`), and `ReplaceFormat`/`Match.ResultFormat` for upstream's
`subf`/`subfn`/`expandf` - a `str.format`-style templating language (`{0}` is the whole match)
distinct from the `$1` templates `Replace` and `Result` take, used 15 times in the upstream
suite.

Things that translate onto the listed surface and were therefore **not** added: `groups()`,
`groupdict`, `capturesdict`, `spans()`, `starts()`/`ends()`, `regs` - all reachable through
`Groups` and `Captures`.

`lastgroup` and `lastindex` were on that list until the blind review disproved it. They are
**not** derivable: `regex.match('((a))', 'a').lastindex` is `1` although groups 1 and 2 both
succeed and both span `(0, 1)`, and `regex.match('(?P<a>a(b))', 'ab').lastgroup` is `'a'`
although the unnamed group 2 succeeded later. They are now `Match.LastGroupNumber` and
`Match.LastGroupName`. Upstream asserts them at `test_regex.py:760-765` and `1490,1493,1498`.

### Decisions S02-S05 must know

1. **`Match` keeps its .NET meaning** - it searches anywhere, i.e. upstream's `search`. Upstream's
   `match` (anchored at pos) is `MatchAtStart`. Do not translate `regex.match(...)` to
   `FuzzyRegex.Match(...)`; that is a silent semantic change.
2. **Static conveniences take `(input, pattern, options)`** - `Regex`'s argument order, not
   upstream's `(pattern, string)`. The example in the `port-tests` skill was written before this
   decision; it has not been changed, so read the signature, not the example.
3. **`pos`/`endpos` become `beginning`/`length`**, `Regex`-style. Translate `endpos` as
   `beginning + length`.
4. **`maxSplits`, not `count`, on `Split`** - upstream counts splits, `Regex.Split`'s `count`
   counts resulting pieces. Different meanings, so different names.
5. **`RightToLeft`, not `Reverse`.** This file listed both; they are the same flag, and a
   duplicate enum member trips CA1069.
6. **`FuzzyRegexOptions` values are upstream's `RegexFlag` bit values**
   (`_regex_core.py:73-90`), so the parser port can use them directly. `ExplicitCapture` has no
   upstream counterpart and takes `0x20000`, the first bit above upstream's range.

### Corner cut, with an expiry

`.editorconfig` gained a block suppressing MA0025, S2325 and IDE0060 for `src/FuzzyRegex/*.cs`
(root only). All three are true of a stub and false of the finished type. **Phase 2 must delete
that block**; it says so in the file.

### Not ported, recorded in PORTMAP.md

`concurrent=` (no GIL to release), `purge`/`cache_all`, `splititer`, `scanner`/`Scanner`,
`named_lists`, `detach_string`, `template`/`TEMPLATE`, and the `ASCII`/`LOCALE`/`UNICODE`/`WORD`/
`DEBUG` flags. Each has a reason in the table. `scanner` (11 uses) and `named_lists` (1) will need
a home when the slice that ports their tests arrives.

### Blind review

Six defects, all confirmed against upstream or by running the thing, all fixed:

1. **CI would have failed.** `tools/tests/fixtures/sample.trx` and the id literals in
   `tools/tests/PortTools.Tests.ps1` still carried `FuzzyRegex.Tests.*` after `PortTools.psm1`
   was re-rooted, so `Get-FeatureArea` returned `Unknown` for every fixture row and the ported
   table came out empty. `run-tool-tests.ps1` was 49 passed / 5 failed, exit 5. The rename sweep
   covered `.cs`, `.csproj`, `.ps1` and `.psm1` and missed the `.trx` fixture - a file type, not
   a directory, is what the grep missed.
2. A literal U+0008 backspace byte in `PORTMAP.md` where `\b` was meant. Cause found and worth
   knowing: **this shell's heredoc collapses `\\` to `\` even when the delimiter is quoted**, so
   `\\b` in the script became `\b` in the Python string and then a backspace in the file. Use
   `chr(92)` or the Write tool for anything containing backslashes.
3. `lastindex` and `lastgroup` were claimed derivable from `Groups`. They are not - see above.
4. `Escape` could not express upstream's `special_only` or `literal_spaces`.
5. The CS0104 clash was described as three types resolved by one alias. It is seven, and it needs
   a namespace alias.
6. `subf`/`subfn`/`expandf` had no member and no PORTMAP row.

Findings 3, 4 and 6 are all the same mistake: the usage count that justified widening the surface
was taken over the operations already suspected, not over the whole of `_main.py`. Counting is
only evidence if the thing counted is the whole population.

Each fix that could be pinned was pinned: two gap tests now assert the surface can express every
upstream operation the ported suite calls, and one was mutated to confirm it fails when a
signature is wrong.

### One more, found at commit time

`tools/tests/fixtures/sample.trx` was gitignored by a blanket `*.trx` rule and had
never been committed, so CI's Pester step has been running against a file that does not exist on
a fresh clone since Phase 0. The fixture is an input the tests read, not test output.
`.gitignore` now negates it. Fixing finding 1 was impossible without this, which is how it
surfaced: a fix you cannot commit is not a fix.

### Surprises

- `TimeSpan.FromMilliseconds(-1)` **is** `Timeout.InfiniteTimeSpan`, so `-1` is a valid match
  timeout meaning "no limit", not a negative one. A gap test pins this.
- The ratchet did its job on the rename: 21 baselined test ids vanished and it went RED until
  `-AcceptRemovals` was passed. Expect the same on any future namespace change.
- `Sort-Object Name` in `run-slices.ps1` is why the new `S01b` tooling slice runs before S02.

## Addendum: the delta review (2026-08-29, after the commit)

The new rule was applied retroactively to S01's own fix batch - the ~200 lines of public API the
first reviewer never saw. It found two more defects, both mine, both the same mistake:

1. `FuzzyRegex.Escape`'s XML doc said `Regex.Escape` is closest to `special_only=False`. Backwards.
   `Regex.Escape("foo!?")` is `foo!\?`, which is upstream's *default*; `special_only=False` gives
   `foo\!\?`. Over printable ASCII the default disagrees with `Regex.Escape` on 5 characters of 95,
   against 19 of 95 for `special_only=False`.
2. `PORTMAP.md` said `regex.Scanner` is "not in `__all__`'s public contract in the way the rest is".
   It is in `__all__`, sitting between `template` and the rest.

Both were claims about an external system that I reasoned my way to instead of running - the one
thing the house rules say never to do. The engine had no bug; the map of the engine did. Worth
noting because a documentation defect in a port survives every test you can write.

The fixes corrected text the reviewer had already read and added no new surface, so under the new
rule they needed no further pass. That is the rule working, not the rule being dodged.

---

**Superseded in part by S02 (2026-08-29).** The note above describes `Replace` and `Result` as
taking `$1` templates. They do not: S02 settled the replacement-template language as **upstream's**
(`\1`, `\g<name>`, `\n`, `\x41`, `\N{...}`, with `$` as ordinary text), because the two languages
disagree about `\` and cannot both be honoured. The contrast S01 was drawing still holds - upstream's
`expandf`/`subf` really are a separate `str.format`-style language from the one `expand`/`sub` take -
only the name of the second language was wrong. See `docs/plan/DECISIONS.md` and
`src/FuzzyRegex/Match.cs`. Left in place rather than rewritten: the slice records are history.
