# A fold-aware first-character screen for case-insensitive alternations (2026-10-11)

Design note for `ManyInputsBenchmarks.CaseFoldAccented`: `IsMatch` of `straße|café` with
`IgnoreCase | FullCase` over the 100,000 lines of `UsageCorpus.Accented`. Read-only study of main
a1191e65; nothing was built or timed. Working notes: `.scratch/casefold-screen/notes.md`.

Terms used below:

- **Attempt**: one try at matching from one start position. A search that fails at position 3 makes
  its next attempt at position 4.
- **Screen** (or prefilter): a cheap test that skips start positions at which no match can begin,
  so the engine only makes attempts at the others. A screen may let through positions that then
  fail (false positives); it must never skip one where a match begins.
- **Full case folding**: the Unicode mapping that can turn one character into several, so `ß`
  folds to `ss` and `ﬅ` (U+FB05) to `st`. Simple folding maps one character to one.

## The workload

`UsageCorpus.BuildAccented` (bench/FuzzyRegex.Benchmarks/UsageCorpus.cs:231-251) writes 5 words
per line, each chosen uniformly from `Größe naïve résumé Ångström façade Øresund jalapeño Zürich`,
each followed by a space; 5% of lines then get `STRASSE` or `Café`. A line is about 37 UTF-16 units
(mean word 6.4 characters plus a space), all BMP, and about 5% match.

The characters that can begin a match are `s`, `S`, `ſ`, `c`, `C` and the ligatures `ﬅ`, `ﬆ`
(measured, below). In this corpus they occur in `résumé`, `Ångström`, `Øresund` (s) and `Zürich`
(c), plus `ß` in `Größe` if the screen admits it. So a line holds about 3.1 candidate positions
(5 words x 5/8), plus about 0.1 from the targets, against about 38 attempts today.

## 1. How the search finds start positions today

Path for this pattern, all in `src/FuzzyRegex/Engine/`:

| Step | Where | What happens for `straße\|café` |
|---|---|---|
| Entry | `Matcher.cs:19245` `DoExactMatch` -> `:12457` `BasicMatch` -> `:12534` `BasicMatchCore` | Called **once per line**. The method's prolog, including the stack zeroing, is paid once per `IsMatch`, not once per position |
| Start test | `NodeCompiler.cs:213`, `Node.cs:489` `LocateTestStart` | The start node is a `Branch`, which `LocateTestStart` returns unchanged (its default arm), as upstream's does |
| Required string | `Matcher.cs:12769`, `:11776` | None: an alternation has no single required string, so `RequiredStringScreen` never runs |
| Fuzzy-literal filter | `Matcher.cs:12636` | Null: the pattern is not fuzzy |
| Leading literals | `Matcher.cs:12642`, `:12812`; `PatternObject.cs:1300` `FindLeadingStrings` | Null: the walk only accepts case-sensitive `String` or `Character` nodes, and these branches open with `StringFld` and `StringIgn` |
| `SearchStart` | `Matcher.cs:12144`, default arm `:12358` | The `Branch` test has no arm, so the first call clears `DoSearchStart` and returns the position unchanged |
| Each attempt | `start_match:` `Matcher.cs:12650` to `advance:` `:12927` | Cancellation gate, `Bstack`/`Pstack` pushes, clearing call, verb and reach state |
| Branch | `Matcher.cs:13401` | `TryMatch` on `StringFld "st"` (`:7477`, refused on the first character since S60b item 3), then the second branch's `StringIgn "café"` node runs as a step and fails |
| Bump-along | `FAILURE` arm `Matcher.cs:18144-18245` | No leading repeat to skip; `TextPos = NextPos(MatchPos)`, `ClearGroups`, `ResetGuards`, stack resets, `goto start_match` |

So every position of every line is an attempt: about 38 per line (37 characters plus the attempt at
the end of the line), about 3 matching-loop steps each, which matches the profile's 116 steps per
line after S60b item 3 (`.scratch/perf-profile/notes.md`). There is **one `BasicMatchCore` call per
line**. The cost that grows with the line is the per-attempt block above, not the method prolog: the
inline-array fix to the prolog removes a fixed cost per line, and this screen removes about 35 of the
38 attempts. The two are independent and add up.

