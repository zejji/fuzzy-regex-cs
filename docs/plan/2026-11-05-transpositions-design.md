# Transpositions as a fuzzy error kind: proposal and open questions

**Status:** proposal, parked for a release after 1.3. No release assigned and no work started.
Owner decisions of 2026-11-05 are marked **Decided**; everything else is a proposal or an open
question. Recorded in `DECISIONS.md` and `ROADMAP.md` ("Candidates parked for later") the same day.

## 1. Summary

Add a fourth fuzzy error kind, the **transposition** (two adjacent characters swapped, "TECSO" for
"TESCO"), counted as one error. It is off by default, so every existing pattern keeps its meaning
and its upstream answer. It is turned on in either of two ways:

- **Decided:** a new `FuzzyRegexOptions` flag, which lets the generic error budget `e` count a swap
  as one error everywhere in the pattern. With the flag, `(?:tesco){e<=1}` matches "tecso".
- **Decided:** an explicit `t` in a fuzzy constraint, such as `{t<=1}` or `{1i+1d+1s+1t<=2}`,
  which allows swaps in that section with or without the flag.
- **Decided:** with neither, behaviour is exactly today's: a swap costs two errors (two
  substitutions, or a deletion plus an insertion). The one exception is a pattern holding a
  literal brace group that starts with `t`, such as `{t<=1}`, which becomes a constraint; hence
  release 2.0.0 (10.13).

This is the first addition of pattern syntax that upstream lacks. Every earlier difference from
upstream was API shape, speed or a fixed bug (`docs/DIVERGENCES.md`; `AGENTS.md:129-131`, "do not
'improve'"). That is the main cost of the feature, more than the engine work.

## 2. Why

A user matching hand-typed names against reference text found that `(?:tesco){e<=1}` misses
"TECSO" because the swap costs 2. The only workarounds today are looser (`{e<=2}` also accepts "DISCO") or need the caller
to generate swapped variants (section 9).

How common swaps are among typing errors:

- Damerau (1964), *A technique for computer detection and correction of spelling errors*, CACM
  7(3):171-176, read 2026-11-06 from the ACM PDF (`.scratch/papers/damerau1964.pdf`). The
  population was the index terms a coordinate indexing and retrieval system rejected because they
  were not on its master list of acceptable terms. Of those rejected for spelling errors, "over 80
  percent fell into one of four classes of single error--one letter was wrong, or one letter was
  missing, or an extra letter had been inserted, or two adjacent characters had been transposed"
  (p. 171). The paper gives no count for that inspection. Its larger test, Table 4 (p. 176), had
  964 spelling errors, "most of the errors were the result of equipment malfunction": wrong letter
  567, missing letter 153, transposed 23, extra letter 99, multiple 122. So in the one sample the
  paper counts, transpositions are 23 of 964 errors (2.4%) and 23 of the 842 single errors (2.7%).
  The correction rule for a transposition applies only to words of equal length that "differ in
  two adjacent positions": "these two characters of the entry word are interchanged" (pp. 172-174).
  The program skipped words of three characters or fewer (p. 172).
- Pollock & Zamora. The 1984 paper, *Automatic spelling correction in scientific and scholarly
  text*, CACM 27(4):358-368, was read 2026-11-06 (`.scratch/papers/10.1145_358027.358048.pdf`).
  Its population is over 50,000 misspellings drawn from about 25,000,000 words of seven scientific
  and scholarly databases, using a 40,000-word dictionary (p. 358). It says that "90-95 percent of
  misspellings in raw keyboarding typically contain only one error" (p. 358); that substitutions
  "ordinarily account for less than 20 percent of keyboarded misspellings" (p. 358); that by
  frequency of error operation "OMISSION > INSERTION >> SUBSTITUTION" (p. 363); and that an
  insertion "is approximately twice as frequent as" a transposition (p. 363). It defines a
  transposition as "two adjacent characters in the string are interchanged" (p. 361). It cites
  all of these figures to its companion paper [9], the 1983 JASIS paper below, and gives no
  per-kind table of its own.
- Pollock & Zamora (1983), *Collection and characterization of spelling errors in scientific and
  scholarly text*, JASIS 34(1):51-58. The full text is behind Wiley's paywall (the PDF answered
  403, 2026-11-06); the abstract and the paper's figures were read. The abstract gives over 50,000
  misspellings from about 25,000,000 words, an incidence of 0.2%, "90-95% of spelling errors have
  only a single mistake", "omission is the commonest type" and "transposition is heterogeneous".
  The per-kind split is the paper's own Fig. 4, "Distribution of spelling errors" (image from
  Semantic Scholar's figure extraction, `.scratch/papers/pz1983/4-Figure4-1.png`, read
  2026-11-06): of all 52,963 misspellings, omissions 34%, insertions 27%, substitutions 19%,
  transpositions 12.5% and more than one error 7.5%. The shares sum to 100%, so they are shares of
  all misspellings, single errors 92.5% of them. Transpositions per database run 10-15% (CA 13%,
  ACS 13%, ACS-2 15%, DOLE 11.5%, BA 15%, CIN 12.5%, ISA 10%), apart from PI's 40% on only 80
  misspellings. The 1984 paper agrees where it overlaps: substitutions under 20%, and insertions
  (27%) about twice transpositions (12.5%). Damerau's 2.4% (above) comes from a small sample
  dominated by equipment faults, not keyboarding.
