# Comparing FuzzyRegex with Python's `regex` and with `System.Text.RegularExpressions`

FuzzyRegex is a .NET 10 port of [mrab-regex](https://github.com/mrabarnett/mrab-regex), the Python
`regex` module. The pattern syntax is the same: character classes, quantifiers, lookaround,
backreferences, named groups, inline flags, set operations under version 1, and the fuzzy
(approximate) matching syntax that `regex` adds on top of `re` - error budgets in `{...}`, named
lists with `\L<name>`, `BESTMATCH` and `ENHANCEMATCH`. Where the two engines are given the same
pattern and the same subject, and the port has finished the corresponding matching feature, they
answer the same question the same way; the port's test suite pins this position by position against
a Python oracle.

What is deliberately different is the shape of the API around that shared core, and a short list of
places where the port answers on purpose differently from upstream - a few defaults, a handful of
error and Unicode edge cases, and bugs upstream has that this port does not reproduce. Every one of
these is recorded, with the reasoning and the exact behaviour, in
[`docs/DIVERGENCES.md`](DIVERGENCES.md); that file is the authoritative list, and this document
quotes and links its row headings rather than restating them from memory. `docs/DIVERGENCES.md`
also tracks work that is designed but not shipped yet (`PLANNED` rows); nothing in this document
describes a `PLANNED` row as current behaviour.

Table A was checked row by row against `upstream/regex/_main.py` on 2026-09-16.

## Table A: Python `regex` to FuzzyRegex

Module-level functions:

| Python `regex` | FuzzyRegex | Notes |
|---|---|---|
| `regex.compile(pattern, flags=0)` | `new FuzzyRegex(pattern, options)` | Also `new FuzzyRegex(pattern)`, `new FuzzyRegex(pattern, options, matchTimeout, namedLists)`. |
| `regex.search(pattern, string)` | `FuzzyRegex.Match(input, pattern, options, ...)` (static) | See "**`Match` means upstream's `search`...**" below - this is the renamed member. |
| `regex.match(pattern, string)` | `FuzzyRegex.MatchAtStart(input, pattern, options, ...)` (static) | Anchored at the start position only; see the same row. |
| `regex.fullmatch(pattern, string)` | `FuzzyRegex.FullMatch(input, pattern, options, ...)` (static) | Requires the whole subject. |
| `regex.findall(pattern, string)` | no equivalent | See "**There is no `findall`.**" below. |
| `regex.finditer(pattern, string)` | `FuzzyRegex.EnumerateMatches(input, pattern, options, ...)` (static) | Lazy, like `finditer`. `FuzzyRegex.Matches` is the eager counterpart - see "**A lazy walk times each STEP...**" below. |
| `regex.sub(pattern, repl, string)` | `FuzzyRegex.Replace(input, pattern, replacement, options, ...)` (static) | Template language is upstream's, not `$1` - see "**Replacement templates speak upstream's language**" below. |
| `regex.subf(pattern, format, string)` | `FuzzyRegex.ReplaceFormat(input, pattern, format, options, ...)` (static) | `str.format`-style template. |
| `regex.subn(pattern, repl, string)` | `regex.Replace(input, replacement, count, out int replacements, ...)` (instance overloads only; no static form) | Returns the count through an `out` parameter instead of a tuple. |
| `regex.subfn(pattern, format, string)` | `FuzzyRegex.ReplaceFormat(input, format, count, out int replacements, ...)` (instance overload with `out int`) | Same shape as `subn`, format-template flavour. |
| `regex.split(pattern, string)` | `FuzzyRegex.Split(input, pattern, options, ...)` (static) | `-1` means no limit; upstream's `maxsplit=0` does - see "**`Split` spells "no limit" as `maxSplits = -1`**" below. |
| `regex.splititer(pattern, string)` | `FuzzyRegex.EnumerateSplits(input, pattern, options, ...)` (static) | Lazy twin of `Split`. |
| `regex.escape(pattern)` | `FuzzyRegex.Escape(input, specialOnly, literalSpaces)` (static) | Same idea; upstream's `special_only` and `literal_spaces` are `specialOnly` and `literalSpaces` here, both usable by name on either side. |
| `regex.purge()` | `FuzzyRegex.CacheSize = 0` | Upstream clears its module-global pattern cache. The static conveniences here read a bounded most-recently-used cache of fifteen patterns, which S59 added; setting `CacheSize` to `0` empties it and stops it storing, where `purge` clears a cache that stays enabled. The constructors never consult it. |

Compiled `Pattern`'s methods (an instance of upstream's `Pattern`, a compiled `FuzzyRegex` here):
every matching method above (`search` through `splititer`) has an instance counterpart with the
same name and the same mapping, called on the compiled object instead of passed a pattern string;
`escape` is static only and `findall` and `purge` have no counterpart on either - `pattern.search(string)` is
`fuzzyRegex.Match(input, ...)`, `pattern.sub(repl, string)` is `fuzzyRegex.Replace(input,
replacement, ...)`, and so on. Two `Pattern` members have no such static twin above because upstream
itself has none:

| Python `regex` | FuzzyRegex | Notes |
|---|---|---|
| `pattern.groups` (the count) | `FuzzyRegex.GroupNumbers` | Reshaped: a list of the group numbers, group 0 first, not a count. `GroupNumbers.Count - 1` is upstream's `groups` (measured 2026-09-16: `(a)(?<n>b)` gives `0,1,2`). |
| `pattern.groupindex` (the dict) | `FuzzyRegex.GroupNumberFromName`, `FuzzyRegex.GroupNameFromNumber`, `FuzzyRegex.GroupNames` | Reshaped: two lookups and a name list, not a dictionary property. |
| `pattern.scanner()`, `regex.Scanner` | none | See the "Upstream members with no port equivalent" table in `docs/DIVERGENCES.md`. |
| `pattern.prefixmatch(string)`, `regex.prefixmatch` | `FuzzyRegex.MatchAtStart` | An exact alias of `match` upstream; the port has one name for it. |

`Match` object members:

| Python `regex` | FuzzyRegex | Notes |
|---|---|---|
| `match.group(n)` | `match.Groups[n].Value` | No standalone `group` method; go through the group. `match.Groups[0].Value` is also `match.Value` (inherited - see Table B). |
| `match.groups()` | `match.Groups` (numbers 1 upward) | `GroupCollection` is enumerable; skip index 0 for upstream's `groups()` shape. |
| `match.groupdict()` | the named entries of `match.Groups` | See "**`Match.Groups` is an `IReadOnlyDictionary<string, Group>`...**" below - `Groups` is total (every group, keyed by name or by number-as-text), where `groupdict()` is named-only. There is no built-in filter to the named subset; a caller writes one over the dictionary face - cast first, because `Groups` has two enumerable faces and `Groups.Where(...)` is ambiguous (see the row): `((IReadOnlyDictionary<string, Group>)match.Groups.Where(kv => !int.TryParse(kv.Key, out _))`. |
| `match.start([group])`, `match.end([group])`, `match.span([group])` | `match.Groups[n].Index`, `.Index + .Length`, or the pair | No `Start`/`End`/`Span` names; `(Index, Length)` carries the same information. `match.Groups[0]` is `match` itself. |
| `match.captures([group])` | `match.Groups[n].Captures` | A `CaptureCollection`; kept for every group, not only ones inside a repeated construct - see Table B's `Group.Captures` row. |
| `match.starts([group])`, `match.ends([group])`, `match.spans([group])` | `Group.Captures` with `(Index, Length)` | Per `docs/DIVERGENCES.md`'s "Upstream members with no port equivalent" table: all six of upstream's per-index accessors collapse onto one `Group.Captures` returning a `CaptureCollection`. |
| `match.fuzzy_counts` | `match.FuzzyCounts` | A `FuzzyCounts` record (`Substitutions`, `Insertions`, `Deletions`, `Total`). |
| `match.fuzzy_changes` | `match.FuzzyChanges` | A `FuzzyChanges` record (`Substitutions`, `Insertions`, `Deletions`). Substitution and insertion entries are subject positions in UTF-16 code units; a deletion entry is where the missing character would sit in a subject with all earlier deletions put back, which can lie past the end of the match or of the subject (upstream shifts them the same way). |
| `match.expand(template)` | `match.Result(replacement)` | Same template language as `sub`, not `Regex`'s `$1` - see Table B. |
| `match.expandf(format)` | `match.ResultFormat(format)` | `str.format`-style template. |
| `match.detach_string()` | none | Drops the match's reference to the subject so Python can free it; .NET's GC needs no such hint. |
| `match.lastindex` | `match.LastGroupNumber` | Python's `None` (no group matched) is `-1` here - see "**`Regex`-shaped surface**" below. |
| `match.lastgroup` | `match.LastGroupName` | Python's `None` is `null` here. |
| `match.pos`, `match.endpos` | none | The arguments are ported as `beginning`/`length`; only reading them back afterwards is absent. |
| `match.string`, `match.re` | none | `System.Text.RegularExpressions.Match` has neither either. |
| `match.regs` | none | `Groups[n].Index`/`.Length` carries the same information per group. |
| `match.allcaptures`, `match.allspans` | none | See "**`Match.allcaptures`, `allspans`, `groupdict` and `capturesdict` are not on the public surface...`**" below. |

## Table B: `System.Text.RegularExpressions` to FuzzyRegex

