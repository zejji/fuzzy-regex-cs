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
  substitutions, or a deletion plus an insertion).

This is the first addition of pattern syntax that upstream lacks. Every earlier difference from
upstream was API shape, speed or a fixed bug (`docs/DIVERGENCES.md`; `AGENTS.md:129-131`, "do not
'improve'"). That is the main cost of the feature, more than the engine work.

## 2. Why

A user matching hand-typed names against reference text found that `(?:tesco){e<=1}` misses
"TECSO" because the swap costs 2. The only workarounds today are looser (`{e<=2}` also accepts "DISCO") or need the caller
to generate swapped variants (section 9).

How common swaps are among typing errors:

- Damerau (1964), *A technique for computer detection and correction of spelling errors*, CACM
  7(3):171-176: about 80% of misspelled words were one wrong, missing or extra letter or one
  transposition of two adjacent letters. The words were those an information-retrieval system
  rejected from its list of acceptable terms, so the figure describes non-word errors only. Read
  through secondary citations only: Navarro 2001 (below), p. 35, "80% of these errors are corrected
  allowing just one insertion, deletion, substitution, or transposition [Damerau 1964]"; the
  thesis at https://arxiv.org/pdf/cmp-lg/9702003, chapter 2; Wikipedia's Damerau-Levenshtein
  article. **The primary paper was not read:** on 2026-11-06 `dl.acm.org` and `cacm.acm.org`
  answered 403 to a plain fetch and a Cloudflare challenge to `curl`. Reading it needs a browser
  session on the ACM Digital Library.
- Pollock & Zamora (1983), JASIS 34(1):51-58, over 50,000 misspellings from about 25 million words
  of scientific and scholarly text: 7.5% had more than one error, and the rest split into
  deletions 34%, insertions 27%, substitutions 19% and transpositions 12.5%. These four add up to
  92.5%, so they are shares of **all** misspellings, not of single errors as this note said
  before 2026-11-06. Source: the thesis above, chapter 2, read 2026-11-06. The abstract
  (https://asistdl.onlinelibrary.wiley.com/doi/10.1002/asi.4630340108) says 90-95% of misspellings
  had one error. **The primary's tables were not read** (Wiley and the ACM DL, for the 1984 CACM
  follow-up 27(4):358-368, both need a session).
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
  - **Unrestricted Damerau-Levenshtein** (Lowrance & Wagner 1975) allows a swapped pair to be
    edited again, so the same costs 2 (swap to "AC", insert B between).
  Measured 2026-11-06 with RapidFuzz 3.14.6: `OSA.distance("CA","ABC")` = 3,
  `DamerauLevenshtein.distance("CA","ABC")` = 2
  (https://en.wikipedia.org/wiki/Damerau%E2%80%93Levenshtein_distance;
  https://rapidfuzz.github.io/RapidFuzz/Usage/distance/OSA.html).
- **Navarro 2001:** G. Navarro, *A guided tour to approximate string matching*, ACM Computing
  Surveys 33(1):31-88, read 2026-11-06 from
  https://users.csc.calpoly.edu/~dekhtyar/570-Fall2011/papers/navarro-approximate.pdf. Cited by
  section and page below.

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
| fuzzy-regex-rs (Rust) | Yes, on by default, OSA | Read from source at commit `d3c0a577` (2026-08-11); not run (no Rust toolchain here). `e` includes swaps unless a builder option `mrab_compat` is set (`src/api/builder.rs:165-173`). A swap consumes both characters and the next step only clears a flag, so the pair is never edited again (`src/engine/damlev.rs:212-221`, `531-541`); its tests check against a reference named "optimal-string-alignment (restricted Damerau)" (`src/engine/bitap.rs:4033-4035`). A swap needs the two pattern characters to differ (`damlev.rs:566-570`). Among candidates it prefers the leftmost, then fewest edits, then fewest insertions plus deletions, then the longest (`damlev.rs:648-660`). `fuzzy_counts()` is still the `(s, i, d)` triple without swaps; swaps are read from `edits().swaps` (`src/api/match_result.rs:244-257`). Naming `i`, `d` or `s` zeroes `t` unless `t` is named; naming `t` does not zero the others (`src/parser/ast.rs:201-214`, `278-286`). |
| mrab-regex (upstream) | No | Issue #400, "Fuzzy matching should support for Damerau-Levenshtein distance", migrated from Bitbucket by mrabarnett on 2021-01-28, labelled enhancement and major; open, no comments, 3 thumbs-up (read 2026-11-06; triaged A in `2026-08-31-upstream-issue-triage.md:38`). Measured with `regex` 2026.9.29: `{t<=1}` is literal text (section 10.13). |
| TRE | No | Read from the local source (`.venvs/tre-src`, commit `71bfcaf0`, 2026-05-23): `include/tre/tre.h:238-243` has costs and maxima for insertion, deletion and substitution only, and `TODO:24` lists "Transposition operation for the approximate matcher?" |
| Hyperscan 5.4.1 | No | Read: "three possible edit types considered: insertion, removal and substitution" (https://intel.github.io/hyperscan/dev-reference/compilation.html). |
| PostgreSQL 18 `fuzzystrmatch` | No | Read: `levenshtein` takes insertion, deletion and substitution costs; no transposition function in the module. |
| Python `fuzzysearch` | No | Read: README names Levenshtein distance, substitutions, insertions and deletions only. |
| Perl `String::Approx` 3.28 | No | Read: its notes say a transpose needs an edit distance of two. |
| agrep (Wu-Manber) | No, **unverified** | Not read from source. Secondary only: Wu and Manber describe insertions, deletions and substitutions; Navarro 2001 section 8.2.3 adapts their filter to transpositions as an extension. |

The search libraries that count swaps use the restricted form and turn it on by default.
Distance libraries offer both forms, and return a number without an explanation. Among regex
engines only fuzzy-regex-rs counts swaps; it turns them on by default and offers an opt-out.

## 5. Semantics

### 5.1 What counts as a swap (proposal)

A swap matches two consecutive single-character pattern items against two text characters in the
opposite order: text[p] matches item[k+1] and text[p+1] matches item[k]. It costs one error and
advances past both items and both characters. Each swapped character is used by that edit only,
so the engine is restricted (OSA) by construction. Unrestricted Damerau-Levenshtein would need the
engine to remember earlier edits and is not proposed.

A first version allows swaps **only inside a literal** (`STRING` and its case-insensitive forms),
where both items are characters of one node. Swaps across nodes (a class followed by another node,
a group boundary, a repeat, `END_FUZZY`, a call) and under full case folding (ß folding to "ss")
are refused in the first version and listed as open questions.

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
  must set (question 10.7). The counts in the first three rows confirm that today a swap is
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
  "ab{t<=1}". Measured upstream 2026-11-06 (`regex` 2026.9.29, span (0, 8)); this port follows the
  same code path but was not run (no .NET runs during the timing window). Recognising `t` changes the meaning of such patterns (question
  10.13).
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
  (`Engine/Node.cs:466-481`). A fourth renumbers the value layout (`Node.cs:457-531`), which the
  compile-parity corpus compares with upstream's bytecode.
- `FuzzyValue.` is used about 220 times in `Matcher.cs` and about 50 times elsewhere, including the
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
  Question 10.12.
- Size: a literal-only first version touches the parser, the permission and cost functions, the
  string arms, the prefilter, the public types and the docs. Swaps across nodes, case folding and
  partial matching add several times that. Comparable to the larger D-series fixes, each of which
  needed several blind-review rounds.

## 8. Verification

There is no upstream answer for any pattern that allows swaps, so the differential oracle cannot
judge them. Proposed instead, under `docs/VERIFICATION.md`'s rules:

1. **Flag off and no `t`: byte-identical behaviour.** The whole ratchet and the oracle at three
   seeds, unchanged. This is the compatibility guarantee and the first gate.
2. **An independent reference for literals:** a brute-force OSA matcher over every candidate span,
   compared with the engine under `BestMatch` (minimum cost) on generated literals and subjects.
3. **fuzzy-regex-rs as a second comparator** (already an external comparator, `S63-performance-gate.md`),
   with care: its `e` includes swaps by default, so compare against this port with the flag on;
   it reads `{e<=2,t<=1}` differently (question 10.3) and ranks ties differently (10.7), so
   compare only constraints where the two agree, and spans and totals rather than counts.
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

Rewritten 2026-11-06. Each question gives the context with an example, the options with their
pros and cons, what other engines do, and a recommendation with what would change it.

How the evidence was gathered: Python `regex` 2026.9.29 from `.venvs/regex-2026.9.29`, and
RapidFuzz 3.14.6 and jellyfish 1.3.0 from `.scratch/transpositions-venv`, all run with
`python -I` on 2026-11-06. This port was **not** run, because a timing run held the machine; where
a question says what the port does, it is read from the code and says so. Rows that describe the
feature itself are derived from the engine's search order and the measured upstream rows, since
the feature does not exist yet.

The research items of the old list are done and folded in: the primary sources (section 2, with
what could not be read and why), Lucene's default and fuzzy-regex-rs's semantics (section 4, read
from source), and demand (10.16). The old questions are all here: 1 is 10.5, 2 is 10.6, 3 is
10.4, 4 is 10.8, 5 is 10.9, 6 is 10.10, 7 is 10.11, 8 is 10.1, 9 is 10.14, 11 is 10.15, 12 is
10.7, 13 is 10.16. Questions 10.2, 10.3, 10.12 and 10.13 are new.

| # | Question | Recommendation | Confidence |
|---|---|---|---|
| 10.1 | How far apart may swapped characters be; may a swapped pair be edited again? | Adjacent only, never edited again (OSA) | High |
| 10.2 | Does a swap of two equal characters count? | No: only when the pair fails to match in order | High |
| 10.3 | Does naming `t` zero the kinds a constraint does not name? | Yes, the same rule as `i`, `d`, `s`; fix the 5.2 table | High |
| 10.4 | `t` and cost equations | Upstream's rules unchanged, `t` treated like the others | High |
| 10.5 | Where the swap sits in the search order | First, tried only when it fits | Medium |
| 10.6 | What the counts promise | One valid explanation; a cheapest one only under `BestMatch`/`EnhanceMatch` | High |
| 10.7 | Equal-cost ties with different spans under `BestMatch` | Add a last key, "more transpositions" | Medium |
| 10.8 | Edit tests, `{t<=1:[a-z]}` | Both swapped characters must pass | Medium |
| 10.9 | Partial matching halfway through a swap | Accept it as one transposition | High |
| 10.10 | Swaps across nodes | Literals only in the first version; never across a capture boundary | Medium |
| 10.11 | Case folding | Simple folding in the first version; no swaps on full-fold items yet | Medium |
| 10.12 | The literal prefilter | Count each allowed swap as 2 errors | High |
| 10.13 | `{t...}` is literal text today | Recognise `t` anyway, with a release note; **owner** | Medium |
| 10.14 | Flag name, bit, inline letter, counts | `Transpositions`, a high bit, no letter, a non-positional property | High |
| 10.15 | Comment on upstream #400? | Not now; **owner** | Medium |
| 10.16 | Build it, or document the workaround? | Document the workaround now; build when demand shows; **owner** | Medium |

### 10.1 How far apart may the swapped characters be, and may a swapped pair be edited again?

**Context.** "Transposition" means four different things in the literature, and they give
different answers on the same strings.

- **Adjacent swap, restricted (OSA).** Two neighbouring characters change places and neither is
  touched again. This is Damerau's operation. Navarro 2001, section 3.1, p. 37, lists
  "Transposition: (ab, ba) for a ≠ b, i.e. swap the adjacent letters a and b", and frames every
  distance in the survey so that "once the operation has converted a substring z into w, no
  further operations can be done on w". It is what section 5.1 proposes.
- **Adjacent swap, unrestricted (Lowrance & Wagner 1975, JACM 22(2):177-183).** The same swap, but
  the pair may be edited again: characters inserted between the two swapped letters, or
  characters deleted so that two letters become neighbours and then swap. Their algorithm is exact
  when a swap costs at least the average of an insertion and a deletion, 2W_T >= W_I + W_D.
  Read through Wikipedia's Damerau-Levenshtein article and Navarro section 5.1 (which cites them
  for the dynamic-programming rule); **the paper itself was not read** (ACM DL, as for Damerau).
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

- OSA and unrestricted differ only when a swap combines with a second edit inside or beside the
  pair ("ca" to "abc": swap, then insert "b" between).
- Swapping characters that are not neighbours ("abc" to "cba") costs 2 under every edit distance:
  two substitutions are already as cheap.
- Jaro gives "ab" and "ba" a similarity of 0, because for two-character strings the window is 0
  positions wide. It does not see the very swap this feature is for.
- Only block moves make "cdab" cheap.

**Options.**

- **A. Adjacent, restricted (OSA).**
  Pros: Damerau's definition, and what Lucene, Elasticsearch, Commons Text and fuzzy-regex-rs ship
  (section 4). It fits upstream's design: one arm reads two pattern items and two characters,
  moves past both, and remembers nothing. The independent reference of section 8 is a short
  dynamic program. Easy to explain: "two neighbours in the wrong order cost one error".
  Cons: not a metric. OSA("CA","AC") + OSA("AC","ABC") = 1 + 1 < OSA("CA","ABC") = 3, so the
  triangle inequality fails (Wikipedia, Damerau-Levenshtein). Nothing in this library relies on the
  triangle inequality, so this costs nothing here.
- **B. Adjacent, unrestricted (Lowrance & Wagner).**
  Pros: a true metric; "ca" to "abc" costs 2.
  Cons: a swap whose two halves are separated by insertions, or made adjacent by deletions, is an
  edit that spans other edits. The engine would have to carry a half-finished swap from step to
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
  No shipped engine or source found that defines it as an edit operation. It turns every pair of
  positions into a candidate: a word of n letters has n - 1 adjacent swaps but n(n - 1)/2 swaps at
  any distance (arithmetic, not measured on data), so the set of strings one error away from a
  word grows, and with it the false matches.

**What other engines do.** Every engine that counts swaps in a budget uses A (section 4). B
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

**Options.**

- **A. A swap is tried only when the pair does not match in order** (for literals, when the two
  characters differ after the section's case folding).
  Pros: Navarro's definition says a ≠ b; no duplicate paths; generalises to classes in 10.10.
  Cons: one extra comparison, which the exact attempt has usually just made.
- **B. No rule.** Cons: duplicate paths and a spurious error.

**What other engines do.** fuzzy-regex-rs requires the two pattern characters to differ
(`damlev.rs:566-570`).

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
  Pros: matches fuzzy-regex-rs. Cons: two rules, one of them unlike anything upstream has.

**What other engines do.** fuzzy-regex-rs chose B: "Transposition (`t`) is a fuzzy-regex extension
that mrab lacks, so it does NOT trigger this defaulting on its own: `{t<=1,e<=2}` leaves i/d/s
governed by the `e` budget" (`src/parser/ast.rs:211-214`). How it treats `{t<=1}` alone was not
established (not run).

**Recommendation: A**, and correct the 5.2 row for `{e<=2,t<=1}` to "t only". Confidence high.
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

**What other engines do.** fuzzy-regex-rs zeroes an unnamed `t` once any kind is in play, and
treats a capped kind missing from the equation as free: "operations priced at 0 (explicitly capped
but absent from the equation, so mrab matches them for free) may all be used"
(`src/parser/ast.rs:278-286`, `301-305`). That is A.

**Recommendation: A.** Confidence high. Without a cost equation the engine builds one with every
coefficient 1 (`Nodes.cs:4721-4729`), so with the flag on, `{e<=1}` prices a swap at 1, as section
5.2 wants.

### 10.5 Where the swap sits in the search order

**Context.** At a pattern item that fails to match, the engine tries substitution, then
insertion, then deletion (section 5.3), and without `BestMatch` the first path that fits the
budget is the match. Measured upstream, where no swap exists: `(?:ab){e<=1}` searched in "ba"
answers (0,1) with one deletion. Substituting "b" for "a" leaves "a" against "b" with no budget;
inserting "b" leaves "b" unmatched at the end; deleting the pattern's "a" lets "b" match. With the
flag on, the swap's place in that order decides the answer:

| Order | `(?:ab){e<=1}` in "ba" | `(?:tesco){e<=2}` in "tecso" |
|---|---|---|
| Swap last, after deletion | (0,1), one deletion | (0,5), two substitutions |
| Swap first, only when it fits | (0,2), one transposition | (0,5), one transposition |

**Options.**

- **A. Last.** Pros: every path found today is still found first. Cons: a user who turns swaps on
  still gets half the word in the first row and two substitutions in the second; the swap is
  reported only when nothing else fits.
- **B. First, tried only when it fits** (text[p] matches item k+1, text[p+1] matches item k, and
  the pair fails in order, 10.2).
  Pros: when it fits, a swap costs 1 where the paths it displaces cost 2, so it is the locally
  cheapest edit; it reports what the user means; fuzzy-regex-rs and the distance libraries pick
  the fewest edits, so they agree. Cons: for a pattern that allows swaps, the first match can
  differ from the same pattern without them (the point of opting in). Costs one two-character
  check at each failing item, in sections that allow swaps only.
- **C. Second, after substitution.** Gives the second row's two substitutions; no advantage over
  B.

**Cost and risk.** With the flag off and no `t`, the arm must never run: a per-section check
before the look-ahead, measured as the house rules require (BenchmarkDotNet fuzzy rows against
main, A-B-B-A for small effects). Upstream's walk increments `fuzzy_type` from `Sub`; keeping
Sub, Ins and Del at their values and adding the swap after them, then special-casing the walk to
try it first, keeps the value layout the compile-parity corpus compares (section 7).

**What other engines do.** No regex engine with a search order has swaps except fuzzy-regex-rs,
whose automaton keeps every path and picks by fewest edits (`damlev.rs:648-660`), so order does not
arise there.

**Recommendation: B.** Confidence medium: the semantics are clear, the speed cost is not measured.
What would change it: a measured slowdown on swap-enabled rows the owner will not accept, or a
pattern where swap-first finds a clearly worse first match.

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
the fewest-edit path per start (`damlev.rs:611-619`).

**Recommendation: A**, stated in the XML docs of `FuzzyCounts` and in `GUIDE.md`. Confidence high.

### 10.7 Equal-cost ties with different spans under `BestMatch`

**Context.** Measured on upstream and on this port 1.0.0 (section 5.3): `(?b)(?:ab){e<=2}` searched
in "ba" answers (0,1), one deletion. With swaps, (0,2) with one transposition also costs 1. Today's
ranking is cost, then fewer errors, then earliest start: both candidates cost 1, have one error and
start at 0. A further key is needed.

**Options.**

- **A. No key; the first found wins.** Cons: the answer depends on search order, so a refactor
  can flip it.
- **B. Longest span.** Pros: reads naturally. Cons: as a general key it changes today's answers
  where ties already exist, so it would have to apply only when swaps are allowed: two rankings.
- **C. More transpositions.** With swaps off every candidate has 0, so today's ranking is
  unchanged by construction. With swaps on it prefers the candidate that explains both characters
  of the pair over the one that leaves a character out of the match.
- **D. Fewer insertions plus deletions** (fuzzy-regex-rs). Cons: changes today's ties between
  kinds, so it breaks the flag-off guarantee.

**What other engines do.** fuzzy-regex-rs: leftmost, fewest edits, fewest insertions plus
deletions, longest (`damlev.rs:648-660`).

**Recommendation: C**, proved by the flag-off ratchet and oracle staying identical, plus a test on
the "ba" row. Confidence medium. What would change it: a case where preferring the swap picks a
plainly worse span.

### 10.8 Edit tests: `{t<=1:[a-z]}`

**Context.** A test set limits the characters an edit brings into the match: a substitution and an
insertion are tested on the text character, a deletion is not tested (`COMPARISON.md`, "constrain
which characters an edit may touch"). A swap brings in two text characters, each at the place of
the other's pattern character. Example: `(?:a-b){t<=1:[a-z]}` against "-ab" swaps "a" and "-".
Measured upstream with today's kinds:

| Pattern | Text (fullmatch) | Counts | Changes (s, i, d) |
|---|---|---|---|
| `(?:a-b){s<=2:[a-z]}` | -ab | no match | |
| `(?:a-b){e<=2:[a-z]}` | -ab | (0,1,1) | ([], [1], [0]) |

The second row moves the "a" (delete it, insert it after the hyphen), so only the letter "a" is
tested and it passes.

**Options.**

- **A. Both swapped characters must pass.** "-ab" is refused at cost 1.
  Pros: the same answer as two substitutions; matches the likely intent of `[a-z]` ("only letters
  may be edited; punctuation must be exact"). Cons: stricter than today's insertion-plus-deletion
  route at cost 2, which accepts "-ab".
- **B. At least one must pass.** "-ab" is accepted.
  Pros: the same answer as the insertion-plus-deletion route. Cons: lets punctuation move under a
  letters-only test.
- **C. Not tested**, as a deletion is not, since a swap adds no character the pattern lacks.
  Cons: makes the test meaningless for swaps.

**What other engines do.** Not established: fuzzy-regex-rs's handling of test sets with swaps was
not read. No other engine with swaps has test sets.

**Recommendation: A.** Confidence medium; B is defensible, and the choice is about intent rather
than evidence. What would settle it: a user's example of a test set used with swaps.

### 10.9 Partial matching halfway through a swap

**Context.** A partial match means "the text ran out, and so far nothing contradicts the pattern".
The `partial-prefix-closure` invariant (`docs/ORACLE-INVARIANTS.md:265-268`) says that if a full
match exists, every truncation of it inside the span gives a partial match. Example:
`(?:tesco){t<=1}` with `partial: true` on "tec". The full text "tecso" matches with one swap, and
"tec" ends after the first swapped character. With `t` alone nothing else can explain the "c".
Measured upstream for comparison: `(?:tesco){d<=1}` on "tec" gives a partial (0,3) with one
deletion; `(?:tesco){s<=0}` gives only the empty partial (3,3).

**Options.**

- **A. Accept the half swap at the end of the text as a partial match, counting one
  transposition** at that position. Pros: keeps the prefix invariant. Cons: the count promises a
  swap the text has not finished.
- **B. Refuse it.** "tec" then gets only the empty partial (3,3), though "tecso" matches: the
  invariant breaks.
- **C. Accept it but charge nothing.** Cons: lets `{t<=0}` accept it; the budget is wrong.

Under `{e<=1}` with the flag, the substitution arm already gives the partial, so the question
only bites when substitutions are not allowed.

**Recommendation: A**, with reverse search (`(?r)`) mirrored. Confidence high for accepting,
medium for how the count is reported.

### 10.10 Swaps across nodes

**Context.** The first version allows swaps only inside one literal node. Example outside it:
`(?:\d-\d){t<=1}` against "-12" swaps the class `\d` with the hyphen. Cases, in rising difficulty:

1. Single-character items in one sequence: classes, `.`, `\d`, properties, and literal characters
   that sit in different nodes.
2. The edge of a repeat: `a+b` against "aaba".
3. A capture boundary: `(a)(b)` against "ba". Group 1 and group 2 would each have to take the
   other's character, so their spans cross. There is no sensible answer.
4. Alternation, calls, lookaround, and the edge of a fuzzy section (which section pays for a swap
   whose halves sit in two of them?).

Upstream's engine works one node at a time; a swap needs the next item, which is known when the
compiler builds the node list only in case 1 with nothing in between.

**Options.**

- **A. Literals only, permanently.** Pros: smallest. Cons: `(?:\w+ jumps){t<=1}`-style patterns
  never get swaps where a class meets a literal.
- **B. Then case 1**, checked when the pattern is compiled: adjacent single-character items in one
  sequence with no capture boundary, repeat edge, branch, lookaround, call or section edge between
  them. Pros: covers classes, the next most common need. Cons: a second implementation step with
  its own reference matcher.
- **C. Everything.** Cons: case 3 has no defined answer; cases 2 and 4 multiply the engine work.

**What other engines do.** Lucene, Elasticsearch and the distance libraries work on whole strings
and have no structure to cross. fuzzy-regex-rs computes swaps in its literal automata
(`damlev.rs`, `bitap.rs`), reached from the matcher through a bridge keyed by literal text
(`src/engine/matcher.rs`, around 2855-2870); whether it swaps across classes or groups was not
established.

**Recommendation: A for the first version, B when a user asks, never case 3.** Confidence
medium.

### 10.11 Case folding

**Context.** Under simple case folding (`(?i)`) every character folds to one character, so the swap
check uses the same comparison as the exact match: `(?i)(?:tesco){t<=1}` against "TECSO" is a swap
of "C" and "S". Under full case folding (`(?f)`) one character can fold to several: "ß" matches
"ss". With pattern `(?fi)(?:aß){t<=1}`, the text "ssa" is the two items in the opposite order (a
swap of items, three characters), while "sas" would split the "ß" fold across the swap.

**Options.**

- **A. Swaps of items:** each item is matched as it normally would be, with its own fold length.
  "ssa" is a swap, "sas" is not.
- **B. Swaps of folded characters:** "sas" is also a swap. Cons: a swap inside one pattern item.
- **C. First version: no swap when either item's full fold is more than one character;** the
  other edits still apply. Pros: no new rule yet. Cons: a silent gap, which the docs must state.
- **D. A compile error for `(?f)` with swaps.** Cons: refuses every pattern for one rare letter
  set.

**What other engines do.** Not established for any engine with swaps.

**Recommendation: simple folding in the first version; C for full folding, then A.** Confidence
medium for simple folding, low for full folding. What would settle it: reading how the full-fold
string arms count a substitution on "ß" (`Engine/Matcher.cs:12774`, `:13460`) and choosing the
swap rule that matches.

### 10.12 The literal prefilter

**Context.** `FuzzyLiteralFilter` splits a literal into k + 1 pieces, where k is the most errors
allowed; one piece must then occur unchanged, because one insertion, deletion or substitution
damages at most one piece. A swap across a piece boundary damages two. Example: `(?:tesco){t<=1}`
with k = 1 might split into "tes" and "co"; "tecso" changes the "s" in the first and the "c" in
the second, so neither occurs, the filter finds no candidate, and a real match is missed.

**Options** (the first two are Navarro 2001, section 8.2.3, p. 64):

- **A. Count each allowed swap as 2 in k** (`MostErrors`, `Engine/FuzzyLiteralFilter.cs:878-895`),
  which is the "2k + 1 pieces" remedy. Pros: a one-line change with a published proof. Cons:
  shorter pieces, so a weaker filter, only for sections that allow swaps.
- **B. Leave a one-character hole between pieces**, "so that the transposition cannot alter
  both". Pros: keeps the pieces almost as long. Cons: more code and a new proof obligation.
- **C. Withhold the filter when swaps are allowed.** Pros: trivially sound. Cons: the slowest.

**Recommendation: A,** with a witness test that fails without it (rule 12); B only if a benchmark
shows the weaker filter matters. Confidence high.

### 10.13 `{t...}` is literal text today

**Context.** A brace group that is neither a quantifier nor a valid constraint is literal text
(section 6, corrected). Measured upstream: `(?:ab){t<=1}` matches "ab{t<=1}",
`(?:ab){e<=1,t<=1}` matches "ab{e<=1,t<=1}", and `a{t}` matches "a{t}"; this port follows the same
code path (not run). Once `t` is recognised, these become fuzzy constraints. So the decision of
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

How likely such a literal is was not measured; a search of public code for `{t<=` in regex
patterns would settle it.

**Recommendation: A**, with the release note. Confidence medium. **Owner:** it qualifies a
decided item.

### 10.14 Flag name, bit value, inline letter, counts and changes

**Context and options.**

- **Name.** `Transpositions` (Lucene's parameter and Elasticsearch's field use the word),
  `TranspositionsAsOneError` (says the effect, but long), `DamerauEdits` (needs a definition to
  read). Recommend `FuzzyRegexOptions.Transpositions`: the industry's word, and the same as the
  new counts property.
- **Bit.** `FuzzyRegexOptions` is an `int` enum whose values are upstream's flag values, the
  highest `Posix = 0x10000`. Recommend a high bit such as `0x40000000`, which avoids the sign bit
  and stays clear of anything upstream is likely to add, with a `DIVERGENCES.md` row.
- **Inline letter.** None of `t`, `T` and the other unused letters compiles as an inline flag
  upstream (measured, `(?t)a` raises). A letter could clash with a later upstream one, and `t` in
  braces already gives in-pattern control. Recommend none in the first version.
- **Counts and changes.** As section 6: keep the positional `(Substitutions, Insertions,
  Deletions)`, add `int Transpositions { get; init; }`, include it in `Total`, and add
  `FuzzyChanges.Transpositions` holding the subject index of the first swapped character. With
  swaps off, `Transpositions` is 0 and `Total` is unchanged. fuzzy-regex-rs made the same choice:
  `fuzzy_counts()` stays the upstream triple and swaps are read separately
  (`src/api/match_result.rs:244-257`).

**Recommendation:** as above. Confidence high.

### 10.15 Comment on upstream issue #400?

**Context.** #400 asks for Damerau-Levenshtein as an option. It has been open since 2021-01-28,
with no comments and 3 thumbs-up (read 2026-11-06). The upstream-report rule is a log only, and
the owner approves any text before it is filed.

**Options.**

- **A. Say nothing now; log the feature in `LEDGER.md` when it ships.** Pros: follows the rule.
- **B. Comment now with the proposed syntax.** Pros: could align a future upstream syntax. Cons:
  five years without a reply suggests no effect; the feature is not built.
- **C. Comment when it ships, pointing to a working design.**

Two engines using `t` (fuzzy-regex-rs and this one) makes `t` the obvious letter if upstream ever
adds it.

**Recommendation: A, with C open.** Confidence medium. **Owner:** a policy call.

### 10.16 Build it, or document the workaround?

**Evidence.** One user so far (section 2). Upstream #400: 3 thumbs-up and no comments in over five
years. fuzzy-regex-rs, the one regex engine with swaps: 6 GitHub stars, created 2026-01-04 (GitHub
API, 2026-11-06). Lucene and Elasticsearch turn swaps on by default, so their users get them
without asking, which says nothing either way about demand in regex form. The workaround of
section 9 was run on upstream and works, but reports the swap as no error and needs one list entry
per adjacent pair.

**Options.**

- **A. Document the workaround in `GUIDE.md` now; build nothing yet.** Pros: correct today, no
  divergence. Cons: the counts hide the swap.
- **B. Build the literal-only first version after 1.3.** Cons: the costs of sections 7 and 8 and
  the first syntax upstream lacks.
- **C. Build beyond literals.** Cons: 10.10's work on top of B.

**Recommendation: A now whatever else happens; B if the user's case needs the counts or a second
request arrives.** Confidence medium. **Owner:** a priority call.

## 11. Documents to update when it ships

`docs/DIVERGENCES.md` (a row for the flag and one for `t`), `docs/COMPARISON.md` (a "no upstream
equivalent" section beside the cost forms), `docs/GUIDE.md` (fuzzy matching; the section 9
workaround), `README.md` (fuzzy counts example), `CHANGELOG.md`, `docs/plan/upstream-reports/LEDGER.md`
if #400 is answered, and the XML docs on `FuzzyRegexOptions`, `FuzzyCounts` and `FuzzyChanges`.
`2026-09-18-fuzzy-regex-rs-techniques.md:42` listed swaps under "never copy" as a semantic of that
engine; the `DECISIONS.md` entry of 2026-11-05 records why an opt-in form is now acceptable.
