# D163: compile cost per call, options

Design note, 2026-11-18. Checklist item 15 (D163). Evidence: the D163 row of `docs/KNOWN-DEFECTS.md`,
`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-18-main-vs-2026-09-27-baseline.md`.

## The problem

Every benchmark row that compiles a pattern per call is slower than the 2026-09-27 baseline, while every
matching row is faster. `UncachedIsMatch` (`new FuzzyRegex(@"(quick|slow)\s+(?<colour>[a-z]+)\s+fox").IsMatch(short)`)
went from 8,689 to 10,020 ns (1.15x) and from 31,120 to 34,816 B. A user who caches the pattern never
sees this; one who builds a pattern per call, which the static API does on a cache miss, pays it each time.

## Where the cost went

- **Bytes**, bisected over first-parent main: two steps. The D11 merge (65265cfd) added about 1,100 B of
  compile-time nodes. S60b (06616c61) added about 2,900 B, mostly a `SearchValues<string>` (Teddy) and a
  `string[][]` that `PatternObject.FindLeadingStrings` builds for the leading `quick|slow`; S62 later took
  back about 1,250 B elsewhere.
- **Time**, one WPR profile on main (indicative, 8,876 samples): the constructor is 80% of the call and
  spread thin. Two costs added since the baseline have names: `FindLeadingStrings` 4.2% inclusive, and
  `Matcher.FillAsciiAnswers` 5.8% (S62, refined by D141: every node's ASCII answer table, filled last in
  `PatternObject.Compile`). Together about 10% of a call, most of the 15% rise.

## Options

1. **Build the leading strings and the ASCII answers lazily**: the leading strings at the first search that
   reads them; the ASCII answers for the whole node list at the first match, behind one check per match
   (never per node: the tables are read on the matching hot path). Thread-safe publish
   (`Interlocked.CompareExchange`; a race builds twice and keeps one).
   - For: no compile cost for a pattern that is never searched, or searched only where the screen is not
     used (a match at the start, a short subject). Behaviour unchanged: the same values, built later.
   - Against: the first search pays instead, so an uncached `Search` that does use the screen gains
     nothing; one check per search or match; a racy first use allocates twice.
2. **Gate the Teddy build on the work it can save**: build `SearchValues<string>` only when the
   alternation is large enough that `IndexOfAny(char)` on the first characters would be much weaker.
   - For: removes the most expensive single allocation outright for small alternations like this one.
   - Against: a threshold to choose and justify by measurement; a search that would have used Teddy on
     a long subject may get slower.
3. **Leave it** and document that compiling per call costs about 10 us; recommend caching.
   - For: no code. The static API's cache already hides it for repeated patterns.
   - Against: the regression is real and the benchmark rows stay red against the baseline.

## Recommendation

Laziness alone does not help the rows that regressed: each of them searches at once, its subject does not
match at position 0, so the search uses both the leading-string screen and the ASCII tables, and the first
search would pay what the compile paid. What would help is option 1 with a size gate: build each structure
lazily, and only when a search's subject is long enough for it to repay its build; a shorter subject takes
the plain path that already exists (the screen is an optimisation over trying each position; the tables
are an optimisation over `Matcher.MatchesSet`). The threshold comes from measurement: the build cost of
each structure against what it saves per character, on this pattern and on the search rows that rely on
the screen. Those search rows must not get slower beyond noise, and a cached pattern pays the build once
either way.

Before building: measure the two build costs alone (Release, pinned, a quiet window) and the per-character
saving of each, to see whether a threshold exists that recovers most of the 15% without touching the
search rows. If none does, option 3. The D11 node growth is separate: look at which nodes D11 adds to a
pattern without partial matching, and whether they can be built only when a partial match is asked for.