| `System.Text.RegularExpressions` | FuzzyRegex | Notes |
|---|---|---|
| `new Regex(pattern, options)` | `new FuzzyRegex(pattern, options)` | `FuzzyRegexOptions` has its own bit values (upstream's `RegexFlag` values, not `RegexOptions`'s); nothing casts between the two enums. |
| `new Regex(pattern, options, matchTimeout)` | `new FuzzyRegex(pattern, options, matchTimeout, namedLists)` | Every input-dependent method also takes its own `timeout` and `CancellationToken` - see "**A `CancellationToken` on every input-dependent method.**" and "**A per-call `timeout` on every input-dependent method**" below. |
| `Regex.IsMatch(input)` | `FuzzyRegex.IsMatch(input)` | Same shape; also a `ReadOnlySpan<char>` overload, which copies the span into a pooled buffer where `Regex`'s reads it in place, and a `ReadOnlyMemory<char>` one, which `Regex` lacks and which reads the buffer in place. `Count` has the same two. |
| `Regex.Match(input)` | `FuzzyRegex.Match(input)` | Same meaning (search anywhere) - see "**`Match` means upstream's `search`...**" below for why this needed no rename. |
| `regex.Match(input).Success` / `.Value` / `.Index` / `.Length` | `match.Success` / `.Value` / `.Index` / `.Length` | Same names, inherited from `Group`/`Capture` rather than declared on `Match`. No `Match.Span`; neither engine has one. |
| `Regex.Matches(input)` | `FuzzyRegex.Matches(input)` | The built-in's `MatchCollection` is lazy (finds the next match as it is enumerated); this port's is eager - see "**A lazy walk times each STEP, where `Matches` times the whole scan.**" below. `EnumerateMatches` is the lazy one here. |
| `Regex.EnumerateMatches(ReadOnlySpan<char>)` | `FuzzyRegex.EnumerateMatches(ReadOnlySpan<char>)` | Same shape: a `ValueMatchEnumerator` of `ValueMatch` values holding `Index` and `Length`. This port's copies the span into a pooled buffer once, at the start of the walk, where `Regex`'s reads it in place. |
| `Regex.Split(input)` | `FuzzyRegex.Split(input)` | See "**`Split` spells "no limit" as `maxSplits = -1`**" and "**`Split` returns `string?[]` and puts `null`...**" below - both the limit convention and the null handling differ. |
| `Regex.Replace(input, replacement)` | `FuzzyRegex.Replace(input, replacement)` | See "**`Replace`'s `count` is inverted the same way**" and "**Replacement templates speak upstream's language**" below. |
| `Regex.Replace(input, MatchEvaluator)` | `FuzzyRegex.Replace(input, MatchEvaluator)` | Same idea; `MatchEvaluator` here is `Fuzzy.Text.RegularExpressions.MatchEvaluator`, a distinct delegate type shaped after the built-in one. |
| `Regex.Escape(input)` | `FuzzyRegex.Escape(input, specialOnly, literalSpaces)` | Two extra parameters; `specialOnly` and `literalSpaces` change which characters are escaped, following upstream's own `escape` rather than `Regex.Escape`'s fixed metacharacter set. |
| `Match.NextMatch()` | `Match.NextMatch()` | Same name and meaning: resumes from where this match ended, within the same slice. The one input-dependent method with no per-call `timeout` or `CancellationToken`: it runs under the pattern's `MatchTimeout`, as `Regex`'s does. |
| `Match.Result(replacement)` | `Match.Result(replacement)` | Same name, different template language - `\1`/`\g<name>` here, `$1` there. `$` is ordinary text in this port's templates. |
| `Group.Success`, `.Value`, `.Index`, `.Length` | `Group.Success`, `.Value`, `.Index`, `.Length` | Same shape. |
| `Group.Captures` | `Group.Captures` | Here, every group keeps its full capture list, oldest first, even outside a quantified construct; the built-in only populates this for a group inside a repeat. |
| `Capture.Value`, `.Index`, `.Length` | `Capture.Value`, `.Index`, `.Length`, plus `.ValueSpan` | Same shape, with an added zero-copy `ReadOnlySpan<char>` accessor. |
| `GroupCollection[name]`, `[number]` | `GroupCollection[name]`, `[number]` | See "**`Match.Groups` is an `IReadOnlyDictionary<string, Group>` as well as a list**" below: the dictionary face here is total (every group), matching .NET 5+'s own `GroupCollection`, not upstream's named-only `groupdict`. |
| `GroupCollection.Count` | `GroupCollection.Count` | Includes group 0 on both. |
| `MatchCollection.Count`, `[index]` | `MatchCollection.Count`, `[index]` | Same shape; see the eager/lazy note above. |

## Fuzzy syntax in one page

Fuzzy matching is written inside `{...}` straight after the construct it applies to - usually a
group. Every example below compiles against `src/FuzzyRegex/PublicAPI.Unshipped.txt` as it stands
today.

### `{e<=n}`: allow up to `n` errors of any kind

`e` is the total error count - substitutions, insertions and deletions added together.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("foxbar", "^(foobar){e<=1}$");
Console.WriteLine(m.Success);   // True
(int subs, int ins, int dels) = m.FuzzyCounts;
Console.WriteLine($"{subs} {ins} {dels}");   // 1 0 0
```

### `{s,i,d,e}`: separate budgets per kind of error

`s`, `i` and `d` cap substitutions, insertions and deletions individually; `e` still caps the total.
A bound with no comparison, like bare `s`, means "any number of this kind is allowed".

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("oobargoobaploowap", "(foobar){i<=2,s<=2,e<=2}");
Console.WriteLine((m.Index, m.Length));   // (5, 6)
```

### Cost forms: `{Ni+Md<n}` weights errors instead of just counting them

A term names how many of one kind cost how much; a kind the equation does not name costs nothing.
The equation is still combined with per-kind caps in the same braces.

```csharp
using Fuzzy.Text.RegularExpressions;

// "3oifaowefbaoraofuiebofasebfaobfaorfeoaro" - one insertion costs nothing here because the
// equation never names 'i'; two deletions or three substitutions are capped, and the total
// weighted cost must come to less than 4.
const string subject = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro";
Match m = FuzzyRegex.Match(subject, "(foobar){i<=1,d<=2,s<=3,2d+1s<4}");
Console.WriteLine((m.Index, m.Length));   // (6, 7)
```

### `{0<e<5}`: two-sided form, at least one error and fewer than five

Reads as a range on the total error count, so a match with no errors is rejected as firmly as one
with too many. An exact occurrence can still match with one error, by leaving out its last letter;
Python's `regex` answers None there, which this port treats as a bug (`docs/DIVERGENCES.md`).

```csharp
using Fuzzy.Text.RegularExpressions;

Match fuzzy = FuzzyRegex.MatchAtStart("servic detection", "(?:service detection){0<e<5}");
Console.WriteLine((fuzzy.Index, fuzzy.Length));   // (0, 16)

Match exact = FuzzyRegex.MatchAtStart("service detection", "(?:service detection){0<e<5}");
Console.WriteLine((exact.Index, exact.Length, exact.FuzzyCounts.Deletions));   // (0, 16, 1)
```

### `{e<=n:[set]}`: constrain which characters an edit may touch

The part after the colon is a character set, and it constrains the edits that put a character into
the match: an insertion is allowed only if the character it brings in is in the set, and a
substitution only if the character it puts there is. A deletion removes a character and adds none,
so the set does not constrain it at all.

```csharp
using Fuzzy.Text.RegularExpressions;

Match ok = FuzzyRegex.FullMatch("ae", @"(?:a){e<=1:[a-z]}");
Console.WriteLine(ok.Success);   // True - the inserted 'e' is in [a-z]

Match rejected = FuzzyRegex.FullMatch("a-", @"(?:a){e<=1:[a-z]}");
Console.WriteLine(rejected.Success);   // False - the inserted '-' is not in [a-z]
```

The set is the last thing inside the braces, so it follows whichever budget form is already there: a
budget per kind of error, a cost equation, or both. `{i<=1,d<=1,s<=1:[a-z]}` and
`{i<=1,d<=2,s<=3,2d+1s<4:[a-z]}` are both valid, and in each the set does the same job - it
constrains insertions and substitutions, and leaves deletions alone. Which kinds the equation
prices makes no difference to it: `{i<=2,d<=0,s<=0,2d+1s<4:[a-z]}` refuses to insert a digit,
though `2d+1s` says nothing about insertions.

Writing the set first is neither an error nor a test set. `{[a-z]:i<=1,d<=1,s<=1}` compiles here and
upstream, and matches nothing where the correct spelling matches - measured on 2026-09-21 against
`regex` 2026.9.10 and this port, which agree on every row here.

That deletions go unconstrained is worth stating twice, because it is the rule that surprises:
`(?:col-our){d<=1:[a-z]}` matches "colour" by removing a hyphen the set does not hold, and answers
identically with the hyphen added to the set or with no set at all.

```csharp
using Fuzzy.Text.RegularExpressions;

Match perKind = FuzzyRegex.Match("colur", @"(?:colour){i<=1,d<=1,s<=1:[a-z]}");
Console.WriteLine(perKind.Value);   // colur - one substitution and one deletion, both within [a-z]
Console.WriteLine(FuzzyRegex.Match("col0ur", @"(?:colour){i<=1,d<=2,s<=3,2d+1s<4:[a-z]}").Success);   // False - the substitution would put a digit in
```

### `FuzzyRegexOptions.BestMatch` / `(?b)`: take the best fuzzy match rather than the first

Without this flag, fuzzy matching stops at the first match that satisfies the budget, scanning
left to right. With it, the engine keeps looking and reports the best-fitting one instead - here,
the occurrence closest to a perfect match rather than the leftmost.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?b)(foobar){e}");
Console.WriteLine((m.Index, m.Length));   // (11, 5)
```

### `FuzzyRegexOptions.EnhanceMatch` / `(?e)`: tighten a match after it is found

`ENHANCEMATCH` keeps the first match's position but then tries to shrink or improve its fit, which
can shorten an otherwise unbounded error match considerably.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?e)(foobar){e}");
Console.WriteLine((m.Index, m.Length));   // (0, 3)
```

### `\L<name>`: fuzzy matching against a named list of words

A named list is a fixed set of literal alternatives, supplied at compile time and referenced by
name in the pattern; combined with a fuzzy budget it fuzzy-matches against every word in the list
at once, taking the closest fit.

The example opens with `(?e)`, the `EnhanceMatch` flag described above, because the tightest fit is
what a list of words is usually for. Drop it and the first match here is ` dog` with a leading
space, since a budget of one error is enough to pay for that space.

```csharp
using Fuzzy.Text.RegularExpressions;

var namedLists = new Dictionary<string, IReadOnlyCollection<string>> { ["words"] = ["cat", "dog"] };
MatchCollection matches = FuzzyRegex.Matches(
    " book dog cot desk ",
    @"(?e)\b\L<words>{e<=1}\b",
    FuzzyRegexOptions.None,
    namedLists
);
foreach (Match m in matches)
{
    Console.WriteLine(m.Value);
}
// dog
// cot
```

`"cot"` matches `"cat"` from the list with one substitution; `"dog"` matches exactly.

## Matching modes the built-in engine does not have

Three switches that change how a search is run rather than what the pattern means. All three are
upstream's, and none of them has an equivalent in `System.Text.RegularExpressions`.

### `FuzzyRegexOptions.Posix` / `(?p)`: leftmost-longest instead of leftmost-first

An alternation normally takes the first branch that matches at the leftmost position - what Perl,
.NET and Python all do. With POSIX matching the engine keeps looking at that same position and takes
the longest match instead, which is the rule `grep` and `awk` follow.

```csharp
using Fuzzy.Text.RegularExpressions;

Match first = FuzzyRegex.Match("abcd", "a|ab|abc");
Console.WriteLine(first.Value);   // a

Match longest = FuzzyRegex.Match("abcd", "a|ab|abc", FuzzyRegexOptions.Posix);
Console.WriteLine(longest.Value);   // abc
```

It costs time, because the position is not settled until every branch has been tried, and it is the
right answer when a rule set has to agree with a POSIX tool rather than with Perl.

### Partial matching: `partial: true` means "so far, so good"

A partial match is one that ran out of subject before it ran out of pattern. It is how a search box
tells a half-typed entry from a wrong one: keep accepting while the match is partial, reject when it
is neither partial nor complete. `Match.PartialMatch` says which of the two a successful match is,
and it is only ever true when a complete match was not available at that position.

```csharp
using Fuzzy.Text.RegularExpressions;

var date = new FuzzyRegex(@"\d{4}-\d{2}-\d{2}");

Match sofar = date.Match("2026-09", partial: true);
Console.WriteLine((sofar.Success, sofar.PartialMatch, sofar.Value));   // (True, True, 2026-09)

Match whole = date.Match("2026-09-19", partial: true);
Console.WriteLine((whole.Success, whole.PartialMatch));   // (True, False)

Match wrong = date.Match("not a date", partial: true);
Console.WriteLine((wrong.Success, wrong.PartialMatch, wrong.Index));   // (True, True, 10)

Match contradicted = date.Match("2026-0b", partial: true);
Console.WriteLine((contradicted.PartialMatch, contradicted.Index, contradicted.Length));   // (True, 7, 0)
```

The third answer is the one to read twice: an empty partial match at the end of the subject is
upstream's way of saying "nothing here contradicts the pattern yet", because the empty tail of the
subject is a prefix of something the pattern could still accept. `partial` is available on
`Match`, `MatchAtStart` and `FullMatch` only. The scanning entry points do not take it, because
upstream's `finditer` and `findall` do not either; neither does `IsMatch`, which asks a yes/no
question that a partial match cannot answer without the match itself to inspect.

