# Review of "Upstream members with no port equivalent" before Phase 7

Owner request, 2026-09-16: a final Fable review of every row in that section of `docs/DIVERGENCES.md`,
asking (i) is the API-design decision right, (ii) does the absence hide any correctness gap, and
(iii) what effect does each absence have on optimisation, performance, efficiency and usability.
Evidence gathered the same day: the port's public surface (`src/FuzzyRegex/FuzzyRegex.cs`,
`Match.cs`, `MatchCollections.cs`, `FuzzyRegexOptions.cs`, `Engine/Iteration.cs`), PORTMAP's
deliberately-not-ported table, DECISIONS (owner decision D, 2026-08-30), upstream's `README.rst`,
and Microsoft Learn for `Regex.EnumerateMatches` (.NET 7+, "amortized allocation free", one match
per `MoveNext`), `Regex.EnumerateSplits` (.NET 9+, lazy), the static-method cache ("the last 15 most
recently used static regular expression patterns are cached", `Regex.CacheSize`) and
`GroupCollection` (implements `IReadOnlyDictionary<string, Group>` since .NET 5).

## Verdict

Seventeen of the twenty-one rows are correct and stay as they are. Four rows are correct as records
of what shipped but the decision behind them should change before Phase 7, because each is an
additive API change that is free before 1.0 and awkward after, and two of the four have a direct
performance effect that Phase 7 would otherwise optimise around. One row from the neighbouring
API-shape table (`Replace` without `beginning`/`length`) is in the same position.

No row hides a correctness defect. Every absent member is either a Python-runtime concern, an alias,
a reshaping onto an existing member, or a convenience whose information is reachable another way.

## Rows that stand (no change)

| Row | Why it is right |
|---|---|
| `prefixmatch` | Exact alias of `match`; a second name for `MatchAtStart` is surface without behaviour. |
| `__copy__`, `__deepcopy__`, `__sizeof__` (Pattern and Match) | Python object protocol. Patterns and matches are immutable here and safe to share across threads (S52b), which is what copying protects against in Python. |
| `Pattern.groups` (count), `groupindex` (dict) | `GroupNumbers.Count`, `GroupNames`, `GroupNumberFromName`, `GroupNameFromNumber` carry the same facts. See item 4 below for the dictionary idiom on `GroupCollection`, which is the .NET place for it. |
| `Match.pos`, `endpos`, `string`, `re` | `System.Text.RegularExpressions.Match` exposes none of them either; four reads in the upstream suite, all in `test_getattr`. `Match` does hold the subject and the pattern privately, so adding them later costs nothing if a user asks. |
| `Match.regs`, `starts`, `ends`, `spans`, `allcaptures`, `allspans`, `groupdict`, `capturesdict` | All reachable through `Groups[n]`, `Group.Captures`, `(Index, Length)`. Owner decision D on the Python names stands. |
| `Match.detach_string` | GC hint for CPython; no .NET meaning. |
| `regex.template`, `TEMPLATE`/`T` | Upstream implements no behaviour for it. |
| `LOCALE`, `DEBUG` | Locale matching is not offered (own Behaviour row); `DEBUG` prints a parse tree. |
| `compile(ignore_unused=)`, `compile(cache_pattern=)` | Named lists are constructor arguments here, so there is no unused-keyword path to relax; no cache to opt out of (see item 2). |
| `regex.DEFAULT_VERSION` settable at runtime | A process-global mutable default is exactly the kind of state S52b proved absent. A compile-time default plus `Version0`/`Version1` options is the .NET shape. |
| `Pattern.scanner`, `regex.Scanner` | A stateful lexer object with no `Regex` counterpart. Its one real use, tokenising from a position, is `MatchAtStart(input, beginning)` in a loop. Eleven upstream lines. |
| `findall` | The group-text result shape is a Python convenience; `Matches` plus a projection is the .NET idiom, and `Count` avoids materialising anything when only the number is wanted. Documented as a trap for Phase 8. Stands, subject to item 1 making `Matches` cheap to project lazily. |

## Rows whose decision should change

### 1. `splititer`, and `finditer`'s laziness: `Matches` is eager and there is no lazy entry point

`FuzzyRegex.Matches` runs the whole scan into a `List<Match>` before returning
(`Engine/Iteration.FindAll`, `MatchCollections.cs:110-118` records this as a deliberate corner cut).
The DIVERGENCES justification for dropping `splititer`, "a caller who wants laziness can stream the
array", is not true of laziness: the array is complete before streaming begins, so a caller wanting
the first two matches of a large subject pays for all of them in time and allocation. The built-in
`Regex.Matches` is lazy, and .NET 7 and 9 added `EnumerateMatches` and `EnumerateSplits` precisely
because lazy, allocation-light scanning is what performance-sensitive .NET callers now expect.

The comment's stated reason for not making it lazy, that a half-enumerated iterator would leak the
engine's rented buffers, does not hold for the design the port already has: `Match.NextMatch()`
creates and disposes a fresh `MatchState` per step (`Iteration.Next`, `using var state`). An
`IEnumerable<Match> EnumerateMatches(...)` written over `NextMatch` leaks nothing and needs no
engine change. Phase 7 can later make the enumerator reuse one state; that is easier to do behind an
API that exists than to add alongside benchmarks that measured the eager one.

Effect on optimisation: S54 pins a 1 MB-subject `Matches` workload as a baseline. If the shape users
should call is a lazy enumerator, baseline it now, or Phase 7 optimises the wrong entry point.

Recommendation: add `EnumerateMatches` (returning `IEnumerable<Match>`) and `EnumerateSplits`
(returning `IEnumerable<string?>`, the same nulls as `Split`) with the same `beginning`/`length`/
`overlapped`/`partial`/`timeout`/`cancellationToken` parameters, before S54 runs. Keep `Matches`
eager (its `Count` promise is honest). Rewrite the `splititer` row: port equivalent
`EnumerateSplits`, status SHIPPED once done.

### 2. `purge`, `cache_all`: the port's static conveniences compile on every call

`FuzzyRegex.IsMatch(input, pattern, ...)` and its five siblings each do
`new FuzzyRegex(pattern, options)` (six sites in `FuzzyRegex.cs`). Both reference implementations
cache here: upstream's module cache is what `purge`/`cache_all` control, and .NET keeps the 15 most
recent static patterns behind `Regex.CacheSize`. A .NET user who writes the static form in a loop,
as they do with `Regex`, gets a full compile per iteration and no signal that anything is wrong.

The roadmap already says caches appear in Phase 7 (ROADMAP:411, with the AOT constraint). The API
decision belongs before that: mirror .NET with a static `CacheSize` property (default 15, 0 to
disable), which is the .NET equivalent of `purge`/`cache_all` and answers this row. Thread safety of
the cache falls under S52b's contract and needs its own concurrent test.

Recommendation: no code now; author it as the first Phase 7 slice, and change this row's status to
PLANNED (Phase 7) with `CacheSize` as the port equivalent, so Phase 8 does not document "no cache".

### 3. `ASCII`, `UNICODE`, `WORD` are inline-only

Decision D (Phase 2) declined to add them to `FuzzyRegexOptions` because every ported test could use
`(?a)`, `(?u)`, `(?w)`. That was a test-porting discipline, not an API judgement, and the API now has
three concrete costs:

- A caller cannot apply `WORD` (Unicode default word boundaries for `\b`/`\B`, and the wider
  line-separator set for `.`, `^`, `$`, upstream README:723 and :1011) to a pattern they did not
  write without concatenating `"(?w)"` onto a user-supplied string. Every other upstream flag with a
  meaning here is an option.
- `FuzzyRegex.Options` strips them: `_unexposedFlags` is "every bit not in the enum"
  (`FuzzyRegex.cs:71`, `:221`), so a pattern compiled as `(?w)\bx\b` reports `Options` without
  `Word`. A reporting inaccuracy today, and it becomes a cache-key bug in Phase 7 if a cache keys on
  `(pattern, Options)` rather than on the raw flags.
- .NET convention puts flags in the options enum (`RegexOptions.ECMAScript`, `CultureInvariant`).

`LOCALE` and `DEBUG` stay out; they have no behaviour here.

Recommendation: add `Ascii`, `Unicode`, `Word` with upstream's bit values. Risk is low and
verifiable: the parser already handles the bits, and the compile-parity corpus can prove
`flags=regex.WORD` and a leading `(?w)` produce identical bytecode, as the 28 `regex.A`/`regex.U`
sites were proven in Phase 2. Also stop stripping them from `Options`.

### 4. `groupindex`, `groupdict`, `capturesdict`: the .NET idiom is the dictionary interface on `GroupCollection`

Since .NET 5, `System.Text.RegularExpressions.GroupCollection` implements
`IReadOnlyDictionary<string, Group>` with `Keys`, `Values`, `TryGetValue` and `ContainsKey`. The
port's `GroupCollection` implements only `IReadOnlyList<Group>` plus a string indexer. Decision D
was about not adding Python names, and it stands; this is about matching the .NET type the surface
is shaped after. Cheap, additive, and it gives Phase 8 a one-line answer to "where is groupdict".

Recommendation: implement `IReadOnlyDictionary<string, Group>` on `GroupCollection` over the
existing name table.

### Also: `Replace`/`ReplaceFormat` take no `beginning`/`length`

Recorded in the API-shape table as "an unclosed gap", not a decision. Upstream's `sub`, `subf`,
`subn`, `subfn` all take `pos`/`endpos`. Adding optional parameters to a public method later is a
binary-breaking change for compiled callers; before 1.0 it is free, and the oracle already records
`sub` rows. Recommendation: close it in the same slice as items 1, 3 and 4.

## Proposed packaging

One slice before S54, "API completeness before optimisation" (working name S53b): items 1, 3, 4
and the `Replace` slicing, each test-first against the oracle or the compile-parity corpus, with
the DIVERGENCES rows rewritten in the same commit. About one sitting. Item 2 becomes the first
Phase 7 slice and a PLANNED row now.

Effect if none of this is done: the port stays correct. It would ship with an eager-only scan that
.NET users will benchmark against `EnumerateMatches` and lose, static helpers that recompile per
call, and two flags reachable only by string concatenation. All four are cheaper now than after
Phase 7 has measured and tuned around them.
