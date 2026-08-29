# FuzzyRegex

A .NET 10+ port of [mrab-regex](https://github.com/mrabarnett/mrab-regex) (the Python `regex`
module): a full-featured regex engine whose headline capability is **fuzzy (approximate) matching**
with per-error-type budgets - insertions, deletions and substitutions inside patterns, e.g.
`(?:foobar){e<=2}` - which no existing .NET library provides.

**Status: planning / early implementation.** Not yet usable. See
[`docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md`](docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md)
for the design and [`docs/plan/OPERATIONS.md`](docs/plan/OPERATIONS.md) for how the port is run.

## Licensing

Apache-2.0 (this port), derived from mrab-regex, which is `Apache-2.0 AND CNRI-Python`
(portions derived from CPython's `re` module). See LICENSE and NOTICE (added with the first code)
for full attribution. The upstream source is vendored read-only as the `upstream/` git submodule,
pinned to the exact commit this port tracks.