## 2. What upstream does

Upstream compiles the pattern (regex 2026.9.10, code captured by wrapping `_regex.compile`) as

```
BRANCH  STRING_FLD "st"  STRING_IGN "ra"  STRING_FLD "sse"
NEXT    STRING_IGN "café"
END     SUCCESS
```

and has **no first-character screen** for it:

- `_check_firstset` gives up on full case folding: `if case_flags == (FULLCASE | IGNORECASE): return
  None` (`upstream/regex/_regex_core.py:401`). The comment two lines up (`:385`) reads "If we ignore
  the case, for simplicity we won't build a firstset."
- The zero-width precheck in front of a branch, `Branch._add_precheck`
  (`_regex_core.py:2178-2192`), only collects literals whose `case_flags == NOCASE` (`:2183`), so
  plain `(?i)` alternations get none either.
- `locate_test_start` (`upstream/src/_regex.c:3476-3518`) returns the `BRANCH` node, and
  `search_start`'s `default:` arm (`_regex.c:9175-9182`) sets `do_search_start = FALSE`.
- `string_search_fld` (`_regex.c:6637-6708`) and `search_start_STRING_FLD` (`:8321`) exist, but only
  for a required string or a start test that is itself a `STRING_FLD`; `string_search_fld` is a
  character-at-a-time loop that folds every subject character, not a set scan.

So upstream also tries every position, with about 115 steps per line. It is faster today only
because its per-attempt constant is smaller. `match_many_*` (`_regex.c:3537-4990`) is the
one-character-set sweep `SearchStart` uses for a one-character start test; it plays no part here.

## 3. What the fastest engines do

- **.NET `Regex`** (RegexCompiler, source generator, interpreter alike). Since .NET 7, case
  insensitivity is resolved at construction: "all casing-related work is done when the `Regex` is
  constructed", and an ignore-case character becomes a set of its equivalents, so `(?i)abcd` is
  `[Aa][Bb][Cc][Dd]` and "We now have sets of known characters we can search for with methods like
  `IndexOfAny`". The leading set of `(?i)Surname|(Last[_]?Name)` is searched with the span overload
  for 4-5 characters. Source: Stephen Toub, "Regular Expression Improvements in .NET 7",
  devblogs.microsoft.com/dotnet/regular-expression-improvements-in-dotnet-7 (fetched 2026-10-11).
  .NET has no full case folding, so it never faces `ß`/`ﬅ`.
- **`SearchValues<char>` itself** chooses an implementation from the set's shape
  (`SearchValues.Create`, dotnet/runtime main,
  `src/libraries/System.Private.CoreLib/src/System/SearchValues/SearchValues.cs`, fetched
  2026-10-11): sets of 1-5 values get dedicated vector searches, all-ASCII sets a vectorised bitmap,
  and a mix of ASCII and non-ASCII values gets `ProbabilisticWithAsciiCharSearchValues`, which runs a
  vectorised ASCII search until it meets a non-ASCII character and then hands the rest of the span to
  `ProbabilisticMap` (`ProbabilisticWithAsciiCharSearchValues.cs`, fetched 2026-10-11). The map
  "is vectorized" where `Sse41.IsSupported || AdvSimd.Arm64.IsSupported` (`ShouldUseProbabilisticMap`
  in `SearchValues.cs`). The set this note proposes for the row (9 units, mixed) lands on that path.
