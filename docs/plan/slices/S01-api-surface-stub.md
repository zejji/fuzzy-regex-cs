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