The fourth answer is that same rule read from the other side. `2026-0b` starts like a date, and then `\d`
meets the `b` and refuses it, so the candidate that began at index 0 is contradicted rather than unfinished, and
the empty tail at index 7 is again all that survives: a partial match is one that ran out of
subject, and a character the pattern refuses ends a candidate outright. An error budget changes the
answer, because it lets that `b` be a substitution rather than a contradiction -
`(?:\d{4}-\d{2}-\d{2}){e<=1}` returns a partial match of all seven characters at index 0, partial
because the subject ran out while the budget still had room.

### `FuzzyRegexOptions.RightToLeft` / `(?r)`: search from the right

The search starts at the end of the subject and works backwards, so the first match found is the
last one in the text. The pattern itself is not reversed: it still reads left to right.

```csharp
using Fuzzy.Text.RegularExpressions;

Match forwards = FuzzyRegex.Match("one two three", @"\w+");
Console.WriteLine(forwards.Value);   // one

Match backwards = FuzzyRegex.Match("one two three", @"\w+", FuzzyRegexOptions.RightToLeft);
Console.WriteLine(backwards.Value);   // three
```

The built-in engine's flag does the same thing, so the meaning carries over from it. The name is the
only part the two share: `FuzzyRegexOptions` carries upstream's bit values rather than
`RegexOptions`'s. The one place a reversed search answers differently from upstream is a
reversed *partial* match at a slice start; see "**Reversed partial matches run out of text at the
slice start**" below.

## Syntax the built-in engine spells differently

Three places where a pattern that works here means something else in
`System.Text.RegularExpressions`, or nothing at all. The first two follow Python's `regex` exactly;
the third, case folding, follows it apart from the Turkic `I` pairings, which that section names.
Every answer below was measured on 2026-09-21 against .NET 10.0.11, `regex` 2026.9.10 and this port.

### Set operations: `[[a-z]--[aeiou]]` here, `[a-z-[aeiou]]` in the built-in engine

Version 1, which is this port's default, lets one set nest inside another and reads four operators
between them: `--` subtracts, `&&` intersects, `||` unions and `~~` takes the symmetric difference.
The built-in engine has subtraction alone, written as a single dash before a nested class.

The two spellings collide quietly. Neither engine rejects the other's, so the same pattern gives two
different answers with no error to warn you.

```csharp
using Fuzzy.Text.RegularExpressions;

Match here = FuzzyRegex.Match("abc123-", @"[\w-[\d]]+");
Console.WriteLine(here.Value);   // abc123- - word characters and a literal dash, unioned
Console.WriteLine(System.Text.RegularExpressions.Regex.Match("abc123-", @"[\w-[\d]]+").Value);   // abc - there, the digits are subtracted
```

Going the other way, `[[a-z]--[aeiou]]` matches `bcd` in "abcde" here and nothing at all there,
because the built-in engine closes a character class at the first unescaped `]`. It reads a class of
`[` and `a` to `z`, then two literal dashes, then a vowel, then a literal `]` - which is why it
matches the subject "a--e]". Upstream's other spelling, `[\w--\d]`, is a parse error there:
`Invalid pattern '[\w--\d]+' at offset 7. Cannot include class \d in character range.`

Set operations ride on version 1, so `FuzzyRegexOptions.Version0` or `(?V0)` turns them back into
plain classes here, which is what upstream does by default.

### Unicode properties: `\p{Greek}` names a script here, `\p{IsGreek}` names a block there

Here, as upstream, `\p{Greek}` is the Greek script, and `\p{IsGreek}` is another way to write it.
The Greek block is `\p{InGreek}` or `\p{Block=Greek}`. Script and block are not the same set: U+1F00,
the Greek letter alpha with psili, has script Greek and lives in the Greek Extended block.

The built-in engine has no scripts. `\p{Greek}` there is the parse error `Unknown property 'Greek'`,
and `\p{IsGreek}` is the block, so it does not match U+1F00 while `\p{IsGreekExtended}` does. A
pattern carried across keeps its spelling and changes its meaning.

```csharp
using Fuzzy.Text.RegularExpressions;

Match here = FuzzyRegex.Match("ἀ", @"\p{IsGreek}");
Console.WriteLine(here.Success);   // True - the script, which covers the Greek Extended block
Console.WriteLine(System.Text.RegularExpressions.Regex.IsMatch("ἀ", @"\p{IsGreek}"));   // False - there, IsGreek is the block alone
```

This port also carries upstream's named properties beyond the general categories, such as
`\p{Alphabetic}` and `\p{Word}`. The built-in engine knows neither, and rejects an unknown name as a
parse error, as this port does.

### `IgnoreCase` folds a whole string here, one character at a time in the built-in engine

Version 1 turns on full case folding, so a single character matches the several it folds to, in both
directions: `ß` matches "SS", and the ligature `ﬁ` matches "FI". The built-in engine folds one
character to one character and matches neither. Both engines pair `k` with the Kelvin sign U+212A,
which is a single-character fold.

```csharp
using Fuzzy.Text.RegularExpressions;

Match here = FuzzyRegex.Match("SS", "ß", FuzzyRegexOptions.IgnoreCase);
Console.WriteLine(here.Success);   // True - full case folding, version 1's default
Console.WriteLine(System.Text.RegularExpressions.Regex.IsMatch("SS", "(?i)ß"));   // False - one character folds to one character
```

Greek brings a second difference, and full folding is not what causes it: capital sigma matches the
final form `ς` here and does not there, and that pairing survives both `(?V0)` and `(?-f)`. Full
folding itself belongs to version 1, so either of those turns it off and leaves single-character
folding behind. The Turkic `I` pairings are the one fold upstream applies that this port does not;
see "**The Turkic `I` pairings are not applied by default**" below.

### A repeat pass that matched nothing but changed a tested group goes round again

Every backtracking engine stops a repeat whose pass matched no text, or `(a?)*` would loop for
ever. The built-in engine, like Perl and PCRE2, looks only at the position: a pass that read nothing
ends the repeat. Here, as upstream, a pass that read nothing but changed a group that a conditional
or backreference later tests counts as progress, because the next pass can now match something the
first could not.

```csharp
using Fuzzy.Text.RegularExpressions;

// Pass 1 cannot read 'c' (group 1 unset), so it sets group 1 and reads nothing; pass 2 reads 'c'.
Console.WriteLine(FuzzyRegex.Match("c", @"^(?:(?(1)c|z)|())*$").Success);   // True
Console.WriteLine(System.Text.RegularExpressions.Regex.IsMatch("c", @"^(?:(?(1)c|z)|())*$"));   // False - the repeat stops after pass 1
Console.WriteLine(System.Text.RegularExpressions.Regex.IsMatch("c", @"^(?:(?(1)c|z)|()){2,}$"));   // True - so there, {2,} matches what * does not
```

The rule keeps `X*` matching everything `X{2,}` matches, which position-only checking does not.
The survey and the reasoning are in `docs/plan/2026-09-26-empty-iteration-survey.md` (D12).

## Behaviour that differs and why

One section per SHIPPED row in `docs/DIVERGENCES.md`, quoting each row's heading exactly. See that
file for the full reasoning and decision trail behind each one.

### Version 1 is the default

