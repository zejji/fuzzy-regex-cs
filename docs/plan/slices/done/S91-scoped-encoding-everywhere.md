---
slice: S91
phase: 7
title: A scoped (?a:...) or (?u:...) answers exactly as the same encoding set for the whole pattern
delivers: []
---

# S91 - Scoped encodings reach every node that depends on the encoding

STATE finding 0 (owner rule 2026-09-25: before any optimisation). Only property nodes and `\b`
carry the encoding of the scope they were parsed in. Every other node that depends on the encoding
uses the PATTERN's, so a scoped `(?a:...)` or `(?u:...)` is ignored for it.

## The rule and why

A scoped encoding must give exactly what the same encoding gives as a global flag. Evidence, in
order of weight:

1. **Upstream contradicts itself.** `(?ai)k` refuses the Kelvin sign U+212A; `(?i)(?a:k)` accepts
   it. The regex README lists `(?a)` among the scoped flags, with no carve-out.
2. **CPython `re`** (docs fetched 2026-09-25, 3.14.7): scoped `(?a:...)` "switches to ASCII-only
   matching ... only in effect for the narrow inline group"; with IGNORECASE and ASCII, `[a-z]`
   matches "only letters 'a' to 'z' and 'A' to 'Z'". CPython 3.14.7 refuses U+212A for
   `(?i)(?a:k)`, `(?a:(?i:k))`, `(?i)(?a:[a-z])` and accepts it for `(?ai)(?u:k)`.
3. **Perl 5.42**: `(?i)(?aa:k)`, `(?aa:(?i:k))`, `(?i)(?aa:[a-z])` refuse U+212A exactly as
   `(?aai)k` does, and `(?aai)(?u:k)` accepts it as `(?i)k` does.
4. .NET's `Regex` has no ASCII case mode, so it says nothing either way.

## Measured scope (2026-09-25, regex 2026.9.10)

A metamorphic grid (`tools/probes/s91-scoped-encoding-grid.py`, to be committed with the slice):
42 atoms x 16 scoped/global form pairs x 31 subjects x V0/V1 = 40,672 rows, each comparing the
scoped spelling against its global twin. Upstream disagrees with itself on 2,207. By construct:

- case-insensitive characters, strings, ranges, sets (and the set form of a cased property),
  repeats of them, alternations compiled to sets, named lists `\L<...>`, backreferences under
  IGNORECASE, fuzzy matching of all of these, reversed matching, lookarounds holding them;
- `\m`, `\M` (upstream `StartOfWord`/`EndOfWord` carry an encoding in the code word that the
  matcher never reads), and `\b`/`\m`/`\M` under `(?w)`, whose `DEFAULT_` forms drop the scope;
- `.`, `$`, `^` under `(?w)` (the line-separator set depends on the encoding);
- `\X` (ASCII makes every position a grapheme boundary);
- FULLCASE: `(?V1)(?a)(?i:\xdf)` refuses `\xdf` and matches `ss` (to be minimised).

## Plan

1. Commit the grid as a probe; record upstream's and the port's disagreement counts.
2. Tests first, one per construct family, each seen red.
3. Carry the scope's encoding on every encoding-dependent node at parse time (the parser already
   computes it: `ParseFunctions.PropertyEncoding`), and resolve it through `Matcher.NodeEncoding`
   wherever the node folds or classifies: `MatchesCharacterIgn`, `MatchesRangeIgn`, `MatchesSetIgn`
   and `AllCases`, the string matchers and string search (simple and full folding), fuzzy matching,
   backreferences, named-list folding (`PatternCompiler.FoldCase` folds with the global flags),
   word boundaries and their `DEFAULT_` forms, line separators, grapheme boundaries, and every
   prefilter, first-set or required-string builder that folds (note S60b's unmerged prefilters in
   its slice file).
4. Grid to 0 port disagreements; oracle at three seeds; new oracle entry keyed on judged rows;
   LEDGER entry 36; DIVERGENCES.md row, COMPARISON.md heading and ComparisonSamples test.

## Closing notes (2026-09-26)

**Done in e78f8d2 and 5522bc3.** Every node whose answer depends on the encoding now carries the
one it was parsed under (`Node.Encoding`), and a scoped form answers exactly as its global twin.
Characters, strings, ranges, sets, backreferences and named lists carry a scoped encoding in their
case flags, and only when they ignore case and the scope differs from the pattern, so an unscoped
pattern compiles exactly as upstream's (`CompileParityTests`). The zero-width, dot, line and
grapheme nodes hold it as a value of their own. Nodes that differ only in encoding are never
merged; a required string whose folding needs the scope is not offered, and a firstset never folds
two encodings with one.

**Grid.** `ScopedEncodingGridTests` runs 37 atoms by 15 form pairs over 31 subjects, V0 and V1,
plus a full-folding grid of 7 atoms by 10 forms over 16 subjects. On the 40,672-row probe grid the
port's disagreements went from 1,596 to 0; upstream's stay at 2,207.

**Review.** The first blind review found two scoped forms still answering differently, both fixed
in 5522bc3: a set merged from an alternation inside `(?u:...)` chose its full-fold expansions with
the pattern's encoding (`(?aif)(?u:x|\xdf)` refused 'ss'; 154 of the review's 14,300 pairs), and a
fuzzy constraint outside a scoped backreference folded the subject with its own encoding
(`(?aif)(ss)(?u:\1){i<=1:[t]}` refused 'ss' U+FB05 's'). The second review found nothing further.
The oracle is GREEN at seeds 7, 4242 and 20260925 (agree 7462, 7475 and 7484 of 7580, diverge 0,
recheck 326 of 326). No new oracle entry: no row of the default wave differs.

**Recorded, not changed** (ledger entry 36, DECISIONS 2026-09-26). A fuzzy constraint written
outside a scoped backreference tests an inserted character with its own encoding; the form has no
global twin. A scoped `(?u:[...])` holding U+00DF in an `(?a)` pattern compiles one 'ss'
alternative where the global form has three, with equal answers.

**Docs.** Ledger entry 36, a DIVERGENCES.md row, the COMPARISON.md heading "A scoped `(?a:...)`
or `(?u:...)` answers exactly as the same encoding set for the whole pattern" and its
`ComparisonSamples` test.

**Left open.** Plan step 1's probe, `tools/probes/s91-scoped-encoding-grid.py`, is cited by
`ScopedEncodingGridTests` but was not committed.
