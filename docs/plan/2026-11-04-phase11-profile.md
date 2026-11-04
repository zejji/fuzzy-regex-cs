# Phase 11 profile: where 1.2.0's time goes (2026-11-04)

Point-in-time record for release 1.3 (phase 11). The checklist lives in `STATE.md`.

## How it was measured

WPR CPU traces (`FuzzyRegexWprStart`/`Stop`), one trace per workload holding two processes: main
(1.2.0 candidate, 04913bf5) and the D82 baseline (2373e273, worktree `d82base`). Each side runs one
workload for 5-6 s after 2 s of warm-up, Release build. The harness also reports time and bytes
allocated per call (`GC.GetAllocatedBytesForCurrentThread`). The workloads copy the BDN rows'
shapes. A peer session ran builds during some traces, so read the times to about 10%; the sample
shares within one process do not depend on that.

| Workload (BDN row) | main ns/call | base ns/call | main B/call | base B/call |
|---|---|---|---|---|
| compile `(quick\|slow)\s+(?<colour>[a-z]+)\s+fox` + Match (`UncachedMatch`) | 14,738 | 9,763 | 36,744 | 31,816 |
| compile `(?:needle){e<=1}` + IsMatch (`UncachedFuzzyIsMatch`) | 26,605 | 25,456 | 16,232 | 11,240 |
| compile 300-way alternation (`CompileLargePattern`) | 336,335 | 330,004 | 1,045,480 | 962,816 |
| `\w+` `Matches(1 MB)[1]` (`MatchesFirstTwo`) | 756 | n/a (eager walk then) | 3,032 | n/a |

## Findings

1. **Fuzzy compile is mostly exception handling (D139).** `RegexBase.GetFirstset` throws
   `FirstSetErrorException` for any node with no first set (upstream raises `_FirstSetError`), and
   the compiler catches it. In the fuzzy compile trace `System.Runtime.EH.DispatchEx` and the
   runtime's unwinder take about 70% of samples on both main and base. Every fuzzy pattern and
   every pattern opening with something like `.*` pays it on each compile.
2. **Leading global inline flags throw too (D140).** `(?e)`, `(?b)`, `(?r)`, `(?V1)` at the start
   make `ParseFlagsSubpattern` throw `UnscopedFlagSetException` and the parse restart
   (upstream's design). One throw per compile. Common in fuzzy patterns (`(?e)`, `(?b)`). Not yet
   timed separately.
3. **`FillAsciiAnswers` is 18.6% of the ordinary compile** (S62, 3f4f1da0): every class, property
   and case-insensitive one-character node asks its predicate about 128 codepoints when the
   pattern is compiled. It is most of the 1.5x compile regression since the D82 baseline; the
   rest is spread over `FindLeadingStrings` (4.8%), `BranchChain` and the S60b compile-time work.
4. **An abandoned `MatchCollection` builds a new `MatchState` every call (D138 and more).** The lazy
   collection holds its walk's state until the walk ends, so `Matches(s)[1]`, `.First()` or any
   partial read never returns it to the pattern's cache. Measured on main: `Matches(s)[1]` 653 ns
   and 3,032 B; the same two matches through `EnumerateMatches` with a `break` (which disposes,
   so the state is reused) 301 ns and 416 B. `MatchState..ctor` is 25% of the call; it allocates
   three `ByteStack`s, about 16 empty lists and the 146-field object.
5. **Compile allocation:** fuzzy compile +5 KB against base (D29), large pattern +83 KB (8%).

## Ranking, by users touched x measured gain / cost

| Order | Item | Who gains | Gain (measured or bounded) | Cost and risk |
|---|---|---|---|---|
| 1 | D139 first set without an exception | every fuzzy compile; `.*`-led patterns | up to ~70% of fuzzy compile time | small, parse-time only; compiled code must be identical |
| 2 | D140 unscoped global flag without an exception | patterns opening with a global flag | one exception per compile (to time) | parser restart is deep recursion; size first |
| 3 | `FillAsciiAnswers` cheaper or lazy | every uncached compile with a class | up to ~18% of compile | small-medium; matcher reads the bits |
| 4 | Cheaper fresh `MatchState` (lazy lists, D138) | partial `Matches` reads, first call, concurrent calls | up to ~350 ns and 2.6 KB per call | medium; MatchStateCacheTests guards field resets |
| 5 | D29, D135 compile allocation | uncached compiles | 5 KB fuzzy, 0.3 KB lookaround | small each |
| 6 | Matching hot path (D68, S60b-S63 leftovers) | fuzzy and group-call matching | needs its own profile | large |

Items 1-5 are compile- or per-call-cost work that a cached pattern does not pay; the static API
caches compiled patterns, so they matter most to callers who build patterns per request and to
`Matches` users who read only the first few matches. Item 6 is where long matching time goes and
needs its own profile of the fuzzy rows.
