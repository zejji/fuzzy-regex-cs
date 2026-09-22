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
| `Regex.IsMatch(input)` | `FuzzyRegex.IsMatch(input)` | Same shape; also a `ReadOnlySpan<char>` overload. |
| `Regex.Match(input)` | `FuzzyRegex.Match(input)` | Same meaning (search anywhere) - see "**`Match` means upstream's `search`...**" below for why this needed no rename. |
| `regex.Match(input).Success` / `.Value` / `.Index` / `.Length` | `match.Success` / `.Value` / `.Index` / `.Length` | Same names, inherited from `Group`/`Capture` rather than declared on `Match`. No `Match.Span`; neither engine has one. |
| `Regex.Matches(input)` | `FuzzyRegex.Matches(input)` | The built-in's `MatchCollection` is lazy (finds the next match as it is enumerated); this port's is eager - see "**A lazy walk times each STEP, where `Matches` times the whole scan.**" below. `EnumerateMatches` is the lazy one here. |
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

Reads as a range on the total error count, so an exact match is rejected as firmly as one with too
many errors.

```csharp
using Fuzzy.Text.RegularExpressions;

Match fuzzy = FuzzyRegex.MatchAtStart("servic detection", "(?:service detection){0<e<5}");
Console.WriteLine((fuzzy.Index, fuzzy.Length));   // (0, 16)

Match exact = FuzzyRegex.MatchAtStart("service detection", "(?:service detection){0<e<5}");
Console.WriteLine(exact.Success);   // False
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
var pattern = new FuzzyRegex(@"(a|a)*\b\B");
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

### The search prefilters answer the slow path's answer, not upstream's

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

### Inherited upstream bugs are fixed here

Several bugs that exist in upstream's own C engine are fixed in this port rather than reproduced,
each tracked against a ledger entry. One clear example: a pattern whose backtracking would run away
indefinitely raises `InvalidOperationException` (the 1 GB backtracking bound) here, where upstream
keeps allocating memory until it raises `MemoryError` or, on some inputs, hangs. See
`docs/plan/upstream-reports/LEDGER.md` for the rest - there are more than half a dozen, and this
document does not enumerate all of them. There is no short example: reaching the bound takes
seconds and gigabytes by design, and the timeout example above is the practical guard.

