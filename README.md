# FuzzyRegex

A .NET 10+ port of [mrab-regex](https://github.com/mrabarnett/mrab-regex) (the Python `regex`
module): a full-featured regex engine whose headline capability is **fuzzy (approximate) matching**
with per-error-type budgets - insertions, deletions and substitutions inside patterns, e.g.
`(?:foobar){e<=2}` - which no existing .NET library provides. The public namespace is
`Fuzzy.Text.RegularExpressions`, and the main type is `FuzzyRegex`, shaped after
`System.Text.RegularExpressions.Regex`.

**Status: pre-1.0.** Feature-complete against the pinned upstream release - the whole ported
upstream test suite passes (`docs/STATUS.md`) - and the public API is frozen. What is left before
1.0 - performance work, packaging, mutation-test coverage, the rest of this documentation and a
browser demo - is tracked in [`docs/plan/ROADMAP.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/plan/ROADMAP.md).

**Try it in your browser: [the FuzzyRegex demo](https://zejji.github.io/fuzzy-regex-cs/).** This is
v1 of the page: pattern, flags and subject, the matches highlighted, every group and capture in a
table, and eight worked examples. The engine runs in a Web Worker in your own browser and nothing is
sent anywhere. Its source, and how to run it locally, is in
[`demo/`](https://github.com/zejji/fuzzy-regex-cs/blob/main/demo/README.md).

See [`docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md)
for the design and [`docs/plan/OPERATIONS.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/plan/OPERATIONS.md) for how the port is run.

## Install

```console
dotnet add package FuzzyRegex
```

## Quick start

Every example below compiles against the public API in
[`src/FuzzyRegex/PublicAPI.Unshipped.txt`](https://github.com/zejji/fuzzy-regex-cs/blob/main/src/FuzzyRegex/PublicAPI.Unshipped.txt). Add
`using Fuzzy.Text.RegularExpressions;` before running any of them.

### Exact match with named groups

`FuzzyRegex.Match` is the static convenience for a search, and `Match.Groups` is a dictionary
as well as a list: it is keyed by every group's name, or by its number as text where it has
none, in ascending group number.

```csharp
using Fuzzy.Text.RegularExpressions;

Match match = FuzzyRegex.Match("2026-09-16", @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})");

foreach (string key in match.Groups.Keys)
{
    Console.WriteLine($"{key}: {match.Groups[key].Value}");
}
// 0: 2026-09-16
// year: 2026
// month: 09
// day: 16
```

### Fuzzy match with an error budget

`(?:pattern){e<=2}` allows up to two errors - substitutions, insertions or deletions - inside
the group. `Match.FuzzyCounts` reports how many of each kind the match actually used, and is
`(0, 0, 0)` for an exact match.

```csharp
using Fuzzy.Text.RegularExpressions;

var regex = new FuzzyRegex(@"(?:foo){e<=2}");
Match match = regex.MatchAtStart("fou");

(int substitutions, int insertions, int deletions) = match.FuzzyCounts;
Console.WriteLine(match.Value);
Console.WriteLine($"{substitutions} substitution(s), {insertions} insertion(s), {deletions} deletion(s)");
// fou
// 1 substitution(s), 0 insertion(s), 0 deletion(s)
```

### Enumerating matches with a timeout

`EnumerateMatches` finds matches lazily, one at a time, so an early exit does not pay for the
whole subject. Its `timeout` bounds each step rather than the whole walk; see the divergence "A per-call
`timeout` on every input-dependent method" in [`docs/DIVERGENCES.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/DIVERGENCES.md).

```csharp
using Fuzzy.Text.RegularExpressions;

var regex = new FuzzyRegex(@"\w+");

foreach (Match match in regex.EnumerateMatches("one two three", timeout: TimeSpan.FromSeconds(1)))
{
    Console.WriteLine(match.Value);
}
// one
// two
// three
```

## Coming from Python `regex`

| Python `regex` | FuzzyRegex |
|---|---|
| `regex.search(pattern, subject)` | `FuzzyRegex.Match(subject, pattern)` |
| `regex.match(pattern, subject)` | `FuzzyRegex.MatchAtStart(subject, pattern)` |
| `regex.fullmatch(pattern, subject)` | `FuzzyRegex.FullMatch(subject, pattern)` |
| `regex.findall(pattern, subject)` | No direct equivalent. Read `Match.Groups[1].Value` over `FuzzyRegex.Matches(subject, pattern)`, or over `EnumerateMatches` for a lazy projection. |
| `regex.sub(pattern, replacement, subject)` | `FuzzyRegex.Replace(subject, pattern, replacement)`, with the replacement template in upstream's syntax (`\1`, not `$1`) |

