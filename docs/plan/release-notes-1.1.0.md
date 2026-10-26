<!--
  Draft of the GitHub release text for v1.1.0, for the owner's review. Sourced from the 1.1.0
  entry in CHANGELOG.md and the open rows of docs/KNOWN-DEFECTS.md on main 3c8d7986. The release
  workflow (.github/workflows/release.yml) publishes everything after the first line that is
  exactly `---` as the GitHub release text, so this comment stays out.
-->

---

# FuzzyRegex 1.1.0

FuzzyRegex is a .NET port of [mrab-regex](https://github.com/mrabarnett/mrab-regex), the Python
`regex` module, with fuzzy matching as its headline feature. This release moves to upstream
2026.9.29 and Unicode 18.0.0, makes matching faster across the board and fixes three partial
matching and allocation defects. The public API is unchanged from 1.0.0.

```console
dotnet add package FuzzyRegex --version 1.1.0
```

## What changed

- **Tracks mrab-regex 2026.9.29** (commit `0e8cb05`): the Unicode data is 18.0.0, up from
  17.0.0. New scripts (Jurchen, Seal), blocks, joining groups, case pairs and character names
  work in patterns, and so does U+1DF95, the first character outside the BMP whose case folding
  lands inside it (`(?i)ß` finds it). `\X` follows Unicode 18.0.0's simplified Indic conjunct
  rule, which upstream's `\X` does not yet.
- **Case-insensitive lists of literals are found two to three times faster**: `(?i)cat|dog`, and
  `\L<list>` with IgnoreCase, now skip ahead with a vectorised search, including lists with a `k`
  or an `s`, whose Kelvin sign and long s are handled near where they occur.
- **Matching is faster on most of the benchmark suite.** Splitting the engine's main matching
  method took 9-25% off most benchmarks when it was measured against the code before it, most on
  counting many matches (`Count` over a megabyte about 21% faster), repeats and short subjects.
  Each change in this release was timed against the code before it; 1.1.0 has not yet been
  timed against 1.0.0 as a whole.

## What is fixed

- **Partial matching with a capturing lookahead inside a repeat that a later conditional reads**
  is polynomial, not exponential up to the fork budget, and answers the exact partial where it
  used to answer a partial at the attempt's start that no text completes:
  `(?:(?=(?:a|(b))*y)a)+(?(1)c|d)` over 'a' * 10 + 'b' is a partial at (11, 11), not (0, 11).
- **A conditional in a repeat whose pattern ends in a backreference** no longer allocates per
  call: `(\w)(?:(?(?=\w)\w+|\s))*\1$` over 1,000 characters allocated about 105 MB a call, now
  24 bytes.
- **Partial matching with a negative lookaround whose fuzzy body can delete its way to the empty
  string** decides the lookaround at the end of the text, where the body matches whatever
  follows: `(x)(?!(?:x){d<=1})` over 'xx' is a partial at the edge, (2, 2), as `(x)(?!x?)` is,
  not (1, 2), which no text completes.

## Known limitations

The limitations listed for 1.0.0 still apply, except where fixed above. Open defects are in
[KNOWN-DEFECTS.md](https://github.com/zejji/fuzzy-regex-cs/blob/v1.1.0/docs/KNOWN-DEFECTS.md).
One of them can give a wrong answer outside partial matching:

- **D121: a search can miss the leftmost match** on a rare combination of a lazy repeat, a
  lookahead that captures, a conditional with a lookaround test and a backreference to that
  capture at the end. `[ab]*?(?=(?P<g>[abc]*?)(?:c|cc))(?(?!c)[ab]|c)*$(?=(?P=g))` over 'bcccba'
  reports the match starting at 1 where one starts at 0. Not yet fixed.

## Reporting a bug

Open an issue at <https://github.com/zejji/fuzzy-regex-cs/issues> with the pattern, the subject,
the flags and the method you called (with `partial: true` or not), what you got and what you
expected. If you know what Python's `regex` answers for the same call, include it.
