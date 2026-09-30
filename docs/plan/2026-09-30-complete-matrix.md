# The complete interaction matrix (2026-09-30)

The owner asked for the size of the remaining correctness work before any more fixing: "an extremely
thorough matrix, identifying the expected, correct answers based on careful research (including
surveys of various engines and regex principles) and the cases in which the port is incorrect."
This document has two parts. **Part A is the answer key**: for every cell of the matrix, what the
correct behaviour is and how we know it. **Part B is the measurement** of the port against that key,
written by the harness work. The port and upstream are not judges in Part A. Upstream (python
`regex` 2026.9.10) is one surveyed engine among seven.

## Part A: the answer key

### A1. What a cell is, and how the key was built

**Constructs.** The 38 construct ids are frozen in `tools/matrix/constructs.json` (id, meaning,
example, family). The harness uses the same ids. Every id belongs to a family; seven families are
the risky set: call, fuzzy, lookaround, conditional, verb, partial and reverse.

**Cells.** A cell is a set of families a row uses together. Part A covers the 70 risky cells: the 7
single families, the 21 pairs and the 35 triples of the risky set, and capture with each risky
family. The harness adds every construct pair (not only the risky ones) at 50 rows or more.

**Three sources of truth, in order of weight.**

1. A **principle**: a rule from an engine's documentation or from regex semantics that decides the
   answer, cited by document and section.
2. A **measured survey**: the row run through every engine that can express it, with the
   answers compared. Agreement of independent engines on a principle-free question is evidence;
   disagreement with no principle to decide it is an OPEN question for the owner (section A6).
3. An **existing ruling** (DIVERGENCES.md, DECISIONS.md, ExpectedDivergences): an input, re-checked
   in section A7, never taken on trust.

**The engines** (all measured on this machine on 2026-09-30):

| Engine | Version | Can be asked about |
|---|---|---|
| regex (upstream) | 2026.9.10 | everything |
| PCRE2 | 10.47 (2025-10-21), through ctypes on Git for Windows' DLL | all but fuzzy, reverse and regex-only flags; partial (soft) |
| Perl | 5.42.3 (cygwin) | all but fuzzy, reverse, partial; lookbehind up to 255 characters |
| .NET | 10 BCL `Regex` (backtracking) | lookarounds, conditionals, captures, atomic; reverse through `RightToLeft`; no calls, verbs, `\K`, branch reset or possessives |
| JavaScript | node 24 | lookarounds and captures only |
| python `re` | CPython 3.14 | lookarounds (fixed width), group conditionals, atomic and possessive |
| TRE | 0.8.0 in WSL | fuzzy rows with one whole-pattern budget, search only, existence only (TRE is leftmost-longest) |

The pip `pcre2` 0.7.1 binding wraps the same PCRE2 10.47, but it has no partial matching and no
ANCHORED or ENDANCHORED options, so the survey drives the library directly
(`tools/matrix/survey_worker.py` says so in its header).

**Two key judges** answer where no second engine can:

- **The fuzzy reference matcher** (`tools/probes/fuzzy-reference-matcher.py`): a complete, ordered,
  first-match search that implements upstream's documented fuzzy order plus the owner's fuzzy
  rulings (ledger entries 42, 44, 50, 51). The survey runs it with the "needed" rule for empty
  deleting iterations, which is the ruling; its own default is the rejected literal reading. It
  cannot model a fuzzy section inside a lookaround, calls, flags, partial or finditer, and says so
  per row.
- **The brute-force partial judge** (`tools/matrix/d11_brute_judge.py`, copied unchanged from
  `maint/d11-partial-boundary`): a partial answer is right when some continuation of the text
  completes a match. It tries every continuation up to 4 characters over a 10-character alphabet,
  using upstream's non-partial matcher. Its "None" is a lower bound (a completion longer than 4
  characters is not found), and it inherits upstream's non-partial answers, including the fuzzy
  ones the owner has ruled wrong.

**Reproduce** (about 30 seconds of engine time for all rows):

    python tools/matrix/survey_rows.py --out .scratch/matrix/survey-rows.jsonl
    python tools/matrix/survey.py .scratch/matrix/survey-rows.jsonl --out .scratch/matrix/survey
    python tools/matrix/survey.py tools/matrix/survey-open-rows.jsonl --out .scratch/matrix/open
    python tools/matrix/survey_report.py .scratch/matrix/survey --markdown
    python tools/matrix/survey_report.py .scratch/matrix/survey --verdict judge --disagreements 100

Each engine runs as one worker process per batch. A watchdog in `survey.py` kills a worker whose
current row passes its time limit (3 s; 20 s for the fuzzy reference, 30 s for the brute judge) or
whose process tree passes 1.5 GB, records the row as `timeout` or `memory`, and restarts after it.
In the final run one row hit upstream's own 2 s `timeout=` (`call+verb+reverse#9`) and 14 rows raised `MemoryError` inside upstream itself; no worker had to be killed. Spans are
normalised to codepoints. A construct an engine does not have is `n/a`, never a disagreement.

### A2. The governing rule for each construct

Each rule is followed by a one-line example and its source. "Survey" means the rule held on every
surveyed row of the cell named, across every engine that could answer.

