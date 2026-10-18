<!--
  Draft of the GitHub release text for v1.0.0, for the owner's review. Generated from
  docs/DIVERGENCES.md (every SHIPPED row) and docs/plan/upstream-reports/LEDGER.md, as
  docs/plan/RELEASE.md step 3 requires, plus the S63 gate record, docs/KNOWN-DEFECTS.md and
  DECISIONS.md. Paste everything below the second rule into the release form.

  The divergence links point at line numbers of DIVERGENCES.md in the v1.0.0 tag. If that file
  changes before the tag is made, re-check every link (the row lines were 25-31, 37-51, 57-135
  when this was drafted on main ff86226e).
-->

---

# FuzzyRegex 1.0.0

FuzzyRegex is a .NET port of [mrab-regex](https://github.com/mrabarnett/mrab-regex), the Python
`regex` module. It is a full regular expression engine whose headline feature is fuzzy matching: a
pattern carries its own error budget, so `(?:hello){e<=1}` matches `hallo` and reports one
substitution. The main type is `FuzzyRegex` in the `Fuzzy.Text.RegularExpressions` namespace,
shaped after `System.Text.RegularExpressions.Regex`, so most of what a .NET developer already knows
carries over.

```console
dotnet add package FuzzyRegex
```

Try it without installing anything: [the FuzzyRegex demo](https://zejji.github.io/fuzzy-regex-cs/)
runs the engine in your browser.

## What 1.0 supports

- **Everything upstream's parser accepts**, tracking mrab-regex 2026.9.10 (commit `7dd71c1`). All
  1,967 ported upstream tests pass, with none skipped (`docs/STATUS.md`).
  <!-- TODO(orchestrator): if S81a (upstream issue 619, the 2026.9.19 fix) lands before the tag,
  change the version and commit above to 2026.9.19 and say so here. -->
- **Fuzzy matching**: per-kind budgets (`{i<=1,d<=2,s<=1}`), weighted cost equations
  (`{2i+1d+1s<4}`), the `{...:test}` form that limits which characters an error may fall on, error
  counts and change positions on every match, and the two ranking modes `EnhanceMatch` (`(?e)`) and
  `BestMatch` (`(?b)`).
- **Every non-fuzzy construct upstream has**: lookahead and variable-length lookbehind, recursion
  and group calls (`(?R)`, `(?&name)`, `(?(DEFINE)...)`), conditionals, branch reset, named lists
  (`\L<name>`), possessive quantifiers, `\K`, the `(*PRUNE)`, `(*SKIP)` and `(*FAIL)` verbs,
  reverse matching (`(?r)`), POSIX leftmost-longest matching (`(?p)`) and partial matching.
- **Version 1 by default**: nested sets with set operations (`[[a-z]--[aeiou]]`) and full Unicode
  case folding under `IgnoreCase`, so `ß` matches `SS`. Version 0 is one flag away.
- **Unicode 17.0.0** data, including the word and grapheme boundary rules, checked against
  Unicode's own conformance files.
- **A .NET-shaped API**: `Match`, `MatchAtStart`, `FullMatch`, `Matches` (lazy),
  `EnumerateMatches` (with a `ReadOnlySpan<char>` overload that builds no `Match` objects),
  `Replace`, `Split` and `Count`; a per-call `timeout` and a `CancellationToken` on every method
  that reads the subject; a compiled pattern and its `Match` results that are safe to share across
  threads; Native AOT compatible.
- **Target**: `net10.0`.

The [guide](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/GUIDE.md) is written for a
reader who knows `System.Text.RegularExpressions` and has never used Python's `regex`, and
[COMPARISON.md](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/COMPARISON.md) maps every
upstream and `Regex` member to its counterpart here.

## What a partial match promises

Pass `partial: true` and a match that is still open when the subject runs out comes back with
`PartialMatch` set. This is for text that arrives in pieces: a partial tells you to keep the text
from its start and read more.

**The promise is "more text may complete this match".** It is a possibility only. `ba(*FAIL)` over
`b` is a partial, because the match still wanted an `a` when the text ended, although `(*FAIL)` then
fails whatever comes next. The engine judges each test at the end of the text on its own, so two
tests that no single next character can satisfy together, such as `a\b\B` over `a`, also give a
partial. PCRE2, Java's `hitEnd()`, Boost and upstream make the same promise.

**The promise holds firmly the other way.** Where some longer text does complete a match, a partial
call never answers "no match". A caller that stops reading at the first "no match" never misses one.

**For one class of pattern the partial start is a bound in one direction.** The class is a pattern
with a fuzzy section in which one insertion fits, searched without `BestMatch`, that also has a
position test (`^`, `$`, `\A`, `\Z`, `\G`, `\b`, `\B`, `\m`, `\M`, a grapheme boundary or a
lookaround), a construct whose result depends on the path taken (a backreference, a conditional, a
group call, a verb, `\K`, an atomic group or a possessive quantifier), or a fuzzy minimum such as
`{1<=i<=2}`. For these the partial can start earlier than the match more text would give, and
never later. For example, `(?:ab){1<=i<=2}` over `xxa` is a partial at 0, while the completed text
`xxab` matches at 1. On the generated searches this rule moved, the start was early by 1 character
on 61, by 2 on 4, and by 3 at most; that bound was observed, and is unproven. Under `(?r)` the same
holds mirrored, at the end of the match. A caller that keeps the text from the partial start keeps
a few extra characters and still holds the start of every match that is arriving.

**One accepted kind of partial differs between a group call and the same group written out.** When
a group call is opened inside a body whose outcome feeds a decision (a negative lookaround, a
conditional's test, a lookahead inside an atomic group or possessive repeat, any pattern with
`(*PRUNE)` or `(*SKIP)`, or any lookaround in a fuzzy pattern) and the text runs out inside that
call, the engine answers the partial at once. So `(x)(?!y?(?1))\Z` over `xy` is a partial at
(0, 2), where the written-out `(x)(?!y?x)\Z` gives no match; a search can also report an earlier
start than the written-out form. Each such answer is either a partial that no text completes or
one that starts early, and none is a missed match. PCRE2 10.47 and upstream give the same partials.
The fix that would make the two forms agree is a candidate for 1.1.

Under `BestMatch` and `EnhanceMatch` a partial search reports the leftmost start whose match was
still open, because those modes rank complete matches by their errors and a partial has no final
error count yet. The guide's
[Reading a result](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/GUIDE.md#reading-a-result)
section has runnable examples of each case.

## Deliberate differences from Python `regex`

Each line below is one row of
[DIVERGENCES.md](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md), which
gives the reason, the evidence, the decision and, where one exists, how to get upstream's behaviour
back. All 101 rows are in the released code. Most of the behaviour rows are upstream defects that
this port fixed and pinned with tests; the rest are choices made for .NET callers.

### Defaults

- Patterns compile as version 1 by default; upstream defaults to version 0. Use `FuzzyRegexOptions.Version0` or `(?V0)` to switch. [details][d25]
- An inline `(?V0)` really selects version 0 under the version 1 default. [details][d26]
- The "unterminated character set" error suggests `Version0` and the `\[` escape, but only when version 0 accepts the pattern. [details][d27]
- Upstream's `concurrent` argument has no counterpart: this port takes no global lock. [details][d28]
- A `Match` may be read from any thread, which goes further than `System.Text.RegularExpressions.Match`. [details][d29]
- Every method that reads the subject takes a `CancellationToken`. [details][d30]
- Every such method also takes a per-call `timeout`, upstream's `timeout=` in .NET form. [details][d31]

### API shape

- `Match` is upstream's `search`; upstream's anchored `match` is `MatchAtStart` and `fullmatch` is `FullMatch`. [details][d37]
- The surface follows `Regex`: `Match`, `Group` and `Capture` with `Index` and `Length`; `expand` is `Match.Result`, `lastindex` is `LastGroupNumber`. [details][d38]
- Positions and lengths count UTF-16 code units, including the parse error's `Offset`. [details][d39]
- Upstream's `pos` and `endpos` become `beginning` and `length`, where `-1` means "to the end". [details][d40]
- `Split` spells "no limit" as `maxSplits = -1`. [details][d41]
- `Replace` spells "no limit" as `count = -1`, and `0` replaces nothing. [details][d42]
- There is no `findall`; read `Groups[1].Value` over `Matches` or `EnumerateMatches`. [details][d43]
- `Match.Groups` is a dictionary keyed by every group's name or number as well as a list. [details][d44]
- A lazy walk (`Matches`, `EnumerateMatches`, `EnumerateSplits`) times each step against the `timeout`, so time spent in your loop body never counts. [details][d45]
- `Split` returns `string?[]` with `null` where a capturing group did not take part, as upstream does and the built-in `Regex.Split` does not. [details][d46]
- Replacement templates use upstream's syntax (`\1`, `\g<name>`); `$1` is ordinary text. [details][d47]
- Upstream's errors map to .NET exceptions: `FuzzyRegexParseException` for pattern errors, `RegexMatchTimeoutException` for timeouts, `InvalidOperationException` for the backtracking bound. [details][d48]
- `allcaptures`, `allspans`, `groupdict` and `capturesdict` are left out; `Groups` and `Group.Captures` hold the same data. [details][d49]
- The "unused keyword argument" message prints a non-ASCII name as written. [details][d50]
- Members with no .NET meaning are left out, among them `Pattern.scanner`, `Match.detach_string`, `regex.template` and the pattern cache controls. [details][d51]

### Unicode, case folding and encodings

- The Turkic `I` pairings are off by default, as in PCRE2, Perl and .NET: `(?i)I` does not match `ı`. [details][d58]
- The Unicode data is version 17.0.0, fixed by the library, where upstream uses whatever its Python ships. [details][d62]
- A digit's decimal value comes from the port's own Unicode tables, so the two never disagree. [details][d63]
- `\N{...}` named sequences raise "undefined character name" where upstream raises `TypeError`. [details][d64]
- `(?L)` works only where no casing is requested; case-insensitive matching under it is refused at construction. [details][d65]
- Under ASCII, a case-insensitive cased property such as `(?ai)\p{Lu}` means the 52 ASCII letters. [details][d81]
- A scope that names no encoding keeps the one around it: `(?a:(?s:\w))` stays ASCII. [details][d82]
- A POSIX class such as `[[:alpha:]]` takes the encoding of its scope. [details][d83]
- Under `IgnoreCase` a cased property answers the same bare and inside a set. [details][d84]
- A case-insensitive set matches each member first, then combines them. [details][d85]
- An encoding named by positional flags inside a group, as in `(?a:(?u)\w)`, replaces the one in force. [details][d86]
- A scoped `(?a:...)` or `(?u:...)` answers exactly as the same encoding set for the whole pattern, for every construct. [details][d87]
- A literal under a scoped `(?i:...)` is found in text that holds only its full case folding, such as `ss` in `ß`. [details][d88]
- A lazy repeat finds a full-folded literal that starts at the repeat's last position. [details][d89]
- The `(?w)` word boundary is Unicode's default one and passes all 1,944 lines of `WordBreakTest.txt`. [details][d91]
- `\X` matches the same grapheme cluster backwards as forwards, over all 766 lines of `GraphemeBreakTest.txt`. [details][d92]

### Fuzzy matching

- A search may open a fuzzy match with an insertion at the position it started from, when no later match would start inside it. [details][d73]
- A fuzzy deletion that finishes a full-case-folded string or backreference costs one edit. [details][d74]
- A full-folded backreference that ends half-way through a folding charges the rest as one edit. [details][d75]
- A retried fuzzy edit in a full-folded backreference steps past what it used up. [details][d76]
- A fuzzy full-folded match can stop part-way into a folding, at one deletion per folded character given back. [details][d77]
- A fuzzy section that is undone takes its error total with it. [details][d78]
- A fuzzy repeat takes an empty, deleting iteration only when something needs it. [details][d79]
- A constraint that allows no errors, such as `{e<=0}`, limits the errors made inside it however it is written. [details][d90]
- A fuzzy item that matched exactly can still be deleted, and that choice is tried before any earlier one. [details][d93]
- A `\G` inside a fuzzy section gets an answer where upstream raises "invalid RE code". [details][d101]
- A fuzzy run can edit `ß` or a ligature as one character. [details][d102]
- A lookaround that fails inside a fuzzy section can be passed by inserting a character in front of it. [details][d103]
- A section's minimum error count can be met by a character inserted after its last item. [details][d104]
- Below a section's minimum, an item that matched exactly is also tried with a character inserted in front of it. [details][d105]
- A fuzzy run can edit a subject character that expands under case folding, such as `ǰ`, as one character. [details][d106]
- A verb that cuts through a fuzzy section closes it, so its limits stop where it ends. [details][d110]

### BestMatch and EnhanceMatch

- `(?e)` and `(?b)` rank by fuzzy cost where upstream ranks by error count (upstream issue 470). The two agree unless the pattern weights its error kinds differently. [details][d57]
- A `BestMatch` or `EnhanceMatch` scan does not skip positions after an empty match. [details][d98]
- Under these modes `\G` holds only where the search began. [details][d99]
- They count only the errors a match contains, leaving out sections an atomic group, lookaround, verb or conditional's test threw away. [details][d111]
- Their budget never decides a negative lookaround or a conditional's test. [details][d112]
- They keep a fit that ends in trailing insertions. [details][d129]

### Partial matching

- A word or grapheme boundary decided at the end of the text gives a partial match, as in PCRE2 (upstream issue 589). [details][d60]
- A partial is refused where the match failed on text the engine already held, so no text could complete it. [details][d61]
- A reversed partial match reports running out of text at the slice start consistently. [details][d71]
- Running out of text inside a lookaround's body no longer ends the call with a partial by itself. [details][d123]
- A partial is reported where upstream refuses a completion that exists, such as `.*?(?<=c)` over `aa`. [details][d124]
- A partial search tries a start inside the text still to come, for a pattern with no first set. [details][d125]
- `\b` over the empty text is a partial, and `\Z` at the edge is false in a partial call's second pass. [details][d126]
- A partial reports a group that closed before a repeat's tail ran out of text. [details][d127]
- A reversed partial search lets `$` hold before a final newline in the text to come. [details][d128]
- A partial search's first-set check does not answer before the anchors it skips. [details][d130]

### Verbs and `\K`

- A `(*SKIP)` acts when backtracking reaches it, so a later `(*PRUNE)` decides the next start. [details][d94]
- A verb, branch, group call or fuzzy section that starts every alternative stays in each one, as in PCRE2 and Perl. [details][d95]
- A verb reached by backtracking inside an unfinished atomic group or positive lookaround ends the attempt. [details][d96]
- A verb after a lazy leading repeat does not hide the starts inside the repeat's run. [details][d97]
- A verb inside a lookbehind acts in right-to-left order, as the lookbehind's body runs. [details][d100]
- A `\K` inside a lookaround cannot make a scan or a search repeat itself for ever. [details][d132]
- A `\K` inside a lookaround, condition, atomic group or possessive repeat is undone when matching backtracks past it. [details][d133]
- A `\K` before a `(*PRUNE)` or `(*SKIP)` does not make a search skip start positions. [details][d134]

### Group calls, recursion, conditionals and backreferences

- A group that can call itself before matching any text (exact left recursion) is refused at compile time; upstream runs out of memory. [details][d67]
- A group call that would re-enter the same group at the same position fails that path only. [details][d68]
- A fuzzy recursive pattern whose calls fail matches in milliseconds; some shapes stay exponential, bounded by `MatchTimeout`. [details][d69]
- In a branch reset, a group never takes a number another group in the same branch will use (upstream issue 425). [details][d70]
- A group call inside a lookaround leaves no capture behind when the lookaround's body is thrown away. [details][d107]
- A group call keeps none of the captures its body made, as in Perl and PCRE2. [details][d108]
- A call to the whole pattern inside a pattern that is one fuzzy section returns to its caller. [details][d109]
- The calls inside a called group run with the call's direction and fuzziness. [details][d113]
- A whole-pattern call that re-opens a fuzzy section where the same call is already open is refused. [details][d114]
- A call inside a lookaround in a fuzzy section runs its group exactly, as the lookaround's own body does. [details][d115]
- A call in a conditional's lookbehind test runs its group backwards. [details][d116]
- A backwards copy of the whole pattern does not jump to the required string's forward end. [details][d117]
- A called copy adds nothing to the pattern's minimum width. [details][d118]
- A called group's copy runs as the group written out at the call. [details][d119]
- An alternation at the start of a negative condition's yes branch keeps all its alternatives. [details][d120]
- A conditional's lookaround test is matched exactly inside a fuzzy section. [details][d121]
- A conditional whose negative test fails keeps the captures the test made. [details][d122]
- A `DEFINE` group with a no-branch is refused at construction, as PCRE2 and Perl refuse it. [details][d131]
- A search that reads a group after a guarded repeat finds the leftmost match. [details][d135]

### Search and limits

- Search prefilters never change the answer the full match would give, so on several families `search`, `finditer` and partial matching agree with upstream's own anchored `match` where upstream's `search` does not. [details][d59]
- CPython's 4,300-digit cap on integers is absent, so a very long group number compiles. [details][d66]
- A pattern whose compiled graph would exceed `maxCompiledNodes` (a million by default) is refused at construction. [details][d72]
- Inherited upstream bugs are fixed here, each with a ledger entry; this row lists the earlier ones. [details][d80]

## Upstream bugs this port does not share

Building the port meant reading upstream's C closely and comparing the two engines on millions of
generated rows, and that turned up defects in upstream itself. Each one is recorded in
[LEDGER.md](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/plan/upstream-reports/LEDGER.md)
with a minimal reproduction, the faulting code where it was traced, and this port's answer. The
ledger holds 90 entries, numbered 1 to 91 (there is no 41). Entry 10 was fixed by upstream itself in
2026.9.10. None of the others has been reported upstream yet. Some notable ones:

- A POSIX search of a fuzzy pattern crashes the C engine (entry 9).
- A self-recursive call around a fuzzy section that can match empty exhausts memory (entry 14), and so does a left-recursive group call (entry 73).
- The default case-folding tables carry Unicode's Turkic-only rows, so `(?i)I` matches `ı` with no locale asked for (entry 7).
- `BestMatch` loses a match that plain fuzzy matching finds when the best fit ends in a trailing insertion (entry 12).
- A fuzzy match reports change positions that contradict its own change counts (entry 11).
- A regression in 2026.9.10 itself: `(*SKIP)` blocks the one repeat retreat a partial match needs (entry 15).
- Branch reset gives two groups in the same branch the same number (entry 17, upstream issue 425).
- A repeated capture group costs hundreds of bytes per repetition (entry 18, upstream issue 554).
- The `(?w)` word boundary fails 268 lines of Unicode's own conformance test (entry 40), and a reversed `\X` takes one codepoint where it should take a grapheme cluster (entry 43).
- A `\K` inside a lookaround makes a scan, or one search, repeat an attempt for ever (entry 85).

## Performance

The 1.0 gate compares this library with Python `regex` 2026.9.10 on the same machine, workload by
workload, never averaged. It passes when no more than 10% of workloads are slower than Python and
none is slower by more than 1.25x. Measured on 2026-10-18 on release candidate `5730e15c`
(i7-13850HX, Windows 11, .NET 10.0.13, BenchmarkDotNet `--job medium`; Python 3.14.8 with pyperf,
both pinned to the same performance cores):

**The gate passes.** Of 40 paired workloads, 38 are at or below Python's time and 2 are slower,
both inside the tolerance. Every one of the 40 gave the same answer in both engines. The fuzzy
workloads run between 25 and 1,600 times faster than upstream (ratios of 0.04x and below), and a
fuzzy phrase search over 100,000 short records takes 44 to 139 ms depending on its form.

Two workloads are slower than Python. Both were accepted for 1.0 under the performance freeze (plan
decision 3), and each has a route to a fix in 1.1:

| Workload | FuzzyRegex | Python `regex` | Ratio |
|---|---|---|---|
| ConditionalPerTokenNoMemo: `(\w)(?:(?(?=\w)\w+\|\s))*\1$` failing over 200 short words and a `!` | 114 ms | 105 ms | 1.09x |
| TestedGroupRepeat: `(a)(?:\1\|b)*c` failing over `a`, then `ab` 200 times, then `d` | 2.44 ms | 2.24 ms | 1.09x |

In both, the backreference turns off a failure memo for the whole pattern. The first also began
allocating about 105 MB per call after a correctness fix (known defect D103). The full table, the
method and the raw data are in
[the gate record](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-10-18-S63-gate.md).

## Known limitations

- **D90, a partial match with a lookaround that sets a group.** <!-- TODO(orchestrator): status to
  be filled in by the orchestrator: fix in progress. --> In a partial call's second pass a repeat
  guard can cut a path that matches, when a lookaround that sets a group keeps paths with other
  captures: `a*(?:(?=(?:(a)|b)*)a)+\1ab?` searched over `ab` with `partial: true` gives (2, 2)
  where (0, 2) is right.
- **Refused patterns.** These raise `FuzzyRegexParseException` (or `NotSupportedException` for
  `(?L)`) when the pattern is compiled, where upstream compiles them and then fails or runs out of
  memory: exact left recursion, such as `(?(DEFINE)(?<list>(?&list),\w+|\w+))^(?&list)$`; a whole-pattern
  call that re-opens a fuzzy section where the same call is already open; a `DEFINE` group with a
  no-branch; a pattern whose compiled graph would exceed `maxCompiledNodes`; case-insensitive
  matching under `(?L)`.
- **Some recursive fuzzy patterns take exponential time.** The failure memo covers calls that fail
  without returning; shapes whose calls return and whose callers then fail can still grow
  exponentially. Set a `matchTimeout` when patterns come from users.
- **Performance work deferred to 1.1.** D99: a chain of recursive calls that re-enter at the same
  position inside a fuzzy section repeats each level's failed work (`(?:(?P<g>a(?&g)b|c)){d<=6}`
  over 12 `a`s and a `c` takes about 43 times the steps it took before the fix that caused it).
  D103: the 105 MB per call allocation in ConditionalPerTokenNoMemo, above. Other timing and
  allocation rows (D29, D33, D68, D71, D82, D91) and the slowdowns accepted with the D85 and D92
  correctness fixes are in
  [KNOWN-DEFECTS.md](https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/KNOWN-DEFECTS.md).
- **Upstream 2026.9.29** (Unicode 18.0.0) is planned for 1.1; 1.0 uses Unicode 17.0.0.

## Reporting a bug

Open an issue at <https://github.com/zejji/fuzzy-regex-cs/issues>. The fastest report to act on
gives the pattern, the subject, the flags and the method you called (`Match`, `MatchAtStart`,
`FullMatch`, with `partial: true` or not), what you got and what you expected. If you know what
Python's `regex` answers for the same call, include it, but say if you think upstream is wrong too:
several of the differences above are cases where it is.

[d25]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L25
[d26]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L26
[d27]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L27
[d28]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L28
[d29]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L29
[d30]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L30
[d31]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L31
[d37]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L37
[d38]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L38
[d39]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L39
[d40]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L40
[d41]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L41
[d42]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L42
[d43]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L43
[d44]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L44
[d45]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L45
[d46]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L46
[d47]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L47
[d48]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L48
[d49]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L49
[d50]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L50
[d51]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L51
[d57]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L57
[d58]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L58
[d59]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L59
[d60]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L60
[d61]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L61
[d62]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L62
[d63]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L63
[d64]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L64
[d65]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L65
[d66]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L66
[d67]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L67
[d68]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L68
[d69]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L69
[d70]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L70
[d71]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L71
[d72]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L72
[d73]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L73
[d74]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L74
[d75]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L75
[d76]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L76
[d77]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L77
[d78]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L78
[d79]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L79
[d80]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L80
[d81]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L81
[d82]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L82
[d83]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L83
[d84]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L84
[d85]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L85
[d86]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L86
[d87]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L87
[d88]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L88
[d89]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L89
[d90]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L90
[d91]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L91
[d92]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L92
[d93]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L93
[d94]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L94
[d95]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L95
[d96]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L96
[d97]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L97
[d98]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L98
[d99]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L99
[d100]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L100
[d101]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L101
[d102]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L102
[d103]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L103
[d104]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L104
[d105]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L105
[d106]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L106
[d107]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L107
[d108]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L108
[d109]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L109
[d110]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L110
[d111]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L111
[d112]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L112
[d113]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L113
[d114]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L114
[d115]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L115
[d116]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L116
[d117]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L117
[d118]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L118
[d119]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L119
[d120]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L120
[d121]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L121
[d122]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L122
[d123]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L123
[d124]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L124
[d125]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L125
[d126]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L126
[d127]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L127
[d128]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L128
[d129]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L129
[d130]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L130
[d131]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L131
[d132]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L132
[d133]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L133
[d134]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L134
[d135]: https://github.com/zejji/fuzzy-regex-cs/blob/v1.0.0/docs/DIVERGENCES.md?plain=1#L135