- **PCRE2** records a first code unit when every match starts with one (`(cat|cow|coyote)` gives
  `c`), and otherwise may build "a 256-bit table that defines a fixed set of values for the first code
  unit in any match" (pcre2api, `PCRE2_INFO_FIRSTCODETYPE`/`FIRSTCODEUNIT`/`FIRSTBITMAP`,
  pcre.org/current/doc/html/pcre2api.html, fetched 2026-10-11). Under caseless matching the first
  code unit is searched in both cases. *Not verified here:* the exact caseless rule (from memory of
  pcre2_compile.c's `firstcu`/`REQ_CASELESS` flag, not re-read today).
- **Rust `regex-automata`**: a prefilter is "to very quickly find candidate locations in a haystack
  where a regex *could* match"; the engine then confirms each, and a high false-positive rate "can
  overall make a regex search slower" (docs.rs/regex-automata, `util::prefilter`, fetched
  2026-10-11). *Not verified here:* that `regex-syntax`'s literal extractor expands case-insensitive
  literals into their case variants and that `memchr`'s Teddy (SIMD multi-literal) searches them;
  this is my recollection of those crates, not something I re-read.

The common shape: decide at compile time which characters can start a match, search for them with
a vectorised set search, run the real matcher only there. None of them has full case folding, so
none has to think about `ß` or ligatures; the set below does.

## 4. Design

### The rule

A forward match starts on a character boundary, and the first subject character must either be a
case of the branch's first pattern character or have a full folding that begins with it. The engine
already computes a **superset** of exactly that for the required-string screen:
`RequiredStringScreen.FirstUnits` (`RequiredStringScreen.cs:243-299`) takes the closure of the
first character under `Encodings.AllCases` and adds every member of `Specials` (characters whose
full folding is longer than one character or not a plain equivalence class, derived from the case
tables and re-derived by `RequiredStringScreenTests`) whose folding holds that character anywhere
(`FoldingHolds`, `:422`). For `straße|café`:

| Set | Members | How obtained |
|---|---|---|
| Exact, `(?fi)` | `C S c s ſ ﬅ ﬆ` | upstream `regex.compile(p).match(chr(cp), partial=True)` over every codepoint, 2026-10-11 |
| Exact, `(?i)` | `C S c s ſ` | same |
| Exact, `(?ai)` | `C S c s` | same |
| `FirstUnits` rule, `(?fi)` | the exact set plus `ß ẞ` | `FoldingHolds` admits `ß` because its folding `ss` holds `s`; the attempt then fails on the `t` |

The two extra members cost about 0.6 failed attempts per line here (one per `Größe`), about 1% of
the line's time on the estimate in section 5. A two-character check that would drop them is not
worth its code.

Under the ASCII encoding the closure and folding are ASCII-only, which matches the `(?ai)` row; the
`LOCALE` encoding has no ported tables and is refused, as `FirstUnits` already refuses it. The
module has no Turkish flag; `İ` (U+0130) and `ı` (U+0131) are just members of `Specials` and the
case tables. The exhaustive test below covers them rather than a hand list.

### Mechanism (recommended: option A)

1. **Factor the codepoint set out of `FirstUnits`** into `RequiredStringScreen.FirstCodepoints(Node
   node, bool reverse)` returning the codepoint set, and keep `FirstUnits` as its caller that turns
   codepoints into units. Extend it to the one-character nodes `Character` (the value) and
   `CharacterIgn` (closure plus specials), since a one-character branch compiles to those.
2. **Generalise `FindLeadingStrings`** (`PatternObject.cs:1300`) into a walk that, for each path
   through the opening branch, accepts any forward literal node: `String`, `Character` (as today),
   and also `StringIgn`, `StringFld`, `CharacterIgn` with `Match` set. Each node's encoding must not be
   `Locale`. The walk keeps today's look-through of `StartGroup`, nested `Branch` and the zero-width
   first-set node, today's 1,000-branch cap, and today's refusals: fuzzy, reversed, any lone
   surrogate codepoint, anything else at the head of a path.
   - When every head is case-sensitive, keep `LeadingStrings` exactly as today (a multi-literal
     search beats a first-character one).
   - Otherwise set a new field `LeadingUnits` (`SearchValues<char>`): the union, over the heads, of
     the **first UTF-16 unit** of each codepoint in `FirstCodepoints`. A high surrogate stands for an
     astral character; a low surrogate is never added, so a hit can never land in the middle of a
     pair. A lone high surrogate in the subject can then be a false positive, which is harmless.