| Construct | Rule | Example | Source |
|---|---|---|---|
| alternation, repeat, lazy | Leftmost-first backtracking: the earliest start wins; at a start, alternatives are tried left to right, greedy repeats try one more iteration first, lazy ones one fewer. | `ab\|a` over 'ab' is (0,2) | perlre "Quantifiers"; Python `re` docs; survey: every single-family cell |
| capture | A group reports its last completed capture; a group that did not take part is unset. upstream and .NET also keep the history (`captures()`, `Group.Captures`). | `(a\|b)+` over 'ab' gives group 1 = (1,2) | upstream README "Repeated captures"; survey: capture+lookaround 30/30 over five engines |
| branch-reset | Each alternative numbers its groups from the same start. | `(?\|(a)\|(b))` over 'b' gives group 1 = (0,1) | pcre2pattern "Duplicate group numbers"; perlre "(?\|pattern)" |
| lookahead, lookbehind | A lookaround is atomic: once true, it is never re-entered. A successful positive lookaround keeps its captures; a negative one that succeeds keeps none. | `(?=(a))a\1?` over 'aa' is (0,2), group 1 (0,1) | pcre2pattern "Assertions" ("no captured substrings are ever retained after a successful negative assertion"; "Perl lookaround assertions are atomic"); survey: lookaround 30/30, five engines |
| atomic, possessive | Once the group has matched, its choice points are discarded. `X*+` is `(?>X*)`. | `(?>a+)b` over 'aab' is (0,3); `a*+a` over 'aa' is None | pcre2pattern "Atomic grouping and possessive quantifiers" |
| conditional | A group test asks whether the group has captured; a lookaround test runs the assertion. Captures made by a test that succeeds are kept (upstream, PCRE2, Perl; .NET drops them). | `(a)?(?(1)b\|c)` over 'c' is (0,1) | pcre2pattern "Conditional groups"; survey: conditional 29/30 (the one split is the finditer rule below) |
| call | A call runs the group's pattern at the call site, with the call site's flags and fuzziness, and answers as the group written out there. Calls can be backtracked into. The caller's captures are visible inside; captures made inside revert when the call returns. | `(a\|b)(?1)\1` over 'aba' is (0,3), group 1 = (0,1) | pcre2pattern "Groups as subroutines" ("any capturing parentheses that are set during the subroutine call revert to their previous values afterwards") and "Differences in recursion processing between PCRE2 and Perl"; perlre on `(DEFINE)` ("capture groups matched inside of recursion are not accessible after the recursion returns"); D40 and D51 rulings; survey: call 30/30, capture+call 30/30 |
| verb | `(*FAIL)` fails now. `(*PRUNE)` and `(*SKIP)` do nothing until backtracking reaches them; then the attempt at this start fails, and `(*SKIP)` also moves the next start to where it was passed. Scope rules are in A3. | `a+(*SKIP)b\|a` over 'aac' is None | upstream README (`(*PRUNE)`, `(*SKIP)`, `(*FAIL)`); pcre2pattern "Verbs that act after backtracking"; survey: verb 30/30 |
| search-anchor `\G` | Matches where this search began (or where the previous match ended, in finditer). | `\Ga` over 'aa' at pos 1 is (1,2) | upstream README "Search anchor" |
| keep `\K` | The reported match starts where `\K` was last passed. | `a\Kb` over 'ab' is (1,2) | upstream README "Added \K" |
| fuzzy | The first match in the documented order that meets the constraints: an item is tried exactly first, then substitution, insertion, deletion; unnamed error kinds are forbidden once any kind is named; each section's limits bound its own errors and nested sections'. | `(?:abc){e<=1}` over 'xabd' is (1,4), one substitution | upstream README "Approximate fuzzy matching" ("searches for the first match that meets the given constraints"); reference matcher rules 1-10; rulings 42, 44, 50, 51, ledger 39; survey: TRE agrees on existence 40/40 (fuzzy-core); reference agrees with upstream on 140 of 145 rows |
| fuzzy-min, fuzzy-cost | A minimum is checked at the section's end, after trailing insertions; a cost equation permits only the kinds it names. | `(?:b){1<=e<=1}` fullmatch 'bx' is one insertion | reference matcher rules 6-7; ledger 51 |
| flag-b, flag-e | BESTMATCH returns the lowest-cost match in the slice; ENHANCEMATCH improves the first match found. Cost, not error count, ranks. | `(?b)(?:dog){e<=1}` over 'cat and dog' is 'dog' | upstream README; owner ruling (DIVERGENCES, "rank candidates by fuzzy COST") |
| partial | A partial match is reported if and only if some longer text could complete a match; a complete match is preferred (soft). A partial search reports the least start from which some continuation matches. | `abc` match 'ab' partial is P(0,2) | upstream README "Added partial matches" ("whether a complete match could be possible if the string had not been truncated"); pcre2partial "Partial matching using pcre2_match()" ("it prefers a complete match"); D11 design section 1 |
| flag-r (reverse) | The search runs from the end: the match that ends latest wins. Lookarounds keep their own direction; spans and captures are ordinary positions. | `(?r)a+` over 'baab' is (1,3) | upstream README "Reverse searching"; survey: .NET `RightToLeft` agrees on lookaround+reverse 30/30, conditional+reverse and capture+reverse 29/30 |
| op-finditer | Python's rule: an empty match may be followed by a non-empty match at the same position. | `(?=a)\|a` over 'aa' gives (0,0),(0,1),(1,1),(1,2) | Python `re` docs for `finditer` (3.7 change); upstream, re, PCRE2 (the pcre2demo loop) and Perl agree; .NET and JavaScript skip that match, a dialect difference |
| flag-i, flag-f, flag-V1, flag-w, flag-p, set, named-list, word-boundary, anchor, slice | Outside the risky set. Their rules are the existing rulings (DIVERGENCES "Defaults" and case-folding rows) and are not re-derived here. | | DIVERGENCES.md |

### A3. The rules for the risky interactions

**Pairs.** One row per pair within the risky set, then capture with each.