Version 1 turns on nested sets and set operations (`[[a-z]--[aeiou]]`) and full Unicode
case-folding under `IgnoreCase`. Upstream defaults to version 0 for backward compatibility with
`re`; this port has no such legacy users, so `FuzzyRegexOptions.Version1` is implied when neither
version flag is given.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("d", "[[a-z]--[aeiou]]");
Console.WriteLine(m.Success);   // True - set subtraction, version 1's default behaviour
```

To get upstream's default, pass `FuzzyRegexOptions.Version0` or write `(?V0)` in the pattern.

### `(?V0)` really means version 0, where upstream's own algorithm would leave version 1's `FULLCASE` on

An inline `(?V0)` compiles under version 0 in every respect here, including turning off full
case-folding. Upstream's own front end has a bug reachable only once `regex`'s own default version
changes (which it never does), so it is not reproducible upstream as shipped; there is no example
to run against upstream for this one.

### The "unterminated character set" parse error names `FuzzyRegexOptions.Version0` and the `\[` escape

Under version 1, a pattern like `[[]` or `[a[b]` - which `re`, `System.Text.RegularExpressions` and
version 0 all accept as a literal `[` - fails to compile, because an unescaped `[` opens a nested
set. The parse error names `Version0` and the `\[` escape as the two ways out, rather than upstream's
bare "unterminated character set" text.

```csharp
using Fuzzy.Text.RegularExpressions;

try
{
    _ = new FuzzyRegex("[[]");
}
catch (FuzzyRegexParseException ex)
{
    Console.WriteLine(ex.Message.Contains("Version0"));   // True
}
```

### No `concurrent` parameter

Upstream's `concurrent=True` releases the GIL during a match. There is no GIL in .NET to release,
so every call is already concurrent; there is no parameter and no observable behaviour to show.

### A `Match` may be read from any thread, where `System.Text.RegularExpressions.Match` may not

A `Match` this library returns holds a copy of everything it reports, so it can be read from several
threads at once with no synchronisation. The built-in `Regex` documents no such guarantee, so there
is nothing to print here: the difference is in what is safe to do with the answer, and a call
returns the same thing either way.

### A `CancellationToken` on every input-dependent method

Every method that reads the subject - `Match`, `Matches`, `Replace`, `Split`, and so on - takes a
`CancellationToken` as its last parameter, with one exception: `Match.NextMatch()` takes neither a
token nor a timeout and runs under the pattern's `MatchTimeout`, as `Regex`'s does. Upstream has no equivalent, because a Python caller
interrupts a long match with Ctrl-C instead.

```csharp
using Fuzzy.Text.RegularExpressions;

var cts = new CancellationTokenSource();
cts.Cancel();
try
{
    FuzzyRegex.IsMatch("aaaaaaaaaa", "(a+)+b", cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("cancelled");   // cancelled
}
```

### A per-call `timeout` on every input-dependent method

Every input-dependent method except `Match.NextMatch()` also takes a `TimeSpan? timeout`, defaulting
to `null`, which means "use the pattern's own `MatchTimeout`" (`NextMatch` always uses it). This is parity with upstream, which has always taken a
per-call `timeout=`; what is new is that the built-in `Regex` never gave an instance method one, so
the shape here is closer to upstream than to `Regex`.

```csharp
using Fuzzy.Text.RegularExpressions;

// (a|a)* with a tail that can never hold is exponential in this engine as in upstream: measured
// 2026-09-19, regex 2026.9.10 spends 24.5 s on "(a|a)*b" over "a"*26 + "cb" (ROADMAP records
// 23.3 s at n=26). "(a|a)*b" itself is NOT the demonstration to reach for any more - since S60
// this port has upstream's required-string prefilter, so both engines refuse a subject with no
// "b" in it before matching starts. \b\B is false at every position and gives the prefilter no
// literal to work with, so the search still has to be made, and the timeout is what stops it.
// (a+)+b is NOT a good demonstration either, upstream's repeat guards answer it in milliseconds.
// The \1 matters too. This port remembers where a repeat's body has already failed, which makes
// (a|a)*\b\B fail at once; it does not do that in a pattern with a backreference, because what
// follows can then depend on the captures as well as the position.
var pattern = new FuzzyRegex(@"(a|a)*\1\b\B");
try
{
    pattern.IsMatch(new string('a', 26), timeout: TimeSpan.FromMilliseconds(50));
}
catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
{
    Console.WriteLine("timed out");   // timed out
}
```

### `Match` means upstream's `search`; upstream's anchored `match` is `MatchAtStart`

Upstream's `search` scans anywhere in the subject and is what `Regex.Match` also means, so this
port keeps the .NET name for it. Upstream's `match`, which anchors at the start position only, is
`MatchAtStart` here instead - the name a .NET caller would otherwise misread as searching anywhere.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.Match("xab", "ab").Success);          // True  - search finds it anywhere
Console.WriteLine(FuzzyRegex.MatchAtStart("xab", "ab").Success);   // False - anchored at position 0
```

### `Regex`-shaped surface

The public types are named `FuzzyRegex`, `Match`, `Group`, `Capture` with `(Index, Length)`, and the
scanning members are `Matches`/`Split`/`Replace` rather than upstream's `finditer`/`split`/`sub`.
`expand` is `Match.Result`, `expandf` is `Match.ResultFormat`, `lastindex` is `Match.LastGroupNumber`
(with Python's `None` as `-1`), and `lastgroup` is `Match.LastGroupName` (with `None` as `null`).

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("a", "(x)?(a)");
Console.WriteLine(m.LastGroupNumber);   // 2 - group 1 never took part
Console.WriteLine(m.LastGroupName ?? "null");     // null - group 2 has no name
```

### **Indices are UTF-16 code units**, where upstream counts codepoints

`Index`, `Length` and `FuzzyRegexParseException.Offset` all count UTF-16 code units, matching
`System.Text.RegularExpressions`. Upstream counts Unicode codepoints, so a subject containing a
character outside the Basic Multilingual Plane (which needs two UTF-16 code units) reports a
different number here than it does upstream.

```csharp
using Fuzzy.Text.RegularExpressions;

// U+1F600 GRINNING FACE is one codepoint but two UTF-16 code units.
Match m = FuzzyRegex.Match("\U0001F600b", "b");
Console.WriteLine(m.Index);   // 2 - upstream reports 1
```

### Upstream's `pos`/`endpos` are reshaped to `beginning`/`length`

Every method that takes a starting position and a limit takes `beginning` and `length`, where
`length` is a length in UTF-16 code units and `-1` means "the rest of the subject": neither an end
index nor upstream's Python-slice reading of a negative `endpos`.

```csharp
using Fuzzy.Text.RegularExpressions;

var pattern = new FuzzyRegex("b.");
Console.WriteLine(pattern.Match("abcde", beginning: 1, length: 2).Value);   // bc
```

### `Split` spells "no limit" as `maxSplits = -1`

Upstream's `maxsplit=0` means no limit, and a negative `maxsplit` means "make no splits at all".
This port inverts that at the boundary: `-1` (the default) means no limit, and `0` means "split
nowhere - return the whole subject as one piece".

```csharp
using Fuzzy.Text.RegularExpressions;

var pattern = new FuzzyRegex(",");
Console.WriteLine(string.Join("|", pattern.Split("a,b,c", maxSplits: 0)));   // a,b,c
Console.WriteLine(string.Join("|", pattern.Split("a,b,c")));                 // a|b|c
```

### `Replace`'s `count` is inverted the same way

Upstream's `count=0` means "replace every match", and a negative count replaces nothing. Here `-1`
(the default) means no limit, and `0` means "replace nothing".

```csharp
using Fuzzy.Text.RegularExpressions;

var pattern = new FuzzyRegex("a");
Console.WriteLine(pattern.Replace("aaa", "b", count: 0));    // aaa - "replace nothing"
Console.WriteLine(pattern.Replace("aaa", "b"));               // bbb - "-1" means no limit
```

### There is no `findall`

Upstream's `findall` returns the text of the single capturing group when the pattern has exactly
one, and a tuple of group texts when it has more than one - not the matched text itself. This port
has no equivalent; `Matches` returns `Match` objects, `EnumerateMatches` returns them lazily, and
`Count` returns only a count. `regex.findall(r'(\w\w\K\w\w)', 'abcdefgh')` is `['abcd', 'efgh']`
upstream, while the actual matches (what `Matches` in this port sees) are `'cd'` and `'gh'` - the
`\K` resets where the match is considered to start, but `findall` keeps returning the group's own
text, which upstream's own documentation calls out as a trap.

```csharp
using Fuzzy.Text.RegularExpressions;

MatchCollection matches = FuzzyRegex.Matches("abcdefgh", @"(\w\w\K\w\w)");
foreach (Match m in matches)
{
    Console.WriteLine(m.Value);
}
// cd
// gh
```

### `Match.Groups` is an `IReadOnlyDictionary<string, Group>` as well as a list

`Groups` is indexable both by number and by name, and it also implements
`IReadOnlyDictionary<string, Group>` keyed by every group's name - or its number as text for a group
with none - in ascending group number. This matches .NET 5+'s own `GroupCollection`, which turned
out to be total (every group) rather than named-only when measured; upstream's `groupdict` is the
named-only filter over the same information.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("abc", @"(?<a>a)(b)(?<c>c)?");
Console.WriteLine(string.Join(",", m.Groups.Keys));   // 0,a,2,c
```

### A lazy walk times each STEP, where `Matches` times the whole scan

`EnumerateMatches` and `EnumerateSplits` take a `timeout` like every other entry point, but each
match is found on a fresh engine state, so the clock restarts for every step rather than covering
the whole walk. `Matches`, by contrast, holds one state for the entire scan and so has one budget
for it - upstream's `finditer` scanner works the same way `Matches` does here. There is no single
printable output that shows a restarting clock; a caller who needs the walk bounded as a whole uses
`Matches` instead of `EnumerateMatches`.

### `Split` returns `string?[]` and puts `null` where a capturing group did not take part

Upstream puts `None` in that slot; the built-in `Regex.Split` omits the entry entirely, which both
shortens the array and loses the difference between a group that matched empty and one that never
ran at all. This port keeps upstream's shape.

```csharp
using Fuzzy.Text.RegularExpressions;

var pattern = new FuzzyRegex("(x)|(1)");
Console.WriteLine(string.Join("|", pattern.Split("a1b").Select(static s => s ?? "null")));
// a|null|1|b
```

### Replacement templates speak upstream's language

Replacement templates use `\1`, `\g<name>`, `\n`, `\x41` and `\N{...}`: the escape character is
upstream's `\` where `Regex` uses `$`. `$` is ordinary text in these templates.

```csharp
using Fuzzy.Text.RegularExpressions;

var pattern = new FuzzyRegex(".");
Console.WriteLine(pattern.Replace("x", @"\n") == "\n");   // True - \n is a newline, as upstream reads it
```

### Exception mapping

Upstream's `error` (and its bare `ValueError` rejections) become `FuzzyRegexParseException`.
`Match.expand`'s `IndexError("unknown group")` becomes `ArgumentException`, while the same mistake
inside `sub` stays `FuzzyRegexParseException` - upstream itself raises two different errors there
too. Internal-error escapes (`RuntimeError`, `AttributeError`, `TypeError`, `KeyError` from
`V0|V1`) become `NotSupportedException` or `ArgumentOutOfRangeException`; the 1 GB backtracking
bound and runaway recursion raise `InvalidOperationException`, never `OutOfMemoryException`; a
matching timeout raises `System.Text.RegularExpressions.RegexMatchTimeoutException`; `\p{Infinity}`
raises `OverflowException` carrying Python's own message.

```csharp
using Fuzzy.Text.RegularExpressions;

try
{
    _ = new FuzzyRegex("(");
}
catch (FuzzyRegexParseException)
{
    Console.WriteLine("parse error, as expected");   // parse error, as expected
}
```

### `Match.allcaptures`, `allspans`, `groupdict` and `capturesdict` are not on the public surface, and will not be

Every upstream use of these four is already reachable through `Groups` and `Group.Captures`, so the
owner decided against adding four more accessors for the same data. There is no example to give for
an absence; use `Groups` and `Group.Captures` instead, as shown under `match.captures` in Table A.

### The "unused keyword argument" message interpolates the name as written

When a named list is supplied but never referenced in the pattern, the error message includes the
name exactly as it was written. Upstream instead formats it through Python's `ascii()` repr, so a
named list called `é` reports `unused keyword argument '\xe9'` upstream and `unused keyword argument
'é'` here. ASCII names read identically on both engines; a non-ASCII name reaches the check through
the constructor's `namedLists` argument:

```csharp
using Fuzzy.Text.RegularExpressions;

var lists = new Dictionary<string, IReadOnlyCollection<string>> { ["é"] = new[] { "x" } };
try
{
    _ = new FuzzyRegex("a", FuzzyRegexOptions.None, lists);
}
catch (FuzzyRegexParseException ex)
{
    Console.WriteLine(ex.Message);   // unused keyword argument 'é'
}
```

No test in `tests/FuzzyRegex.Tests` pins this message's exact wording; it is pinned only in
`docs/DIVERGENCES.md`.

### Not ported at all

A number of upstream members have no port equivalent of any kind: `Pattern.scanner`/`regex.Scanner`,
`Match.pos`/`endpos`/`string`/`re`/`regs`/`detach_string`, `regex.template` and the `TEMPLATE`/`T`
flag, the `LOCALE` and `DEBUG` flags as public options, `_compile`'s `ignore_unused`,
`regex.compile`'s `cache_pattern`, `regex.prefixmatch` (folded into `MatchAtStart`), and the
`__copy__`/`__deepcopy__` protocol on patterns and matches. See the "Upstream members with no port
equivalent" table in `docs/DIVERGENCES.md` for the member-by-member reasoning; there is no single
example that covers an absent member.

### `(?e)` and `(?b)` rank candidates by fuzzy COST

Under `EnhanceMatch` or `BestMatch`, when the pattern has exactly one fuzzy section and a weighted
cost equation, this port ranks candidate matches by cost first (cheapest, then fewer errors, then
earliest), where upstream ranks by error count alone. With unit costs, or with a nested second
fuzzy section, the two engines fall back to the same rule and agree exactly.

```csharp
using Fuzzy.Text.RegularExpressions;

// "3oifaowefbaoraofuiebofasebfaobfaorfeoaro" under a weighted equation that prices a deletion
// at 2 and a substitution at 1: this port finds the cheaper two-substitution-one-insertion match;
// upstream finds the one-substitution-one-deletion match instead, because it counts errors, not cost.
const string subject = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro";
Match m = FuzzyRegex.Match(subject, "(?b)(foobar){i<=1,d<=2,s<=3,2d+1s<4}");
Console.WriteLine((m.Index, m.Length));   // (26, 7) - upstream answers (34, 5)
```

### The Turkic `I` pairings are not applied by default

`(?i)` and `(?fi)` here do not pair `I` with dotless `ı`, or `i` with dotted capital `İ`; upstream
pairs them unconditionally, which is a Turkish-locale rule applied with no locale requested. No
Turkic mode is offered on this port's API.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.FullMatch("a\u0131", "aI", FuzzyRegexOptions.IgnoreCase);
Console.WriteLine(m.Success);   // False - upstream's regex.fullmatch('aI', 'aı', I) matches
```

### A case-insensitive cased property under ASCII means the 52 ASCII letters

With both `(?a)` and `(?i)`, `\p{Lu}`, `\p{Ll}`, `\p{Lt}`, `\p{Upper}`, `\p{Lower}`, `[[:upper:]]`
and `[[:lower:]]` match `a` to `z` and `A` to `Z`, and nothing else. Upstream answers this three
ways: its `match` also accepts letters such as `É`, its `search` misses the lowercase `a`, and its
set form `[\p{Lu}x]` gives the ASCII letters. Perl and PCRE2 give the ASCII letters for
`[[:upper:]]` and `[[:lower:]]`, and Python's `re` documents the same rule for `[A-Z]`. Wrap the
property in `(?u:...)` for the Unicode answer.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.MatchAtStart("É", @"(?ai)\p{Lu}").Success);  // False - upstream's regex.match matches
Console.WriteLine(FuzzyRegex.Match("a", @"(?ai)\p{Lu}").Success);              // True - upstream's regex.search is None
```

### A scope that names no encoding keeps the one around it

`(?a:(?s:\w))` refuses 'é' here, as Python's `re` does; upstream's inner `(?s:` drops the outer
`(?a:` and matches it.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.FullMatch("\u00E9", @"(?a:(?s:\w))").Success);  // False - upstream matches
```

### A POSIX class takes the scope's encoding

`(?a:[[:alpha:]])` refuses 'é', exactly as `(?a:\p{L})` does. Upstream reads a POSIX class with the
pattern's tables whatever scope it sits in.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.FullMatch("\u00E9", "(?a:[[:alpha:]])").Success);  // False - upstream matches
```

### A case-insensitive cased property answers the same bare and in a set

Under `(?i)`, `\p{Lu}`, `\p{Ll}`, `\p{Lt}`, `\p{Upper}` and `\p{Lower}` mean "any cased letter"
wherever they appear, which is what Perl and .NET's `Regex` do. Upstream uses that rule for a bare
property and a different one inside a set, so wrapping a property in brackets changed its answer.
It also changes when the property can start the match after an optional item, because upstream
then checks each start position against a set of the possible first items. `\p{Upper=No}` is
the complement of `\p{Upper}`.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.FullMatch("\u0138", @"(?i)[\p{Lu}x]").Success);  // True - upstream's set form refuses
Console.WriteLine(FuzzyRegex.FullMatch("a", @"(?i)\p{Upper=No}").Success);     // False - upstream matches
Console.WriteLine(FuzzyRegex.Match("\u2102aa", @"(?i)\p{Ll}?a{2}", FuzzyRegexOptions.Version0).Index);  // 0 - upstream: 1, skipping U+2102
```

### A case-insensitive set matches each member first, then combines them

Under `(?i)` each member of a set matches case-insensitively on its own, and only then does the set
union, intersect or subtract the answers. Perl's extended sets and .NET's set subtraction work this
way. Upstream tests every case variant of the character against the case-sensitive set instead, so
the answer changed with how the set was written: `(?i)[\p{Greek}x]` matched the micro sign where
`(?i)\p{Greek}` refused it, and `(?i)[x[\w--\p{Lu}]]` matched 'a' although `\p{Lu}` under `(?i)`
covers every cased letter.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.FullMatch("\u00B5", @"(?i)[\p{Greek}x]").Success);  // False - upstream matches
Console.WriteLine(FuzzyRegex.FullMatch("a", @"(?i)[x[\w--\p{Lu}]]").Success);    // False - upstream matches
```

### An encoding named by positional flags inside a group replaces the one in force

`(?a:(?u)\w)` matches 'é', as `(?a:(?u:\w))` does. Upstream keeps both encodings after `(?u)` and
ASCII wins. Python's `re` rejects the positional spelling inside a group, so it has no answer.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.FullMatch("\u00E9", @"(?a:(?u)\w)").Success);  // True - upstream refuses
```

### A scoped `(?a:...)` or `(?u:...)` answers exactly as the same encoding set for the whole pattern

`(?i)(?a:k)` means exactly what `(?ai)k` means, for every construct whose answer depends on the
encoding: case-insensitive letters, ranges, sets, backreferences and named lists, fuzzy matching,
`\m` and `\M`, the `(?w)` word and line rules, `\X`, and full case folding. So under ASCII rules the
Kelvin sign U+212A never matches 'k', whether ASCII is set for the whole pattern or only for a group.
Python's `re` and Perl answer this way. Upstream applies a scoped encoding only to `\p{...}`
properties and `\b`, and reads the pattern's encoding everywhere else.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.FullMatch("\u212A", "(?i)(?a:k)").Success);   // False - upstream matches
Console.WriteLine(FuzzyRegex.FullMatch("\u212A", "(?ai)(?u:k)").Success);  // True - upstream refuses
```

### This port's search prefilters never change the slow path's answer

`Match`, `EnumerateMatches` and partial matching can answer differently from upstream on several
pattern families. A prefilter is a cheap scan that picks the next position worth trying, so that the
matcher is not asked about positions where it would obviously fail. This port has both of upstream's,
but narrowed: they are withheld wherever upstream's own prefilter and upstream's own matcher would
disagree, which happens when a `(*SKIP)` moves the region under consideration mid-attempt, when a
partial match is being reported, and on the case-folded scans. So where the two engines differ, this
port answers what upstream's own anchored `match` answers. That answer has been checked against a
second independent engine and is treated as permanently correct rather than as a placeholder to
invert later.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("A", "(?ai)\\p{Ll}");
Console.WriteLine(m.Success);   // True - upstream's regex.search(r'(?ai)\p{Ll}', 'A') is None
```

### The Unicode data is version 17.0.0

`\N{...}` and other Unicode-table lookups resolve against Unicode 17.0.0 here, where upstream
resolves through whatever `unicodedata` version the host CPython build ships (16.0.0 at the time
this was measured). A small number of codepoints named in 17.0.0 but not 16.0.0 resolve here and
raise upstream.

`tests/FuzzyRegex.Tests/Gaps/Unicode/UnicodeCharacterNameTests.cs`'s `Every_stored_name_resolves_to_its_own_codepoint`
pins one such name: `"TOLONG SIKI DIGIT ONE"` resolves to U+11DE1, a codepoint Unicode 17.0.0 added.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("\U00011DE1", @"\N{TOLONG SIKI DIGIT ONE}");
Console.WriteLine(m.Success);   // True - upstream on a 16.0.0 unicodedata host has no such name
```

### A digit's decimal value is derived from upstream's own tables

Where a `\d`-style construct needs a digit's numeric value, this port derives it from upstream's own
digit tables (by the codepoint's position within its run of decimal digits), rather than from
.NET's `CharUnicodeInfo`, which is built from an older Unicode version and disagrees with the
`\d`/digit-set gate on some very recent codepoints. There is no single-line example: the affected
codepoints are outside the Basic Multilingual Plane and the difference is only visible by comparing
against `CharUnicodeInfo` directly, which is internal engine behaviour rather than public surface.

### `\N{...}` named sequences are not carried

A named character *sequence* (multiple codepoints under one `\N{...}` name, as opposed to a single
named character) is not recognised; this port reports "undefined character name" where upstream
raises `TypeError` when it tries to call `ord()` on the multi-codepoint result. Both engines reject
the pattern, so the only observable difference is in the error type and message.

```csharp
using Fuzzy.Text.RegularExpressions;

try
{
    _ = new FuzzyRegex(@"\N{KEYCAP DIGIT ZERO}");
}
catch (FuzzyRegexParseException ex)
{
    Console.WriteLine(ex.Message.Contains("undefined character name"));   // True
}
```

### `(?L)` works only when casing is not requested

Upstream reads the process's C locale only when `(?L)` needs casing. .NET has no equivalent of that
locale, so this port allows patterns that do not request casing and rejects those that do. A plain
`(?L)a` matches exactly as it does without the flag. Any case-insensitive or full-folding operation,
including a literal, set or backreference, is rejected when the pattern is constructed. The
exception is `NotSupportedException`; its message identifies `(?L)` and recommends `(?u)` or
`(?a)`.

```csharp
using Fuzzy.Text.RegularExpressions;

var literal = new FuzzyRegex("(?L)a");
Console.WriteLine(literal.FullMatch("a").Success);   // True
Console.WriteLine(literal.FullMatch("A").Success);   // False

try
{
    _ = new FuzzyRegex("(?Li)a");
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message.Contains("(?L)", StringComparison.Ordinal));   // True
}
```

### CPython's 4300-digit cap on `int(str)` is not ported

Upstream raises `ValueError` when a group reference like `\g<...>` is followed by more than 4300
digits, because CPython caps how many digits `int()` will parse. This port has no such cap, so the
same pattern compiles here. There is no compact example worth showing for a 4301-digit literal; the
behaviour is exactly "it compiles here and does not upstream".

### A group call that would re-enter the same group at the same text position fails that PATH

If a subroutine call like `(?&name)` would call back into the same group at the same position it
started from, only that one matching path fails - not the whole match - because a call that returns
to its own starting point cannot have consumed any text and so cannot ever succeed. Upstream has no
such guard and allocates memory until it runs out.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.FullMatch("abab", "(?P<g1>(?:ab)?(?&g1)?)");
Console.WriteLine((m.Index, m.Length));   // (0, 4)
```

### A fuzzy recursive pattern whose calls fail is matched in milliseconds, but some shapes stay exponential, and `MatchTimeout` is their bound

When a recursive call runs out of choices without ever returning, the engine remembers what the
call could see when it started - its position, its error budget and a little more - and fails any
later call that starts the same way at once. A fuzzy recursive pattern reaches the same call in
many ways, because the callers spent their errors differently, so this turns searches that took
minutes into ones that take milliseconds. Upstream raises `MemoryError` on the same patterns.

It does not cover a call that returns and whose caller then fails, so some patterns of that shape
still take time exponential in the length of the text. Give a pattern that mixes fuzzy sections
with recursion a `matchTimeout`: a match that runs out of time throws
`RegexMatchTimeoutException`, and never returns a wrong answer.

```csharp
using Fuzzy.Text.RegularExpressions;

var regex = new FuzzyRegex(
    "(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}",
    FuzzyRegexOptions.None,
    TimeSpan.FromSeconds(2));
Console.WriteLine(regex.Match("baxbax").Success);   // False - in milliseconds
```

### In a branch reset, a group never takes a number another group in the same branch will use

A branch-reset group `(?|...)` restarts the numbering at every `|`, so each branch hands out the
same numbers. A name reused across branches keeps the number it was given first. Upstream combines
those two rules by numbering each branch straight through, which lets an unnamed group take a
number that a name later in the same branch also owns. Both groups then write to one slot and the
later write wins, so the other group's text is left with no number of its own and `groups()` reports
`None` for a group that matched.

This port reserves those numbers before the branch is parsed, so no two groups in one branch share
a slot. The reservation is the maintainer's own option 3 from upstream issue 425, and it applies
whichever group comes first:

```csharp
using Fuzzy.Text.RegularExpressions;

// The named group first.
Match m = FuzzyRegex.FullMatch("BUG!", "(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))");
Console.WriteLine(m.Groups["bug"].Value);      // BUG - upstream answers "!"

// The unnamed group first: upstream gives ('BUG', None) and leaves "!" with no number.
Match n = FuzzyRegex.FullMatch("!BUG", "(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))");
Console.WriteLine(n.Groups[2].Value);          // ! - upstream reports no match for group 2
```

Upstream can still recover both texts through `m.captures(1)`, which lists every write to the
shared slot. What its numbering costs is knowing which group each capture came from. See
`docs/DIVERGENCES.md` for the full row, including the one ported upstream test that asserts
upstream's answer and is inverted here.

### Reversed partial matches run out of text at the slice start

Matched with `(?r)` (reversed) and `partial: true`, this port reports a partial match at
`beginning` whenever the pattern still needs more characters and `beginning` is where the
matchable text ends - treating a caller-imposed start bound the same way .NET's own `Regex` and
Boost.Regex treat it: as the true end of the text. Upstream instead answers a partial on some
pattern shapes there and no match at all on others, because two different code paths inside it
disagree about whether the bound behaves like the end of the string.

```csharp
using Fuzzy.Text.RegularExpressions;

var pattern = new FuzzyRegex("(?r)ya", FuzzyRegexOptions.None);
Match m = pattern.Match("xya", beginning: 2, length: 1, partial: true);
Console.WriteLine((m.Success, m.PartialMatch, m.Index));   // (True, True, 2) - upstream answers None
```

### A word or grapheme boundary decided at the end of the available text makes a partial match

With `partial: true`, a `\b`, `\B`, `\m`, `\M` or `\X` judged at the end of the text is judged on
text that may not all be there yet. This port says so and reports a partial match. Upstream reports
no match at all, because it reports a partial only when a node runs out of characters to read, and a
boundary reads none.

The case for saying so is the streaming caller the option exists for. Take "is this chunk something
other than a keyword", `(?!(True|False)\b)(.*)`, and ask it of the chunk `"True"` at the start of
that chunk. Upstream answers None, which tells the caller no further text can rescue this. A next
chunk of `"s"` does: the word is
then `Trues`, the lookahead's `\b` fails, and the pattern matches. PCRE2 takes this port's side and
names `\z`, `\Z`, `\b`, `\B` and `$` as the constructs that "always give a partial match"
([pcre2partial(3)](https://www.pcre.org/current/doc/html/pcre2partial.html), read 2026-09-21).

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = new FuzzyRegex(@"(?!(True|False)\b)(.*)").MatchAtStart("True", partial: true);
Console.WriteLine((m.Success, m.PartialMatch, m.Index, m.Length));  // (True, True, 0, 4)
                                                                    // upstream answers None
Match n = new FuzzyRegex(@"aa\B").FullMatch("aa", partial: true);
Console.WriteLine((n.Success, n.PartialMatch, n.Index, n.Length));  // (True, True, 0, 2)
                                                                    // upstream answers None
```

A complete match still wins, and so does a partial found the ordinary way, so this only ever replaces
an answer of "no match". `True\b` over `"True"` is a complete match in both engines, boundary or no
boundary. The partial spans the attempt that reached the end of the text and reports no capture
groups, since no path through the pattern finished. Under `(?r)` the end of the available text is its
start, and the rule reads the same way there.

### A partial match is refused where the match failed on text the engine already held

The other side of the rule above. A partial match says a longer subject could complete the match, so
this port reports one only where the attempt asked for a character the text did not have yet. Where
it failed on a character it already had, no further text can change the outcome and the answer is no
match. Upstream reports a partial on some of these.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = new FuzzyRegex(@"(\S??)\.").FullMatch(".a", partial: true);
Console.WriteLine(m.Success);   // False - upstream answers a partial over the whole of ".a"
```

Nothing appended to `".a"` can complete that match: the pattern matches at most two characters, and a
two-character match ends in a full stop. Upstream's own documentation agrees in principle - it
defines a partial as the answer to whether "a complete match could be possible if the string had not
been truncated" - and its answer one character later agrees in practice, since `".ab"` is no match
there too. PCRE2 refuses the partial on every one of these subjects.

### Compile budget

A counted repeat is compiled by writing out one copy of its body per repetition, so nested counted
repeats multiply: `((a{150}){150}){150}` is 3,375,000 copies. Upstream builds that graph until the
process runs out of memory. This port refuses it at construction instead, once compiling has created
more nodes than `maxCompiledNodes` allows - a million by default, about 250 MB. The timeout does not
cover this, because the cost is paid in the constructor before there is any subject to match.

```csharp
using Fuzzy.Text.RegularExpressions;

try
{
    _ = new FuzzyRegex("((a{150}){150}){150}");
}
catch (FuzzyRegexParseException e)
{
    // The rest of the message names the limit that was set and how to raise it.
    Console.WriteLine(e.Message.Split(',')[0]);   // compiling this pattern needs more than 1000000 nodes
}

// Or lower the budget, which is the point of it: compiling a pattern that arrived from outside
// the process under a ceiling you chose rather than the default quarter of a gigabyte.
string untrustedPattern = @"(\w+)\s*=\s*(\w+)";   // whatever arrived from outside
var strict = new FuzzyRegex(
    untrustedPattern,
    FuzzyRegexOptions.None,
    FuzzyRegex.InfiniteMatchTimeout,
    maxCompiledNodes: 50_000        // about 12 MB
);
```

Two details worth knowing. The budget counts the nodes compiling *creates* rather than the nodes
the finished pattern keeps: the optimiser prunes about half of a counted repeat's graph afterwards, and
the memory has already been spent by then, so a graph that is refused may be smaller than the budget
once finished. And there is no "unlimited" value, deliberately - `int.MaxValue` nodes is around
500 GB.

### A fuzzy section may open with an inserted character at the search anchor, where a position assertion pins the match there

Upstream refuses to let a fuzzy section begin with an inserted character at the exact position the
search started from, on the reasoning that starting the search one character later finds the same
match without paying for the insertion. That reasoning holds only when the match is free to slide.
Put a position assertion in front of the fuzzy section and it is not free: `^` in multiline mode
holds at the start of `"xabc"` and nowhere else in it, so there is no later start to try, and
upstream returns nothing.

```csharp
using Fuzzy.Text.RegularExpressions;

// One insertion, 'x', at the position the search began.
Match m = new FuzzyRegex("(?m)^(?:abc){i<=1}").Match("xabc");
Console.WriteLine(m.Success ? m.Value : "no match");   // xabc - upstream: no match
```

This port permits the insertion where a position assertion at the head of the pattern holds at the
anchor and fails one character on, which is exactly the case upstream's own reasoning does not
cover. Everywhere else the upstream rule stands, so `(?:abc){i<=1}` over `"xabc"` still matches
`abc` rather than `xabc` in both engines.

Upstream contradicts itself here, which is why this is a fix rather than a preference: the same
pattern over the same span answers two ways according to where the caller started. With
`\m(?:Y){i}\M`, upstream's `search(" XY", 0)` finds (1, 3) and `search(" XY", 1)` finds nothing.
Upstream issues 563 and 564 have tracked it since 17 April 2025, with the maintainer's own comment
"It looks like a bug". There is no option to restore the upstream answer.

### A fuzzy deletion that finishes a full-case-folded string or backreference costs one edit

Under `IgnoreCase` with version 1, a literal holding a pair that one character can fold to (fi, ff,
st, ss) is matched against each subject character's full folding, so `ﬁ` matches `fi` and `ß`
matches `ss`. Upstream's bookkeeping for that folding goes wrong when a fuzzy deletion is the last
step of the literal: it charges an extra edit for a subject character nothing was compared with, so
a one-deletion match costs two edits or is not found at all.

```csharp
using Fuzzy.Text.RegularExpressions;

// Delete the E and the T: two edits, inside the budget of two.
var phrase = new FuzzyRegex("(?:copper field studio){e<=2}", FuzzyRegexOptions.IgnoreCase);
Match m = phrase.Match("COPPER FILD SUDIO HARBOUR");
Console.WriteLine(m.Success ? m.Value : "no match");   // COPPER FILD SUDIO - upstream: no match
```

Upstream in version 0, which folds one character at a time and never builds these items, finds the
same match, and so does this port on all 300,000 searches of a benchmark corpus of short records.
A folding that was partly used is still charged, so `(?fi)(?:sst){e<=1}` over `ßﬆ` still needs its
one insertion. There is no option to restore the upstream answer; `(?V0)` gives simple folding,
where the two engines already agree.

### A full-folded backreference that ends half-way through a folding charges the rest as an edit

The same folding applies to a backreference. When the group's text runs out part way through a
subject character's folding, the rest of that folding is charged as an edit here, as upstream
already does for a literal. Upstream's backreference gives up on the match instead.

```csharp
using Fuzzy.Text.RegularExpressions;

// ß folds to ss. The group's s matches the first s; the second costs one substitution.
var twice = new FuzzyRegex(@"(s)(?:\1){e<=1}", FuzzyRegexOptions.IgnoreCase);
Console.WriteLine(twice.Match("sß").Value);   // sß - upstream: no match
```

Upstream in version 0, where `ß` is one character, finds the same match with one substitution, and
so does upstream's literal `(?:sss){e<=1}` over `ßß` in version 1. There is no option to restore
the upstream answer.

### A retried fuzzy edit in a full-folded backreference steps past what it used up

A fuzzy match tries one kind of edit and, if the rest of the pattern then fails, goes back and
tries the next. In a backreference under `IgnoreCase` with version 1, upstream's second try
compares a character the edit has already used, so it fails where it should succeed. No ligature
is involved.

```csharp
using Fuzzy.Text.RegularExpressions;

// A substitution of x for a is tried first and fails at the b; the retry inserts the x.
var repeated = new FuzzyRegex(@"(ab)(?:\1){e<=1}", FuzzyRegexOptions.IgnoreCase);
Console.WriteLine(repeated.Match("abxab").Value);   // abxab - upstream: no match
```

Upstream finds the same match without `IgnoreCase`, or in version 0. The fix also changes a
best-match answer: `(?b)(?fi)(ßa)(?:\1){s<=1,i<=1,d<=1}` over `ßasa` costs one deletion here and two
edits upstream, whose own literal form `(?:ßa)` finds the one deletion. There is no option to
restore the upstream answer.

### A fuzzy full-folded match can stop part-way into a folding

When a full-folded literal or backreference runs out part way through a subject character's
folding, the match here ends before that character, and each folded character it had already
matched there costs one deletion. Upstream's deletion in that place charges an edit and changes
nothing, so the match cannot end there. It finds a later match or none, and when deletions are free
it never stops.

```csharp
using Fuzzy.Text.RegularExpressions;

// ß folds to ss. The first ß gives two of the three s; the third is deleted.
var three = new FuzzyRegex("(?:sss){d<=1}", FuzzyRegexOptions.IgnoreCase);
Match m = three.Match("ßß");
Console.WriteLine(m.Index);   // 0 - upstream: 1
```

Upstream gives the match at 0 over `ß` alone, so a second character loses it. With free deletions,
`(?:sss){0d+1s+1i<=1:[x]}` over `ßß` is one deletion here and a `MemoryError` upstream. There is no
option to restore the upstream answer.

### A fuzzy section that is undone takes its error total with it

A fuzzy match keeps a running total of its errors beside the count of each kind. When upstream
gives up on a fuzzy section that went over its budget, it takes back the counts but not the total,
so a later match through another branch carries errors it never made. A best-match search then
never finds a match better than the last one, and finds the same match again for ever.

```csharp
using Fuzzy.Text.RegularExpressions;

// The first branch fails over budget; the second matches the 2 exactly.
var either = new FuzzyRegex(@"(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)");
Match m = either.Match("2y");
Console.WriteLine(m.Value);   // 2 - upstream: never returns
```

Under `(?e)` the same pattern gives `2` with no errors here, and `2y` with two substitutions
upstream. Upstream agrees with this port once the stale total cannot arise, for instance with the
inner `{s<=1}` removed. There is no option to restore the upstream answer.

### A fuzzy repeat takes an iteration that matches no text by deleting only when something needs it

Inside a repeat, a fuzzy pattern can match nothing at all by leaving out every character of the
body, at one deletion each. Upstream counts each such pass as progress, so a greedy repeat takes as
many of them as its budget allows before a character it cannot match, except at the end of the
text; and where a fuzzy group inside the repeat gets a fresh budget on every pass, it goes round
until it runs out of memory. Here such a pass is taken only when something needs it: the repeat's
minimum count, a minimum error count that its deletions raise, or a group that the pattern tests
later. Everything else is unchanged, including a pass that matches nothing without an error.

```csharp
using Fuzzy.Text.RegularExpressions;

// The digits match exactly; nothing needs the two passes that leave out a digit.
Match digits = FuzzyRegex.Match("42kg", "(?:[0-9]+){d<=2}");
Console.WriteLine(digits.FuzzyCounts.Deletions);   // 0 - upstream: 2 (and 0 over "42")

// "At least one deletion": one pass after 42 leaves out a digit.
Match needed = FuzzyRegex.Match("42", "(?:[0-9]+){1<=d<=2}");
Console.WriteLine((needed.Index, needed.Length, needed.FuzzyCounts.Deletions));   // (0, 2, 1) - upstream: (1, 1, 1)

// The + needs one pass, which leaves out the x; then y matches.
Match loop = FuzzyRegex.Match("y", "(?:(?:x){d<=1})+y");
Console.WriteLine(loop.FuzzyCounts.Deletions);   // 1 - upstream: MemoryError
```

The rule, the alternatives, what other engines do and the evidence are in
`docs/plan/2026-09-26-empty-iteration-survey.md`. There is no option to restore the upstream
behaviour. Ledger entries 33 and 44.

### A literal under a scoped `(?i:...)` is found in text that holds only its full case folding

Version 1 folds case fully under `(?i)`, so `ss` matches 'ß', and it does so whether `(?i)` covers
the whole pattern or only a group. Upstream first searches the subject for a string every match
must contain, and when the only `(?i)` is scoped it searches for that string with simple folding,
so it never finds 'ß' and reports no match. Its matcher alone gives the right answer: `(?i:ss)|q`,
which has no such string, matches. Perl agrees with this port.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(FuzzyRegex.Match("\u00DF", "(?i:ss)").Success);  // True - upstream: no match
```

### A lazy repeat finds a full-folded literal that starts at the repeat's last position

A lazy repeat such as `[^k]??` first tries to match nothing, then one character. Under `(?i)` in
version 1 the literal after it is matched with full folding, and upstream, looking ahead for that
literal, stops reading at the last position the repeat can reach. A literal that starts there is
never read to its end, so the match at the start of the text is lost and the search reports a later
one or none at all. Upstream finds the match in version 0, which uses simple case folding, and with
a greedy repeat. Python's `re` and Perl agree with this port.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("ass", "(?i)[^k]??ss");
Console.WriteLine(m.Index);                                           // 0 - upstream: 1
Console.WriteLine(FuzzyRegex.Match("aass", "(?i)a{0,2}?ss").Success);  // True - upstream: no match
```

### A fuzzy constraint that allows no errors limits the errors made inside it, whichever way it is written

`{e<=0}`, `{e<1}` and `{s<=0,i<=0,d<=0}` allow no errors, and so do `{d<=0}` and
`{1s+1i+1d<=0}`, because naming one kind of error rules out the others. Upstream's parser throws
the first three away and keeps the last two. On their own that makes no difference, but next to
another fuzzy section it does: a zero constraint around a section should stop that section making
errors, and one inside a section should keep the outer budget off its own text. Here every
spelling does both.

```csharp
using Fuzzy.Text.RegularExpressions;

// The inner (?:ab) must match exactly, so the outer budget cannot pay for the x.
var inner = new FuzzyRegex("(?:c(?:ab){e<=0}){e<=1}");
Console.WriteLine(inner.MatchAtStart("cax").Success);   // False - upstream: True
Console.WriteLine(inner.MatchAtStart("xab").Success);   // True - the x for the c is outside it
```

Upstream issue 306 asked for exactly this, and its pattern `(dogf(((oo){e<1})|((00){e<1}))d){e<2}`
still finds `dogfxod` upstream. A zero constraint with no other fuzzy section involved answers as
before and costs nothing, since it compiles to the same code as the pattern without it. There is
no option to restore the upstream answer; deleting the constraint from the pattern gives it.

### The WORD flag's word boundary is Unicode's default one

`(?w)` switches `\b`, `\B`, `\m` and `\M` to Unicode's default word boundaries, the rules of
[UAX #29](https://www.unicode.org/reports/tr29/). Unicode publishes a conformance file for them,
`WordBreakTest.txt`, and this port agrees with every one of its 1,944 lines for Unicode 17.0.0, the
version of its tables. Upstream disagrees with 268, for three reasons.

Combining marks and format characters, such as U+0308 COMBINING DIAERESIS or U+2060 WORD JOINER,
are meant to be invisible to the rules that look two characters away. Those rules keep "e.g." and
"3,456" in one word, so a mark after the punctuation should not split the word; upstream lets it.
A mark at the very start of the text should stand on its own; upstream joins it to what follows.
A single flag letter, a regional indicator, should join only another one. And upstream keeps an
apostrophe at the start of the text with a following vowel, a rule Unicode offers only as a
French and Italian adjustment that breaks there instead.

```csharp
using Fuzzy.Text.RegularExpressions;

// 'a', colon, U+0308, 'a' is one word: the mark does not separate the colon from the second 'a'.
var boundary = new FuzzyRegex(@"(?w)\b");
Console.WriteLine(string.Join(" ", boundary.Matches("a:\u0308a").Select(static m => m.Index)));  // 0 4 - upstream: 0 1 4
Console.WriteLine(new FuzzyRegex(@"(?w)a\b").Match("a:\u0308a").Index);   // 3 - upstream: 0
```

Without `(?w)`, `\b` is the simple boundary between a word character and anything else, and
nothing changes there.

### `\X` matches the same grapheme cluster backwards as forwards

`\X` matches one user-perceived character, a grapheme cluster in the sense of
[UAX #29](https://www.unicode.org/reports/tr29/): 'e' followed by U+0301 COMBINING ACUTE ACCENT is
one cluster, and so is CR LF. Where a cluster starts and ends is a property of the text, so a
reversed search, `(?r)`, should find the same clusters in the opposite order. Here it does, on every
line of Unicode's conformance file for Unicode 17.0.0. Upstream's reversed `\X` stops after one
codepoint, so it splits the accent from its letter and loses the letter, and a lookbehind that
holds `\X`, which also reads backwards, sees only part of a cluster.

```csharp
using Fuzzy.Text.RegularExpressions;

// 'e' with its accent is one cluster in either direction.
var backwards = new FuzzyRegex(@"(?r)\X");
Console.WriteLine(string.Join(" ", backwards.Matches("e\u0301a").Select(static m => m.Length)));  // 1 2 - upstream: 1 1
Console.WriteLine(new FuzzyRegex(@"(?<=^\X)b").Match("\r\nb").Index);                              // 2 - upstream: no match
```

Forward matching is unchanged: upstream's forward `\X` already agrees with the whole file.

### A `(*SKIP)` acts when backtracking reaches it, so a later `(*PRUNE)` decides the next start

`(*SKIP)` and `(*PRUNE)` do nothing when the matcher passes them. They act only if the match then
fails and backtracking comes back to them: `(*PRUNE)` ends the attempt, so the next one starts one
character on, and `(*SKIP)` ends it too, but the next attempt starts where the verb was reached.
When a path passes both, backtracking reaches the later one first, and that one decides. This is
how PCRE2 and Perl define the verbs. Upstream moves the start of the next attempt the moment
`(*SKIP)` runs, so a `(*PRUNE)` after it cannot take that back, and a `(*SKIP)` inside an atomic
group that has finished still acts.

```csharp
using Fuzzy.Text.RegularExpressions;

// At 0, 'aa' passes (*SKIP) at 2 and 'x' passes (*PRUNE); 'y' fails, so the (*PRUNE) acts and
// the next attempt starts at 1, where the second branch matches.
Console.WriteLine(new FuzzyRegex(@"aa(*SKIP)x(*PRUNE)y|a").Match("aaxz").Index);   // 1 - upstream: no match
Console.WriteLine(new FuzzyRegex(@"(?>aa(*SKIP))x").Match("aaax").Index);          // 1 - upstream: no match
```

A `(*SKIP)` that backtracking does reach behaves as before: `aa(*SKIP)x|a` over 'aab' finds
nothing in either library, because the attempt after the one at 0 starts at 2.

### A verb, branch, group call or fuzzy section that starts every alternative stays in each one

When every alternative of a branch starts with the same item, the compiler may move that item out
in front: `xab|xac` becomes `x(?:ab|ac)`, and `x` is tested once. That is safe for `x`, which
matches in one way or not at all. It is not safe for an item that can match in more than one way,
or that does something when the match backtracks through it. `(*SKIP)` and `(*PRUNE)` end the whole
attempt at this start position when backtracked into, so inside the first alternative they stop the
second from being tried; moved in front of the branch they are reached only after both alternatives
have failed. A nested branch, a group call such as `(?1)`, and a fuzzy section can each match more
than one way, and moving one out changes which way is tried first. Here those four kinds stay where
the pattern put them, and the answers agree with PCRE2 and Perl. Upstream moves them, so its answer
depends on whether two alternatives happen to start with the same verb.

```csharp
using Fuzzy.Text.RegularExpressions;

// Backtracking into (*SKIP) ends the attempt, so the \b alternative is never tried at 0 or 1.
var m = new FuzzyRegex(@"(*SKIP)[ab]+|(*SKIP)\b").Match("ccb");
Console.WriteLine($"({m.Index}, {m.Index + m.Length})");  // (2, 3) - upstream: (0, 0)
```

Reversed, `(?r)`, the same holds for an item that ends every alternative. Items that match in one
way, such as literals, sets, anchors, `\K`, lookarounds and atomic groups, are still moved out.

### A verb that backtracking reaches inside an unfinished atomic group or positive lookaround ends the attempt

An atomic group `(?>...)`, a possessive repeat such as `a*+`, and a positive lookaround promise that
once they have matched, nothing backtracks into them. Before they have matched, that promise has not
taken effect. So if a failure inside one backtracks onto a `(*PRUNE)` or `(*SKIP)` in it, the verb
does what it always does and ends the whole attempt at this start position. This is PCRE2's rule,
and Perl agrees in these cases. Upstream instead fails only the group, and matching carries on
outside it. A negative lookaround or a conditional test still stops the verb, as in both libraries:
there the failure is itself an answer (the negative lookaround becomes true, a positive condition
false).

```csharp
using Fuzzy.Text.RegularExpressions;

// At 0, 'a' passes (*PRUNE) and 'b' fails against 'c', so the attempt at 0 ends; the second
// branch is never tried there, and nothing matches at 1.
Console.WriteLine(new FuzzyRegex(@"(?>a(*PRUNE)b)|a").Match("ac").Success);    // False - upstream: True, (0, 1)
Console.WriteLine(new FuzzyRegex(@"(?>aa(*SKIP)b)|a").Match("aaca").Index);    // 3 - upstream: 0
Console.WriteLine(new FuzzyRegex(@"(?!(?>a(*PRUNE)b)|a)a").Match("ac").Index); // 0 - upstream: no match
```

A group called with `(?1)`, `(?&name)` or `(?R)` does not stop the verb, as in upstream, Perl and
Boost; PCRE2 alone makes the call fail instead.

### A fuzzy item that matched exactly can still be deleted, and that choice comes before any earlier one

A deletion leaves a pattern character out of the match. Upstream tries that only for a character
that failed to match: when a fuzzy character matches and something after it then fails, leaving it
out is never tried, so a match within the budget is missed. Here the choice is tried as soon as
everything after the exact match has failed, before going back to any earlier choice, which is the
order upstream's own README gives for `(?:cats|cat){e<=1}`.

```csharp
using Fuzzy.Text.RegularExpressions;

// Leaving out the fuzzy a is one deletion, within d<=1.
Match m = FuzzyRegex.MatchAtStart("a", "(?:a){d<=1}a");
Console.WriteLine((m.Index, m.Length, m.FuzzyCounts.Deletions));   // (0, 1, 1) - upstream: no match

// At 0 the match leaves out the b that matched; upstream skips it and answers (3, 3), the exact abb.
Match first = FuzzyRegex.Match("abxabb", "(?:ab){d<=1}b");
Console.WriteLine((first.Index, first.Length));   // (0, 2) - upstream: (3, 3)

// The first branch, with its a left out, comes before the second branch.
Match branch = FuzzyRegex.Match("ab", "(?:(?:a){d<=1}ab|a)");
Console.WriteLine((branch.Index, branch.Length));   // (0, 2) - upstream: (0, 1)
```

Upstream finds such a match when the character happens to fail first: `(?:a|b){d<=1}a` matches
`a`, because its `b` branch fails and tries the deletion. There is no option to restore the
upstream answer. Ledger entry 42.

### A `\G` inside a fuzzy section is answered where upstream raises an error

`\G` holds only where the search started. An error cannot make it true, so inside a fuzzy section
it means what it means outside one. When a `\G` fails there, the matcher may try an insertion after
it, and if that insertion later has to be undone, upstream stops with `RuntimeError: invalid RE
code` instead of answering. This port answers.

```csharp
using Fuzzy.Text.RegularExpressions;

// The first branch fails its \G at 1; the second holds it at 0 and inserts 'a' before 'b'.
Console.WriteLine(new FuzzyRegex(@"(?:a\G|\Gb){i<=1}").MatchAtStart("ab").Length); // 2 - upstream: RuntimeError
Console.WriteLine(new FuzzyRegex(@"(?:a\G){i<=1}").MatchAtStart("ab").Success);    // False - upstream: RuntimeError
```

### A fuzzy run can edit `ß` or a ligature as one character

Under full case folding `ß` matches `ss`, and a fuzzy section can also treat it as the one character
it is: substitute it with one other character, or delete it. Upstream allows that only when the `ß`
stands alone. Written next to other letters, it becomes its two-letter folding, and replacing it
costs two edits. This port treats both the same way.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(new FuzzyRegex(@"(?fi)(?:ß){s<=1}x").Match("ax").Length); // 2 - upstream: 2
Console.WriteLine(new FuzzyRegex(@"(?fi)(?:ßx){s<=1}").Match("ax").Length); // 2 - upstream: no match
Console.WriteLine(new FuzzyRegex(@"(?fi)(?:ßx){d<=1}").Match("x").Length);  // 1 - upstream: no match
```

### A lookaround that fails inside a fuzzy section can be passed by inserting a text character in front of it

An insertion is a text character the pattern does not account for. Upstream lets one stand in front
of a failing `\b` or `$`, which moves the assertion one character on, but never in front of a
failing lookaround. Here a lookaround is treated like every other zero-width assertion: when it
fails, one inserted character is tried in front of it and the lookaround is tried again.

```csharp
using Fuzzy.Text.RegularExpressions;

// The inserted x puts the lookahead in front of the c.
Match m = FuzzyRegex.Match("bxc", "(?:b(?=c)){i<=1}");
Console.WriteLine((m.Index, m.Length, m.FuzzyCounts.Insertions));   // (0, 2, 1) - upstream: no match

// With any error allowed, the insertion at 0 comes before upstream's substitution at 1.
Match e = FuzzyRegex.Match("bxc", "(?:b(?=c)){e<=1}");
Console.WriteLine((e.Index, e.Length));   // (0, 2) - upstream: (1, 1)
```

Negative lookarounds and lookbehinds work the same way. A lookaround can still be neither
substituted nor deleted, since it matches no character. There is no option to restore the upstream
answer. Ledger entry 50.

### A fuzzy section's minimum error count can be met by a text character inserted after its last item

A constraint such as `{1<=e<=2}` asks for at least one error. When the section's text matches
exactly, upstream fails it at the section's end without trying the one error still open to it: a
text character inserted after the last item. It finds that insertion after a string of two or more
characters, but not after a single character or a class. Here the minimum is checked after the
trailing insertions, so both find it.

```csharp
using Fuzzy.Text.RegularExpressions;

// The second 'a' is the inserted character that meets the minimum.
Match m = FuzzyRegex.MatchAtStart("aab", "(?:a){1<=e<=2}b");
Console.WriteLine((m.Index, m.Length, m.FuzzyCounts.Insertions));   // (0, 3, 1) - upstream: no match

// A search now finds that match at 0, before upstream's deletion at 2.
Match s = FuzzyRegex.Match("aab", "(?:a){1<=e<=2}b");
Console.WriteLine((s.Index, s.Length));   // (0, 3) - upstream: (2, 1)
```

A minimum on substitutions or deletions alone cannot be met this way, since an insertion is
neither. There is no option to restore the upstream answer. Ledger entry 51.

### A fuzzy run can edit an expanding subject character as one character

The same holds in the subject, the text being searched. `ǰ` folds to two characters, `j` and a
combining caron, and upstream edits a folding one folded character at a time, so replacing a letter
of a run with `ǰ` costs two edits, although it costs one where the fuzzy section covers only that
letter. This port also lets the run substitute or insert the whole subject character.

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(new FuzzyRegex(@"(?fi)(?:s){s<=1}sx").Match("ǰsx").Length);  // 3 - upstream: 3
Console.WriteLine(new FuzzyRegex(@"(?fi)(?:ssx){s<=1}").Match("ǰsx").Length);  // 3 - upstream: no match
Console.WriteLine(new FuzzyRegex(@"(?fi)(?:fst){i<=1}").FullMatch("fßst").Length); // 4 - upstream: no match
```

A fuzzy backreference edits its subject the same way. Upstream edits one fine, for example
substituting `a` for a letter of the group, but not `ǰ`:

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(new FuzzyRegex(@"(?fi)(ss)x(?:\1){s<=1}").FullMatch("ssxas").Length); // 5 - upstream: 5
Console.WriteLine(new FuzzyRegex(@"(?fi)(ss)x(?:\1){s<=1}").FullMatch("ssxǰs").Length); // 5 - upstream: no match
```

So does a captured character that expands. Upstream edits the literal `ß` as one character, but
the same `ß` captured by the group costs two edits:

```csharp
using Fuzzy.Text.RegularExpressions;

Console.WriteLine(new FuzzyRegex(@"(?fi)(ß)x(?:ß){s<=1}").FullMatch("ßxa").Length);  // 3 - upstream: 3
Console.WriteLine(new FuzzyRegex(@"(?fi)(ß)x(?:\1){s<=1}").FullMatch("ßxa").Length); // 3 - upstream: no match
```

### A group call inside a lookaround leaves no capture behind when the lookaround's body is thrown away

A group call such as `(?1)` adds to the called group's capture list, as the group itself does.
Upstream saves and restores the capture lists around a lookaround only when its body holds a
capture group, so when the body only calls one, the call's capture outlives a negative lookaround
whose body matched, or a positive one the match backtracked past. Here a call counts as a group, so
the lists are restored, as they are for a group written directly.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = FuzzyRegex.Match("aaab", "(a)(?:(?!.(?1))|.)+?b");
Console.WriteLine(m.Groups[1].Captures.Count);   // 1 - upstream: 2
Console.WriteLine(m.Groups[1].Index);   // 0 - upstream: 0
```

Spans, group values and fuzzy counts are unchanged; only capture lists differ. There is no option
to restore the upstream answer. Ledger entry 54.

### A call to the whole pattern inside a pattern that is one fuzzy section returns to its caller

When the whole pattern is one fuzzy section, such as `(?:z(?R)|){e<=1}`, each `(?R)` enters the
section again. Here that inner instance nests like any inner section: its errors are added to the
instance that called it, so the one `{e<=1}` bounds the whole match, however deep it recurses.
Upstream never returns from such a call. Most such patterns run out of memory there, and the rest
can report a match with more errors than the limit allows.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = new FuzzyRegex("(?:z(?R)|){e<=1}").Match("bz");
Console.WriteLine(m.FuzzyCounts.Substitutions);   // 1 - upstream: MemoryError
Console.WriteLine(new FuzzyRegex("(?:b||b(?0)*){e<=2}").FullMatch("azx").Success);   // False - upstream: True, with 3 errors
Console.WriteLine(new FuzzyRegex("(?:|z?(?R)?a){e<=3}").FullMatch("aaba").FuzzyCounts.Total);   // 3 - upstream: 4
```

There is no option to restore the upstream answer. Ledger entry 55.

### A verb that cuts through a fuzzy section closes it

A `(*PRUNE)` or `(*SKIP)` inside a negative lookaround or a condition's test can end that construct
with a fuzzy section opened inside it still unfinished. Here the section closes with the construct,
so the enclosing section's limits apply to what follows. Upstream keeps the inner section's limits in
force, so the match below moves one character on.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = new FuzzyRegex("(?:(?!(?:a(*PRUNE)b){d<=0})cd){e<=2}").Match("ad");
Console.WriteLine(m.Index);   // 0 - upstream: 1
```

There is no option to restore the upstream answer. Ledger entry 61.

### BESTMATCH counts only the errors a match contains

A negative lookaround or condition whose body matched fuzzily is thrown away, and so are its errors.
Upstream keeps them in the total that BESTMATCH and ENHANCEMATCH rank by, so an exact match can lose
to one with an error.

```csharp
using Fuzzy.Text.RegularExpressions;

Match m = new FuzzyRegex("(?b)(?:(?(?!(?:a){e<=1})c|d)c|(?:bc){e<=1})").Match("cdcx");
Console.WriteLine(m.Index);   // 1 - upstream: 0, with a deletion
Console.WriteLine(m.FuzzyCounts.Total);   // 0
```

There is no option to restore the upstream answer. Ledger entry 32.

### `BestMatch` keeps a fit that ends in trailing insertions

`(?b)` asks for the best match among those the constraints allow. It is not meant to remove any.
Upstream can lose a match whose best fit ends in inserted characters, because the check that
allows one more trailing insertion counts the errors made so far twice. The match is lost
wherever the fit has to reach the end of the text: `FullMatch`, or a pattern ending in `$`. This
port counts each error once, so it answers what upstream answers without the flag.

```csharp
using Fuzzy.Text.RegularExpressions;

// 'b' matches the first character and the other two are insertions.
Console.WriteLine(new FuzzyRegex(@"(?b)(?:b){e<=2}").FullMatch("bba").Length);      // 3 - upstream: no match
Console.WriteLine(new FuzzyRegex(@"(?b)(?:b){e<=2}$").MatchAtStart("bba").Length);  // 3 - upstream: no match
Console.WriteLine(new FuzzyRegex(@"(?:b){e<=2}").FullMatch("bba").Length);          // 3 - upstream: 3
```

### Inherited upstream bugs are fixed here

Several bugs that exist in upstream's own C engine are fixed in this port rather than reproduced,
each tracked against a ledger entry. One clear example: a pattern whose backtracking would run away
indefinitely raises `InvalidOperationException` (the 1 GB backtracking bound) here, where upstream
keeps allocating memory until it raises `MemoryError` or, on some inputs, hangs. See
`docs/plan/upstream-reports/LEDGER.md` for the rest - there are more than half a dozen, and this
document does not enumerate all of them. There is no short example: reaching the bound takes
seconds and gigabytes by design, and the timeout example above is the practical guard.