3. **One plug-in point**: the existing leading-literal jump, `Matcher.cs:12812-12822`. Read
   `pattern.LeadingUnits` under the same guard as `leadingStrings` (`:12642`: search only, no partial
   side, no `(*SKIP)`), and when it is set, `foundPos += span[foundPos..SliceEnd].IndexOfAny(units)`,
   or `MatchStatus.Failure` when there is no hit. Nothing else in the matcher changes. Every attempt
   (first, and each one after the `FAILURE` arm's bump-along) passes through `start_match:` and so
   through this jump.
4. **Records**: add `LeadingUnits` to the `ThreadSafetyTests` list of compile-time fields
   (`ThreadSafetyTests.cs:895`), widen the existing `SYNC-DIVERGENCE.md` row for `FindLeadingStrings`
   (row at line 68), and a `sync-divergence:` comment at the walk.

### Why it cannot change an answer, risk by risk

| Risk | Handling |
|---|---|
| `ß` -> `ss`, ligatures, `ſ`, Kelvin sign, Greek iota-subscript forms | Covered by closure plus `Specials`; the exhaustive test proves no start character is missing |
| Pattern starts with a multi-character folding (`ß...` compiles to `STRING_FLD "ss..."`) | The values are already folded, so the first value is `s` and `ß`, `ẞ`, `ſ`, `S`, `s` are all admitted. Upstream: `(?fi)ssx\|q` finds `ßx` and `ẞx` at (1, 3) |
| A match starting part way through a subject character's folding | Impossible: an attempt starts on a character boundary |
| Astral characters (Deseret and other cased astral scripts) | High surrogate of each candidate; upstream: `(?i)\U00010400x\|y` over `a\U00010428x` is (1, 3) |
| Lone surrogate in the pattern's first character | Refused, as `FindLeadingStrings` refuses it today |
| `LOCALE` encoding | Refused |
| Fuzzy | Refused: an error can delete or replace the first character |
| Reverse search | Refused. A mirror image (last-character set, `LastIndexOfAny`) is possible later; not needed for any row |
| Partial matching | Withheld at the call site: a partial match can be a prefix of nothing yet |
| `(*SKIP)` | Withheld at the call site (`HasSkipVerb`), as every start jump is; `(*PRUNE)` is safe for the reason the existing jump is: a skipped position fails before any verb |
| Lookbehind, `\G`, anchors, empty branch at the head | The walk refuses anything that is not a literal at the head of every path |
| `MustAdvance` / an empty match at the search anchor | Not reachable: every accepted branch consumes at least one character |
| Timeout and cancellation | The jump is one sweep with no poll, as the existing one is; the sweep is linear and vectorised |

### Options

| Option | What | Pros | Cons |
|---|---|---|---|
| **A. Fold-aware leading units at the existing jump** (recommended) | Section above | Reuses the screen rules and tables already proven by `RequiredStringScreenTests`; one call site that already carries the right guards; covers `(?i)` and `(?fi)` alternations of any size; no change to compiled code, `DEBUG` output or the ported dispatcher | One more compile-time field; widens an existing sync divergence; the probabilistic `SearchValues` path is slower per character than a pure-ASCII bitmap |
| B. A `BRANCH` arm in `SearchStart` | Give `SearchStart` a case for a branch start test, with the precomputed set | Sits where upstream's own fast search lives | Edits the ported dispatcher and its narrowings, which are tuned to upstream's arms; `DoSearchStart` is per state and the `try_match` look-ahead logic does not fit a branch; more sync cost for the same skip |
| C. Emit a zero-width first-set node in front of the branch | Relax `_check_firstset`'s full-case refusal | Matches upstream's existing mechanism for case-sensitive branches | **Wrong answers**: a one-character set node under simple case cannot accept `ﬅ` for `st`, so `(?fi)straße\|café` over `ﬅraße`, which upstream matches at (0, 5), would be refused. It also changes the compiled code and the parity corpus. Rejected |
| D. `SearchValues<string>` with `StringComparison.OrdinalIgnoreCase` | Feed the literals to the existing string search ignoring case | Smallest code change | Not a superset of regex case rules: ordinal ignore-case uses simple one-to-one mappings, so it cannot find `ß` for `ss` or `ﬅ` for `st`; whether it finds U+212A KELVIN SIGN for `k` was *not checked* (no `dotnet` runs allowed today). Rejected on the folding cases alone |
| E. Leave it; cut the per-attempt constant | Inline-array prolog fix, cheaper attempt reset | Helps every row | The prolog is once per line here, so it removes a fixed cost, not the 35 wasted attempts; on its own unlikely to bring 1.37x under 1.25x (estimate) |

## 5. Cost on paper

Let `n` be the line length in units, `k` the candidates in it, `c` the cost of one failed attempt.

- **Today**: about `(n + 1) * c` per line, plus a fixed per-call cost `f`.
- **With the screen**: `f + k * c + scan`, where the scan is one `IndexOfAny` per attempt and each
  sweep resumes after the previous candidate, so the total sweep is at most `n` units per line:
  linear, never repeated.
- **Memory at compile**: one `SearchValues<char>`. For this row that is the probabilistic map plus
  an ASCII bitmap, a few hundred bytes (estimate). Compile time is the closure and the specials scan
  (about 100 `FoldingHolds` calls) per distinct head codepoint, deduplicated; microseconds.
- **Adversarial input**: a line of `sssscccc...` that never matches (or `ßßßß` with the superset).
  Every position is a candidate, so the attempts are the same as today and the extra cost is one
  `IndexOfAny` call per attempt that returns at offset 0: a call and a few compares, small beside `c`.
  Estimate: within 5-10% of main on that input, never asymptotically worse.
- **Mostly non-ASCII text**: the mixed set takes the probabilistic map path once a non-ASCII
  character is met, which is vectorised on SSE4.1 but slower per character than the ASCII bitmap;
  still a few nanoseconds per 8-16 characters against `c` per position.

**Expected gain on this row (ESTIMATE, not measured).** Today about 1.47 us a line (the S60b
experiment's 147 ms per 100,000 lines, harness). With `f` taken as 0.3-0.4 us (bounded by
`ValidateEmails` and `ParseLogLines`, which spend about 0.55 us a line including their matching),
`c` is about 28-31 ns. With about 3.2 attempts a line and about 0.05 us of sweeping: **about 0.45-0.55
us a line, so roughly 45-60 ms against Python's 123 ms, a ratio of about 0.4-0.5x** instead of the
estimated 1.37x. The precedent is the case-sensitive version of the same pattern, which the existing
leading-literal jump took from 14.88 to 0.12 ms over a megabyte with no match (SYNC-DIVERGENCE row
68).

**Other rows**: none expected to move. `CaseFoldedScan` is a single literal, handled by the
required-string screen. The `FuzzyPhrase*` rows are fuzzy and refused. No other benchmark pattern is a
non-fuzzy case-insensitive alternation (grep of `bench/FuzzyRegex.Benchmarks`).

## 6. Divergence from upstream and sync cost

Upstream has no screen for any case-insensitive alternation, so this widens an existing divergence
(the S60b item 14 leading-literal jump) rather than starting a new one: no ported function changes
shape; one compile-time walk and one existing call site grow. Sync cost: small. A sync slice
must act only if upstream gains its own first-set for case-insensitive branches (then drop ours) or
adds a node kind that can head a branch (the walk refuses it until it is listed). Record it in the
existing SYNC-DIVERGENCE row with the measured gain and in `docs/PORTMAP.md`. This is not a major
structural divergence under the 2026-10-11 policy.

## 7. Test plan

Write these first; they should fail where noted before the change.

1. **Exhaustive start set (derived from the engine, not a hand list).** For each pattern in a list
   (`(?fi)straße|café`, `(?i)straße|café`, `(?ai)straße|café`, `(?fi)ss|x`, `(?fi)st|x`, `(?fi)k|x`,
   `(?fi)i|x`, `(?fi)ı|x`, `(?fi)İ|x`, `(?fi)ᾀ|x`, `(?fi)ι|x`, `(?fi)µ|x`, `(?i)\U00010400|x`) and
   every codepoint up to U+10FFFF except surrogates, run **our own matcher with partial matching**,
   which is withheld from the screen, on the one-character subject; every codepoint that gives a
   match or a partial match must have its first UTF-16 unit in `LeadingUnits`. Also assert the unit
   set is no bigger than the exact set plus the `Specials` superset, so it cannot silently grow to
   "everything". Upstream's answers for the same sets, from `regex.match(p, chr(cp), partial=True)`
   on 2026-10-11, agree with the table in section 4 (`(?fi)i|x`: `I X i x U+0130`;
   `(?fi)ᾀ|x`: `X x U+1F00 U+1F08 U+1F80 U+1F88`; `(?i)\U00010400|x`: `X x U+10400 U+10428`).
2. **A field test**: `LeadingUnits` is set for `(?fi)straße|café` and null for fuzzy, reversed,
   `LOCALE`, lone-surrogate, lookbehind-headed and `\G`-headed patterns (fails before the change: the
   field does not exist).
3. **Witness rows**, each pinned to upstream's answer (regex 2026.9.10, `search`, 2026-10-11):

   | Pattern | Subject | Upstream |
   |---|---|---|
   | `(?fi)straße\|café` | `ﬅraße` | (0, 5) |
   | `(?fi)straße\|café` | `xſtrasse` | (1, 8) |
   | `(?fi)straße\|café` | `Größe STRASSE` | (6, 13) |
   | `(?fi)straße\|café` | `Größe` | None |
   | `(?fi)straße\|café` | `xxCAFÉ` | (2, 6) |
   | `(?ai)straße\|café` | `xſtraße` | None |
   | `(?i)straße\|café` | `xſtraße` | (1, 7) |
   | `(?fi)ssx\|q` | `aßx` | (1, 3) |
   | `(?fi)ssx\|q` | `aẞx` | (1, 3) |
   | `(?i)k\|q` | `a` + U+212A | (1, 2) |
   | `(?fi)i̇x\|q` (i + U+0307) | `aİx` | None |
   | `(?fi)ᾀ\|x` | `aἀι` | (1, 3) |
   | `(?i)\U00010400x\|y` | `a\U00010428x` | (1, 3) |
   | `(?i)\U00010428a\|b` | `\U00010400a` | (0, 2) |
   | `(?fi)(straße)\|(café)` | `..CafÉ` | (2, 6), group 2 |
   | `(?fi)(?:a\|b)c\|d` | `xxBC` | (2, 4) |

   Plus the guards: a partial search, a reverse search, a `(*SKIP)` pattern and a fuzzy pattern
   over the same subjects, each equal to main's answer.
4. **Differential against main** (`tools/probes/port-probe.cs`, never `--no-build`), at least 50,000
   generated rows biased to the shape: alternations of 2-20 literals under `(?i)`, `(?fi)`, `(?ai)`,
   with capture groups and nested branches at the head, heads drawn from the special characters
   (`ß ẞ ſ ﬅ ﬆ K İ ı µ ᾀ ι` and Deseret), subjects mixing those with ASCII and accented text; every
   API (`search`, `match`, `fullmatch`, `findall`, `finditer`, `sub`, `split`), with `pos`/`endpos`,
   overlapping, `(*PRUNE)`, `(*SKIP)`, partial and reverse variants. Zero differences required.
5. **Oracle at three seeds**, ratchet, AOT tests and smoke, as the optimise skill lists. Benchmarks:
   `CaseFoldAccented` before and after, plus an adversarial row (`(?fi)straße|café` over lines of
   `sScC`) to confirm the 5-10% ceiling, in both JIT and Native AOT.

## Recommendation

Take option A: generalise the leading-literal walk to case-insensitive and full-folding literal
heads, build one `SearchValues<char>` from the existing `RequiredStringScreen` rules (first unit only,
high surrogate for astral characters), and jump with `IndexOfAny` at the existing call site
`Matcher.cs:12812`. It reuses tested folding rules, touches no ported code, and is estimated
(unmeasured) to take this row from about 1.37x to about 0.4-0.5x of Python. Keep the prolog fix as a
separate slice; it removes a per-call cost, which this does not.