| Pair | Rule | Example | Source |
|---|---|---|---|
| call + fuzzy | The called group runs with the call site's fuzziness; one budget is shared, with each section's own minimum. A whole-pattern call returns to its caller. | `(?:b\|\|b(?0)*){e<=2}` fullmatch 'azx' is None (upstream: (0,3) with 3 errors, over its budget) | D37 and D40 rulings (written-out equivalence); no second engine |
| call + lookaround | A call inside a lookaround runs exactly as the lookaround's body does; its captures follow the lookaround's rule (thrown away with a failed or negative body). | witness rows w2 and w3 of `survey_rows.py`: every engine agrees | D10, D42 rulings; survey: call+lookaround 25 agree, 1 groups-only split (Perl) |
| call + conditional | A test inside a call reads the caller's captures unless the call itself set the group. | `(?<g>(a)?(?(2)b\|c))(?&g)` over 'abc' is None | PCRE2 and upstream agree, as does the written-out pattern; Perl alone matches (Q8 rows) |
| call + verb | A verb inside a called group: **OPEN-1** (A6). | | |
| call + partial | A call is text like any other: the partial rule of A2 applies to the written-out pattern. | `(a(?1)?b)` match 'aab' partial is P(0,3) | brute judge; PCRE2 agrees on 28 of 30 |
| call + reverse | A call inside a reversed pattern runs its group backwards. | `(?r)(?(DEFINE)(?<g>ab))(?&g)` over 'ab' is (0,2) | D43, D48 rulings; no second engine |
| fuzzy + lookaround | A lookaround's body is exact even inside a fuzzy section; a failing lookaround in a fuzzy section may be passed by inserting a text character in front of it. Errors made by a fuzzy section inside a lookaround body count. | `(?:b(?=c)){s<=1}` over 'bxc' is (1,2) | reference matcher rule 10; ledger 50 |
| fuzzy + conditional | The test runs exactly; the chosen branch is fuzzy if the section is. A fuzzy budget only adds matches: a pattern that matches exactly still matches, with no errors, under any budget. | `(?:(?(?=a)ab\|b)){s<=1}` over 'b' is (0,1) with no errors; upstream says None, although it gives (0,1) under `{e<=0}` and `{i<=1}` (A8) | budget monotonicity (LEDGER, the entry arguing "fuzzy budgets are monotone by construction"); reference matcher (7 rows judged, all agree; it has no lookaround tests) |
| fuzzy + verb | A verb that cuts through a fuzzy section closes it; the errors thrown away are not counted. | D44 witness `(?!(?:a){e<=1}(*PRUNE)b)` | D44, D45 rulings; reference matcher (20 rows judged, 1 split, ledger 44) |
| fuzzy + partial | A trailing insertion or a minimum met at the edge counts as running out of text. | `(?:b){1<=e<=1}` fullmatch 'b' partial is P(0,1) | D41 ruling; brute judge |
| fuzzy + reverse | The section is edited in the reverse direction; the error kinds are the same. | `(?r)(?:ab){s<=1}` over 'xb' is (0,2) | upstream README; no second engine |
| lookaround + conditional | A lookaround test is the assertion; the "no" branch runs when it is false. | `(?(?=a)ab\|b)` over 'ab' is (0,2) | survey 30/30 (.NET, PCRE2, Perl) |
| lookaround + verb | A verb in a lookaround that has completed never acts. Backtracking into one inside a standalone positive lookahead acts on the whole match; inside a negative assertion it makes the assertion true; inside a positive condition it makes the condition false. | `(?=aa(*SKIP)x)\|a` over 'aab' is None | pcre2pattern "Backtracking verbs in assertions"; DIVERGENCES verb row (ledger 47); survey Q7 rows: PCRE2 and Perl agree on all three; upstream differs on the standalone positive form only |
| lookaround + partial | A lookaround that reads past the edge is undetermined: partial only if some continuation makes the whole pattern match. | `a(?=bc)` search 'ab' partial is P(0,2) | brute judge; D18, D19 designs |
| lookaround + reverse | Lookarounds keep their direction under `(?r)`. | `(?r)a(?=b)` over 'abab' is (2,3) | survey 30/30 with .NET |
| conditional + verb | As lookaround + verb for lookaround tests; a group test contains no verb. | `(?(?=a(*SKIP)x)a\|b)` over 'ab' is (1,2) | pcre2pattern; survey 25/25 |
| conditional + partial | A test decided at the edge is undetermined, as for a lookaround. | `(x)?(?(?=.*z)a\|a)b(?(1)y)` match 'ac' partial is None | brute judge; D36 |
| conditional + reverse | The test reads in its own direction; a group test is direction-free. | `(?r)(?(1)b\|a)(a)?` | survey 29/30 with .NET (the split is finditer) |
| verb + partial | A verb acts as usual; an attempt a verb kills cannot be the start of a partial. | `b+(*SKIP)(*F)\|a` search 'bbb' partial is P(3,3) | brute judge; D11 design section 1 (Q11 rows) |
| verb + reverse | `(*SKIP)` moves the next start leftwards to where it was passed. | `(?r)a(*SKIP)b\|a` over 'aab' is (1,3) | upstream README; no second engine |
| partial + reverse | The text runs out at the start of the slice, not its end. | `(?r)ya` match 'xya'[2:3] partial is P(2,3) | DIVERGENCES reversed-partial row (ledger 24) |
| capture + call | Captures made inside a call revert on return (D51). | `(?(DEFINE)(?<c>a))(?&c)b` over 'ab': c unset | pcre2pattern "Groups as subroutines"; perlre; survey 30/30 |
| capture + fuzzy | A captured group's span is the text the fuzzy match consumed for it. | `(?:(b)+a*?){1<=e<=2}` match 'bcb' gives group 1 (2,3) | reference matcher (22 judged) |
| capture + lookaround | See A2 lookaround. | | survey 30/30, five engines |
| capture + conditional | Captures from a test: kept after a positive test; after a negative test that fails, **OPEN-2**. | | |
| capture + verb | A verb does not change captures on the path that succeeds. | | survey 30/30 |
| capture + partial | Only a partial's span is defined. Upstream reports the groups captured so far; the port reports none at a boundary (DIVERGENCES boundary row). The key compares spans only; no documented rule decides the groups, so they are unsettled (A9). The survey reads only PCRE2's span on a partial. | `(a)b` match 'a' partial is P(0,1); upstream gives group 1 = (0,1) | measured (upstream) |
| capture + reverse | Captures are ordinary spans; the last capture is the leftmost one taken. | `(?r)(a)+` over 'aa' gives group 1 = (0,1) | survey 29/30 with .NET |

