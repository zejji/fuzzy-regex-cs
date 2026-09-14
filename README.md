# FuzzyRegex

A .NET 10+ port of [mrab-regex](https://github.com/mrabarnett/mrab-regex) (the Python `regex`
module): a full-featured regex engine whose headline capability is **fuzzy (approximate) matching**
with per-error-type budgets - insertions, deletions and substitutions inside patterns, e.g.
`(?:foobar){e<=2}` - which no existing .NET library provides.

**Status: planning / early implementation.** Not yet usable. See
[`docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md`](docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md)
for the design and [`docs/plan/OPERATIONS.md`](docs/plan/OPERATIONS.md) for how the port is run.

## One deliberate difference from mrab-regex's defaults

**Patterns compile as mrab-regex's version 1 by default, where mrab-regex itself defaults to
version 0.** Upstream's front end picks version 0 so that `regex` stays a drop-in replacement for
Python's `re`; this library has no `re` users to protect, and the two behaviours version 1 adds are
the reason to use it over `System.Text.RegularExpressions`:

- **nested sets and set operations** - `[[a-z]--[aeiou]]` is "a to z except the vowels", where
  `System.Text.RegularExpressions` offers subtraction alone (`[a-z-[aeiou]]`);
- **full Unicode case-folding** under `IgnoreCase`, so `ß` matches `SS` and `ﬁ` matches `fi`.
  `System.Text.RegularExpressions` folds simply and matches neither.

Pass `FuzzyRegexOptions.Version0`, or write `(?V0)` at the start of the pattern, to get the `re`
and `Regex` reading back. The only pattern that changes meaning is an unescaped `[` inside a set:
`[[]` is a set containing `[` under version 0 and an unterminated nested set under version 1, and
the parse error says so. Every other deliberate difference from upstream is listed in
[`docs/DIVERGENCES.md`](docs/DIVERGENCES.md).

## Licensing

Apache-2.0 (this port), derived from mrab-regex, which is `Apache-2.0 AND CNRI-Python`
(portions derived from CPython's `re` module). See LICENSE and NOTICE (added with the first code)
for full attribution. The upstream source is vendored read-only as the `upstream/` git submodule,
pinned to the exact commit this port tracks.