## Coming from `System.Text.RegularExpressions`

- **`Match` searches the whole subject.** Upstream's anchored `match` is `MatchAtStart`, and
  `fullmatch` is `FullMatch`. See "`Match` means upstream's `search`; upstream's anchored `match`
  is `MatchAtStart`" in `docs/DIVERGENCES.md`.
- **Replacement templates use `\1` where .NET uses `$1`.** See "Replacement templates speak
  upstream's language" in `docs/DIVERGENCES.md`.
- **`Split` returns `string?[]` with a `null` entry where a capturing group did not take
  part**, rather than omitting the entry as the built-in `Regex.Split` does. See "`Split` returns
  `string?[]` and puts `null` where a capturing group did not take part" in
  `docs/DIVERGENCES.md`.

These are the three that catch a caller by surprise. See
[`docs/COMPARISON.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md) for the
complete Python `regex` and `System.Text.RegularExpressions` mapping and every other difference,
worked examples included.

## One deliberate difference from mrab-regex's defaults

**Patterns compile as mrab-regex's version 1 by default, where mrab-regex itself defaults to
version 0.** Upstream's front end picks version 0 so that `regex` stays a drop-in replacement for
Python's `re`; this library has no `re` users to protect, and the two behaviours version 1 adds are
the reason to use it over `System.Text.RegularExpressions`:

- **nested sets and set operations** - `[[a-z]--[aeiou]]` is "a to z except the vowels", where
  `System.Text.RegularExpressions` offers subtraction alone (`[a-z-[aeiou]]`);
- **full Unicode case-folding** under `IgnoreCase`, so `ß` matches `SS` and `ﬁ` matches `fi`.
  `System.Text.RegularExpressions` applies simple case folding and matches neither.

Pass `FuzzyRegexOptions.Version0`, or write `(?V0)` at the start of the pattern, to get the `re`
and `Regex` reading back. The only pattern that changes meaning is an unescaped `[` inside a set:
`[[]` is a set containing `[` under version 0 and an unterminated nested set under version 1, and
the parse error says so. Every other deliberate difference from upstream is listed in
[`docs/DIVERGENCES.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/DIVERGENCES.md).

## Thread safety

**A compiled `FuzzyRegex` is immutable and thread safe.** Compile it on any thread, share it
between threads, and call its matching methods from as many at once as you like; nothing it does
alters global state. **A `Match` may be read from any thread too**, and from several at once - it
holds a copy of everything it reports.

The one exception is the one .NET has everywhere: an **enumerator** belongs to the thread that made
it. A `MatchCollection` can be read from several threads, but each needs its own `foreach`.

This is a slightly stronger promise than `System.Text.RegularExpressions.Regex`, which is itself
thread safe but documents its `Match` and `MatchCollection` results as single-thread objects,
because they may compute parts of their answer lazily. Compiling a pattern is the expensive step
here as it is there, so share one instance rather than constructing per call.

Upstream's `concurrent=True` argument has no equivalent and needs none: it asks mrab-regex's C to
release CPython's global interpreter lock during a match, and this port never takes a global lock.

## Where the docs are

- [`docs/COMPARISON.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md) - the
  complete Python `regex` and `System.Text.RegularExpressions` mapping, with a worked example for
  every difference.
- [`docs/DIVERGENCES.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/DIVERGENCES.md) -
  the running ledger of every deliberate difference from upstream: what, why, and how to get
  upstream's behaviour back.
- [`docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md) -
  the design spec.
- [`docs/plan/OPERATIONS.md`](https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/plan/OPERATIONS.md) -
  how the port itself is run, for contributors.

## Licensing

Apache-2.0 (this port), derived from mrab-regex, which is `Apache-2.0 AND CNRI-Python`
(portions derived from CPython's `re` module). See LICENSE and NOTICE (added with the first code)
for full attribution. The upstream source is vendored read-only as the `upstream/` git submodule,
pinned to the exact commit this port tracks.