- Rates vary by language and input method (2.6% and 13.1% in two Sindhi corpora,
  https://arxiv.org/pdf/1403.4759). The figures describe human typing, not machine-generated text.

## 3. Terms

- **Transposition (swap):** the text has two adjacent characters in the opposite order to two
  adjacent pattern items: pattern "ab", text "ba".
- **Levenshtein distance:** the fewest insertions, deletions and substitutions that turn one string
  into another. It is what upstream's `e` counts, and a swap costs 2 in it.
- **Damerau-Levenshtein distance:** Levenshtein plus the swap of two adjacent characters at cost 1.
  The name covers two different distances:
  - **Restricted transposition, or optimal string alignment (OSA):** no character takes part in
    more than one edit, so "CA" to "ABC" costs 3 (delete C, insert B, insert C).
  - **Unrestricted Damerau-Levenshtein** (Lowrance & Wagner 1975) lets characters be inserted
    between a swapped pair, or deleted from between two characters that then swap, so the same
    costs 2 (swap to "AC", insert B between). Section 10.1 gives their cost model from the paper.
  Measured 2026-11-06 with RapidFuzz 3.14.6: `OSA.distance("CA","ABC")` = 3,
  `DamerauLevenshtein.distance("CA","ABC")` = 2
  (https://en.wikipedia.org/wiki/Damerau%E2%80%93Levenshtein_distance;
  https://rapidfuzz.github.io/RapidFuzz/Usage/distance/OSA.html).
- **Navarro 2001:** G. Navarro, *A guided tour to approximate string matching*, ACM Computing
  Surveys 33(1):31-88, read 2026-11-06 from the journal PDF (`.scratch/papers/navarro2001.pdf`).
  Page numbers below are the journal's printed ones: the transposition definition is on p. 37
  (section 3.1) and the remark on filters and transpositions on p. 64 (section 8.2.3).
- **Wagner & Fischer 1974**, *The string-to-string correction problem*, JACM 21(1):168-173, read
  2026-11-06: the base algorithm, with three operations (change, delete, insert), "based on three
  of Morgan's operations" (p. 168).

## 4. Prior art

Checked 2026-11-06. "Read" means the source or documentation was read; "measured" means it was run
here. Nothing was taken from memory.

| Library | Swaps? | Evidence |
|---|---|---|
| Lucene `FuzzyQuery` | Yes, on by default, OSA, at most 2 edits | Read from source, `lucene/core/.../search/FuzzyQuery.java` on `main`: `public static final boolean defaultTranspositions = true;` (about line 57); class javadoc "based on the Damerau-Levenshtein (optimal string alignment) algorithm"; `transpositions` false gives "the classic Levenshtein algorithm". `LevenshteinAutomata.MAXIMUM_SUPPORTED_DISTANCE = 2`. Not run. |
| Elasticsearch | Yes, on by default | Read: fuzzy query `transpositions` and match query `fuzzy_transpositions` both "Defaults to `true`" (https://www.elastic.co/docs/reference/query-languages/query-dsl/query-dsl-fuzzy-query, `.../query-dsl-match-query`). Not run. |
| Apache Commons Text | Yes (OSA), separate class | Read from source: `DamerauLevenshteinDistance` (`@since 1.15.0`; 1.15.0 reached Maven Central on 2025-12-04) links to Wikipedia's OSA section and its `unlimitedCompare` javadoc says "using optimal string alignment". `LevenshteinDistance` has no swaps. Not run. |
| RapidFuzz 3.14.6 | Both, as two functions | Measured: `OSA` and `DamerauLevenshtein` (unrestricted) give 3 and 2 for "CA" to "ABC". Neither has an `editops` method (measured `hasattr` false), so both return a number, never which edits. |
| jellyfish 1.3.0 | Unrestricted only | Measured: `damerau_levenshtein_distance("CA","ABC")` = 2. No OSA function in the module. |
| fuzzy-regex-rs v0.2.0 (Rust) | Yes, on by default, OSA, only within one run of literal text | Measured: built and run in `rust:1.88-slim` (`.scratch/research/frr/run.log`, `.scratch/frr-probe/run.log`). Swaps are allowed inside `e`: `(?:tesco){e<=1}` matches "tecso". The default answers like upstream's `(?b)`, not leftmost-first: `(?:abc){e<=1}` in "xbc abc" gives the exact (4,7). `fuzzy_counts()` omits swaps ((0,0,0) for a one-swap match); they appear only in `edits().swaps` and the total. `{t<=1}` alone also allows a deletion ("tesc") and a substitution ("texco"). A capped `t` missing from a cost equation is not free: `{t<=1,1i+1d+1s<=1}` refuses "tecso", against its own source comment; `{1i+1d+1s+1t<=1}` also refuses it, while `<=2` accepts. Test sets are ignored for swaps. `{1<=t<=1}` is an error ("range constraint only valid for 'e'"); `(?r)` is a parse error. No swap across a class, a repeat edge or a group edge (`\d-\d` "-12", `[a-c][x-z]` "xa", `tes(co)` "tecso", `(a)(b)` "ba"); a swap inside a capture group works (`te(sc)o`). Apparent defects: `(?:abc\|tesco){t<=1}` refuses "tecso" although `(?:ab\|cd){t<=1}` swaps "ba", and `(?:\w+ jumps over){e<=1}` refuses "the fox jumsp over". Read from source: `mrab_compat` turns swaps off (`src/api/builder.rs:165-173`); swaps live only in the literal automata (`src/engine/fuzzy_bridge.rs:117-210`). |
| NR-grep 1.1.2 (Navarro) | Yes, on by default (`-k<n>`; `-k<n>[idst]` picks kinds) | Measured: built in `gcc:14` and run (`.scratch/research/nrgrep/probe.log`, `probe2.log`). `tesco` `-k1t` matches "tecso" and refuses "tesc" and "texco" (`t` alone means swaps only). With `-x` (whole line) a swap crosses classes (`[0-9]-[0-9]` "-12", `[a-c][x-z]` "xa"), `.` (`a.` "Xa") and an alternation edge (`x(y\|z)` "yx"), but not inside literal alternatives (`(ab\|cd)` "ba", `(abc\|tesco)` "tecso": no match in either mode). At a repeat edge only in substring mode (`a+b` "ba", `ab+` "ba": match without `-x`, none with it). `-i` works with `t` on ASCII. No test sets, no partial matching, no error counts, no capture groups. |
| liblevenshtein 4.0.0-rc.5 (Rust) | Yes, `Algorithm::Transposition` (OSA); unrestricted refused for regexes | Measured: the crates.io release built and run in `rust:1.95-slim` (`.scratch/research/liblev/run.log`, `run2.log`); the GitHub master does not build alone (sibling path dependencies). A whole-string acceptor that reports a distance only: no search, spans, partial matching or test sets. A swap counts once across classes (`[a-c][x-z]` "xa" = 1), repeat edges (`ab+` "ba" = 1), alternation (`(abc\|tesco)` "tecso" = 1) and capture groups (`(a)(b)` "ba" = 1); "ca" against "abc" exceeds a budget of 2, so the form is OSA. |
| FREJ (Java) | Yes, OSA recurrence | Read from source: `Fuzzy.java:31-36` names "swap of two adjacent chars" as one of four mistakes, `:289-291` is the OSA step. It compares one pattern word with one input token; its regex tokens match exactly. Case by `Character.toUpperCase`, one character to one. Not run. |
| uFuzzy (npm) | Yes, at most one error per term | README read: `intraMode: 1` allows one substitution, transposition, insertion or deletion per literal term, each switchable (`intraTrn`); always case-insensitive. Not run. |
| fuzzy-aho-corasick 0.5.2 (Rust) | Yes, literal patterns only | Read from source: insertions, deletions, substitutions and swaps with per-pattern caps; case-insensitive mode lower-cases per grapheme; its bit-parallel prefilter charges a swap as 2 (`src/search.rs:509-552`). Not run. |
| IOCCC 2011 "fredriksson" (C) | Yes, `-t` | README read: "'ab' matches 'ba' with one transposition"; patterns of literals, `.` and classes. The source is obfuscated and was not run, so how a swap meets a class is not known. |
| mrab-regex (upstream) | No | Issue #400, "Fuzzy matching should support for Damerau-Levenshtein distance", migrated from Bitbucket by mrabarnett on 2021-01-28, labelled enhancement and major; open, no comments, 3 thumbs-up (read 2026-11-06; triaged A in `2026-08-31-upstream-issue-triage.md:38`). Measured with `regex` 2026.9.29: `{t<=1}` is literal text (section 10.13). |
| TRE | No | Read from the local source (`.venvs/tre-src`, commit `71bfcaf0`, 2026-05-23): `include/tre/tre.h:238-243` has costs and maxima for insertion, deletion and substitution only, and `TODO:24` lists "Transposition operation for the approximate matcher?" |
| Hyperscan 5.4.1 | No | Read: "three possible edit types considered: insertion, removal and substitution" (https://intel.github.io/hyperscan/dev-reference/compilation.html). |
| PostgreSQL 18 `fuzzystrmatch` | No | Read: `levenshtein` takes insertion, deletion and substitution costs; no transposition function in the module. |
| Python `fuzzysearch` | No | Read: README names Levenshtein distance, substitutions, insertions and deletions only. |
| Perl `String::Approx` 3.28 | No | Read: its notes say a transpose needs an edit distance of two. |
| agrep 3.41 (Wu-Manber) | No | Read from source (Wikinaut mirror): costs for deletion, insertion and substitution only (`agrephlp.c:129-132`); the one "transposition" in `bitap.c` means lower-casing. |
| ugrep 7.8.5 `-Z` | No | Measured: `-Z1 tesco` misses "tecso", `-Z2` finds it; `--help fuzzy` names insertions, deletions and substitutions. |

Among pattern matchers, swaps exist in fuzzy-regex-rs, NR-grep and liblevenshtein (measured) and
in FREJ, uFuzzy, fuzzy-aho-corasick and the IOCCC entry (read). Every one whose form could be
checked uses the restricted form (OSA), and the regex ones turn swaps on by default. They differ
most on scope: fuzzy-regex-rs swaps only inside one run of literal text, NR-grep across classes
and alternation edges, liblevenshtein across everything. Only fuzzy-regex-rs was found with test
sets, and it ignores them for swaps. Distance libraries offer both forms
and return a number without an explanation. Other engines checked and found without swaps (TRE
bindings, prex, ZGorlock FuzzyRegex, fuzzysearch, Vespa and more) are listed in
`.scratch/research/AGENT-FINDINGS.md`, task 1.

## 5. Semantics

### 5.1 What counts as a swap (proposal)

A swap matches two consecutive single-character pattern items against two text characters in the
opposite order: text[p] matches item[k+1] and text[p+1] matches item[k]. It costs one error and
advances past both items and both characters. Each swapped character is used by that edit only,
so the engine is restricted (OSA) by construction. Unrestricted Damerau-Levenshtein would need the
engine to remember earlier edits and is not proposed.

A first version allows swaps **only inside a literal as written**: two adjacent literal characters
of the pattern, even where the compiler splits the literal into several nodes (under version 1 a
case-insensitive literal is split at "st", "ss", "fi", "ff" and "fl", question 10.10). Swaps
involving a class, a repeat edge, a group edge, an alternation edge, `END_FUZZY` or a call are
refused in the first version (10.10). Under full case folding a swap is tried only when both
swapped characters fold to one character each (10.11).

### 5.2 How `t` combines with the other kinds (proposal)

Today, naming any of `i`, `d`, `s` sets the unnamed ones to 0, and naming none leaves them
unlimited within `e` (`Parsing/Nodes.cs:4699-4707`). Proposal:

| Constraint | Flag off | Flag on |
|---|---|---|
| `{e<=1}` | i, d, s (as today) | i, d, s, t |
| `{s<=1}` | s only | s only (naming a kind still zeroes the others, `t` included) |
| `{t<=1}` | t only | t only |
| `{e<=2,t<=1}` | **question 10.3** (by upstream's rule, t only) | same |
| `{1i+1d+1s<=2}` | i, d, s; t not allowed | t not allowed (question 10.4) |
| `{1i+1d+1s+1t<=2}` | all four, swap costs 1 | same |

A swap counts once toward `e`, toward the section's limits and toward `FuzzyCounts.Total`.

### 5.3 How the engine decides which kind of edit a change was

The owner asked this directly, and it needs care, because the answer is "whichever explanation
the search reaches first" unless a best-match flag asks for the cheapest.

How it works today:

1. The engine first tries the item exactly. Only when that fails does it try an error there.
2. It tries the kinds in a fixed order, substitution, then insertion, then deletion: "The caller
   walks `fuzzy_type` from `FuzzyValue.Sub` upwards, so substitution is preferred to insertion and
   insertion to deletion" (`Engine/Matcher.cs`, remarks on `NextFuzzyMatchItem`, upstream
   `next_fuzzy_match_item` line 10116).
3. Without `BestMatch` or `EnhanceMatch`, the first path that satisfies every constraint is the
   match. Its counts describe that path, not the cheapest explanation. "TECSO" against
   `(?:tesco){e<=2}` is reported as two substitutions because substitution is tried first.
4. `BestMatch` searches for the cheapest candidate (by cost, then fewer errors, then earliest;
   see `DoBestFuzzyMatch`'s remarks and `DECISIONS.md` 2026-09-13), and `EnhanceMatch` refines
   the first match found. Only these flags make the reported counts a minimum.
5. Even the current three kinds can be attributed differently from upstream when two explanations
   tie: `2026-09-19-oracle-divergence-fuzzy-edit-attribution.md` records a row where upstream and
   this port agree on the span and the totals but put the substitution and the insertion at
   different positions.

What a fourth kind adds:

- **Where `t` sits in the order matters.** If `t` is tried last, then under `{e<=2}` with the flag
  "TECSO" is still reported as two substitutions: a correct match, but not the explanation a user
  expects, and `FuzzyCounts.Transpositions` is 0. If `t` is tried first, a swap is preferred
  whenever it fits, which changes which match is found first for patterns that allow swaps, and
  costs a look-ahead at every failing position. Question 10.5.
- **Libraries that compute a distance** (Lucene, Commons Text, RapidFuzz) do not have this
  problem: a dynamic-programming distance is a minimum by definition. A backtracking regex engine
  reports a path. The spec has to say which it promises, per flag.
- **The cheapest match may be a shorter one, not a swap.** Measured on FuzzyRegex 1.0.0
  (2026-11-05, a scratch `dotnet run` app):

  | Pattern | Text | Options | Counts (s,i,d) | Span |
  |---|---|---|---|---|
  | `(?:tesco){e<=2}` | tecso | none | (2,0,0) | (0,5) |
  | `(?:tesco){e<=2}` | tecso | `BestMatch` | (2,0,0) | (0,5) |
  | `(?:ab){e<=2}` | ba | none | (2,0,0) | (0,2) |
  | `(?:ab){e<=2}` | ba | `BestMatch` | (0,0,1) | (0,1) |

  The last row matters: `BestMatch` already finds a one-error answer for "ba" by matching only
  "b" and counting "a" as deleted. With swaps allowed, a swap over the whole span (0,2) also costs
  one. Which of two equal-cost matches with different spans wins is a tie-break rule this spec
  must set (question 10.5, which also covers ties). The counts in the first three rows confirm that today a swap is
  reported as two substitutions, because substitution is tried first.

## 6. Public API (proposal)

- **Options flag.** Name to be decided (`Transpositions`, `TranspositionsAsOneError`,
  `DamerauEdits`). The bit values of `FuzzyRegexOptions` are upstream's `RegexFlag` values
  (`FuzzyRegexOptions.cs:7-10`) and run to `0x10000` (`Posix`), so the new flag takes a high bit
  well clear of upstream's range and gets a `DIVERGENCES.md` row. No inline flag letter in the
  first version: a letter could clash with a future upstream one, and `t` already covers
  in-pattern control.
- **Syntax.** `t` joins `i`, `d`, `s` in `ParseConstraint` (`Parsing/ParseFunctions.cs:832`) and
  `ParseCostTerm` (`:906-913`), and in `SpecificErrorTypes` (`Parsing/Nodes.cs:4586`, today
  `"dis"`). **Corrected 2026-11-06:** today `t` is not a parse error. A brace group that does not
  parse as a quantifier or a constraint is literal text (`Parsing/ParseFunctions.cs:398-427`, the
  `ParseErrorException` caught at `:671`), so `(?:ab){t<=1}` matches the eight characters
  "ab{t<=1}". Measured 2026-11-06 on upstream (`regex` 2026.9.29, span (0, 8)) and on this port
  (span (0,8); `a{t}` matches "a{t}" too; `.scratch/research/port/port.log`). Recognising `t`
  changes the meaning of such patterns (question 10.13).
- **Defaults.** Adding `t` to `SpecificErrorTypes` as it stands would be wrong: when a constraint
  names none of the specific kinds, the fallback at `Parsing/Nodes.cs:4699-4707` makes every
  unnamed kind unlimited, so `{e<=2}` would allow swaps with the flag off. Part of the flag's
  implementation is therefore this rule: when nothing is named, an unnamed `t` is unlimited only
  when the flag is set, and 0 otherwise (questions 10.3, 10.4).
- **Counts.** `FuzzyCounts` is a positional record struct `(Substitutions, Insertions, Deletions)`
  (`FuzzyCounts.cs:28-32`) and the README deconstructs it as `(int s, int i, int d)`. A fourth
  positional parameter would break that, so add a non-positional
  `int Transpositions { get; init; }` and include it in `Total`. `FuzzyChanges` (`FuzzyCounts.cs:59-63`)
  gains `IReadOnlyList<int> Transpositions`, holding the subject index of the first swapped
  character. Check both against package validation (`PackageValidationBaselineVersion`).
- **Naming.** The plural noun matches `Substitutions`, `Insertions`, `Deletions`.

## 7. Engine design surface

From a read of the code on 2026-11-05 (main `e1ec5af4`). Not a design; it lists what a design must
cover.

- Error kinds are fixed at three: `FuzzyValue.Sub/Ins/Del` = 0-2, `Err` = 3, `Count` = 3
  (`Engine/Node.cs:466-481`). The values are packed blocks of four, one slot per kind plus `Err`:
  `MinBase` = 1, `MaxBase` = 5, `CostBase` = 9 (`Node.cs:457-531`, upstream `RE_FUZZY_VAL_*`).
  There is no free slot, so a fourth kind either renumbers every base or adds a separate block.
  Question 10.17.
- 283 lines in `src/` mention `FuzzyValue`, 234 of them in `Matcher.cs` (counted 2026-11-06), including the
  inline count buffers (`Engine/InlineBuffers.cs:42-43`) and fold edit kinds (`Matcher.cs:12429-12438`).
- Errors are made one item at a time in `NextFuzzyMatchItem` (`Matcher.cs:9967-10070`), walked by
  `FuzzyMatchItem`/`RetryFuzzyMatchItem` (`:11751`, `:11884`). The string forms
  (`FuzzyMatchString`, `:12120`, `:12198`) are where a literal-only swap fits; the case-folding forms
  (`:12774`, `:13460`) need their own rule or a refusal.
- Budget and cost checks (`TotalCost`, `AnyErrorPermitted`, `ThisErrorPermitted`,
  `FuzzyWithinConstraints`, `Matcher.cs:9552-9680`) and the nested-section walk
  `OneMoreErrorFitsTheOpenSections` (D133) need a fourth term.
- Best-match ranking and the save and restore of counts and changes (`:4286`, `:4320`,
  `:9865-9882`) work per kind.
- Edit tests such as `{e<=1:[a-z]}` (`FuzzyExtMatch`, `:9909`) need a rule for which swapped
  character is tested (question 10.8).
- Partial matching (`CheckFuzzyPartial`, `ErrorArmAtTheEdge`) needs an answer when the text ends
  after the first swapped character (question 10.9). Reverse search (`(?r)`) mirrors the step.
- **The literal prefilter becomes unsound.** `FuzzyLiteralFilter` (`Engine/FuzzyLiteralFilter.cs:17-25`)
  relies on "one substitution, insertion or deletion damages at most one piece" (Navarro 2001,
  section 8.2.3, p. 64, which states the k+1 pieces rule). A swap across a piece boundary damages
  two. The same passage gives two remedies: "split the pattern in 2k + 1 pieces", or "leave a hole
  of one character between each pair of pieces, so that the transposition cannot alter both".
  Every later filter that takes the same `k` inherits the problem. Question 10.12 lists them.
- Size: a literal-only first version touches the parser, the permission and cost functions, the
  string arms (with the look-ahead into the next node of the same written literal, 10.10), the
  prefilter, the public types and the docs. Classes (tier 1) and whole-character swaps under full
  folding (10.11 D) would each be a later step of their own. Comparable to the larger D-series fixes, each of
  which needed several blind-review rounds.

## 8. Verification

There is no upstream answer for any pattern that allows swaps, so the differential oracle cannot
judge them. Proposed instead, under `docs/VERIFICATION.md`'s rules:

1. **Flag off and no `t`: byte-identical behaviour.** The whole ratchet and the oracle at three
   seeds, unchanged. This is the compatibility guarantee and the first gate.
2. **An independent reference for literals:** a brute-force OSA matcher over every candidate span,
   compared with the engine under `BestMatch` (minimum cost) on generated literals and subjects.
3. **fuzzy-regex-rs as a second comparator** (already an external comparator, `S63-performance-gate.md`),
   with care: its `e` includes swaps by default, so compare against this port with the flag on;
   its default answers like `(?b)`, so compare against `BestMatch`; it reads `{t<=1}` and cost
   equations differently (questions 10.3, 10.4), ignores test sets for swaps, has no partial
   matching in upstream's sense and no `(?r)`, and refuses some swaps it should find (section 4).
   So compare plain literals under `e` only, spans and totals rather than counts, and treat a
   difference as a lead to check, not a verdict.
4. **Metamorphic invariants** (`docs/ORACLE-INVARIANTS.md`): allowing swaps never removes a match;
   with the flag, the best cost is at most the cost without it; reported counts add up to `Total`.
5. **A soundness argument for the prefilter change,** with a witness test that fails without it
   (rule 12).
6. Oracle generators must never emit `t` or the flag for upstream rows.

## 9. Alternatives without engine changes

- **Caller-generated variants (works today).** Put the word and its adjacent-swap variants in a
  named list and accept either: `\L<v>|(?:tesco){e<=1}` with `v` = {tesco, etsco, tseco, tecso,
  tesoc} (`FuzzyRegex(pattern, options, namedLists)`, `src/FuzzyRegex/FuzzyRegex.cs:244-248`). This
  means "one swap or one other error". Run on upstream 2026-11-06 (`regex` 2026.9.29,
  `regex.search(p, text, v=v)`); this port was not run:

  | Pattern | Text | Span | Counts (s,i,d) |
  |---|---|---|---|
  | `\L<v>\|(?:tesco){e<=1}` | tecso | (0,5) | (0,0,0) |
  | same | etsco | (0,5) | (0,0,0) |
  | same | tesc | (0,4) | (0,0,1) |
  | same | disco | no match | |
  | same | tecs0 | no match | |
  | same | TECSO | no match; with `(?i)`, (0,5) | |
  | `(?:\L<v>){e<=1}` | tecs0 | (0,5) | (1,0,0) |
  | same | xx tecso yy | (2,8), " tecso" | (0,1,0) |

  It works, at two costs: the counts report a swap as no error at all, so a caller cannot tell
  "tecso" from "tesco"; and the list grows by one variant per adjacent pair of every word. (Case
  needs `(?i)`, as for any pattern.) Making the list itself fuzzy, `(?:\L<v>){e<=1}`, means "one swap plus one
  other error", a different budget, and the last row shows its first match taking in a leading
  space as an insertion.
- **`{e<=2}`** accepts every two-error string ("DISCO" for "tesco"). Too loose.
- **Cost weights** cannot make a swap cost 1: a cost is linear in the three counts
  (`Matcher.cs:9556-9558`), and a swap is two of them.

Worth documenting the first in `GUIDE.md` whether or not the feature is built.

## 10. Questions for the owner

**Owner's answers so far (2026-11-06, interactive pass paused at 10.5):**
- 10.16: **B** - build the literal-only version after 1.3, and document the named-list workaround now.
  Keep tier 1 (adjacent single-character items such as `\d`, `[a-z]`, `.`) cheap to add later: one
  general definition of a swap and a reference matcher built on it from day one.
- 10.13: **A with a major version** - always recognise `t`; transpositions ship as 2.0.0, the one
  incompatibility (`{t...}` brace groups were literal text) stated in the release notes. A GitHub code
  search on 2026-11-06 found no regex pattern using such a brace group.
- 10.15: **A** - no comment on #400 now; reconsider commenting when 2.0.0 ships.
- 10.1: **A** (OSA). 10.2: **No** (equal characters never count). 10.3: **A** (`t` follows the same
  zeroing rule as `i`, `d`, `s`; with nothing named, `t` only under the flag). 10.4: **A**.
- 10.5: **B** - the swap is tried first, only when it fits; ties fall to the search order as for
  `i`, `d`, `s` today; `(?e)`/`(?b)` remain the way to ask for the cheapest answer. A
  fuzzy-regex-rs-style ranking mode (best match plus its tie rule, behind a new opt-in flag) may be
  added later without changing any default. 10.6: **A** - the counts promise one valid explanation
  within the budget, the cheapest only under `BestMatch`/`EnhanceMatch`, stated in the docs.
  Measured 2026-11-06 (fuzzy-regex-rs v0.2.0 built in the
  official `rust:1.88-slim` container, probe `.scratch/frr-probe/`): its default behaves like
  upstream's `(?b)` (fewest edits anywhere: `(?:abc){e<=1}` in "xbc abc" gives (4,7) exact, upstream
  plain (0,3)), not "leftmost then fewest" as read from its source; swaps are on inside `e`
  (`(?:tesco){e<=1}` matches "tecso"); `fuzzy_counts()` omits swaps ((0,0,0), total 1); ties differ
  from upstream `(?b)` ("abxc": insertion (0,4) against substitution (0,3)); its `(?e)` answers like
  upstream plain; no swap across a class and a literal (`\d-\d` in "-12" gives "-1" with a deletion).
  Section 4 and 10.5 are corrected accordingly, and section 4 now has NR-grep, liblevenshtein and
  four more engines with swaps, from the evidence in `.scratch/research/EVIDENCE.md`.

Rewritten 2026-11-06, then revised the same day after a blind review and after reading the
primary papers (Damerau 1964, Lowrance & Wagner 1975, Wagner & Fischer 1974, Pollock & Zamora
1984; PDFs in `.scratch/papers/`). Each question gives the context with an example, the options with their
pros and cons, what other engines do, and a recommendation with what would change it.

How the evidence was gathered, all on 2026-11-06: Python `regex` 2026.9.29 from
`.venvs/regex-2026.9.29`, and RapidFuzz 3.14.6 and jellyfish 1.3.0 from
`.scratch/transpositions-venv`, run with `python -I`; this port (Release, main of 2026-11-06)
run side by side with upstream on the rows of 10.8, 10.9, 10.11 and 10.13
(`.scratch/research/port/`); fuzzy-regex-rs v0.2.0, NR-grep 1.1.2 and liblevenshtein 4.0.0-rc.5
built and run in containers (`.scratch/research/EVIDENCE.md`, raw logs beside it). Where a
question says what the port does without a measured row, it is read from the code and says so.
Rows that describe the feature itself are derived from the engine's search order and the
measured rows, since the feature does not exist yet.

The research items of the old list are done and folded in: the primary sources (section 2, with
what could not be read and why), Lucene's default and fuzzy-regex-rs's semantics (section 4, read
from source), and demand (10.16). The old questions are all here: 1 is 10.5, 2 is 10.6, 3 is
10.4, 4 is 10.8, 5 is 10.9, 6 is 10.10, 7 is 10.11, 8 is 10.1, 9 is 10.14, 11 is 10.15, 12 is
10.7 (now folded into 10.5), 13 is 10.16. Questions 10.2, 10.3, 10.12, 10.13 and 10.17 are new.

| # | Question | Recommendation | Confidence |
|---|---|---|---|
| 10.1 | How far apart may swapped characters be; may a swapped pair be edited again? | **Decided A:** adjacent only, never edited again (OSA) | - |
| 10.2 | Does a swap of two equal characters count? | **Decided No:** only when the pair fails to match in order | - |
| 10.3 | Does naming `t` zero the kinds a constraint does not name? | **Decided A:** the same rule as `i`, `d`, `s`; with nothing named, `t` is unlimited only under the flag | - |
| 10.4 | `t` and cost equations | **Decided A:** upstream's rules unchanged, `t` treated like the others | - |
| 10.5 | Where the swap sits in the search order, and how ties fall | First, tried only when it fits; ties fall to search order, as for `i`, `d`, `s` today | Medium |
| 10.6 | What the counts promise | One valid explanation; a cheapest one only under `BestMatch`/`EnhanceMatch` | High |
| 10.7 | Folded into 10.5 | | |
| 10.8 | Edit tests, `{t<=1:[a-z]}` | Follow the section's kinds: at least one swapped character in the set if it allows insertions and deletions, else both (the substitution rule, also for `t` alone) | High |
| 10.9 | Partial matching halfway through a swap | Accept it, charged one transposition before the swap completes; test the arrived character only under the "both" rule | High |
| 10.10 | Swaps across nodes | The literal as written (plain, escaped or one-member class characters; across V1's fold splits and grouping-only parentheses; within one alternative or list word; same section); never across a capture edge in the first version | High |
| 10.11 | Case folding | Swap only characters that fold to one character each; ß and ligatures fall back to today's edits | High |
| 10.12 | The prefilters | k' = `MostErrors` (with `t` added to it) plus the most swaps, in every filter and scan; sound by a short argument, confirmed exhaustively for pure swaps | High |
| 10.13 | `{t...}` is literal text today | **Decided A:** always recognise `t`; ships as 2.0.0 with a release note | - |
| 10.14 | Flag name, bit, inline letter, counts | `Transpositions`, bit `0x40000000` (also in `GlobalFlags`), no inline letter, a non-positional counts property | High |
| 10.15 | Comment on upstream #400? | **Decided A:** not now; reconsider when 2.0.0 ships | - |
| 10.16 | Build it, or document the workaround? | **Decided B:** build the literal-only version after 1.3; document the workaround now | - |
| 10.17 | Where the fourth kind lives in a `FUZZY` node's values | A block for `t` always appended after upstream's values, which stay an identical prefix | High |

### 10.1 How far apart may the swapped characters be, and may a swapped pair be edited again?

**Context.** "Transposition" means four different things in the literature, and they give
different answers on the same strings.

- **Adjacent swap, restricted (OSA).** Two neighbouring characters change places and neither is
  touched again. This is Damerau's operation: "two adjacent characters had been transposed"
  (Damerau 1964, p. 171), corrected only between words of equal length that "differ in two
  adjacent positions" (pp. 172-174). Navarro 2001, section 3.1, p. 37 of the copy read, lists
  "Transposition: (ab, ba) for a ≠ b, i.e. swap the adjacent letters a and b", and frames every
  distance in the survey so that "once the operation has converted a substring z into w, no
  further operations can be done on w". It is what section 5.1 proposes.
- **Adjacent swap, unrestricted (Lowrance & Wagner 1975, JACM 22(2):177-183, read 2026-11-06).**
  Their operation is "interchange any two adjacent characters (weight W_S)" (p. 177). They price an
  alignment, which they call a trace, by drawing a line from each kept character of A to its
  partner in B: W_D for each character of A with no line, W_I for each of B with no line, W_C for
  each line joining different characters, and W_S for each pair of lines that cross (p. 178).
  When "2W_S >= W_I + W_D" (their condition (2), p. 180), some cheapest trace has no line
  crossing more than one other (Theorem 3), and crossing lines join equal characters, so a
  swapped character is never also substituted (Theorem 4, p. 180). The swapped pair need not be
  adjacent in the end: their recurrence (case 4, p. 181) prices a crossing pair whose characters
  sit at x and i in A and at y and j in B as
  H[x-1, y-1] + (i-x-1)W_D + W_S + (j-y-1)W_I. Everything between the pair in A is deleted and
  everything between it in B is inserted. So two characters any distance apart can swap, paying
  for each character between them. With equal weights the condition holds (p. 181). When it
  fails (their example: W_C > W_I + W_D > 2W_S) the cheapest trace can be a long "daisy-chain"
  of crossings, and "we know of no algorithm of speed comparable" for it (p. 181).
- **Swap within a window (Jaro, Jaro-Winkler).** Two characters match if they are equal and no
  more than floor(max(|s1|,|s2|)/2) - 1 positions apart, and the "transpositions" are half the
  matching characters that are out of order. The result is a similarity between 0 and 1, not an
  edit count (Wikipedia, Jaro-Winkler distance, read 2026-11-06).
- **Block moves (edit distance with moves).** Moving a whole substring elsewhere costs 1. Tichy
  1984, *The string-to-string correction problem with block moves*, ACM TOCS 2(4):309-321;
  Cormode & Muthukrishnan, SODA 2002 and ACM TALG 3(1) 2007, approximate it within a factor
  O(log n log* n) in near-linear time; computing it exactly is NP-complete (Shapira & Storer 2002,
  as cited by the siEDM paper, Algorithms 9(2) 2016). Abstracts and citations only; none of these
  papers was read in full.

Costs, pattern on the left, text on the right. Levenshtein, OSA, unrestricted and Jaro measured
with RapidFuzz (jellyfish agrees on every unrestricted and Jaro value). The upstream column is the
smallest `n` for which `regex.fullmatch("(?b)(?:P){e<=n}", T)` succeeds, measured. The block-move
column is worked by hand from the definition, not computed.

| Pattern | Text | Upstream today | Levenshtein | OSA | Unrestricted | Jaro similarity | Block moves |
|---|---|---|---|---|---|---|---|
| ab | ba | 2 | 2 | 1 | 1 | 0.0 | 1 |
| tesco | tecso | 2 | 2 | 1 | 1 | 0.9333 | 1 |
| abcdef | bacdfe | 4 | 4 | 2 | 2 | 0.8889 | 2 |
| ca | abc | 3 | 3 | 3 | 2 | 0.0 | 2 |
| abc | cba | 2 | 2 | 2 | 2 | 0.5556 | 2 |
| abcd | cdab | 4 | 4 | 4 | 4 | 0.0 | 1 |

What the rows say:

- OSA and unrestricted differ only when a swap combines with insertions or deletions between the
  pair ("ca" to "abc": swap, then insert "b" between, W_S + W_I = 2 by Lowrance & Wagner's case 4).
- Swapping characters that are not neighbours ("abc" to "cba") costs 2 under every edit distance:
  two substitutions are already as cheap.
- Jaro gives "ab" and "ba" a similarity of 0, because for two-character strings the window is 0
  positions wide. It does not see the very swap this feature is for.
- Only block moves make "cdab" cheap.

**Options.**

- **A. Adjacent, restricted (OSA).**
  Pros: Damerau's definition (his program swaps only two adjacent positions of equal-length
  words), and what Lucene, Elasticsearch, Commons Text and fuzzy-regex-rs ship
  (section 4). It fits upstream's design: one arm reads two pattern items and two characters,
  moves past both, and remembers nothing. The independent reference of section 8 is a short
  dynamic program. Easy to explain: "two neighbours in the wrong order cost one error".
  Cons: not a metric. OSA("CA","AC") + OSA("AC","ABC") = 1 + 1 < OSA("CA","ABC") = 3, so the
  triangle inequality fails (measured with RapidFuzz: 1, 1 and 3). Nothing in this library relies on the
  triangle inequality, so this costs nothing here.
- **B. Adjacent, unrestricted (Lowrance & Wagner).**
  Pros: a true metric; "ca" to "abc" costs 2.
  Cons: a swap whose two halves are separated by insertions, or made adjacent by deletions, is an
  edit that spans other edits; their algorithm finds the pair by looking back for the last
  matching character on each side (p. 181), which a node-at-a-time matcher has no equivalent of. The engine would have to carry a half-finished swap from step to
  step, through backtracking, the save and restore of counts and changes, nested sections and
  partial matching: the hardest shape for a node-at-a-time engine. It only changes answers where a
  budget of 2 or more already allows a second error. No regex or search engine found ships it;
  RapidFuzz's `DamerauLevenshtein` and jellyfish compute it as a number only.
- **C. Swap within a window (Jaro).**
  A similarity score, not an error count, so it cannot sit in an error budget; it ignores swaps in
  short strings. Not a candidate; listed so the question is closed.
- **D. Block moves.**
  Pros: the only option that makes "abcd" to "cdab" cheap.
  Cons: NP-complete to compute exactly; a moved block in a regex crosses nodes, groups and
  repeats; no regex engine or fuzzy search library found ships it.
- **E. Swap of two characters at any distance ("abc" to "cba" for 1).**
  No shipped engine or source found that defines it as one edit; Lowrance & Wagner reach distant
  characters only by paying for every character between them (option B). It turns every pair of
  positions into a candidate: a word of n letters has n - 1 adjacent swaps but n(n - 1)/2 swaps at
  any distance (arithmetic, not measured on data), so the set of strings one error away from a
  word grows, and with it the false matches.

**What other engines do.** Every engine that counts swaps in a budget and whose form could be
checked uses A: liblevenshtein (measured: "ca" against "abc" exceeds 2), fuzzy-regex-rs and FREJ
(read from source), Lucene and Commons Text (documented). NR-grep's form was not probed. B
appears only in distance functions.

**Recommendation: A.** It is the definition, the industry default and the only form that fits
the engine without a new kind of state. Confidence high. What would change it: a real corpus from
the user where a swap combined with a second error inside the pair is common. Then B could come
later as a separate opt-in, with its own design.

### 10.2 Does a "swap" of two equal characters count?

**Context.** Pattern "aa" and text "aa" match exactly. A swap arm that only checked "text[p]
matches item k+1 and text[p+1] matches item k" would also fire here, at a cost of 1. In a
backtracking engine the exact path is tried first, so the swap path can only repeat a path that
has already failed, one error dearer: wasted work, and under a minimum-error constraint such as
`{1<=e<=1}` a spurious way to meet the minimum. With classes it gets subtler: `[ab][ab]` against
"ab" also matches in order.

Upstream already refuses the analogous edit for its own kinds: an error never stands in for a
character that matches. Measured: `regex.fullmatch(r"(?:aa){1<=e<=1}", "aa")` and
`regex.fullmatch(r"(?:ab){1<=s<=1}", "ab")` are both `None`; a substitution of a character by
itself is not an error.

**Options.**

- **A. A swap is tried only when the pair does not match in order** (for literals, when the two
  characters differ after the section's case folding).
  Pros: Navarro's definition says a ≠ b; it follows upstream's precedent above; no duplicate
  paths; generalises to classes in 10.10.
  Cons: one extra comparison, which the exact attempt has usually just made.
- **B. No rule.** Cons: duplicate paths and a spurious error.

**What other engines do.** fuzzy-regex-rs requires the two pattern characters to differ
(`damlev.rs:566-570`, read). Measured, it rejects `{1<=t<=1}` ("range constraint only valid for
'e'"), and `(?:aa){1<=e<=1}` in "aa" answers (1,2) with one deletion, not a swap.

**Recommendation: A.** Confidence high.

### 10.3 Does naming `t` zero the kinds a constraint does not name?

**Context.** Upstream's rule: if a constraint names any of `i`, `d`, `s`, the ones it does not
name get a maximum of 0; if it names none, they are limited only by `e`
(`Parsing/Nodes.cs:4699-4707`). A bare letter means "any number of this kind". Measured upstream:

| Pattern | Text (fullmatch) | Counts |
|---|---|---|
| `(?:tesco){e<=2,s<=1}` | tesc | no match (deletion not allowed) |
| `(?:tesco){e<=2,s<=1}` | texco | (1,0,0) |
| `(?:tesco){e<=2,i,d,s<=1}` | tesc | (0,0,1) |

The table in section 5.2 assumed two things that cannot both hold: `{t<=1}` means "swaps only"
(so naming `t` zeroes the others), and `{e<=2,t<=1}` means "i, d, s plus at most one swap" (so it
does not).

**Options.**

- **A. `t` joins the rule.** Naming `t` zeroes unnamed `i`, `d`, `s`; naming any of them zeroes an
  unnamed `t`. Then `{t<=1}` is "at most one swap, nothing else", and `{e<=2,t<=1}` is the same
  (its `e` adds nothing). "Up to two errors, at most one of them a swap" is written
  `{e<=2,i,d,s,t<=1}`.
  Pros: one rule, the one upstream users already know; every constraint stays bounded.
  Cons: `{e<=2,t<=1}` reads as "plus" and does not mean it. The docs must show the bare-letter
  form.
- **B. `t` never zeroes the others.** Then `{e<=2,t<=1}` means "two errors, at most one a swap".
  But `{t<=1}` alone would leave `i`, `d`, `s` unlimited (no `e` either), so it needs a second
  rule: "a constraint that names only `t` means `t` only".
  Pros: close to fuzzy-regex-rs. Cons: two rules, one of them unlike anything upstream has.

**What other engines do.** fuzzy-regex-rs chose B without the second rule: "Transposition (`t`) is
a fuzzy-regex extension that mrab lacks, so it does NOT trigger this defaulting on its own"
(`src/parser/ast.rs:211-214`). Measured: `{t<=1}` alone also accepts "tesc" (a deletion) and
"texco" (a substitution), `{e<=2,t<=1}` accepts "txcso" (a substitution and a swap), and naming
`i` still zeroes `d` (`{t<=1,i}` refuses "tesc"). NR-grep's `-k1t` means swaps only: it refuses
"tesc" and "texco" (measured).

**The other half of the rule: a constraint that names nothing.** Upstream makes every unnamed
kind unlimited then (`Nodes.cs:4699-4707`). If `t` simply joined that list, `{e<=2}` would allow
swaps with the flag off, breaking the compatibility guarantee. So the rule is: when nothing is
named, unnamed `i`, `d`, `s` are unlimited as today, and an unnamed `t` is unlimited only when the
flag is set, otherwise 0. This is the flag's whole effect on parsing (section 6, "Defaults").

**Recommendation: A**, with the default rule above, and correct the 5.2 row for `{e<=2,t<=1}` to
"t only". Confidence high.
What would change it: the owner valuing pattern compatibility with fuzzy-regex-rs above one rule.

### 10.4 `t` and cost equations

**Context.** A cost equation such as `{2d+1s<4}` weights the kinds it names. Upstream's rules, in
order: a kind named in the equation gets an unlimited maximum (`Nodes.cs:4686-4695`); the
zeroing rule of 10.3 then applies; and a kind not in the equation costs 0 (`Nodes.cs:4712-4719`).
So an unnamed kind is either forbidden or free. Measured upstream:

| Pattern | Text (fullmatch) | Counts |
|---|---|---|
| `(?:foobar){2d+1s<4}` | fooxxxxbar | no match (`i` zeroed) |
| `(?:foobar){i<=5,2d+1s<4}` | fooxxxxbar | (3,4,0): four insertions at no cost |

With the flag on, `{1i+1d+1s<=2}` does not name `t`, so the rule of 10.3 zeroes it: no swaps.
With the flag off and no `t` anywhere, 10.3's default rule keeps `t` at 0 whatever the equation
says.
The old worry, "swaps would be free", arises only when `t` has its own cap and is missing from the
equation, as in `{t<=1,1i+1d+1s<=2}`.

**Options.**

- **A. Upstream's rules unchanged, `t` treated like `i`, `d`, `s`.** `{t<=1,1i+1d+1s<=2}` allows
  one swap at no cost plus edits costing up to 2.
  Pros: no new rule; it is exactly what `{i<=5,2d+1s<4}` does today. Cons: a free swap may
  surprise; the docs must say so.
- **B. A `t` missing from an equation costs 1.** Cons: a rule for one kind only.
- **C. A parse error when `t` is capped but missing from the equation.** Cons: rejects for `t` what
  upstream accepts for the other three.

**What other engines do.** fuzzy-regex-rs's source says a capped kind missing from the equation
is free: "operations priced at 0 (explicitly capped but absent from the equation, so mrab matches
them for free) may all be used" (`src/parser/ast.rs:278-286`, `301-305`). Measured, it is not:
`{t<=1,1i+1d+1s<=1}` refuses "tecso". It also refuses "tecso" under `{1i+1d+1s+1t<=1}`, where one
swap at cost 1 fits, and accepts it under `<=2`. So it matches none of A, B or C.

**Recommendation: A.** Confidence high. Without a cost equation the engine builds one with every
coefficient 1 (`Nodes.cs:4721-4729`), so with the flag on, `{e<=1}` prices a swap at 1, as section
5.2 wants.

### 10.5 Where the swap sits in the search order, and how ties fall

**Context.** At a pattern item that fails to match, the engine tries substitution, then
insertion, then deletion (section 5.3), and each attempt keeps the first path that fits the
budget. Measured upstream, where no swap exists: `(?:ab){e<=1}` searched in "ba" answers (0,1)
with one deletion. Substituting "b" for "a" leaves "a" against "b" with no budget; inserting "b"
leaves "b" unmatched at the end; deleting the pattern's "a" lets "b" match. With the flag on, the
swap's place in that order decides the answer:

| Order | `(?:ab){e<=1}` in "ba" | `(?:tesco){e<=2}` in "tecso" |
|---|---|---|
| Swap last, after deletion | (0,1), one deletion | (0,5), two substitutions |
| Swap first, only when it fits | (0,2), one transposition | (0,5), one transposition |

**`BestMatch` does not change this.** `DoBestFuzzyMatch` (`Engine/Matcher.cs:26499-26556`,
upstream `do_best_fuzzy_match`) runs two passes: the first walks the start positions with the
budget held one below the cheapest seen, the second re-examines the equal-best candidates for the
earliest, fewest-error fit. Every attempt inside either pass still takes the first path the search
order reaches. So among candidates that tie on cost and errors at the same start, the search order
decides today, for `i`, `d` and `s`. Measured upstream:
`regex.search(r"(?b)(?:abc){e<=1}", "abxc")` answers (0,3), one substitution at 2, although (0,4)
with one insertion costs the same; `(?e)` and no flag give the same. With swaps, `(?b)(?:ab){e<=2}`
in "ba" (today (0,1), one deletion, section 5.3) would answer whatever the first path is: (0,2),
one transposition, with swap-first; (0,1), one deletion, with swap-last. (Derived from the order,
not run: the feature does not exist.) This replaces the old question 10.7, which assumed a ranking
key decided such ties.

**Options.**

- **A. Last.** Pros: every path found today is still found first. Cons: a user who turns swaps on
  still gets half the word in the first row and two substitutions in the second; the swap is
  reported only when nothing else fits.
- **B. First, tried only when it fits** (text[p] matches item k+1, text[p+1] matches item k, and
  the pair fails in order, 10.2).
  Pros: when it fits, a swap costs 1 where the paths it displaces cost 2, so it is the locally
  cheapest edit; it reports what the user means, with and without `BestMatch`; fuzzy-regex-rs and
  the distance libraries pick the fewest edits, so they agree on these rows. Cons: for a pattern
  that allows swaps, the first match can differ from the same pattern without them (the point of
  opting in). Costs one two-character check at each failing item, in sections that allow swaps
  only.
- **C. Second, after substitution.** Gives the second row's two substitutions; no advantage over
  B.
- **D. A new ranking key for ties** (for example "more transpositions", proposed in the first
  draft of this section). Cons: `BestMatch` has no tie-ranking step that sees whole candidates at
  one start; adding one changes the two-pass design for one kind, and B already gives the swap
  answer through the order.

**Cost and risk.** With the flag off and no `t`, the arm must never run: a per-section check
before the look-ahead, measured as the house rules require (BenchmarkDotNet fuzzy rows against
main, A-B-B-A for small effects). Where the swap's value is stored is question 10.17; trying it
first while it sits after deletion in numbering means the walk over `fuzzy_type` cannot be a plain
increment, which is a local change to the walk.

**What other engines do.** No backtracking engine with a search order has swaps. fuzzy-regex-rs
keeps every path and picks by fewest edits (`damlev.rs:648-660`, read). Measured:
`(?:ab){e<=1}` in "ba" gives (0,2), one swap, and `(?:tesco){e<=2}` in "tecso" one swap, so it
agrees with option B on both rows. Its default is fewest edits anywhere, like upstream's `(?b)`:
`(?:abc){e<=1}` in "xbc abc" gives the exact (4,7), where upstream without flags gives (0,3). Its
ties differ from upstream's `(?b)`: "abxc" gives (0,4) with an insertion, upstream (0,3) with a
substitution. NR-grep and liblevenshtein report whether, or how far, the text matches, not which
edits, so they say nothing about order.

**Recommendation: B, with ties left to the search order as for `i`, `d`, `s` today, pinned by
tests** on the two rows above, with and without `(?b)`. Confidence medium: the semantics are
clear, the speed cost is not measured. What would change it: a measured slowdown on swap-enabled
rows the owner will not accept, or a pattern where swap-first finds a clearly worse first match.

### 10.6 What the reported counts promise

**Context.** Without `BestMatch` or `EnhanceMatch` the counts describe the path found, not the
cheapest explanation. Measured upstream on the same text: `(?:tesco){i<=1,d<=1}` in "tecso" gives
(0,1,1), insertion at 2 and deletion at 4; `(?:tesco){e<=2}` gives (2,0,0), substitutions at 2 and
3. Two explanations, both legal.

**Options.**

- **A. "One valid explanation within the budget"**, today's contract for `i`, `d`, `s`, plus "a
  minimum-cost one under `BestMatch` or `EnhanceMatch`". Pros: no new cost; with 10.5 B, a swap
  that fits is reported as a swap. Cons: where several explanations fit, a swap can still be
  reported as other kinds.
- **B. "The cheapest explanation", always.** Cons: a best-match search on every call; upstream does
  not promise it for the three kinds it has.

**What other engines do.** RapidFuzz's `OSA` and `DamerauLevenshtein` return a number with no
explanation (no `editops`, measured); Lucene returns matching terms, not edits; fuzzy-regex-rs keeps
the fewest-edit path per start (`damlev.rs:611-619`, read), and its `fuzzy_counts()` reports a
one-swap match as (0,0,0), the swap visible only in `edits().swaps` and the total (measured).

**Recommendation: A**, stated in the XML docs of `FuzzyCounts` and in `GUIDE.md`. Confidence high.

### 10.7 Folded into 10.5

The first draft proposed a ranking key, "more transpositions", for equal-cost ties with different
spans under `BestMatch`. A review showed that `BestMatch` has no such ranking step: inside each
attempt the first path found wins, so ties already fall to search order for `i`, `d` and `s`
(measured, 10.5). The question is therefore the search order, answered in 10.5.

### 10.8 Edit tests: `{t<=1:[a-z]}`

**The question.** When a fuzzy section has a test set, which of the two swapped characters must
be in the set for the swap to be allowed?

**Context.** A test set is the `:[...]` after a budget, as in `{e<=1:[a-z]}`. It limits the
characters an edit may put into the match: an insertion is allowed only if the text character it
brings in is in the set, a substitution only if the text character it puts in the pattern's place
is, and a deletion is never tested, because it puts nothing in (`COMPARISON.md`, "constrain which
characters an edit may touch"). A swap brings in no character the pattern lacks: it puts two of
the pattern's own characters in each other's places. Today the engine explains the same text with
two edits, and the two explanations test different characters:

- two substitutions test both swapped characters;
- an insertion plus a deletion tests only the inserted one, and either of the two can be the one
  inserted.

**Measured: what today's engine accepts at cost 2.** Upstream 2026.9.29; the port answered the
same on every row it was run on (`.scratch/research/port/`). "Fullmatch" asks for the whole text;
"search" finds the first match anywhere.

| Pattern | Text | Fullmatch | Search |
|---|---|---|---|
| `(?:ab){i<=1,d<=1:[a]}` | ba | (0,2), insertion and deletion | (0,1), one deletion |
| `(?:ab){i<=1,d<=1:[b]}` | ba | (0,2), insertion and deletion | (0,1), one deletion |
| `(?:ab){i<=1,d<=1:[xyz]}` | ba | no match | (0,1), one deletion |
| `(?:tesco){e<=2:[c]}` | tecso | (0,5), insertion and deletion | the same |
| `(?:foo bar){e<=2:\s}` | foob ar | (0,7), insertion and deletion | the same |
| `(?:a1){e<=2:[0-9]}` | 1a | (0,2), insertion and deletion | (0,1), substitution and deletion |
| `(?:tesco){s<=2:[cs]}` | tecso | (0,5), two substitutions | the same |
| `(?:tesco){s<=2:[c]}` | tecso | no match | no match |
| `(?:tesco){e<=2:[xyz]}` | tecso | not run | (0,3) "tec", two deletions |

What the rows say, for a whole-text match running forwards:

- where the section allows both insertions and deletions, a swapped pair is accepted when **at
  least one** of its characters is in the set (`[a]`, `[b]`, `[c]`, `\s`);
- where it allows only substitutions, **both** must be (`[cs]` accepts, `[c]` refuses).

Search does not show the pair being accepted: it takes a shorter span with fewer tested edits
("ba" gives (0,1)). Reverse matching cannot be used as evidence at all: with a set of two or more
separate characters, `(?r)` lets any character through, on upstream and the port (D155,
`KNOWN-DEFECTS.md`). So any check against upstream must be forward and whole-text.

**Measured: engines with swaps.** fuzzy-regex-rs ignores the set for swaps: `{t<=1:[xyz]}`
accepts "tecso", `(?:a-b){t<=1:[a-z]}` accepts "-ab" and `(?:a1){e<=1:[0-9]}` accepts "1a", each
as one swap. For insertions and substitutions it tests as upstream does. NR-grep and
liblevenshtein have no test sets, and no other engine with swaps was found with them.

**Options.**

- **A. Both swapped characters must be in the set** (the substitution rule).
  Pros: a swap never puts a character where a substitution would be refused; in a section that
  allows only substitutions it accepts exactly the pairs today's engine accepts (`[cs]` yes, `[c]`
  no).
  Cons: where insertions and deletions are allowed, today accepts the pair at cost 2 with one
  character in the set, and A refuses it at cost 1, so the swap is no cheaper there.
  `(?:foo bar){e<=2:\s}` accepts "foob ar" today at cost 2, and still does under A with the flag,
  but `{e<=1:\s}` refuses it, because "b" is not whitespace.
- **B. At least one must be in the set** (the insertion-plus-deletion rule).
  Pros: in a section with insertions and deletions, the flag halves the price of a swapped pair and
  never changes which pairs the set admits.
  Cons: in a section without them it admits pairs that nothing admits today: under
  `{s<=2,t<=1:[c]}` it accepts "tecso" with only "c" in the set, where `{s<=2:[c]}` refuses it.
- **C. Not tested**, as in fuzzy-regex-rs. Pros: the simplest. Cons: `{t<=1:[xyz]}` accepts
  "tecso", which no explanation passes under that set today; the set stops meaning anything for
  swaps.
- **D. Follow the section's own kinds.** If the section allows both insertions and deletions (a
  maximum above 0 after 10.3's zeroing), at least one swapped character must be in the set;
  otherwise both must be.
  Pros: in every section that has other kinds, a swapped pair is accepted at cost 1 exactly when
  today's engine accepts it at cost 2 there, so the flag changes the price and never which texts
  the set admits. It is A where A is right and B where B is right. Cost: one bit per section,
  worked out when the pattern is compiled, and one branch in the swap arm.
  Cons: two sections that differ only in `i` and `d` treat the same pair differently, which needs
  a sentence in the docs (today's engine already behaves this way at cost 2). A section with `t`
  alone, or `t` with only `s`, has no insertion-plus-deletion route, so D applies the substitution
  rule there: a choice, not a measurement.

**Recommendation: D.** It is the only option that keeps the feature's promise in every section:
"a swap costs two errors today; with the flag it costs one". The measured rows show today's
engine already uses both rules, chosen by the kinds the section allows; A and B each keep only
one of them. For `t` alone D takes the stricter rule, which can later be loosened without any
match being lost. It is testable against upstream only forward and whole-text: with the flag,
`{t<=1,K:S}` must accept a one-swap text exactly when upstream's fullmatch with `{K,e<=2:S}`
does, for the same kinds K. Confidence high; the rule for `t` alone is the one judgement in it,
and no engine or corpus can inform it, because no engine with swaps tests them.

### 10.9 Partial matching halfway through a swap

**The question.** If the text ends between the two characters of a swap, does a partial search
report a partial match, charged one transposition?

**Context.** A partial search (`partial: true`) answers "so far, so good": the text ran out while
a match was still open (`GUIDE.md`, "Reading a result"). A search box uses it to keep accepting a
half-typed entry and to reject one that has gone wrong; an empty partial match at the end of the
text is how "gone wrong" looks (`COMPARISON.md`, the `2026-0b` example). Example:
`(?:tesco){t<=1}` on "tec". The whole entry "tecso" matches with one swap. "tec" stops after the
"c", the first character of the swapped pair: the "c" stands where the pattern has "s", and the
"s" that would complete the swap has not arrived. With `t` alone, nothing else explains the "c".

**Measured**, partial search; the first four rows on upstream and the port (identical), the
`(?r)` rows on upstream:

| Pattern | Text | Partial answer |
|---|---|---|
| `(?:tesco){d<=1}` | tec | (0,3), one deletion |
| `(?:tesco){s<=1}` | tec | (0,3), one substitution |
| `(?:tesco){i<=1}` | tecs | (0,4), one insertion |
| `(?:tesco){s<=0}` | tec | (3,3), the empty partial only |
| `(?r)(?:tesco){s<=1}` | cso | (0,2), one substitution |
| `(?r)(?:tesco){d<=1}` | cso | (0,1), one deletion |

Every edit upstream charges at the edge is complete when charged: a deletion, substitution or
insertion involves one position, and it has happened. A half swap would be the first edit charged
before it is complete. What the charge means is the same, though: the errors of the path still
open, within every limit. That edit need not be how the finished text is explained: "tecs" is
charged an insertion, and one insertion completes it as "tecsco", not as "tecso".

**Measured: engines with swaps.** fuzzy-regex-rs reports no partial for any prefix: on "tec",
`{t<=1}`, `{e<=1}` and `{d<=1}` all give no match. Its `partial` flag marks a full match that
touches the end of the text ("tecso": partial = true), which is a different notion. NR-grep and
liblevenshtein have no partial matching.

**Options.**

- **A. Accept, charged one transposition at that position;** mirrored under `(?r)`.
  Pros: the half-typed "tec" stays open, as "tecso" deserves. The count is the open path's errors
  and within every limit, the rule `GUIDE.md` states for fuzzy partials.
  Cons: the count names a swap whose second half has not arrived, the first edit charged before it
  is complete; if the text goes on differently, the full match may be explained otherwise, as
  upstream's insertion row already shows for its own kinds.
- **B. Refuse.** "tec" gets only the empty partial (3,3), the answer for a contradicted entry,
  though "tecso" matches. The `partial-prefix-closure` invariant (`docs/ORACLE-INVARIANTS.md:265-268`)
  asks only for "a match", which the empty partial satisfies, so the invariant would not catch
  this; the search-box use would.
- **C. Accept, charged nothing.** Cons: the half swap escapes the budget, so a partial can be
  reported that no continuation completes within it: `{t<=1}` on "etc" spends its swap on "et",
  then takes a free half swap at "c", although "etcso" needs two swaps. That breaks the rule that
  a partial's path is within every limit.

**Test sets on a half swap.** Only one of the two characters has arrived. Under 10.8 D:

- where the section's rule is "both must pass", the arrived character must be in the set, as
  upstream tests a substitution at the edge: `{s<=1:[c]}` on "tec" gives the partial (0,3), and
  `{s<=1:[s]}` only the empty (3,3);
- where the rule is "at least one", the partial is accepted whatever the arrived character is,
  because the one still to come may pass; upstream does the same for its own kinds:
  `{i<=1,d<=1:[s]}` and `{i<=1,d<=1:[xyz]}` on "tec" both give the partial (0,3) with one
  deletion.

**Recommendation: A, with `(?r)` mirrored and the tests above.** It applies to one more kind the
rule the port documents for every fuzzy partial, and it is the only option under which a
half-typed entry that a swap completes stays open. The new thing is charging an edit before it
completes; the docs must say so in one sentence. Confidence high.

### 10.10 Swaps across nodes

**The question.** Between which two pattern items may a swap happen in the first version, given
that the engine works one node at a time and the compiler can split one written word into several
nodes?

**Context.** The engine matches a compiled list of nodes one at a time: a literal string, a
character class, a repeat, a group edge, and so on. A swap needs two pattern items at once, the
current one and the next. Inside one literal node both are at hand; at the edge of a node, the
swap arm has to look into the next node.

Those edges fall where a user cannot see them. Under version 1, this library's default, a
case-insensitive literal is split wherever its case-folded text contains a sequence that some
character folds to: "st", "ss", "fi", "ff", "fl" and others (`Sequence.FlushCharacters` and
`FixFullCasefold`, `Parsing/Nodes.cs:3217-3393`). The pieces are compared in two ways: a
`STRING_IGN` node compares character by character, one to one ("simple folding"); a `STRING_FLD`
node compares after full case folding, where one text character may stand for several pattern
characters (ß for "ss"). Compiled 2026-11-06:

| Pattern | Nodes |
|---|---|
| `(?i)tesco` | one `STRING_IGN` "tesco" |
| `(?i)strasse` | `STRING_FLD` "st", `STRING_IGN` "ra", `STRING_FLD` "ss", `CHARACTER` "e" |
| `(?i)first` | "fi", "r", "st" |
| `(?V0i)strasse` | one `STRING_IGN` of 7 |

So a swap confined to one node would answer `(?i)strasse` against "strsase" ("a" and "s"
swapped) with one error under version 0 and two under version 1, and would treat "first" unlike
"tesco". "Literal" has to mean the literal as written, across the nodes the compiler splits it
into.

Beyond literals, in rising difficulty:

1. Single-character items next to each other: classes, `.`, `\d`, properties.
   `(?:\d-\d){t<=1}` against "-12" swaps the class `\d` with the hyphen.
2. A repeat edge: `ab+` against "ba".
3. An alternation edge: `x(?:y|z)` against "yx".
4. A capture edge: `(a)(b)` against "ba".
5. Calls, lookaround, and the edge of a fuzzy section (which section pays?).

**Measured** (upstream and this port have no swaps; "-" = not probed):

| Pattern | Text | fuzzy-regex-rs `{t<=1}` | NR-grep `-k1t -x` | liblevenshtein, OSA |
|---|---|---|---|---|
| `te(sc)o` | tecso | 1 swap | - | - |
| `tes(co)` | tecso | no swap (an insertion and a deletion) | - | - |
| `(?:tes)co` | tecso | no swap (an insertion and a deletion) | - | - |
| `\d-\d`, `[0-9]-[0-9]` | -12 | no swap | swap | - |
| `[a-c][x-z]` | xa | no swap | swap | 1 |
| `ab+` | ba | no swap | no; a match without `-x` | 1 |
| `x(y\|z)` | yx | no swap | swap | 1 |
| `(ab\|cd)` | ba | 1 swap | no | 1 |
| `(abc\|tesco)` | tecso | no match (apparent defect) | no | 1 |
| `(a)(b)` | ba | no swap | - | 1 (a distance; no group spans) |
| A swap inside a literal that follows a repeat: `\w+ jumps over`, `[a-z]+ jumps` | ... jumsp over, ... jmups | no match under `{e<=1}` (apparent defect) | no; a match without `-x` | 1 (`[a-z]+jumps` against "foxjmups") |

No two engines draw the line in the same place. fuzzy-regex-rs stops at every group edge, even a
non-capturing one, and at every class. NR-grep crosses classes, `.` and alternation edges, but not
within literal alternatives; in whole-line mode it misses any swap at or after a repeat, which it
finds only in substring mode. liblevenshtein crosses everything, and can, because it reports a
distance and no group spans. None crosses a capture edge while reporting captures.

**Options.**

- **A. One node only.** Pros: the smallest change; the swap arm never leaves its node.
  Cons: answers depend on splits the user cannot see: `(?i)strasse` against "strsase" costs 1
  under `(?V0i)` and 2 under the default, and `(?i)first` against "frist" ("i" and "r" swapped
  across the "fi" | "r" seam) gets no swap while `(?i)tesco` against "tecso" does. The reference
  matcher of section 8 would have to model the compiler's splits.
- **B. The literal as written**, the scope 10.16 decided for the first version. Precisely, a swap
  may exchange two pattern items when:
  1. each is a literal character: written plainly, escaped (`\.`, `\x41`), or as a class of one
     member, since `te[s]co` parses to the same five character items as `tesco`;
  2. they are adjacent in one sequence, with nothing between them but seams the compiler made
     (the version 1 fold splits) or parentheses that only group: `(?:tes)co` answers as `tesco`;
  3. inside a branch of character sequences, both lie in the same alternative's word. A written
     alternation `(?:tesco|tesla)` and a named list `\L<names>` (which upstream builds as such a
     branch, `upstream/regex/_regex_core.py:4089-4100`) are treated alike;
  4. no capture edge lies between them;
  5. both lie in the same fuzzy section.
  Each swapped character is compared under its own node's rule (simple or full folding, 10.11).
  Pros: the answer depends only on the pattern as written, and is the same under `(?V0i)` and
  `(?V1i)`; the reference matcher is plain OSA over the literal's text.
  Cons: the compiler must mark a literal node whose next node continues the same written literal,
  and at a node's last character the swap arm must read the next node's first character. More
  work than A, but only at node edges. Adding a capture group changes what matches: `te(s)co`
  refuses "tecso" under `{t<=1}` while `tesco` accepts it, because the swap would cross the
  group's edge (fuzzy-regex-rs, measured, behaves this way: `tes(co)` gets no swap).
- **B'. B, plus swaps across a capture edge with defined group spans.** Each group's span runs
  from the first to the last text position its own pattern characters matched.
  Pros: adding a group never changes what matches.
  Cons: a group's span can then hold a character matched outside it: `(te)sco` against "tseco"
  would give group 1 the span (0,3), which includes the "s". Capture marks would be set out of
  text order, through backtracking and the save and restore of groups, the riskiest code in the
  engine. Hard; no engine that reports captures does it.
- **C. B plus single-character items** (case 1: classes, `.`, properties), as NR-grep does.
  Pros: covers `\d-\d` and patterns where a class meets a literal.
  Cons: a second implementation step. 10.16 decided it comes later, from the same general
  definition of a swap.
- **D. Everything but captures** (cases 1, 2, 3 and 5), as liblevenshtein does.
  Cons: repeat and alternation edges multiply the work in an engine that holds one node at a time;
  even NR-grep finds swaps at a repeat only in substring mode.

**Recommendation: B for the first version, C later from the same definition; B' only if users
show that groups around parts of words are common.** 10.16 decided "literal-only", and the
compiled splits show that only B makes "literal" mean what the user wrote: a word gets the same
answer under either version and whatever letters it contains. The capture con is real but narrow
(a group inside a word, at the swapped pair's seam), shared with fuzzy-regex-rs, and cheaper to
document than B' is to build. Confidence high. The one number not yet known is the cost of the
look-ahead at node edges; it exists only once the arm does, and the standard gate
(BenchmarkDotNet fuzzy rows with the flag off against main, A-B-B-A) shows whether sections
without swaps pay anything.

### 10.11 Case folding

**The question.** Under full case folding, where one character can fold to several (ß to "ss",
ﬁ to "fi"), what counts as the two characters of a swap?

**Context.** Version 1 is this library's default (upstream's is version 0), and under version 1
`(?i)` means full case folding. Measured: on this port `(?i)strasse` matches "straße" and
`(?i)fin` matches "ﬁn"; upstream refuses both under its default and matches both under `(?V1i)`.
For ordinary letters, folding maps one character to one, and a swap is two characters compared
crosswise. The motivating example is that case: `(?i)tesco` against "TECSO" compiles to one
simple-folding node (10.10).

The hard part is a character that folds to more than one: ß, the ligatures ﬀ, ﬁ, ﬂ, ﬃ, ﬄ, ﬅ, ﬆ,
and a few others. Two definitions are possible.

- **Folded units.** Compare the folded texts and swap two adjacent folded characters. This is how
  upstream makes its own edits in a full-fold literal: one folded character per edit, so a
  substitution can consume half of a text "ß" (`NextFuzzyMatchStringFld`).
- **Whole characters.** A swap exchanges two characters of the text, each matched whole against
  the pattern characters its folding covers. This is the port's rule for its own edits since
  ledger entries 49 and 52, the port's fixes that let a fuzzy edit treat ß or a ligature as one
  character, in the pattern (49) and in the text (52). They added whole-character substitutions
  and insertions beside upstream's folded ones and kept both, so no match upstream finds is lost.

They differ only where an expanding character is in the swapped pair:

| Pattern | Text | Folded units | Whole characters | Today, `{e<=2}`, fullmatch |
|---|---|---|---|---|
| `(?i)strasse` | strsase | 1 | 1 | (2,0,0), upstream and the port |
| `(?i)straße` | strßae | 2 ("strasse" against "strssae") | 1 (ß and "a" exchanged) | (0,1,1), upstream `(?V1i)` |
| `(?i)strasse` | strßae | 2 | 1 (ß stands for "ss") | (0,1,1), upstream `(?V1i)` |
| `(?i)fin` | nﬁ | 2 | 1 | (0,1,1), upstream `(?V1i)` |
| `(?i)if` | ﬁ | 1 (the two halves of "fi" exchanged) | not a swap | (1,0,1), upstream `(?V1i)` |

The last column was measured on 2026-11-06; the port was not run on the four upstream-only rows,
where its whole-character edits could explain them differently. Search answers differently from
fullmatch, taking a shorter span: `(?V1i)(?:straße){e<=2}` in "strßae" gives (0,5) with one
substitution and one deletion, and `(?V1i)(?:fin){e<=2}` in "nﬁ" gives (1,2) with one deletion.
The last row is where the definitions disagree in kind: is "ﬁ" for "if" a swap? Folded units say
yes; whole characters say the text has one character, not two in the wrong order.

**What others do.** fuzzy-regex-rs (measured) folds one character to one only: `(?i)strasse`
refuses "straße", `(?i)fin` refuses "ﬁn", and it treats ß as one character, so `(?i)straße`
against "strßae" is one swap; the Kelvin sign swaps like a "k". FREJ (`Character.toUpperCase`)
and fuzzy-aho-corasick (lower case per grapheme) fold one to one too (read from source). NR-grep's
`-i` was measured on ASCII only. No engine with swaps was found that does full folding, so none
has had to answer this.

**Implementation cost** (code trace, `.scratch/research/AGENT-FINDINGS.md`, task 2). In the
simple-folding arms a swap is two crossed one-to-one comparisons: trivial. In the full-folding
arm, folded-unit swaps need the folding of the next text character as well as the current one,
and state for a half-used character: medium. Whole-character swaps need comparisons of varying
length at the start of a folding on both sides, the work of ledger entries 49 and 52 again: hard.

**Options.**

- **A. Single-unit pairs only.** Try a swap only when both swapped text characters fold to one
  character each and both pattern positions are whole pattern characters that fold to one each.
  Otherwise the swap arm is not tried and today's edits apply ("strßae" and "ﬁ" for "if" cost 2,
  as now).
  Pros: on every pair it accepts, both definitions agree, so a later move to B, C or D never
  turns a match A accepts into a refusal. It covers every word without ß or a ligature, including
  the split ones (`(?i)strasse` against "strsase", across the seams, 10.10). In the full-folding
  arm it adds one fold-length check of the next text character; the comparison is then the same
  as in the simple arms.
  Cons: a stated gap: a swap that involves ß or a ligature is not one. A later move would still
  change some answers: in a section with `t` alone, A gives no match for `(?i)straße` on "strßae"
  where C and D match, and search spans can move.
- **B. Folded units.** Pros: upstream's own model for edits in a full-fold literal; "ﬁ" for "if"
  costs 1. Cons: "strßae" for "straße" stays 2, where a German typist would expect one swap;
  medium cost.
- **C. Whole characters.** Pros: the port's model since ledger entries 49 and 52; "strßae" and
  "nﬁ" cost 1. Cons: hard; "ﬁ" for "if" is not a swap.
- **D. Both**, as the port already does for substitutions and insertions. Pros: every row costs 1;
  consistent with the port's existing edits. Cons: the cost of B and C together.
- **E. No swaps in full-folding nodes at all.** Cons: version 1 splits literals into full-folding
  pieces at "st", "ss", "fi", "ff" and "fl", so every case-insensitive word containing one of them
  would lose swaps inside those pieces, which a user cannot predict (10.10, option A's objection).
  Not a candidate.

**Recommendation: A in the first version; D if the gap is ever closed.** A gives exactly the
answers on which every definition agrees, so whichever the owner prefers later, no match A accepts
is lost; it costs one lookup. D is the right target if a user meets the gap, because it repeats
what ledger entries 49 and 52 did for the other kinds. Confidence high for A. Choosing between B,
C and D is deferred rather than open: it needs to know how often an expanding character sits
inside a swap in real text, which no corpus at hand records.

### 10.12 The prefilters

**The question.** How do the fuzzy literal prefilters stay sound once one swap can damage two
pieces of a literal?

**Context.** Before running the full matcher, the engine looks for places where the fuzzy literal
could match, and skips the rest. "Sound" means it never skips a place where a match exists. The
piece filter rests on one fact: split the literal into `k + 1` pieces, where `k` is the most
errors the section allows; an insertion, deletion or substitution touches at most one piece, so a
text within `k` errors keeps at least one piece intact, and the filter searches for the pieces
(`Engine/FuzzyLiteralFilter.cs:17-25`; Navarro 2001, section 8.2.3, p. 64). A swap of the last
character of one piece with the first of the next touches two. Example: `(?:tesco){t<=1}` with
k = 1 might split into "tes" and "co"; "tecso" changes the "s" of the first and the "c" of the
second, neither piece occurs, and the filter skips a real match. Navarro gives two remedies on
the same page: "split the pattern in 2k + 1 pieces", or "leave a hole of one character between
each pair of pieces, so that the transposition cannot alter both".

The other filters are scans. The Myers scan is a bit-parallel search (Myers' algorithm) for every
place where the literal occurs within a given Levenshtein distance, the count of insertions,
deletions and substitutions; a swap counts 2 there, as a deletion plus an insertion.

**Measured.** `.scratch/research/pieces.py`, exhaustive over every literal length from 6 to 14,
k from 1 to 3, and every placement of k non-overlapping swaps, with no other edits:

- with `k + 1` pieces, k swaps can destroy all of them, for every length and k: the filter as it
  stands is unsound for swaps;
- with `2k + 1` pieces at least one always survives (at worst 2k are destroyed);
- with `k + 1` pieces and a one-character hole between neighbours, at most k are destroyed.

Levenshtein distance ≤ 2 × OSA distance held on 200,000 random pairs (RapidFuzz 3.14.6).

**Mixed edits, the argument.** The check covers swaps alone. For a mix: a swap changes two
adjacent positions, so it damages at most two pieces; any other edit damages at most one. So `a`
swaps and `b` other edits damage at most `2a + b` pieces, which is the error count `a + b` plus
the swap count `a`. With `k + s + 1` pieces, where `s` is the most swaps the section allows, one
piece survives. The same sum, `2a + b`, bounds the Levenshtein distance the scans see.

**The bound, k'.** Call the sound bound k' (k prime): the most errors plus the most swaps,
k' = `MostErrors` + min(`t` maximum, `e` maximum, cost limit / `t` cost). `MostErrors`
(`Engine/FuzzyLiteralFilter.cs:1017`) takes the tightest of three bounds: the total (`MaxErr`), the
sum of the per-kind maxima, and the cost limit divided by the cheapest kind's cost. It loops over
`FuzzyValue.Count`, three kinds today, so `t` must join that loop, the per-kind sum and the
cheapest cost, or the formula fails. Example: `{2i+2d+2s+1t<=2}`. Two swaps cost 2 and can damage
four pieces, so 4 is needed. Today's three-kind code finds the cheapest cost 2 and gives
`MostErrors` = 1, so k' = 1 + 2 = 3, one short. With `t` in the loop the cheapest cost is 1,
`MostErrors` = 2 and k' = 4.

**Everything that consumes `k`:**

1. The piece filter, `FuzzyLiteralFilter.TryCreate`. Pieces shorter than `MinPieceLength` = 3
   (declared `:142`, checked `:406`) drop the filter to scan-only, so a larger k pushes short
   literals out of piece filtering.
2. The Myers scan, `LiteralScan.ForLiteral(walk, maxErrors)` (called `:345`, defined `:789`):
   needs k'.
3. D145's partial prefilter, `PartialReachOf(walk, maxErrors)` (merged to main 2026-11-06): how far
   before the end of the text a partial match can start. A swap adds no length, so k' only
   loosens it; it is sound either way.
4. Phase 11 item 8's required-literal screen, `FuzzyLiteralFilter.TryCreateRequired` (committed on
   `perf/item8-required-literal` at ee646911, not merged): a Myers scan of a literal that ends the
   section. It needs k' like item 2. Under 10.10 B a swap can straddle the screened literal's first
   character and the character before it, where the compiler split one written literal; that is at
   most two Levenshtein edits on the screened literal, inside k', and the screen's soundness note
   must say so.

**What others do.** fuzzy-aho-corasick's bit-parallel prefilter charges a swap as 2
(`src/search.rs:509-552`, read), which is option A. Which remedy NR-grep uses was not checked.

**Options.**

- **A. Use k' wherever k is used today,** with `t` added to `MostErrors`. Where every error may be
  a swap, this is Navarro's 2k + 1 pieces.
  Pros: one number in one function, covering the piece filter and both scans; sound by the
  argument above, and the exhaustive check confirms it for swaps alone; with swaps off, the swap
  term is 0 and every filter is unchanged.
  Cons: sections that allow swaps get more, shorter pieces: with k = 1, three pieces of 3 need a
  9-character literal where two pieces need 6, so more literals fall back to scan-only.
- **B. One-character holes between `k + 1` pieces** for the piece filter, k' for the scans.
  Pros: longer pieces: with k = 1, 7 characters give two pieces of 3 and a hole. Shown by the same
  check for swaps alone.
  Cons: a second piece layout, with its own window arithmetic and tests; the scans still need k';
  mixed edits need their own argument (an edit inside a hole damages no piece, so it holds, but it
  is one more proof to review).
- **C. Withhold every filter when swaps are allowed.** Pros: trivially sound. Cons: the slowest,
  and the argument shows it is unnecessary.

**Recommendation: A,** with `t` added to `MostErrors` and a witness test per filter that fails
without the change (a swap straddling a piece seam; `VERIFICATION.md` rule 12), plus the
`{2i+2d+2s+1t<=2}` case. Soundness rests on the argument above; the exhaustive check confirms it
for pure swaps and shows today's filter fails. A is one change and leaves patterns without swaps
untouched. B is a later optimisation, worth it only if a benchmark on swap-enabled patterns shows
the shorter pieces cost something; that number cannot exist before the feature does. Confidence
high.

### 10.13 `{t...}` is literal text today

**Context.** A brace group that is neither a quantifier nor a valid constraint is literal text
(section 6, corrected). Measured upstream: `(?:ab){t<=1}` matches "ab{t<=1}",
`(?:ab){e<=1,t<=1}` matches "ab{e<=1,t<=1}", and `a{t}` matches "a{t}". Measured on this port too:
`(?:ab){t<=1}` matches "ab{t<=1}" and `a{t}` matches "a{t}". Once `t` is recognised, these become fuzzy constraints. So the decision of
2026-11-05, "with neither, every pattern keeps today's and upstream's meaning", is not quite true
for `t`.

**Options.**

- **A. Recognise `t` always,** as decided, and list the change in `CHANGELOG.md` and
  `DIVERGENCES.md`. Pros: the decided design. Cons: a pattern that means a literal "{t<=1}" changes
  meaning in a minor release.
- **B. Recognise `t` only when the flag is on.** Pros: no pattern changes without the flag. Cons:
  reverses the decision that `t` works without the flag.
- **C. Make `t` a parse error first, recognise it a release later.** Cons: still a change, made
  twice.

A GitHub code search on 2026-11-06 found no regex pattern using such a brace group.

**Decided 2026-11-06: A, shipped as a major version (2.0.0)**, with the change stated in the
release notes. It amends the decision of 2026-11-05: the `DECISIONS.md` entry and section 1's
third **Decided** bullet ("with neither, behaviour is exactly today's") need the exception "except
patterns that contain a literal brace group starting with `t`".

### 10.14 Flag name, bit value, inline letter, counts and changes

**The question.** What is the option flag called, which bit does it take, does it get an inline
letter, and how do the counts report swaps?

**Context and evidence.**

- **Name.** The word others use is "transposition": Lucene's `transpositions` parameter,
  Elasticsearch's `transpositions` and `fuzzy_transpositions`, liblevenshtein's
  `Algorithm::Transposition`, uFuzzy's `intraTrn` (all read). The new counts property would carry
  the same word.
- **Bit.** `FuzzyRegexOptions` is an `int` enum whose values are upstream's flag values
  (`FuzzyRegexOptions.cs:7-10`). The highest bit in use is `0x10000` (`Posix`), in upstream
  (`upstream/regex/_regex_core.py:74-91`) and in the port, both checked 2026-11-06. `0x40000000`
  is free in both and stays below the sign bit. A flag that applies to the whole pattern must also
  join `GlobalFlags` (`Parsing/RegexFlags.cs:82`, today `AllVersions | BestMatch | Debug |
  EnhanceMatch | Posix | Reverse`).
- **Inline letter.** Upstream's Python API already has `T = TEMPLATE = 0x1`, a flag with no inline
  form, and `D = DEBUG = 0x200`. Measured: `(?t)a` and `(?T)a` both raise "unknown extension at
  position 2", so `t` is free inline today, but a letter could clash with a later upstream one.
  In-pattern control already exists: `t` inside braces, the letter NR-grep (`-k1t`) and
  fuzzy-regex-rs (`{t<=1}`) also use.
- **Counts.** `FuzzyCounts` is a positional record struct `(Substitutions, Insertions,
  Deletions)` (`FuzzyCounts.cs:28-32`), and the README deconstructs it as
  `(int substitutions, int insertions, int deletions) = match.FuzzyCounts` (`README.md:77`).
  fuzzy-regex-rs kept its triple too: measured, `fuzzy_counts()` reports a one-swap match as
  (0,0,0), and the swap appears only in `edits().swaps` and the total.

**Options.**

- **Name:** `Transpositions`; `TranspositionsAsOneError` (says the effect, but long);
  `DamerauEdits` (needs a definition to read).
- **Bit:** `0x40000000`, with a `DIVERGENCES.md` row; or the next bit after upstream's,
  `0x20000`, which collides as soon as upstream adds a flag.
- **Inline letter:** none in the first version; or `(?t)`.
  None: Pros: no clash with a future upstream letter. Cons: a pattern cannot turn the flag on by
  itself. Without the flag, 10.3's zeroing means "the usual errors plus swaps" must name every
  kind, `{i,d,s,t,e<=1}`, where the flag makes it `{e<=1}`. A caller who stores patterns as text
  and cannot pass options has only the longer form.
  `(?t)`: Pros: `(?t)(?:tesco){e<=1}` is self-contained. Cons: a letter upstream may later take,
  next to its `T`; one more divergence in pattern syntax.
- **Counts, A:** keep the positional triple and add a non-positional `int Transpositions { get;
  init; }`, included in `Total`, plus `FuzzyChanges.Transpositions` holding the text index of the
  first swapped character. Pros: every existing deconstruction compiles and means the same;
  with swaps off, `Transpositions` is 0 and `Total` is unchanged. Cons: as in fuzzy-regex-rs, a
  caller who deconstructs the triple sees (0,0,0) for a one-swap match whose `Total` is 1, so the
  XML docs and the README example must say so.
- **Counts, B:** a fourth positional member. 2.0.0 (10.13) permits a breaking change, but every
  caller who deconstructs the triple would have to change code for a feature most will not use.

**Recommendation:** `FuzzyRegexOptions.Transpositions = 0x40000000`, added to `GlobalFlags`; no
inline letter; counts A. The bit check settles the value. The longer brace form is the price of
no letter, and it costs nothing in correctness; a letter can be added later without breaking
anything, while one taken now cannot be withdrawn. fuzzy-regex-rs shows the counts shape in use,
including the trap the docs must cover. Confidence high.

### 10.15 Comment on upstream issue #400?

**Context.** #400 asks for Damerau-Levenshtein as an option. It has been open since 2021-01-28,
with no comments and 3 thumbs-up (read 2026-11-06). The upstream-report rule is a log only, and
the owner approves any text before it is filed.

**Options.**

- **A. Say nothing now; log the feature in `LEDGER.md` when it ships.** Pros: follows the rule.
- **B. Comment now with the proposed syntax.** Pros: could align a future upstream syntax. Cons:
  five years without a reply suggests no effect; the feature is not built.
- **C. Comment when it ships, pointing to a working design.**

fuzzy-regex-rs uses `t` in braces and NR-grep uses it in `-k1t`; with this port that makes `t` the
obvious letter if upstream ever adds the kind.

**Decided 2026-11-06: A**; reconsider commenting (C) when 2.0.0 ships.

### 10.16 Build it, or document the workaround?

**Evidence.** One user so far (section 2). Upstream #400: 3 thumbs-up and no comments in over five
years. fuzzy-regex-rs, the regex engine closest to this one with swaps: 6 GitHub stars, created
2026-01-04 (GitHub API, 2026-11-06). The other pattern matchers with swaps (section 4) are NR-grep
(2001), liblevenshtein (repository created 2025-10-26, 4 stars) and smaller tools. Lucene and Elasticsearch turn swaps on by default, so their users get them
without asking, which says nothing either way about demand in regex form. The workaround of
section 9 was run on upstream and works, but reports the swap as no error and needs one list entry
per adjacent pair.

**Options.**

- **A. Document the workaround in `GUIDE.md` now; build nothing yet.** Pros: correct today, no
  divergence. Cons: the counts hide the swap.
- **B. Build the literal-only first version after 1.3.** Cons: the costs of sections 7 and 8 and
  the first syntax upstream lacks.
- **C. Build beyond literals.** Cons: 10.10's work on top of B.

**Decided 2026-11-06: B**, after 1.3, with the workaround documented now (A) as well. Keep tier 1
(adjacent single-character items) cheap to add later: one general definition of a swap, and a
reference matcher built on it from the start.

### 10.17 Where the fourth kind lives in a `FUZZY` node's values

**The question.** Where does a compiled `FUZZY` node keep the minimum, maximum and cost of `t`?

**Context.** A `FUZZY` node carries its constraints as packed blocks of four values, one slot per
kind in the order Sub = 0, Ins = 1, Del = 2, then Err = 3: minima from `MinBase` = 1, maxima from
`MaxBase` = 5, costs from `CostBase` = 9 (`Engine/Node.cs`, `FuzzyValue`, a port of upstream's
`RE_FUZZY_VAL_*` defines, `_regex.c` lines 176-200). `Count` = 3 kinds are counted separately.
Slot 3 of each block is `Err`, so there is no free slot for a fourth kind. The compile-parity
corpus (`tests/FuzzyRegex.Tests/Gaps/CompileParity/`) compares these values with upstream's
compiled code. 283 lines in `src/` mention `FuzzyValue`, 234 of them in `Matcher.cs`
(`grep -rn FuzzyValue src --include=*.cs`, 2026-11-06).

The flag-off guarantee (section 1) is that a pattern with neither the flag nor `t` behaves exactly
as today; the layout decides whether that holds by construction or has to be tested.

**What others do.** Not comparable: fuzzy-regex-rs keeps its limits in a struct (`src/types.rs`),
and liblevenshtein chooses the algorithm for the whole automaton (`Algorithm::Transposition`).
Neither has upstream's packed values.

**Options.**

- **A. Renumber: four kinds per block, Err at 4, bases shifted.**
  Pros: one layout; every loop over `FuzzyValue.Count` picks up the new kind.
  Cons: every fuzzy pattern compiles to different values from upstream, so the compile-parity
  corpus breaks for every fuzzy row, flag or not; every loop over the kinds runs four times on
  every fuzzy match, swaps or not; every use above to review.
- **B. A separate block for `t`, present only when `t` or the flag is in use.**
  Pros: without `t` and the flag, every node's values are identical to today's, so the
  compile-parity corpus holds unchanged and the loops stay at three kinds.
  Cons: two layouts; every reader of `t`'s values must first check whether the block exists, a
  branch on the fuzzy path.
- **C. A block for `t` always appended after upstream's values** (minimum, maximum and cost of
  `t`; maximum 0 when neither `t` nor the flag is in use).
  Pros: upstream's values stay an identical prefix of every node, so the compile-parity corpus
  compares that prefix; one layout and no presence check; loops over the three kinds are
  untouched, and the swap arm reads `t`'s maximum at a fixed place, a check every option needs
  anyway. With a maximum of 0 the swap arm never runs, which makes the flag-off guarantee a
  one-line argument.
  Cons: every `FUZZY` node grows by three values, swaps or not (a few bytes per fuzzy section,
  once per compiled pattern); the compile-parity test must compare a prefix for `FUZZY` nodes,
  a small change to the test.

Either way, the per-match counts need a fourth count or a separate field: `FuzzyCountBuffer` is
sized by `FuzzyValue.Count` (`Engine/InlineBuffers.cs:42-43`), so the match state grows, which the
allocation check of every perf change covers.

**Recommendation: C.** It keeps B's main property, upstream's values unchanged, without B's
presence check, and avoids A's cost on every fuzzy pattern. Its only costs are three values per
compiled section and a prefix comparison in one test, both known now. The build-time gate still
applies: the BenchmarkDotNet fuzzy rows with the flag off against main (A-B-B-A) and the allocation
check. Confidence high.

## 11. Documents to update when it ships

`docs/DIVERGENCES.md` (a row for the flag and one for `t`), `docs/COMPARISON.md` (a "no upstream
equivalent" section beside the cost forms), `docs/GUIDE.md` (fuzzy matching; the section 9
workaround), `README.md` (fuzzy counts example), `CHANGELOG.md`, `docs/plan/upstream-reports/LEDGER.md`
if #400 is answered, and the XML docs on `FuzzyRegexOptions`, `FuzzyCounts` and `FuzzyChanges`.
`2026-09-18-fuzzy-regex-rs-techniques.md:42` listed swaps under "never copy" as a semantic of that
engine; the `DECISIONS.md` entry of 2026-11-05 records why an opt-in form is now acceptable.