**Triples.** A triple's answer is the composition of its three pair rules; no triple needs a rule of
its own. The triples matter because the known faults needed all three features at once, so each
triple is listed with the pair rules it composes and the register rows that sit in it.

| Triple | Composes | Known register rows in this cell |
|---|---|---|
| call+fuzzy+lookaround | call+fuzzy, fuzzy+lookaround, call+lookaround | D42 |
| call+fuzzy+conditional | call+fuzzy, fuzzy+conditional, call+conditional | |
| call+fuzzy+verb | call+fuzzy, fuzzy+verb, OPEN-1 | |
| call+fuzzy+partial | call+fuzzy, fuzzy+partial, call+partial | |
| call+fuzzy+reverse | call+fuzzy, fuzzy+reverse, call+reverse | D48 |
| call+lookaround+conditional | call+lookaround, lookaround+conditional, call+conditional | |
| call+lookaround+verb | call+lookaround, lookaround+verb, OPEN-1 | |
| call+lookaround+partial | call+lookaround, lookaround+partial, call+partial | D39 (call form) |
| call+lookaround+reverse | call+lookaround, lookaround+reverse, call+reverse | D43 |
| call+conditional+verb | call+conditional, conditional+verb, OPEN-1 | |
| call+conditional+partial | call+conditional, conditional+partial, call+partial | |
| call+conditional+reverse | call+conditional, conditional+reverse, call+reverse | |
| call+verb+partial | OPEN-1, verb+partial, call+partial | |
| call+verb+reverse | OPEN-1, verb+reverse, call+reverse | |
| call+partial+reverse | call+partial, partial+reverse, call+reverse | |
| fuzzy+lookaround+conditional | fuzzy+lookaround, lookaround+conditional, fuzzy+conditional | |
| fuzzy+lookaround+verb | fuzzy+lookaround, lookaround+verb, fuzzy+verb | D44 |
| fuzzy+lookaround+partial | fuzzy+lookaround, lookaround+partial, fuzzy+partial | D26, D41 |
| fuzzy+lookaround+reverse | fuzzy+lookaround, lookaround+reverse, fuzzy+reverse | |
| fuzzy+conditional+verb | fuzzy+conditional, conditional+verb, fuzzy+verb | D45 |
| fuzzy+conditional+partial | fuzzy+conditional, conditional+partial, fuzzy+partial | D38 |
| fuzzy+conditional+reverse | fuzzy+conditional, conditional+reverse, fuzzy+reverse | |
| fuzzy+verb+partial | fuzzy+verb, verb+partial, fuzzy+partial | |
| fuzzy+verb+reverse | fuzzy+verb, verb+reverse, fuzzy+reverse | |
| fuzzy+partial+reverse | fuzzy+partial, partial+reverse, fuzzy+reverse | |
| lookaround+conditional+verb | lookaround+conditional, conditional+verb, lookaround+verb | |
| lookaround+conditional+partial | lookaround+conditional, conditional+partial, lookaround+partial | D36, D38 |
| lookaround+conditional+reverse | lookaround+conditional, conditional+reverse, lookaround+reverse | |
| lookaround+verb+partial | lookaround+verb, verb+partial, lookaround+partial | D23 (lookbehind) |
| lookaround+verb+reverse | lookaround+verb, verb+reverse, lookaround+reverse | |
| lookaround+partial+reverse | lookaround+partial, partial+reverse, lookaround+reverse | |
| conditional+verb+partial | conditional+verb, verb+partial, conditional+partial | |
| conditional+verb+reverse | conditional+verb, verb+reverse, conditional+reverse | |
| conditional+partial+reverse | conditional+partial, partial+reverse, conditional+reverse | |
| verb+partial+reverse | verb+partial, partial+reverse | |

### A4. The survey, measured

2,192 generated rows (30 per risky cell, 40 in a fuzzy core TRE can answer), 52 witness rows of
existing rulings and register rows, and 46 rows for the open questions and the A8 finds
(`tools/matrix/survey-open-rows.jsonl`). "Agree" means every engine that answered gave the same
span and groups; "groups only" means the spans agreed and a group did not; "one engine" means no
second engine could express the row. The judge columns count rows each key judge answered, and how
many of those upstream answers differently.

