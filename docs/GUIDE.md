# Guide

For a reader who knows `System.Text.RegularExpressions` and has never used Python's `regex`
module. If a term here is unfamiliar, `docs/COMPARISON.md` has the fuller migration tables this
guide points to rather than repeats.

## Contents

1. [What this library is for](#what-this-library-is-for)
2. [Choosing an entry point](#choosing-an-entry-point)
3. [Every flag in one table](#every-flag-in-one-table)
4. [Error budgets and named lists](#error-budgets-and-named-lists)
5. [Timeouts and cancellation](#timeouts-and-cancellation)
6. [Reading a result](#reading-a-result)
7. [Limits and failure](#limits-and-failure)
8. [Thread safety](#thread-safety)

## What this library is for

`System.Text.RegularExpressions.Regex` answers "does this pattern match", exactly. FuzzyRegex
answers the same question and one more: "how close is the nearest match", with a budget you set
per pattern. Reach for it when the input is typed by hand, scanned with OCR, or otherwise
unreliable, and a strict `Regex` would reject a match that is obviously the one you meant.

An error budget lives inside the pattern, next to the part of it that may be wrong:

```csharp
using Fuzzy.Text.RegularExpressions;

Match match = FuzzyRegex.MatchAtStart("hallo", @"(?:hello){e<=1}");
Console.WriteLine(match.Value);
Console.WriteLine(match.FuzzyCounts.Substitutions);
// hallo
// 1
```

`{e<=1}` allows up to one error of any kind - a substitution, an insertion or a deletion - inside
the group it follows. `hallo` costs one substitution (`e` for `a`), so it matches; a second typo
would not. Section 4 covers separate budgets per error kind and weighted cost forms.

## Choosing an entry point

`FuzzyRegex` mirrors `Regex` member for member, so most of what you already know still applies:
most reader methods exist as both a `static` convenience that takes a pattern string (and caches
the fifteen most recently used patterns, see `CacheSize` in Section 7) and an instance method on
a compiled `FuzzyRegex`, for a pattern used more than a handful of times. Group by the question
you are asking:

**Is there a match at all?**

| Method | Answers |
|---|---|
| `IsMatch` | Does the pattern match anywhere in the subject? |
| `IsMatchAtStart` | Does it match starting exactly at the given position? (instance only - the static form is `MatchAtStart(...).Success`) |
| `IsFullMatch` | Does it match the entire subject, start to end? (instance only - the static form is `FullMatch(...).Success`) |

**Where is the first one?**

| Method | Answers |
|---|---|
| `Match` | The first match anywhere in the subject, as a `Match`. |
| `MatchAtStart` | A match anchored at the given position, as a `Match`. |
| `FullMatch` | A match spanning the entire subject, as a `Match`. |

**All of them?**

`Matches` builds the whole `MatchCollection` up front; `EnumerateMatches` finds each one lazily,
so a `foreach` that stops early - a `break`, or a LINQ `First` - never pays for subject it never
looked at. Prefer `EnumerateMatches` unless you need `Count` or random access before the scan
finishes.

**How many?**

`Count` returns the number of matches without materialising any of them - cheaper than
`Matches(...).Count` when the matches themselves are not needed.

**Text that is not a string?**

`IsMatch` and `Count` also take a `ReadOnlyMemory<char>` and read it where it is, so a slice of
a `char[]` or a pooled buffer costs no copy: `regex.Count(buffer.AsMemory(0, filled))`. The slice
is the whole subject, so `^` and `\b` see its edges, not the buffer's. The `ReadOnlySpan<char>`
overloads copy the span to a string first, two bytes per character, because a span cannot be kept
while the engine works. Every method that returns a `Match` takes a `string`, since `Value` is one.

**Cut it up?**

`Split` returns the whole `string?[]` at once; `EnumerateSplits` yields each piece lazily, the
same trade-off as `Matches` against `EnumerateMatches`.

**Rewrite it?**

`Replace` takes either a replacement template - upstream's `\1` syntax, rather than .NET's `$1`,
see `docs/DIVERGENCES.md` - or a `MatchEvaluator` delegate for logic a template cannot express.
`ReplaceFormat` takes a Python `str.format`-style template (`{1}`, `{name}`) instead. If you
already have a `Match` in hand - inside a `foreach` over `EnumerateMatches`, say - its own
`Result` and `ResultFormat` methods apply either template to that one match directly, without a
separate `Replace` call:

```csharp
using Fuzzy.Text.RegularExpressions;

string input = "room 12b, room 7";
string output = FuzzyRegex.Replace(input, @"\d+", (Match m) => "#" + m.Value);
Console.WriteLine(output);
// room #12b, room #7
```

**Escaping user input.** `Escape` turns a plain string into a pattern that matches it literally,
the same purpose as `Regex.Escape`. Its two extra parameters, both upstream's, are `specialOnly`
(escape only characters that need it, on by default) and `literalSpaces` (leave spaces unescaped;
by default they are escaped too, so the result still matches literally under
`IgnorePatternWhitespace`).

**One pattern, many calls: static or instance?** Every static method above compiles its pattern
argument through the same fifteen-entry cache `FuzzyRegex.CacheSize` sizes. That is fine for a
pattern used once or twice. Hold a `new FuzzyRegex(pattern)` instead when one pattern runs
repeatedly, or when more than fifteen patterns are in rotation and each call evicts the one the
next call wants: a constructor never touches the cache, so the pattern is parsed once and stays
compiled for as long as the instance is held.

## Every flag in one table

`FuzzyRegexOptions` is a `[Flags]` enum passed to a constructor or a static method's `options`
parameter; combine values with `|`. `FuzzyRegexOptions.None` means no flags at all, and is what
every static convenience defaults its `options` parameter to. The same flags are written inline,
at the start of a pattern, as `(?letters)` - `(?im)` turns on `IgnoreCase` and `Multiline` for
that pattern regardless of what the caller passed. All fifteen inline letters are listed here,
including `(?L)`, which has no enum member (see its row).

Reading a compiled pattern's own flags back: `.Options` returns every flag actually in effect,
implied defaults included, and `.Pattern` returns the source text it was compiled from.

```csharp
using Fuzzy.Text.RegularExpressions;

var regex = new FuzzyRegex(@"x");
Console.WriteLine(regex.Options);
// Unicode, Version1, FullCase
```

Measured 2026-09-21 on this build, `dotnet run -c Release` over `.scratch/flags-probe`.

| Enum member | Inline | Default | What it does |
|---|---|---|---|
| `Unicode` | `(?u)` | **on** | `\w`, `\s`, `\d` and the word boundaries cover all of Unicode. Already on for every text pattern; see its remarks. |
| `Version1` | `(?V1)` | **on** | Nested sets and set operations (`[[a-z]--[aeiou]]`); full Unicode case-folding under `IgnoreCase`. See "One deliberate difference from mrab-regex's defaults" in the README. |
| `FullCase` | `(?f)` | **on** (via `Version1`) | Full case-folding when matching case-insensitively, so `ß` matches `SS`. Already on under `Version1`; pass `Version0` or write `(?-f)` to turn it off. |
| `IgnoreCase` | `(?i)` | off | Case-insensitive matching. Turkic `I`/`ı` pairing is never applied, in any version. |
| `Multiline` | `(?m)` | off | `^` and `$` also match at the start and end of each line, not only of the whole subject. |
| `Singleline` | `(?s)` | off | `.` also matches a newline. |
| `IgnorePatternWhitespace` | `(?x)` | off | Unescaped whitespace in the pattern is ignored and `#` starts a comment. |
| `Ascii` | `(?a)` | off | `\w`, `\s`, `\d` and the word boundaries cover ASCII only. Rejected together with `Unicode`. |
| `RightToLeft` | `(?r)` | off | Search backwards from the end of the subject. |
| `Word` | `(?w)` | off | `\b`/`\B` follow Unicode's own word-boundary rules (UAX #29) rather than a `\w`-to-`\W` transition, so `can't` never splits. |
| `BestMatch` | `(?b)` | off | Rank fuzzy candidates by cost rather than by error count, under the conditions in `docs/COMPARISON.md`'s "Fuzzy syntax in one page". |
| `EnhanceMatch` | `(?e)` | off | After finding a fuzzy match, try to improve its fit; same ranking change as `BestMatch`. |
| `Posix` | `(?p)` | off | Leftmost-longest matching instead of leftmost-first. Can be much slower; set `matchTimeout`. |
| `Version0` | `(?V0)` | off | Upstream's own default: simple case-folding, and an unescaped `[` inside a set is a literal. |
| *(no enum member)* | `(?L)` | off | Upstream's locale-dependent matching. Not exposed as a flag because .NET has no equivalent locale to ask for; see `FuzzyRegexOptions`'s own remarks. |

Two flags worth a runnable example, both measured against this build:

```csharp
using Fuzzy.Text.RegularExpressions;

var ascii = new FuzzyRegex(@"\w+", FuzzyRegexOptions.Ascii);
var unicode = new FuzzyRegex(@"\w+");
Console.WriteLine(ascii.Match("café").Length);
Console.WriteLine(unicode.Match("café").Length);
// 3
// 4
```

`café`'s `é` is outside ASCII, so `Ascii`'s `\w+` stops before it and `Unicode`'s does not.

```csharp
using Fuzzy.Text.RegularExpressions;

var posix = new FuzzyRegex(@"a|ab", FuzzyRegexOptions.Posix);
var leftmostFirst = new FuzzyRegex(@"a|ab");
Console.WriteLine(posix.Match("ab").Length);
Console.WriteLine(leftmostFirst.Match("ab").Length);
// 2
// 1
```

Both alternatives start at the same position; `Posix` takes the longer one instead of the first
one the pattern lists.

## Error budgets and named lists

An error budget - `{e<=2}`, separate `{s<=1,i<=1,d<=1}` budgets per error kind, or a weighted cost
form such as `{2i+1s<4}` - attaches to a group the same way a quantifier does. `docs/COMPARISON.md`'s
"Fuzzy syntax in one page" is the complete reference for the syntax; this guide does not repeat
it.

A **named list** lets a fuzzy pattern check a placeholder against a fixed vocabulary instead of
matching characters: `\L<colour>{e<=1}` matches a member of a list named `colour`, allowing one
error, where `colour` is supplied through the `namedLists` constructor or static-method
parameter. `NamedLists` reads a compiled pattern's own lists back as
`IReadOnlyDictionary<string, IReadOnlySet<string>>`. See "`\L<name>`: fuzzy matching against a
named list of words" in `docs/COMPARISON.md` for the full syntax and a worked example.

## Timeouts and cancellation

Every method that reads a subject takes both a per-call `timeout` and a `CancellationToken`.
They serve different purposes: a timeout bounds how long a match is allowed to run, for a pattern
whose worst case you do not control; a cancellation token stops a match a caller has decided to
abandon, such as a request whose HTTP client disconnected.

```csharp
using Fuzzy.Text.RegularExpressions;
using System.Threading;

var regex = new FuzzyRegex(@"(a+)+b");
var cts = new CancellationTokenSource();
cts.Cancel();
try
{
    regex.Replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaac", "x", cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("cancelled");
}
// cancelled
```

A compiled instance also carries its own `MatchTimeout`, set at construction and used whenever a
call passes no per-call `timeout`. `FuzzyRegex.InfiniteMatchTimeout` is the sentinel value for "no
timeout", the same role `Regex.InfiniteMatchTimeout` plays, and is what an instance's
`MatchTimeout` reads back as when none was set.

A timeout bounds each **step** of a lazy walk rather than the walk as a whole: `EnumerateMatches` and
`EnumerateSplits` re-arm the clock for every match they produce, where `Matches` and `Split` time
the entire scan once. See "A per-call `timeout` on every input-dependent method" in
`docs/COMPARISON.md` for why a simple runaway pattern such as `(a|a)*b` no longer demonstrates a
timeout at all, now that a compile-time prefilter (`docs/plan/slices/done/`, S60) rejects it
before matching starts.

## Reading a result

A `Match` is a `Group` is a `Capture`, each adding fields the one before it does not have:

- **`Capture`**: `Index`, `Length`, `Value`, and `ValueSpan` for the same text as a
  `ReadOnlySpan<char>`, with no allocation.
- **`Group`** adds `Name`, `Success`, and `Captures` (a `CaptureCollection`) - every time this
  group took part in the match, including any earlier ones, for a group inside a repeated
  quantifier such as `(?:\w+,?)+`.
- **`Match`** adds `Groups`, `LastGroupNumber` and `LastGroupName` (the last group that actually
  captured, upstream's `lastindex`/`lastgroup`), `PartialMatch` (true for a match that stopped at
  the end of the subject because there was no more text to try, only meaningful when the call that
  produced it passed `partial: true`), `NextMatch()` (find the next match after this one, without
  restarting the scan), and `FuzzyCounts`/`FuzzyChanges` (below).

`Match.Groups` (a `GroupCollection`) is both an ordered list and an `IReadOnlyDictionary<string, Group>`: index it by
number (`match.Groups[1]`) or by name (`match.Groups["year"]`), and use `ContainsKey`,
`TryGetValue`, `Keys` and `Values` exactly as on any dictionary. Before matching,
`FuzzyRegex.GroupNumbers` and `GroupNames` list every group the compiled pattern defines, and
`GroupNumberFromName`/`GroupNameFromNumber` convert between the two.

`FuzzyCounts` and `FuzzyChanges` both report `Substitutions`, `Insertions` and `Deletions`;
`FuzzyCounts` gives each as a count, and adds `Total`, the sum of the three, while `FuzzyChanges`
gives each as the list of positions where that kind of error happened:

```csharp
using Fuzzy.Text.RegularExpressions;

Match match = FuzzyRegex.MatchAtStart("fuzzy", @"(?:fuzzy){e<=2}");
Console.WriteLine(match.FuzzyCounts.Total);
Console.WriteLine(match.FuzzyChanges.Substitutions.Count);
// 0
// 0
```

An exact match has empty change lists and every count at zero, as here.

## Limits and failure

**`MaxCompiledNodes`** bounds how large a compiled pattern's internal graph is allowed to grow,
guarding against a pattern whose compiled size (not its runtime, which `matchTimeout` guards
instead) would be disproportionate to what it was worth compiling. It defaults to
`FuzzyRegex.DefaultMaxCompiledNodes` (1,000,000) and is set once, at construction, through the
`maxCompiledNodes` constructor parameter; there is no "unlimited" value. A pattern that exceeds it
throws `FuzzyRegexParseException` at compile time, naming the limit in its message.

**`FuzzyRegex.CacheSize`** is the number of compiled patterns the static convenience methods keep
ready between calls (fifteen by default); see Section 2 for when to bypass it with an instance
instead.

**Exceptions.** `FuzzyRegexParseException` is thrown for anything wrong with the pattern itself -
a syntax error, an unknown flag combination, a pattern over `MaxCompiledNodes` - and carries
`Pattern` (the input pattern) and `Offset` (where in it parsing failed) alongside `Message`. Bad
input elsewhere raises `ArgumentNullException` for a null pattern or subject,
`ArgumentOutOfRangeException` for a group number the pattern does not define,
`RegexMatchTimeoutException` when a `matchTimeout` or per-call `timeout` expires, and
`OperationCanceledException` when a `CancellationToken` is cancelled. A `beginning` or `length`
outside the subject is clamped to it rather than rejected - negative values count from the end, as
in Python slicing. See "Exception mapping" in `docs/COMPARISON.md` for the complete table and a
runnable `FuzzyRegexParseException` example.

## Thread safety

A compiled `FuzzyRegex` is immutable and safe to share and call from as many threads as you like,
and so is a `Match`; the one exception is a `MatchCollection`'s enumerator, which - like every
.NET enumerator - belongs to the thread that created it. See "Thread safety" in the README for
the full statement and how it compares to `System.Text.RegularExpressions.Regex`.
