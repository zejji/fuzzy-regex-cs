<!--
  Draft of the GitHub release text for v1.2.0, for the owner's review. Sourced from the 1.2.0
  entry in CHANGELOG.md and the open rows of docs/KNOWN-DEFECTS.md on main. Every example was run
  on 2026-11-02 against v1.1.0 (8de8eaf8) and main (de4bbc84), Release builds. Performance figures:
  bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-11-04-v1.2.0-vs-v1.1.0.md. The release
  workflow (.github/workflows/release.yml) publishes everything after the first line that is
  exactly `---` as the GitHub release text, so this comment stays out.
-->

---

# FuzzyRegex 1.2.0

FuzzyRegex is a .NET port of [mrab-regex](https://github.com/mrabarnett/mrab-regex), the Python
`regex` module, with fuzzy matching as its headline feature. This is a correctness release: it
fixes wrong answers in fuzzy sections nested inside one another, in partial matching, in a
search through a conditional inside a repeat, and in `LastGroupNumber`. Two of these bugs are in
upstream too. The public API is unchanged from 1.1.0.

```console
dotnet add package FuzzyRegex --version 1.2.0
```

## What is fixed

- **Errors made inside an atomic group, a possessive repeat, a verb or a group call count against
  every fuzzy section around it as soon as they are made.** The limits of an enclosing section
  used to be checked only where that section ended, so a committing construct could commit to a
  path the enclosing section forbids, and the match was lost. Python `regex` behaves the old way.
  Patterns whose fuzzy sections nest also run faster now, because hopeless paths stop early.
- **A search through a conditional inside a repeat finds the leftmost match** where a later part
  of the pattern reads a group: `(a?)(?(?=c)c|[ab])*\1` over 'ac' is (0, 2), not (0, 1).
- **`LastGroupNumber` and `LastGroupName` name the group that closed last** when a group is
  called with another direction or fuzziness: `(?P<g1>x|y(?P>g1)y)(?<=(?P>g1))(a)(b)` over 'cxab'
  reports group 3, not group 1. Python `regex` answers 1.
- **Partial matching no longer reports a partial match that no further text could complete**
  in these cases:
  - an insertion made where the text runs out, inside fuzzy sections that nest:
    `(?:(?:f^){i<=2}){i<=1}` over 'fx' is no match, not a partial at (0, 2);
  - a group call inside a negative lookaround or a conditional's negative test:
    `(x)(?!y?(?1))\Z` over 'xy' is no match, not a partial at (0, 2);
  - a repeated lookahead inside a capture group, read by a later conditional:
    `((?=(?:ab|a)*(b?)a?)a)+(?(2)c|d)` over nine 'a's and a 'b', searched, is a partial at
    (10, 10), not (0, 10).
- **A partial call and a plain call agree on complete matches**: `(?:(?=(?:ab){e<=2})){d<=1}`
  over 'b' is a complete match at (0, 0) both ways. Before, the plain call found no match.

## Performance

Matching speed is unchanged within about 1%. Across the 91 benchmarks of the suite, timed against
1.1.0 on the same machine in alternating passes, the middle ratio is 1.014, and no benchmark is
slower beyond run-to-run noise by more than 6%. The slowest are short calls, where the extra
checks the fixes add weigh most: a cached static `IsMatch` takes 296 ns instead of 286 ns.

Two costs in allocation:

- Compiling a pattern that has both a lookaround and a capture group allocates 7-12% more than
  in 1.1.0, because of the partial matching fix for repeated lookaheads. Other patterns allocate
  200-300 bytes more per compile.
- Each `Matches` call allocates about 240 bytes more, once per call and not per match. `Match`
  and `IsMatch` allocate the same as in 1.1.0.

Release 1.3 is planned to remove both.

## Known limitations

The limitations listed for 1.0.0 still apply, except where fixed above. Open defects are in
[KNOWN-DEFECTS.md](https://github.com/zejji/fuzzy-regex-cs/blob/v1.2.0/docs/KNOWN-DEFECTS.md).
The ones that can give a wrong answer are all rare combinations:

- **Partial matching can answer a partial at the attempt's start** where a later partial is
  right, when the work it needs runs into the per-attempt budget: a `\X` inside nested repeats
  in a negative lookahead that holds a recursion (D126), the repeated-lookahead pattern above
  wrapped in an atomic group (D134), and two shapes of group call (a recursion that reaches a
  verb, and a recursive call in a fuzzy pattern; D101). Python `regex` gives the same answers
  for D126 and D134.
- **Inside a conditional's test, an error is checked only against the fuzzy sections the test
  opened** (D136), as in Python `regex`. Changing it would change which branch runs, so it waits
  for a ruling.

## Reporting a bug

Open an issue at <https://github.com/zejji/fuzzy-regex-cs/issues> with the pattern, the subject,
the flags and the method you called (with `partial: true` or not), what you got and what you
expected. If you know what Python's `regex` answers for the same call, include it.