| Cell | Rows | Engines answering (rows) | Agree | Disagree | Groups only | One engine | Judged: fuzzy ref / brute | Judge differs from upstream |
|---|---|---|---|---|---|---|---|---|
| call | 30 | pcre2 30, perl 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| call+conditional | 30 | pcre2 29, perl 29 | 28 | 0 | 1 | 1 | 0 / 0 | 0 |
| call+conditional+partial | 30 | pcre2 25 | 20 | 5 | 0 | 5 | 0 / 30 | 2 |
| call+conditional+reverse | 30 | - | 0 | 0 | 0 | 28 | 0 / 0 | 0 |
| call+conditional+verb | 30 | pcre2 26, perl 26 | 23 | 2 | 1 | 3 | 0 / 0 | 0 |
| call+fuzzy | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| call+fuzzy+conditional | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| call+fuzzy+lookaround | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| call+fuzzy+partial | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| call+fuzzy+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| call+fuzzy+verb | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| call+lookaround | 30 | pcre2 26, perl 26 | 25 | 0 | 1 | 4 | 0 / 0 | 0 |
| call+lookaround+conditional | 30 | pcre2 23, perl 23 | 23 | 0 | 0 | 6 | 0 / 0 | 0 |
| call+lookaround+partial | 30 | pcre2 26 | 20 | 6 | 0 | 4 | 0 / 30 | 3 |
| call+lookaround+reverse | 30 | - | 0 | 0 | 0 | 27 | 0 / 0 | 0 |
| call+lookaround+verb | 30 | pcre2 21, perl 21 | 20 | 1 | 0 | 8 | 0 / 0 | 0 |
| call+partial | 30 | pcre2 30 | 28 | 2 | 0 | 0 | 0 / 30 | 0 |
| call+partial+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| call+reverse | 30 | - | 0 | 0 | 0 | 26 | 0 / 0 | 0 |
| call+verb | 30 | pcre2 30, perl 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| call+verb+partial | 30 | pcre2 30 | 29 | 1 | 0 | 0 | 0 / 30 | 2 |
| call+verb+reverse | 30 | - | 0 | 0 | 0 | 29 | 0 / 0 | 0 |
| capture+call | 30 | pcre2 30, perl 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| capture+conditional | 30 | dotnet 30, pcre2 29, perl 29, re 5 | 24 | 2 | 4 | 0 | 0 / 0 | 0 |
| capture+fuzzy | 30 | - | 0 | 0 | 0 | 29 | 22 / 0 | 1 |
| capture+lookaround | 30 | dotnet 30, node 28, pcre2 24, perl 25, re 22 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| capture+partial | 30 | pcre2 30 | 30 | 0 | 0 | 0 | 0 / 30 | 0 |
| capture+reverse | 30 | dotnet 30 | 29 | 1 | 0 | 0 | 0 / 0 | 0 |
| capture+verb | 30 | pcre2 30, perl 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| conditional | 30 | dotnet 30, pcre2 28, perl 28, re 6 | 29 | 1 | 0 | 0 | 0 / 0 | 0 |
| conditional+partial | 30 | pcre2 26 | 26 | 0 | 0 | 4 | 0 / 30 | 0 |
| conditional+partial+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| conditional+reverse | 30 | dotnet 30 | 29 | 1 | 0 | 0 | 0 / 0 | 0 |
| conditional+verb | 30 | pcre2 25, perl 25 | 25 | 0 | 0 | 5 | 0 / 0 | 0 |
| conditional+verb+partial | 30 | pcre2 28 | 28 | 0 | 0 | 2 | 0 / 30 | 0 |
| conditional+verb+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| fuzzy | 30 | tre 6 | 6 | 0 | 0 | 24 | 23 / 0 | 1 |
| fuzzy+conditional | 30 | - | 0 | 0 | 0 | 30 | 7 / 0 | 0 |
| fuzzy+conditional+partial | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 1 |
| fuzzy+conditional+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| fuzzy+conditional+verb | 30 | - | 0 | 0 | 0 | 30 | 5 / 0 | 0 |
| fuzzy+lookaround | 30 | - | 0 | 0 | 0 | 30 | 13 / 0 | 0 |
| fuzzy+lookaround+conditional | 30 | - | 0 | 0 | 0 | 30 | 1 / 0 | 0 |
| fuzzy+lookaround+partial | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 1 |
| fuzzy+lookaround+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| fuzzy+lookaround+verb | 30 | - | 0 | 0 | 0 | 30 | 14 / 0 | 1 |
| fuzzy+partial | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| fuzzy+partial+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| fuzzy+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| fuzzy+verb | 30 | - | 0 | 0 | 0 | 30 | 20 / 0 | 1 |
| fuzzy+verb+partial | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 2 |
| fuzzy+verb+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| fuzzy-core | 40 | tre 40 | 40 | 0 | 0 | 0 | 40 / 0 | 1 |
| lookaround | 30 | dotnet 30, node 30, pcre2 24, perl 24, re 23 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| lookaround+conditional | 30 | dotnet 30, pcre2 23, perl 25, re 1 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| lookaround+conditional+partial | 30 | pcre2 24 | 23 | 1 | 0 | 6 | 0 / 30 | 2 |
| lookaround+conditional+reverse | 30 | dotnet 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| lookaround+conditional+verb | 30 | pcre2 21, perl 22 | 22 | 0 | 0 | 8 | 0 / 0 | 0 |
| lookaround+partial | 30 | pcre2 27 | 19 | 8 | 0 | 3 | 0 / 30 | 6 |
| lookaround+partial+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| lookaround+reverse | 30 | dotnet 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| lookaround+verb | 30 | pcre2 25, perl 25 | 25 | 0 | 0 | 5 | 0 / 0 | 0 |
| lookaround+verb+partial | 30 | pcre2 22 | 20 | 2 | 0 | 8 | 0 / 30 | 4 |
| lookaround+verb+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |
| partial | 30 | pcre2 30 | 28 | 2 | 0 | 0 | 0 / 30 | 0 |
| partial+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 0 |
| reverse | 30 | dotnet 30 | 28 | 2 | 0 | 0 | 0 / 0 | 0 |
| verb | 30 | pcre2 30, perl 30 | 30 | 0 | 0 | 0 | 0 / 0 | 0 |
| verb+partial | 30 | pcre2 30 | 28 | 2 | 0 | 0 | 0 / 30 | 2 |
| verb+partial+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 30 | 1 |
| verb+reverse | 30 | - | 0 | 0 | 0 | 30 | 0 / 0 | 0 |

**What the disagreements are.** The generated rows disagree on 46 rows (29 partial, 17 not). Every
one falls into one of these groups; the witness and open-question rows are discussed in A6 and A7.

