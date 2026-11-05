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
  7(3):171-176: more than 80% of misspellings were one insertion, deletion, substitution or
  transposition. Read through secondary citations only (for example
  https://en.wikipedia.org/wiki/Damerau%E2%80%93Levenshtein_distance and Toutanova & Moore, ACL
  2002, https://aclanthology.org/P02-1019.pdf). **The primary paper was not read.**
- Pollock & Zamora (1983), JASIS 34(1):51-58, over 50,000 misspellings: among single errors,
  deletions about 34%, insertions 27%, substitutions 19%, transpositions about 12.5%; 90-95% of
  misspellings had one error (abstract: https://asistdl.onlinelibrary.wiley.com/doi/10.1002/asi.4630340108;
  the per-kind split from a secondary summary, https://arxiv.org/pdf/cmp-lg/9702003). **Per-kind
  figures not read in the primary.**
- Rates vary by language and input method (2.6% and 13.1% in two Sindhi corpora,
  https://arxiv.org/pdf/1403.4759). The figures describe human typing, not machine-generated text.

## 3. Terms

- **Transposition (swap):** the text has two adjacent characters in the opposite order to two
  adjacent pattern items: pattern "ab", text "ba".
- **Restricted transposition, or optimal string alignment (OSA):** no character takes part in more
  than one edit, so "CA" to "ABC" costs 3. **Unrestricted Damerau-Levenshtein** allows a swapped
  pair to be edited again, so the same costs 2
  (https://en.wikipedia.org/wiki/Damerau%E2%80%93Levenshtein_distance;
  https://rapidfuzz.github.io/RapidFuzz/Usage/distance/OSA.html).

## 4. Prior art

| Library | Swaps? | Notes |
|---|---|---|
| Lucene `FuzzyQuery` | Yes, default on | "based on the Damerau-Levenshtein (optimal string alignment) algorithm"; `transpositions=false` gives classic Levenshtein. https://lucene.apache.org/core/9_12_0/core/org/apache/lucene/search/FuzzyQuery.html (default value read from a truncated constants page: confirm) |
| Elasticsearch fuzzy query | Yes, default on | `transpositions`: "Defaults to `true`". https://www.elastic.co/docs/reference/query-languages/query-dsl/query-dsl-fuzzy-query |
| Apache Commons Text `DamerauLevenshteinDistance` | Yes (OSA) | https://github.com/apache/commons-text |
| RapidFuzz | Both | separate `DamerauLevenshtein` and `OSA` functions |
| fuzzy-regex-rs (Rust) | Yes | the only regex engine found with fuzzy swaps: `{t<=1}`, `{2i+1d+1s+1t<=4}`. Its intro page shows `(?:hello){e<=1}` matching "hlelo" as "1 transposition", so its `e` includes swaps **by default**, unlike this proposal. No on/off option documented; OSA or not, and which explanation it reports, not documented. https://raw.githubusercontent.com/kakserpom/fuzzy-regex-rs/master/README.md, https://kakserpom.github.io/fuzzy-regex-rs/intro.html |
| mrab-regex (upstream) | No | issue #400, "Fuzzy matching should support for Damerau-Levenshtein distance", open since 2021-01-28 with no reply (https://github.com/mrabarnett/mrab-regex/issues/400; triaged A in `2026-08-31-upstream-issue-triage.md:38`) |
| TRE, agrep, Hyperscan, PostgreSQL `levenshtein()`, Python fuzzysearch | No | insertion, deletion and substitution only |

The search libraries that count swaps all use the restricted form and default it on. Regex
engines, apart from fuzzy-regex-rs, do not count swaps at all.

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
| `{e<=2,t<=1}` | i, d, s, plus at most one t | same |
| `{1i+1d+1s<=2}` | i, d, s; t not allowed | **open question 3** |
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
  costs a look-ahead at every failing position. Open question 1.
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
  must set (open question 12). The counts in the first three rows confirm that today a swap is
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
  `"dis"`). Today `t` is a parse error, so no existing pattern changes meaning.
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
  character is tested (open question 4).
- Partial matching (`CheckFuzzyPartial`, `ErrorArmAtTheEdge`) needs an answer when the text ends
  after the first swapped character (open question 5). Reverse search (`(?r)`) mirrors the step.
- **The literal prefilter becomes unsound.** `FuzzyLiteralFilter` (`Engine/FuzzyLiteralFilter.cs:17-25`)
  relies on "one substitution, insertion or deletion damages at most one piece" (Navarro 2001,
  section 8.1). A swap across a piece boundary damages two. Either count each allowed swap as 2
  in `k` (`MostErrors`, `:878-895`) or withhold the filter when swaps are allowed.
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
   with care: its `e` includes swaps by default, so compare against this port with the flag on.
4. **Metamorphic invariants** (`docs/ORACLE-INVARIANTS.md`): allowing swaps never removes a match;
   with the flag, the best cost is at most the cost without it; reported counts add up to `Total`.
5. **A soundness argument for the prefilter change,** with a witness test that fails without it
   (rule 12).
6. Oracle generators must never emit `t` or the flag for upstream rows.

## 9. Alternatives without engine changes

- **Caller-generated variants (works today).** Put the word and its adjacent-swap variants in a
  named list and accept either: `\L<v>|(?:tesco){e<=1}` with `v` = {tesco, etsco, tseco, tecso,
  tesoc} (`FuzzyRegex(pattern, options, namedLists)`, `src/FuzzyRegex/FuzzyRegex.cs:244-248`). This
  means "one swap or one other error". **Not yet run.**
- **`{e<=2}`** accepts every two-error string ("DISCO" for "tesco"). Too loose.
- **Cost weights** cannot make a swap cost 1: a cost is linear in the three counts
  (`Matcher.cs:9556-9558`), and a swap is two of them.

Worth documenting the first in `GUIDE.md` whether or not the feature is built.

## 10. Open questions and research still needed

1. **Order of `t` among the kinds** (section 5.3): first, last, or tried only after an exact and
   a substitution attempt both fail at a position? Measure the effect on which match is found
   first and on speed.
2. **What the reported counts promise** without `BestMatch`: "a valid explanation" (today's
   contract for i, d, s) or "the cheapest"? State it in the docs either way.
3. **`t` in a cost equation that does not name it, with the flag on:** today an unnamed kind costs
   nothing (`COMPARISON.md`, cost forms), which would make swaps free. Options: not allowed, or a
   default weight of 1. Not allowed is safer.
4. **Edit tests** (`{t<=1:[a-z]}`): must both swapped characters pass the test, or either?
5. **Partial matching** at the end of the text halfway through a swap.
6. **Across nodes:** classes, `.`, group boundaries, repeats and calls. Which are worth supporting
   after the literal-only version, and how upstream's node-at-a-time design takes a two-item step.
7. **Case folding:** simple folding should work per character; full folding (one character to
   two) needs a definition or a refusal.
8. **Unrestricted Damerau-Levenshtein:** confirm nobody needs it; OSA is what Lucene, Elasticsearch
   and Commons Text ship.
9. **Flag name, bit value and whether an inline letter is ever wanted.**
10. **Primary sources:** read Damerau 1964 and Pollock & Zamora 1983 directly; confirm Lucene's
    `defaultTranspositions` value from the source rather than the truncated constants page; read
    fuzzy-regex-rs's source to learn whether it is OSA and which explanation it reports.
11. **Upstream:** whether to comment on mrab-regex #400 with the design, under the
    upstream-report ledger's owner-approval rule. Matching any later upstream syntax matters more
    than being first.
12. **Equal-cost ties with different spans** under `BestMatch` (section 5.3): a whole-span swap
    against a shorter match with a deletion. Today's rule is cheapest, then fewer errors, then
    earliest; both start at 0 in the measured row, so a further rule (longest? first found?) is
    needed, and it must not change any answer when swaps are off.
13. **Demand:** one user so far (section 2). Worth checking whether the caller-side variants
    (section 9) cover it before committing engine work.

## 11. Documents to update when it ships

`docs/DIVERGENCES.md` (a row for the flag and one for `t`), `docs/COMPARISON.md` (a "no upstream
equivalent" section beside the cost forms), `docs/GUIDE.md` (fuzzy matching; the section 9
workaround), `README.md` (fuzzy counts example), `CHANGELOG.md`, `docs/plan/upstream-reports/LEDGER.md`
if #400 is answered, and the XML docs on `FuzzyRegexOptions`, `FuzzyCounts` and `FuzzyChanges`.
`2026-09-18-fuzzy-regex-rs-techniques.md:42` listed swaps under "never copy" as a semantic of that
engine; the `DECISIONS.md` entry of 2026-11-05 records why an opt-in form is now acceptable.