1. **Partial matching at the edge of the text (29 rows).** On every one, upstream reports a partial
   and PCRE2 does not. PCRE2 declines a partial when the pattern has not yet inspected a character,
   for example `b` search '' is P(0,0) upstream and None in PCRE2. That is PCRE2's documented
   narrower contract (pcre2partial "Requirements for a partial match": "at least one character has
   already been inspected"), not a disagreement about what could match, so the brute judge decides
   these rows. Over all 705 partial rows it disagrees with upstream on 33: 22 where upstream alone
   reports a partial that no continuation completes (phantoms, as in `(?!a?b)a?b` match ''
   partial), and 11 where upstream and PCRE2 both report one (for example `a*?(?!b*)` search
   'ccca', which can never match because `(?!b*)` always fails). Upstream also misses the partial
   of `aa\B` over 'aa', which 'aab' completes; that is the S57d divergence, where the port is right.
2. **Captures from a condition's test (4 rows).** After a negative test that fails, PCRE2 and Perl
   keep the test's captures and upstream and .NET drop them (3 rows); .NET also drops them after a
   positive test that succeeds (`[ab](?(?<=(b))a*?|aa)` over 'b', 1 row). **OPEN-2.**
3. **A verb inside a called group (2 rows, and 3 open-question rows).** PCRE2 confines it to the
   call; Perl and upstream let it end the attempt. **OPEN-1.**
4. **The finditer rule after an empty match (6 rows).** .NET skips a non-empty match that starts
   where an empty one ended, forwards and under RightToLeft; the Python family (upstream, re, PCRE2,
   Perl) keeps it, and JavaScript skips it too (witness `(?=a)|a`). Settled by A2 (op-finditer):
   this is a port of a Python library.
5. **Perl outliers (4 rows, and 3 witness rows).** Perl keeps a capture from inside a negative
   lookahead or negative test that succeeded (`aa(?!(?<g1>a)(?&g1))` over 'bbcaaa', and the witness
   `(?!(a)b)a(\1)?` over 'aa', which is (0,2) in Perl and (0,1) in the other five engines), keeps a
   capture made by a call inside a positive test that failed, reads a stale capture inside a call
   (`(?<g>(a)?(?(2)b|c))(?&g)`), and matches `(?<n2>b*(?(?<=a?b)a|a*?))*a?b` fullmatch 'aabbbb'.
   No path can match that one: every iteration that takes a `b` is then followed by a `b`, so its
   condition demands an `a` that is not there. PCRE2's documentation states the opposite rule for
   the first ("no captured substrings are ever retained after a successful negative assertion").
   The key follows the documented rule and the majority.
6. **Upstream alone, where an existing ruling already says the port is right (1 row, and witness
   rows):** a verb reached inside a standalone positive lookahead
   (`(?<g1>(?=(?:a?b(*SKIP)(*F)|a+))(?:a?b(*SKIP)(*F)|a+)|b(?&g1))` finditer 'aba': upstream
   [(1,3)], PCRE2 and Perl [(2,3)]; the DIVERGENCES verb row). The witnesses add `(*SKIP)` then
   `(*PRUNE)` (ledger 45) and the verb in an unfinished atomic group (ledger 47; A7 item 2).

The fuzzy reference agrees with upstream on 140 of 145 fuzzy rows, spans, groups and fuzzy counts
alike. All five differences are the ruled "needed" rule for iterations that only delete (ledger 44),
for example `(?:b*){e<=2}` over '' is (0,0) with no errors, where upstream charges one deletion. TRE
agrees with upstream on whether a match exists on all 40 fuzzy-core rows.

### A5. How each row's expected answer is derived

- **Surveyed cells with a principle** (lookaround, conditional, capture, verb and call cells that
  PCRE2, Perl or .NET can express): the answer of the engines that follow the cited rule. Where
  every answering engine agrees, that answer; where one engine is an outlier against a documented
  rule (A4 group 5), the documented rule.
- **Fuzzy cells**: the fuzzy reference matcher with the ruled options, where the row is in its
  subset (145 of 730 fuzzy rows answered: no calls, no flags, no partial, no fuzzy section inside a
  lookaround). Outside that subset, the rules of A2 and A3 applied by hand, and for calls the
  written-out equivalence (Part B's check C2, which expands each call in place).
- **Partial cells**: the brute judge, which answers every partial row that has no flag other than
  REVERSE (705 rows). Its None is a lower bound, and on fuzzy rows it inherits upstream's
  non-partial fuzzy answers; on those rows the port's non-partial answer, checked against the
  reference, is the better base (Part B's check C4).
- **Cells no engine and no judge can answer independently** (upstream is the only engine and the
  judges do not apply): call+fuzzy and its triples with conditional, lookaround, reverse and verb;
  call+reverse, call+conditional+reverse, call+lookaround+reverse, call+verb+reverse;
  fuzzy+reverse, fuzzy+conditional (outside the reference's subset), fuzzy+conditional+reverse,
  fuzzy+conditional+verb, fuzzy+lookaround+conditional, fuzzy+lookaround+reverse, fuzzy+verb+reverse;
  verb+reverse, conditional+verb+reverse, lookaround+verb+reverse. For the call cells, the
  written-out equivalence is an independent check (C2). For the reverse cells without calls there is
  no independent check beyond the rules; that is unsettled, and Part B should treat a port answer
  there as "upstream agrees" only, not as confirmed.

### A6. OPEN questions for the owner

**OPEN-1. A backtracking verb inside a called group: does it end the whole attempt, or only the call?**

- *Context.* `(*PRUNE)` and `(*SKIP)` say "if backtracking comes back past this point, give up on
  this starting position". A group call, such as `(?&g)`, runs a group's pattern again somewhere
  else. The question is whether "give up" inside the called group means the whole attempt, or just
  this call, after which the pattern around the call may try something else.
- *Example.* `(?:(?&g)c|ac)(?(DEFINE)(?<g>a(*PRUNE)b))` searched in 'ac'. The call matches 'a',
  passes `(*PRUNE)`, and fails on 'b'. PCRE2 10.47: the call fails, the outer alternative `ac` is
  tried, and the answer is (0,2). Perl 5.42.3 and upstream: the attempt at 0 is over, and the answer
  is None. The same pattern with the group written out in place, `(?:(?:a(*PRUNE)b)c|ac)`, is None in
  all three. Measured on three such rows (a PRUNE, a SKIP and a recursive form) (`tools/matrix/survey-open-rows.jsonl`, Q1).
- *Options.*
  - (a) The verb acts on the whole attempt (Perl, upstream, and the written-out pattern). Pros: a call
    means exactly what writing the group there means, which is the rule the owner already adopted
    for D40 ("a call runs with its call site's features and answers as the group written out");
    it is upstream's answer, which the port inherits unless Part B finds otherwise. Cons: PCRE2 documents the opposite.
  - (b) The verb ends only the call (PCRE2, documented in pcre2pattern "Backtracking verbs in
    subroutines": "(*COMMIT), (*SKIP), and (*PRUNE) cause the subroutine match to fail"). Pros: a
    call becomes a sealed unit, like PCRE2's. Cons: breaks the written-out rule, so C2 would need an
    exception; PCRE2 itself notes "Perl's treatment of the other verbs in subroutines is different".
- *Recommendation:* (a). Perl and upstream agree, and it
  keeps the written-out equivalence that the matrix's check C2 rests on.

**OPEN-2. The captures of a condition's negative test when the test fails.**

- *Context.* In `(?(?!X)yes|no)`, the engine runs `X`. If `X` matches, the negative test is false,
  and the `no` branch runs. Any group `X` captured on the way might be kept or thrown away.
- *Example.* `(?(?!(a))x|\1b)` searched in 'ab'. `(a)` matches, so the `no` branch `\1b` runs. PCRE2
  and Perl keep group 1 = 'a', so `\1b` matches 'ab': the answer is (0,2). Upstream and .NET drop
  the capture, `\1` refers to an unset group, and the answer is None. So the rule decides whether a
  match exists, not only what a group reports. The positive mirror image, `(?(?=(a))\1b|x)`, is
  (0,2) in upstream, PCRE2 and Perl; .NET says None there too.
- *Options.*
  - (a) Keep the captures (PCRE2 and Perl; pcre2pattern "Assertions": "If such an assertion is being
    used as a condition in a conditional group, captured substrings are retained, because matching
    continues with the 'no' branch"). Pros: documented, two engines, and `(?(?!X)A|B)` then means
    exactly `(?(?=X)B|A)`, captures included. Cons: a change from upstream's answer, pinned as a
    divergence.
  - (b) Drop them in both the positive and negative forms (.NET). Pros: simple ("a test never
    captures"). Cons: changes upstream's positive form too, which upstream, PCRE2 and Perl all
    agree on.
  - (c) Keep upstream's mix (keep after a positive test, drop after a negative one). Pros:
    upstream parity. Cons: the two equivalent spellings above give different answers, which is a
    self-contradiction by the matrix's own standard.
- *Recommendation:* (a).

**OPEN-3. A name at different positions in the alternatives of a branch reset.**

- *Context.* In a branch reset `(?|...|...)` each alternative numbers its groups from the same
  start. When a named group sits in a different position in two alternatives, "the name means one
  group" and "each alternative numbers from the same start" cannot both hold. The owner chose
  upstream's maintainer's option 3 on 2026-09-22 (S82): a group never takes a number another group
  in the same alternative will use.
- *Example.* `(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))` searched in '!BUG'. The port (option 3): group 1 =
  bug = 'BUG', group 2 = '!'. Perl 5.42.3: numbers by position, so group 1 = '!' and group 2 = 'BUG'.
  PCRE2 10.47 refuses to compile it ("two named subpatterns have the same name (PCRE2_DUPNAMES not
  set)"). Upstream: group 1 = 'BUG', group 2 unset. When the name sits at the same position
  (`(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))` over 'BUG!'), PCRE2, Perl and the port agree.
- *Options.*
  - (a) Keep option 3 (shipped). Pros: the name always denotes the text it names; upstream's
    maintainer proposed it. Cons: no surveyed engine numbers this way.
  - (b) Perl's positional numbering. Pros: one surveyed engine; "each branch numbers from the same
    start" holds literally. Cons: the name then refers to different numbers in different
    alternatives, which upstream's `groupindex` cannot express.
  - (c) Refuse the pattern, as PCRE2 does. Pros: no silent surprise. Cons: a pattern upstream
    compiles stops compiling.
- *Recommendation:* (a), recorded with this survey as its evidence. Neither alternative has more
  than one engine behind it, and (a) is the only one under which the name is never wrong.

Not open, although the handoff listed them as candidates: which of two equal fuzzy alignments to
report (the ordered first-match rule decides; no new case in 145 judged rows); a verb in a negative
lookaround (PCRE2's documentation and upstream's README give the same outcome, the assertion is
true, and the Q7 rows agree); partial lookaheads past the edge (the brute judge decides, as the D18
and D19 designs assume).

### A7. Existing rulings re-checked against the key

**Contradicted by the survey.**

1. DIVERGENCES, branch reset row (S82, option 3), for a name at different positions: Perl numbers
   the other way and PCRE2 refuses the pattern. Witness: `(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))` over
   '!BUG'. Raised as OPEN-3; the recommendation is to keep the ruling.

**Supported, with evidence weaker than recorded.**

2. DIVERGENCES verb row "A verb that backtracking reaches inside an unfinished atomic group or
   positive lookaround ends the attempt" (ledger 47, F24, entry `verb-unwinds-through-unfinished-groups`).
   PCRE2 agrees on every sibling measured, as recorded, and its documentation confines a verb only
   to a group that has completed ("Verbs that act after backtracking"). But Perl splits: it sides
   with upstream on `(?>a(*PRUNE)b)?a`, `(?>a(*SKIP)b)?a` and `(?>a(*PRUNE)b)*a` over 'ac' ((0,1))
   and with PCRE2 on the alternation forms `(?:(?>a(*PRUNE)x)|a)c` (None). The possessive form
   `(?:a(*PRUNE)b)?+a` is None in all three engines, upstream included, so upstream contradicts
   itself between `(?>X)?` and `X?+`. The ruling stands on the documented rule.
3. KNOWN-DEFECTS D23 cites PCRE2 on `(?m)(?<=$(*SKIP)|a*)`, but PCRE2 10.47 and Perl both refuse that
   pattern (an unbounded lookbehind). The bounded form `(?<=$(*SKIP)|a{0,3})` confirms the recorded
   expected answer: PCRE2 and Perl give (0,0) for match and search, with and without `(?m)`, where
   upstream gives None and (2,2) without `(?m)`.
4. F30 (D10) says Perl discards a capture from a thrown-away lookaround. True for the call form it
   is about (`(?=(?&g))(?<g>a)?b` agrees in all engines); Perl does keep a directly written capture
   from a negative lookahead that succeeds (A4 group 5), which does not affect the ruling.

**Consistent with the key.** Checked by a measured row: D51 (captures in a call revert; 30/30 plus
witnesses), D40 and D10 (call features and lookaround captures), DIVERGENCES `(*SKIP)` then
`(*PRUNE)` (ledger 45; PCRE2 and Perl give (1,2)), the standalone positive lookaround verb row,
the same-position call guard (ledger 14: every engine refuses `(?P<g1>(?:ab)?(?&g1)?)`, upstream
with MemoryError, PCRE2 "nested recursion at the same subject position", Perl "Infinite
recursion", so the port's path refusal has no engine to contradict it), the partial rows S57d
(`aa\B`), ledger 27 (`(\S??)\.`), D18, D19, D28, D36 and D39 (the brute judge gives the recorded
expected answer on each), and the fuzzy rulings 42, 44, 50 and 51 (the reference matcher). Checked
by principle only (no second engine can express the rows): the remaining fuzzy rows (cost ranking,
the pinned-anchor insertion, the full-fold rows, D37, D44-D46), the reversed-partial slice rows,
and the prefilter rows. The case-folding, Unicode, word-boundary and API-shape rows are outside the
risky families and were not re-checked here.

**ExpectedDivergences.** Of the 70 entries, the verb, call, lookaround-capture and partial entries
(`skip-acts-when-backtracked-onto`, `verb-unwinds-through-unfinished-groups`,
`group-call-runs-with-its-call-sites-features`,
`group-call-in-a-discarded-lookaround-leaves-no-capture`, `boundary-at-the-end-of-the-text`,
`partial-the-pattern-can-never-complete`, `search-start-partial` and the `overlapped-skip-*` and
`skip-*` families) rest on rules the survey supports. The fuzzy entries rest on the reference
matcher's rules, which agree with them. The Turkic, full-fold and word-boundary entries are outside
the risky families. No entry's rule is contradicted. Their example rows were not re-run one by one
here; Part B runs them as part of check C1.

**DECISIONS, 2026-09 semantic entries.** The 2026-09-30 D51 entry is supported (PCRE2, Perl, and
perlre's DEFINE paragraph). The 2026-09-26 entries on ledger 42, 44, 45, 46, 47 and the `\K` scope
are supported as above. No 2026-09 semantic entry is contradicted beyond item 1.

### A8. Rows Part B must include

Key rows where upstream gives a wrong answer that is not in the register. Whether the port shares
each is Part B's question; each is a candidate new root cause if it does.

- **A fuzzy section loses an exact match through a conditional's lookaround test.**
  `(?:(?(?=a)ab|b)){s<=1}` searched in 'b': expected (0,1) with no errors, because the same pattern
  matches exactly and a fuzzy budget only adds matches. Upstream: None, and also None for
  `(?:(?(?!a)b|ab)){s<=1}` over 'b' and `(?:c(?(?=a)ab|b)){s<=1}` over 'cb'; but (0,1) under
  `{e<=0}` and `{i<=1}`, and `(?:(?(?<=a)b|c)){s<=1}` over 'c' is (0,1). So the loss needs a
  substitution budget and a lookahead test. Found while checking this document's examples; not in
  the survey sample, because the reference matcher has no lookaround tests.
- **A partial search reports a start that a verb has already ruled out.** `b+(*SKIP)(*F)|a` and
  `(?:(?=b)b*(*SKIP)(*F)|a)` searched in 'bbb' with partial: expected P(3,3), since any continuation
  kills the attempts at 0, 1 and 2 by `(*SKIP)(*F)`; upstream and PCRE2 say P(0,3).
- The 22 upstream-only phantom partials and the 11 shared ones of A4 group 1 (listed by
  `python tools/matrix/survey_report.py <dir> --verdict judge --disagreements 100`). Most are the D11,
  D18 and D19 families; Part B's triage decides which are new.
- The OPEN-1 and OPEN-2 rows, once the owner has decided.

### A9. Unsettled

- The groups of a partial answer (A3, capture + partial): no engine documents them.
- The reverse cells without calls that no second engine can express (A5): their key is the rules
  alone.
- The brute judge's lower bound (continuations of up to 4 characters) and its use of upstream's
  non-partial fuzzy answers.
- Cells where the fuzzy reference matcher cannot answer (calls, flags, a fuzzy section inside a
  lookaround, lookaround tests in conditionals): 585 of the 730 fuzzy rows. For calls, Part B's
  written-out check C2 is the independent check; for the rest there is none yet.

## Part B: the measurement

Written by the harness work (`tools/matrix/` generator and classifier), against this key.
