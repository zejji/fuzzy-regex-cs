# Empty iterations in fuzzy repeats: the problem, the options and a recommendation

Written 2026-09-26 for an owner decision, revised the same day after four blind reviews. Every result
below comes from a run made that day with the probes in `tools/probes/empty-iteration-survey/`
against `regex` 2026.9.10 (the upstream this port follows) unless it says otherwise. Where an
engine could not be run, the row says "documented, not run" and cites the document.

## Summary

A fuzzy pattern may leave out pattern characters that the text lacks, at a cost of one error each.
Inside a repeat, that lets an iteration match no text at all. Upstream has no deliberate rule for
such an iteration: it takes as many as its error budget allows unless the repeat has reached the
end of the text, and where the budget restarts on every iteration it loops until it runs out of
memory. The port needs a rule. After testing seven candidates on about 114,000 generated cases,
the recommendation is the **"needed" rule**: an iteration that matched no text and spent errors is
allowed only when something requires it (the repeat's minimum count, a fuzzy section's minimum
error count that its deletions actually raise, or a group that the pattern tests later), and the
search drops any path that reaches a state an earlier path through the same repeat already reached,
which never changes the answer. In the sweeps it never lost a match that any rule could find and
never looped. It is not free: on some nested patterns with a minimum error count it is many times
slower than the first draft's rule, which answers quickly there only because it gives up on
matches (Performance has the numbers). It is still a first-match rule, so its error counts are not
always the smallest possible.

A second, separate question came out of the review: in exact matching, upstream counts an empty
iteration that changed a tested group as progress, where Perl, PCRE2, Python `re`, Java and .NET do
not. It was surveyed further and decided on 2026-09-28 (D12, at the end): upstream's rule is kept,
because it is the one whose termination check loses no match.

## What the decisions mean in practice

Five small examples, each run on 2026-09-26. The terms are defined in the next section; for now,
a *deletion* means the matcher pretends a pattern character was missing from the text, and each one
counts as an error.

**1. The same text gets a different error count depending on what follows it.** The pattern
`(?:[0-9]+){d<=2}` means "one or more digits, allowing up to two missing characters".

| text | upstream today | "needed" rule | `(?b)` best match, and TRE |
|---|---|---|---|
| `42` | `42`, 0 errors | `42`, 0 errors | `42`, 0 errors |
| `42kg` | `42`, **2 errors** | `42`, 0 errors | `42`, 0 errors |

Both texts contain the digits `42` exactly. Upstream reads `4` and `2`, then tries for another digit,
meets `k`, and pretends a digit was missing, twice, because the budget allows two. At the end of the
text it does not do this. A program that keeps only matches with at most one error would silently
throw away the exact `42` in `42kg`, and a program that ranks matches by error count would rank it
below a genuinely misspelt one.

**2. An exact list is reported as containing an error.** `(?:(?:[0-9]+,){d<=1})+end` ("numbers each
followed by a comma, each allowed one missing character, then `end`") over `12,end`: upstream reports
1 error, "needed" and `(?b)` report 0. The text matches the pattern exactly.

**3. A search that crashes instead of answering.** The same pattern with `{d<=3}`, searched in the
text `end`, raises MemoryError in upstream after 0.7 seconds, with and without `(?b)`. The right
answer is a match of `end` with two missing characters (the one required number-and-comma), which
"needed" finds at once. The port already avoids this crash today (ledger entry 33); any new rule must
too.

**4. When errors are required, a rule that is too strict gives a useless answer.** A pattern can
demand errors, for example to find near misses but not exact hits: `(?:[0-9]+){1<=d<=2}` ("digits,
with at least one and at most two missing characters").

| text | upstream today | "needed" | first draft's rule B |
|---|---|---|---|
| `42kg` | `42`, 2 errors | `42`, 1 error | an empty match before `4`, 1 error |
| `42` | `2` only, 1 error | `42`, 1 error | an empty match before `4`, 1 error |

"Needed" allows the one missing digit after `42` because the "at least one" minimum requires it.
Rule B refuses it, so the only way left to spend an error is to delete the single required digit and
match nothing. Upstream's end-of-text exception makes it skip the match at the start of `42`
altogether and report `2`.

**5. The D12 question, in exact matching (no errors at all).** Take `^(?:(?(1)c|z)|())*$` over `c`.
In words: "repeat: if the marker (group 1) is set, read a `c`; otherwise read a `z`, or set the marker
without reading anything; the whole text must be used". The first pass cannot read `c` (no marker
yet), so it sets the marker and reads nothing. The engines then disagree about whether the loop may
go round again:

| engine | result | why |
|---|---|---|
| upstream `regex`, Ruby (Onigmo) | match `c` | the first pass changed the marker, which counts as progress, so a second pass runs and reads `c` |
| Perl, PCRE2, Python `re`, .NET | no match | the first pass read nothing, so the loop stops, and `c` is never read |
| Java, JavaScript | cannot express it | no conditional syntax |

This only affects patterns that set a group on a pass that reads nothing and test it on a later
pass, which is rare. The practical risk is a pattern copied from a Perl or PCRE2 program matching
more text here than it did there.

Only TRE and upstream implement this kind of fuzzy matching, so for examples 1 to 4 the comparison is
with TRE and with upstream's own `(?e)` and `(?b)` modes; example 5 is where the mainstream exact
engines come in.

## Background

**Fuzzy matching.** In `regex`, a pattern item followed by a constraint such as `{e<=2}` may match
text that differs from it. The pattern part it applies to is a *fuzzy section*. There are three
kinds of error (`upstream/README.rst:530-566`):

- a *substitution* replaces one pattern character with a different text character;
- an *insertion* is an extra text character the pattern does not have;
- a *deletion* is a pattern character missing from the text.

Constraints can cap each kind (`{d<=1}`: at most one deletion), the total (`{e<=2}`), or a weighted
cost (`{2i+2d+1s<=4}`), and can set a minimum (`{1<=e<=3}`: at least one error). A match reports
`fuzzy_counts` as (substitutions, insertions, deletions).

**Which match is returned.** By default `regex` returns the *first* match its backtracking search
reaches that fits the constraints, not the one with the fewest errors. `(?e)` (ENHANCEMATCH)
tries to improve the fit after finding it, and `(?b)` (BESTMATCH) searches for the best one
(`README.rst:590`). So the plain answer can carry errors that a better alignment would avoid; the
README's own example is `fullmatch(r"(?:cats|cat){e<=1}", "cat")` giving (0, 0, 1), because the
first branch is tried first and fits with one deletion (`README.rst:607-615`).

**Empty iterations.** Because a deletion consumes no text, `(?:x){d<=1}` behaves like `(?:x|)`
with the empty branch costing one error. A repeat iteration whose body matched only by deleting
therefore matches no text. It is an *empty iteration*. Every backtracking engine needs some rule
for empty iterations, or `(a?)*` would loop for ever at one position. For exact matching the rules
are well known (next section). For fuzzy matching the question is new, because an empty iteration
is no longer free: it costs errors, changes `fuzzy_counts`, and uses budget that later items might
need.

A short example: `(?:b*){d<=2}` over `bba`. The repeat matches `bb`, then tries another `b` at the
`a`. A deletion lets that iteration succeed with no text. Should the match report 0, 1 or 2
deletions? Upstream reports 2; `(?e)`, `(?b)` and TRE report 0.

## Problem 1: upstream's current fuzzy behaviour

Upstream's rule lives in `RE_OP_END_GREEDY_REPEAT` (`upstream/src/_regex.c:12550-12557`, and the
lazy twin at `:12787-12793`). An iteration counts as *progress*, so the loop may go round again, if
the text position moved or the counter `capture_change` moved. That counter goes up when a group
that the pattern references changes its span (`:12726-12728`), and also on every fuzzy edit
(`fuzzy_match_item` at `:10250`, `fuzzy_match_string` at `:10487`, and six more sites up to
`:11067`). So an iteration that only deleted counts as progress. The one fuzzy exception turns this
off when the repeat has met its minimum and stands at the end of the text.

Measured consequences (`run_mrab_fuzzy.py`, `(s, i, d)` counts):

| pattern | subject | plain | `(?e)` | `(?b)` |
|---|---|---|---|---|
| `(?:b*){d<=2}` | `bb` | (0,2) (0,0,0) | (0,0,0) | (0,0,0) |
| `(?:b*){d<=2}` | `bba` | (0,2) **(0,0,2)** | (0,0,0) | (0,0,0) |
| `(?:ac+){d<=2}` | `a` | (0,1) **(0,0,2)** | (0,0,1) | (0,0,1) |
| `(?:(ab)+){d<=2}` | `ab` | (0,2) **(0,0,2)** | (0,0,0) | (0,0,0) |
| `(?:b{3}){d<=2}` | `b` | (0,1) (0,0,2) | (0,0,2) | (0,0,2) |
| `(?:cats\|cat){d<=1}` | `cat` | (0,3) (0,0,1) | (0,0,0) | (0,0,0) |

So upstream takes as many deleted iterations as the budget allows, except at the end of the text:
the same pattern reports 0 deletions over `bb` and 2 over `bba`.

When the budget restarts on every iteration, the loop never ends. A fuzzy section inside the repeat
body starts its counts at zero each time, so `(?:(?:x){d<=1})+y` over `y` raises MemoryError
(ledger entry 33, `docs/plan/upstream-reports/entry-33-fuzzy-empty-iteration.md`). This survey
found one more shape that does not need an inner section: `fullmatch("(?:(?:b?)*){d<=1}", "a")`
raises MemoryError after 2.1 s, where the right answer is no match. It has not yet been run against
the port.

The port already stops the unbounded loops (ledger entry 33, fixed in S88 and completed
2026-09-25, `src/FuzzyRegex/Engine/Matcher.cs` near line 7315): past the minimum, a repeat with no
maximum stops at an iteration that did not move, changed no referenced group, and charged no edit
to the enclosing section. That rule was designed to change no answer upstream gives, so it keeps
upstream's inflated counts everywhere else.

## Problem 2: what rule should replace it

This is the decision. A good rule should:

1. never lose a match: if some choice of iterations fits the constraints, the rule must allow one;
2. always terminate, whatever the budget, including unbounded ones like `{d}`;
3. not charge errors the text does not need, as far as a first-match engine can manage;
4. leave exact matching (no errors) exactly as it is;
5. be cheap in the matching loop.

## What other engines do with empty iterations (exact matching)

This is the precedent for requirement 4 and for the rule's shape. Each case is searched (first match
anywhere); the table shows the span and group 1 (group 2 for the POSIX form of c07). POSIX engines
get the same pattern without `(?:`.

| case | pattern | subject | what it tells apart |
|---|---|---|---|
| c01 | `(?:(b\|))*` | `bba` | extra empty iteration after the text (A) or not (B) |
| c02 | `(?:(b\|))*` | `bb` | the same, text exhausted |
| c03 | `(?:(b\|))*` | `''` | no text, minimum 0 |
| c04 | `(?:(b\|))+` | `bba` | minimum 1, extra empty iteration after the text |
| c05 | `(?:(b\|))+` | `''` | minimum 1, the one required iteration is empty |
| c06 | `^(b\|)+$` | `bb` | full-match form |
| c07 | `(?:(a)\|b\|)*` | `ab` | does a capture survive a later iteration |
| c08 | `^(a\|)*?$` | `a` | lazy loop |
| c09 | `(?:(b\|)){2,}` | `b` | one empty iteration needed to reach the minimum |
| c10 | `(?:(b\|)){3,}` | `b` | two empty iterations needed to reach the minimum |
| c11 | `(?:(b\|)){2}` | `''` | exact count, every iteration empty |
| c12 | `(\|a)*` | `aa` | empty branch preferred, minimum 0 |
| c13 | `(\|a)+` | `aa` | empty branch preferred, minimum 1 |

| engine | c01 | c02 | c03 | c04 | c05 | c06 | c07 | c08 | c09 | c10 | c11 | c12 | c13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| re | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| regex | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| PCRE2 | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| Perl | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 unset | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| Java | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| .NET | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| .NET NonBacktracking | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| Onigmo (Ruby) | 0,2 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,0 '' | 0,2 '' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |
| Node (ECMAScript) | 0,2 'b' | 0,2 'b' | 0,0 unset | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,2 unset | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,2 'a' | 0,2 'a' |
| glibc (POSIX) | 0,2 'b' | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,2 'a' | 0,1 'a' | 0,1 'b' | 0,1 'b' | 0,0 '' | 0,2 'a' | 0,2 'a' |
| TRE (POSIX) | 0,2 'b' | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,2 unset | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,2 'a' | 0,2 'a' |
| RE2 | 0,2 'b' | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,0 '' | 0,2 'b' | 0,2 'a' | 0,1 'a' | 0,1 '' | 0,1 '' | 0,0 '' | 0,0 '' | 0,0 '' |

c02 and c12 separate the two main rules:

- **Rule A** (after the minimum, one empty iteration is accepted and the loop stops): `''` on c02
  and (0,0) on c12. Perl, PCRE2, Python `re`, `regex`, Java, .NET and Onigmo.
- **Rule B** (after the minimum, an empty iteration fails): `'b'` on c02 and (0,2) on c12.
  ECMAScript, glibc and TRE.

Every engine allows as many empty iterations as the minimum needs (c09, c10, c11).

| engine | version | rule | decisive evidence |
|---|---|---|---|
| Perl | 5.42.3 | A | run; perlre "Repeated Patterns Matching a Zero-length Substring": `(?: NON_ZERO_LENGTH \| ZERO_LENGTH )*` "is made equivalent to" `(?: NON_ZERO_LENGTH )* (?: ZERO_LENGTH )?` |
| PCRE2 | 10.47 | A | run; pcre2pattern: "whenever an iteration of such a group matches no characters, matching moves on to the next item in the pattern" |
| Python `re` | 3.14.7 | A | run; `Modules/_sre/sre_lib.h` (v3.14.0) lines 1179-1189 start another iteration only if the position moved |
| mrab `regex` (exact) | 2026.9.10 | A, plus group progress | run; `_regex.c:12550-12553`: progress if the text moved or a referenced group changed its span (see the open question) |
| Java | OpenJDK 26 | A | run; `Pattern.java` `Loop.match` (line 5053) checks only the position |
| .NET | 10.0.12 | A | run; Microsoft Learn "Quantifiers and Empty Matches": quantifiers "never repeat after an empty match when the minimum number of captures has been found" |
| Onigmo (Ruby) | 3.2.3 | A, plus group progress | run; `regexec.c` `STACK_NULL_CHECK_MEMST` (line 993, k-takata/Onigmo 1d7ee87) treats an empty pass that captured a group at a new place as not empty |
| ECMAScript (V8) | Node 24.16.0 | B | run; ECMA-262 22.2.2.3.1 RepeatMatcher step 2.b: "If min = 0 and y.[[EndIndex]] = matchState.[[EndIndex]], return failure." |
| POSIX | Issue 8 (2024) | B | spec, see below |
| glibc | 2.39 | B | run |
| TRE (exact) | 0.8.0 | B | run |
| RE2 | google-re2 1.1.20251105 | other | run; c02 `'b'` but c12 (0,0): an automaton drops a thread that returns to the loop state at the same position |
| Go, Rust `regex` | - | as RE2 | documented, not run: golang/go#46123, rust-lang/regex#779 |

POSIX states rule B twice, with different scope. For BREs (XBD 9.3.6): "A subexpression repeated
by an <asterisk> ('*') or an interval expression shall not match a null expression unless this is
the only match for the repetition or it is necessary to satisfy the exact or minimum number of
occurrences for the interval expression." For EREs (XBD 9.4.6) the sentence covers only "An ERE
matching a single character repeated by an '*', '?', or an interval expression"; POSIX says
nothing explicit about repeated ERE subexpressions. The idea behind both is the one this note
generalises: an empty repetition is allowed only when it is **necessary**.

Why the rules exist: all of them stop an infinite loop. Rule A lets the empty iteration happen
once, notices the position did not move, and stops; it is cheap and, in exact matching, invisible
except through captures. Rule B refuses the empty iteration beyond the minimum; ECMAScript gives
termination as the reason (Note 4). Automaton engines get termination from the automaton itself.

## TRE, the other fuzzy engine

TRE (`tre_regaexec`) is a minimum-cost matcher: its documentation says it "searches for the best
match", and it defines a deletion as "a character missing from string", the same definition as
upstream's. `tre_probe.c`, deletions only, budget N:

| ERE | subject | N | TRE (s,i,d) | upstream plain |
|---|---|---|---|---|
| `b+` | `bb` | 1 | (0,0,0) | 0 |
| `ac+` | `a` | 2 | (0,0,1) | 2 |
| `b*` | `bba` | 2 | (0,0,0) | 2 |
| `b{3}` | `b` | 2 | (0,0,2) | 2 |
| `(ab)+` | `ab` | 2 | (0,0,0) | 2 |
| `cats\|cat` | `cat` | 1 | (0,0,0) | 1 |

TRE never spends a deletion it does not need. A first-match rule cannot promise that in general:
`(?:b*bb){d<=2}` over `bb` has the greedy `b*` take both characters and delete the two `b`s that
follow, giving (0,0,2) under every first-match rule tested here, while TRE, `(?e)` and `(?b)` give
0. That is alternation and greed order, not empty iterations.

## What the first draft got wrong (blind review, 2026-09-26)

The first draft recommended rule B for iterations that spent errors. A blind review found six
problems, all verified by this revision:

1. **Rule B loses matches when a constraint has a minimum error count.** `match("(?:b*){1<=d<=2}",
   "bba")` is (0,2) with two deletions upstream, but rule B finds nothing: the only way to reach one
   deletion is an empty deleting iteration after the minimum. `fullmatch("(?:(?:b)+){1<=d<=2}",
   "b")` likewise. The reviewer's 9,000-case sweep lost 788 matches, all with minimum constraints;
   this revision reproduces 788 exactly.
2. **Rule B loses entry 33's own case.** `search("(?:(?(1)c|z)|()(?:x){d<=1})*$", "c")` is (0,1)
   with one deletion upstream. The empty deleting iteration sets group 1, which the next pass
   tests. The first draft said entry 33 suggested rule B; entry 33 in fact says that rule is too
   broad for exactly this reason.
3. **Exact matching is not identical across engines.** Upstream counts a changed referenced group
   as progress, so `^(?:(?(1)c|z)|())*$` over `c` matches in upstream but not in `re`, PCRE2 or Perl.
   The draft's claim that exact matching was "identical to upstream and re/PCRE2/Perl" was false.
   See the D12 decision at the end.
4. **The POSIX quote was misattributed.** The subexpression wording is BRE 9.3.6; ERE 9.4.6 covers
   only single-character EREs (quoted above).
5. **A citation was wrong.** Fuzzy edits increment `capture_change` at `_regex.c:10250`, `:10487`
   and others, not `:10486`.
6. **"Rule B equals TRE, (?e) and (?b) on every repeat case" was false in general**
   (`(?:b*bb){d<=2}` over `bb`, above).

## What the second blind review found (2026-09-26)

A second blind review tested the first version of the "needed" rule and found three defects, all
verified and fixed in this revision:

1. **Rule (b) did not always terminate.** When the unmet minimum is on substitutions or insertions,
   deletions can never meet it, and with an unlimited deletion budget `(?:b*){1<=s<=1,d}` over `''`
   ran for ever (see Termination below). Fixed by allowing an iteration only if its deletions raise
   the unmet count.
2. **It could be much slower than rule B** on nested patterns with such minimums (97 s against
   0.71 s on one case). Fixed by the same change and by ending the loop after the last iteration
   that could be needed (see Performance).
3. **The note overstated the capped re-run** of the 14 cases where the oracle timed out: the
   counts agreed only at cap 1. Corrected below, and the re-run is now `needed_sweeps.py s4cap`.

The second fix for defect 2 (ending the loop after the last iteration that could be needed) was
itself wrong; the third review showed it, and it has been replaced (next section).

## What the third blind review found (2026-09-26)

1. **Ending the loop early lost matches.** The second revision ended the loop after the last empty
   iteration that could be needed, on the argument that an iteration consuming text "could equally
   have come before" the empty ones. That is false when the empty iteration is only possible
   first. In `fullmatch("(?:(?:^z|aa)*){1<=d<=1}", "aa")` the `z` can be deleted only while `^`
   still holds, at the start, before `aa` is read; after the deleting iteration the loop ended, so
   `aa` was never read, and "needed" found nothing where upstream and unrestricted give (0,2) with
   one deletion. A conditional does the same: in `(?:(?(1)zz|z)|(aa))*` the deletion costs one
   error before group 1 is set and two after. The reviewer's targeted grid lost 34 of 4,800 cases;
   it is now sweep s6.
2. **The sweep script compared only existence in `search`.** A match lost at the right start but
   found at a later one counted as no loss: `search("(?:(?:(?(1)zz|z)|(aa))*){1<=d<=1}$", "aa")`
   gave (1,2) under the second revision where the oracle gives (0,2). The script now also requires
   the same start in `search`, and every sweep has been re-run.
3. **Exponential time on a simple family.** `fullmatch("(?:(?:a|b|c|d)*){n<=d<=n}", "x")` grew about
   4^n (0.96 s at n = 8) while rule B stayed constant; upstream grows the same way. Deleting `a`,
   `b`, `c` or `d` in an empty iteration leads to the same state, and the search explored all four
   every time.

Defects 1 and 3 have one cause: the search explores many paths that end in the same state. The fix
drops the loop-end shortcut and instead drops an empty iteration whose resulting state an earlier
path through the same repeat already reached (see Termination and Performance for why this is
exact). With it, all three reproductions are correct and fast.

## What the fourth blind review found (2026-09-26)

The state check changed no first-match result on 301,000 cases, and the review's own brute force
found no lost match. It found four remaining defects, all fixed here: the summary claimed "needed"
was never more than a few milliseconds slower than rule B, which this note's own table contradicted
(Performance now states where and by how much); three slow families were missing (added, with
growth measured at four sizes, and the repeat memo made part of the rule because without it they
grow exponentially); the random sweep's brute force had skipped the 27 cases where either rule
timed out (now always run: 0 lost of 7,200); and the reference matcher silently read unsupported
syntax such as `(?=a)` as an ordinary group (it now raises an error).

## The options

Seven candidate rules for an iteration that matched no text and spent errors. Each keeps
upstream's exact-matching behaviour for error-free empty iterations unless noted.

- **D. Upstream as is.** Allowed whenever the budget allows, except at the end of the text.
- **D+. The port today.** D, plus entry 33's stop for unbounded repeats that would otherwise loop.
- **A. Perl rule.** Accepted once after the minimum, then the loop stops.
- **B. The first draft.** Fails once the minimum is met.
- **C. Never.** Always fails, even below the minimum.
- **N. "Needed" (recommended).** Allowed only if (a) the repeat has not reached its minimum count,
  or (b) the iteration's errors raise a count that an open fuzzy section has an unmet minimum for
  (a deletion raises a `d` minimum and an `e` minimum, never an `s` or `i` minimum), or (c) the
  iteration changed the span of a group that a backreference or conditional tests (upstream's own
  progress test, without the fuzzy edits it also counts). Otherwise it fails. Any path, after
  an empty iteration or one that consumed text, is also dropped if an earlier path through the same
  run of the repeat already reached the same state (the repeat memo); that removes only duplicate
  work and never changes the answer.
- **M. Minimum cost.** Return the match with the fewest errors, as TRE and `(?b)` do.

### Measured on generated cases

The reference matcher (`tools/probes/fuzzy-reference-matcher.py`) implements A, B, C and N as
modes, plus an *unrestricted* mode in which an empty deleting iteration may always go round again,
stopped only by the error budget. Unrestricted explores every choice, so it finds a match whenever
one exists; it is the yardstick for requirement 1. `needed_sweeps.py modes` runs 27,144 cases (the
reviewer's 9,000-case grid of items, repeats, constraints and texts; a 13,608-case grid of nested
and lazy repeats; and 4,536 cases with minimum constraints):

| rule | matches lost (unrestricted found one, the rule found none) |
|---|---|
| unrestricted | 0 |
| **N (needed)** | **0** |
| A (Perl) | 426 |
| B (first draft) | 1,801 |
| C (never) | 3,891 |

D cannot be run to completion in general: it is the behaviour that loops.

### Pros and cons

| option | pros | cons |
|---|---|---|
| D, upstream | identical to upstream | loops to MemoryError (entry 33, and `(?:(?:b?)*){d<=1}` above); counts depend on whether the repeat ends at the end of the text; charges errors the text does not need |
| D+, the port today | matches every upstream answer upstream can give; already built and tested | keeps D's inflated and position-dependent counts; the rule is narrow and hard to explain |
| A, Perl | familiar from exact matching; cheap | loses 426 matches; charges one needless deletion on almost every greedy fuzzy repeat that stops before a character it cannot match: `(?:b*){d<=1}` over `bba` gives (0,0,1) |
| B, first draft | the rule of ECMAScript and POSIX BREs; cheap | loses 1,801 matches (minimum constraints, and conditionals like entry 33's) |
| C, never | simplest | loses 3,891 matches, including every repeat whose minimum can only be met by deleting |
| **N, needed** | loses nothing; terminates; spends errors only when something requires them; exact matching unchanged | diverges from upstream's plain counts; still first match, so not always the fewest errors; needs a state memo to stay polynomial on nested repeats, and is slower than rule B where rule B gives up on matches (up to about 50 times in the reference matcher) |
| M, minimum cost | the best answer by definition; what TRE does | changes the meaning of the default mode, which the README defines as first match; expensive (upstream's `(?b)` times out on `(?:(?:x){d<=1}){1,3}y` over `y`, ledger entry 33) |

## Evidence for the "needed" rule

`python tools/probes/empty-iteration-survey/needed_sweeps.py` runs the sweeps below. Each compares
whether "needed" finds a match against unrestricted, and, where the whole pattern is one fuzzy
section around an exact regex, against a **brute force**: is there any string the exact regex
matches that aligns with the text (or, for `match`, a prefix of it) within the limits? Every case
has a 2-second limit.

| sweep | cases | lost vs unrestricted | brute force found a match "needed" missed | "needed" timeouts |
|---|---|---|---|---|
| s1: the reviewer's 9,000-case grid (`match`, `fullmatch`) | 9,000 | 0 | 0 of 9,000 checked | 0 |
| s2: the reviewer's nested and lazy grid (`match`, `fullmatch`, `search`) | 13,608 | 0 | 0 of 7,776 checked | 0 |
| s3: entry 33's case and variants with conditionals, backreferences and tails | 4,492 | 0 | not applicable | 0 |
| s4: minimum constraints (`{2<=d<=2}`, `{1<=e<=3}`, `{1<=i<=2,d<=2}`, minimum plus cost equation), flat | 4,536 | 0 | 0 of 4,536 checked | 0 |
| s4: the same minimums around nested sections | 12,096 | 0 (14 compared at a tighter cap, below) | not applicable | 0 |
| s5: unbounded budgets `{d}`, `{e}`, `{1<=d}`, `{i,d}`, `{2<=e}`, and minimums deletions cannot reach (`{1<=s<=1,d}`, `{1<=i<=1,d}`, `{1<=s<=1}`, `{1<=i<=1}`), with nested repeats and sections | 58,320 | not applicable | not applicable | 0 (slowest 0.03 s) |
| s6: the third review's grid, empty iterations possible only before a consuming one (`^`, conditionals) | 4,800 | 0 | 0 of 4,800 checked | 0 |
| rand: the second review's random nested sweep, seeds 3 and 4, 13 constraint kinds | 7,200 | 0 | 0 of 7,200 checked | 1 (below) |

"Lost" means the oracle found a match and "needed" did not, or, for `search`, that "needed" found
its match at a later start than the oracle (the third review showed the script had checked only
existence there; the s2 and s3 figures above include the start check).

The brute force also checks the other direction (it finds no alignment but "needed" matches). That
happened only in s6, 18 times, all with `^`: `fullmatch("(?:(?:^z|aa){2,}){2<=d<=2}", "")` deletes
`z` twice at position 0, where `^` holds both times, while the brute force works from the exact
language, in which `^z` can occur only once. Upstream matches all 18, so this is a limit of the
brute force, not a defect in the matcher.

Unrestricted timed out on 14 of the nested s4 cases, for example `(?:(?:(?:b?){e<=1})*){1<=d<=3}`
over `abab`: the inner section can insert as well as delete, and the outer section does not
permit insertions, so unrestricted wanders until the outer section finally rejects the result.
`needed_sweeps.py s4cap` re-runs them with the empty-iteration run capped at 1, 2 and 3 in a row.
On existence, "needed" agrees with the loosest cap on all 14, and no cap finds a match "needed"
misses. The first match found does depend on the cap: on the `match` form above, cap 1 gives (0,0)
with one deletion, as "needed" does, but caps 2 and 3 give two and three deletions, because a
looser cap lets unrestricted spend more before it stops. With `{2<=d<=3}`, cap 1 finds nothing
(one empty iteration cannot reach two deletions) while caps 2 and 3 and "needed" all match. The
first draft of this paragraph said the caps agreed on counts too; that held only at cap 1.

In the random sweep, unrestricted timed out on 27 cases; the brute force still covered them.
The figures in the table are with the repeat memo (see Performance), which is now part of the
rule. An earlier version of the script skipped the brute force whenever either rule timed out, so
it checked 7,173 of the 7,200 cases while this note claimed all; it now always runs it, and all
7,200 are checked with 0 lost (the fourth review's independent brute force agrees). Without the
memo, "needed" timed out on 4. Two are the second review's slow case
`fullmatch (?:(?:(?:(?:a??a??)??|(?:a{1,3}?|){2}){1,3}?|){2,}){1<=s<=1,d<=2}` over `acc` and `cac`
(2.1 s against the 2 s limit; see Performance). Two are
`fullmatch (?:(?:(?:(?:c*?|){2}(?:a{2,}|){1,3}?){2}(?:(?:c{2}|){2,}?|(?:c+|c{0,2}){2,}?){2,}?){2}){d<=2}`
over short texts. That pattern has no minimum, so no admission rule applies to it; it is the
reference matcher's own search blowing up on six nested repeats. Over `ab`, "needed" took 11.7 s,
rule B 9.9 s and rule A 29.3 s, and upstream raises MemoryError after 2.7 s. With the memo, only
this pattern over `ccb` still times out; rule B and upstream time out there too.

Cost minimums: upstream has no syntax for a minimum cost. `(?:b*){1<=2i+2d+1s<=4}` is not rejected,
but `b{1<=1d<=4}` fully matches the literal text `b{1<=1d<=4}`, so the braces are not read as a
constraint. The sweeps therefore combine a minimum error count with a cost equation instead.

Rows worth seeing (search unless stated; upstream in the last column):

| pattern | subject | "needed" | unrestricted (first found) | upstream |
|---|---|---|---|---|
| `match (?:b*){1<=d<=2}` | `bba` | (0,2) (0,0,1) | (0,2) (0,0,2) | (0,2) (0,0,2) |
| `match (?:b*){2<=d<=2}` | `bba` | (0,2) (0,0,2) | (0,2) (0,0,2) | (0,2) (0,0,2) |
| `(?:(?(1)c\|z)\|()(?:x){d<=1})*$` | `c` | (0,1) (0,0,2) | (0,1) (0,0,7) | (0,1) (0,0,1) |
| `(?:(?:x){d<=1})+y` | `y` | (0,1) (0,0,1) | (0,1) (0,0,6) | MemoryError |
| `(?:(b?)(?:x){d<=1})*\1c` | `bc` | (0,2) (0,0,2) | (0,2) (0,0,7) | MemoryError |
| `(?:\d+a0b+?){d<=2}` | `67a0bab` | (0,5) (0,0,0) | (0,5) (0,0,2) | (0,5) (0,0,2) |
| `match (?:(ab)+){d<=2}` | `ab` | (0,2) (0,0,0) | (0,2) (0,0,2) | (0,2) (0,0,2) |
| `fullmatch (?:(?:b?)*){d<=1}` | `a` | no match | no match | MemoryError |

Entry 33's case shows the rule's limit. "Needed" gives two deletions where upstream gives one: after
matching `c`, a third pass at the end of the text deletes `x` and moves group 1 from (0,0) to
(1,1). That is a change to a tested group, so rule (c) allows it, even though no later pass uses
it. Upstream avoids it only because it happens to be at the end of the text. A rule that looked
ahead to see whether the change is used would be a different, more expensive design; the extra
deletion is the honest cost of a first-match rule.

## How "needed" compares with upstream, `(?e)`, `(?b)` and TRE

`needed_sweeps.py counts` compares the full match (span and counts) on the 9,000-case grid
(regenerate `needed_counts.tsv` with it for every row; it is not committed). Upstream calls have a 2-second limit.

| upstream mode | same result | "needed" fewer errors | "needed" more errors | same total, other span or types | only "needed" matches | only upstream matches | upstream MemoryError or timeout |
|---|---|---|---|---|---|---|---|
| plain | 4,478 | 1,523 | 37 | 275 | 306 | 0 | 342 |
| `(?e)` | 5,234 | 26 | 791 | 244 | 306 | 0 | 360 |
| `(?b)` | 5,242 | 6 | 804 | 242 | 307 | 0 | 360 |

(The rest, 2,039 in each row, have no match in either.) Reading it:

- Against plain upstream, "needed" mostly gives the same or fewer errors. The 37 cases with more
  errors all have the `{1<=e<=2}` minimum, 34 of them in lazy repeats, where the two take different
  first paths
  (`fullmatch (?:(?:b)*?){1<=e<=2}` over `bba`: two insertions against one substitution).
- The 306 matches only "needed" finds are real: the brute force confirms each (s1 above). They are
  upstream's end-of-text exception at work, as in `match("(?:(?:b)*){1<=d<=2}", "b")`, where
  upstream finds nothing but one deleted `b` after the text fits. Upstream never found a match that
  "needed" missed.
- Against `(?e)` and `(?b)`, "needed" has more errors in about 800 cases. Of the 804 against
  `(?b)`, 589 have constraints that allow substitutions or insertions, where a first-match engine
  absorbs a following character rather than stopping: `match (?:(?:b)*){e<=1}` over `bba` is (0,3)
  with one substitution, where `(?b)` stops at (0,2) with none. The other 215 are deletion-only
  cases where greed or branch order takes a costlier first path, like `(?:b*bb){d<=2}` over `bb`
  above. That is the first-match design, and "needed" does not change it.
- Upstream failed with MemoryError or a timeout in 342 to 360 cases in every mode, for example
  `fullmatch (?:(?:b?)*){d<=1}` over `bba`. "Needed" answered all of them.

TRE (`needed_sweeps.py tre`), on the 3,024 greedy cases of the same grid that have no minimum
(TRE has no minimums or lazy repeats in its fuzzy mode), comparing total errors where both match:

| engine | same total as TRE | more errors than TRE | MemoryError or timeout |
|---|---|---|---|
| upstream plain | 870 | 1,147 | 117 |
| "needed" | 1,652 | 365 | 0 |
| upstream `(?e)` | 2,012 | 5 | 117 |
| upstream `(?b)` | 2,017 | 0 | 117 |

In 14 cases every `regex` mode and "needed" find a match that TRE does not, for example
`fullmatch (?:(?:b){2}){e<=1}` over `bba` (one inserted `a`); that is a TRE difference unrelated to
empty iterations. On `fuzzy.tsv`, "needed" gives `(?b)`'s count on every row except `cats|cat`,
where branch order decides (f02 `ac+` over `a`: 1 deletion, upstream 2; f04 `b*` over `bba`: 0,
upstream 2; f07 `(ab)+` over `ab`: 0, upstream 2).

In short, "needed" gives the same count as upstream wherever upstream does not take a deletion-only
iteration, fewer errors where upstream takes one that nothing required, and sometimes more errors
than `(?b)` and TRE, because those search for the cheapest fit and "needed" returns the first.

## Performance

**In the port's matcher.** The rule is checked only at the end of a repeat iteration that matched
no text, which is already a special case in `RE_OP_END_GREEDY_REPEAT`. It needs:

- whether the iteration spent errors: one saved error total per repeat record, compared with the
  current total. The port already saves a per-section edit count there for entry 33, so this adds
  one integer to a record that is saved and restored anyway;
- whether a section minimum is unmet: a comparison of the current counts with the section's
  minimums, only on this rare path, and only for sections that have a minimum;
- whether a tested group changed: the port already keeps a group-only change counter (the low
  half of `CaptureChange`, `MatchState.cs:452`), because upstream only counts a referenced group
  whose span actually changed (`same_span_as_group`, `_regex.c:12727`). So this is one comparison.

The admission test touches only iterations that matched no text. The repeat memo is different: it
is a set lookup after every iteration of a fuzzy repeat, so it adds a constant cost to the common
path and saves whole subtrees when paths meet. Whether that is a net gain on real workloads must be
measured in the engine; exact (non-fuzzy) matching keeps upstream's position guards and is
unaffected.

**Termination.** Every empty iteration the rule admits must make progress toward something
finite:

- (a) raises the repeat count toward the repeat's minimum;
- (b) raises a counted quantity toward a section minimum that is not yet met. An empty iteration
  consumes no text, so its errors are deletions, and a deletion raises only the `d` count and the
  total `e`. So a run of (b) iterations is bounded by the sum of the unmet `d` and `e` minimums.
  Where sections nest, an outer section counts the inner section's errors, because END_FUZZY adds
  the inner counts to the outer ones when the inner section ends (`_regex.c:12475-12481`); the
  rule counts them the same way;
- (c) changes the span of a tested group. Inside an empty iteration at position p, a group can
  only be set to a span that ends at p, so a run of empty iterations cannot keep changing it. The
  reference matcher adds a guard anyway: a group state already seen in the current run of empty
  iterations is not a change. A lookbehind inside the group could in principle alternate between
  two spans; no sweep produced such a case, and the port slice should pin it with a test and add
  the same guard.

The state check (next paragraph) does not add termination; it removes duplicate work.

The first version of (b) said only "while a section minimum is unmet", and the second review
showed that it did not terminate: with `(?:b*){1<=s<=1,d}` over `''`, the `s` minimum can never be
met by deleting, and `{d}` has no limit, so it allowed deletion after deletion for ever (the same
with `(?:(?:(?:x){d<=1})*){1<=s<=1}`, where the inner section's budget restarts, and
`(?:b*){1<=i<=1,d}`). Upstream answers all three with no match at once. The revised (b) allows an
iteration only if its deletions raise the unmet count, so these now answer no match at once too;
s5 includes all three shapes.

**Dropping repeated states.** Many paths through a repeat can end in the same state: in
`(?:(?:a|b|c|d)*){8<=d<=8}` over `x`, each empty iteration can delete `a`, `b`, `c` or `d`, and all
four lead to the same place, so a plain search explores 4^8 paths. "Needed" records, for each run
of a repeat (one entry into the repeat, with its fixed continuation), the state each admitted empty
iteration leads to, and drops an iteration whose state is already recorded. The state is
everything the rest of the match can depend on:

- the text position;
- the repeat count, clipped to what the repeat can still tell apart (with no maximum, counts at or
  above the minimum are all alike);
- the error counts of every open fuzzy section;
- the spans of the groups a backreference or conditional tests, and the tested part of the (c)
  cycle guard.

Groups that nothing tests, and the totals of fuzzy sections already closed, change what a match
reports but never whether the rest of the pattern can match. Why dropping is exact: the earlier
path with the same state had the same future. The search finished exploring it before reaching the
later path, because two paths with equal states are never on the same branch (every admitted
iteration raises the count, an error count or a tested span, and none of these ever go down). So if
the earlier path found a match, the search had already returned it; if it did not, the later path
cannot either. The first match returned is therefore unchanged, groups and counts included.

**Search size.** The timings below come from the Python reference matcher, which is slow on
purpose; they measure how many branches each rule makes the search explore, not the port's speed.

| case | "needed" | rule B | upstream (2 s limit) |
|---|---|---|---|
| `(?:(?:a\|b\|c\|d)*){n<=d<=n}` over `x`, n = 12 | 0.001 s | 0.000 s | timeout (1.28 s at n = 11) |
| second review's slow case (`...{1<=s<=1,d<=2}` over `cac`), empty-iteration check only | 2.3 s | 0.40 s | timeout |
| the same, with the repeat memo | 0.016 s | 0.43 s | timeout |
| third review's rows (`(?:(?:^z\|aa)*){1<=d<=1}` and variants) | 0.000 s, correct | 0.000 s, no match | 0.000 s |
| 27,144 grid cases, total | 2.0 s | 1.7 s | not run |

The history of the slow case: the first version took 97 s (against rule B's 0.71 s) because the
loose (b) admitted deletions that could never meet the `s` minimum; the corrected (b) brought it to
18 s; state-dropping on empty iterations brings it to 2.3 s. What remains is the search re-trying
the same nested sub-patterns after iterations that did consume text, which has nothing to do with
the empty-iteration rule.

**The repeat memo (part of the recommendation).** The same state check, applied after every
iteration rather than only after empty ones, is just as exact, for the same reason (an iteration
that consumes text moves the position forward, so again two equal states are never on one branch).
It changed no result, groups and counts included, on 2,616 cases checked with it on and off, and
the fourth review found no change on 294,840 cases. It is what keeps "needed" usable on nested
repeats: without it, several families below grow exponentially. It is upstream's own idea: its
repeat guards record positions where a repeat body has already failed (`guard_repeat`,
`_regex.c:9446`), but `is_repeat_guarded` switches them off whenever the pattern is fuzzy
(`:9564-9566`), because a position alone is not a state when error counts differ. Keying the guard
by the state above brings it back for fuzzy patterns. In the reference matcher it is the default
(`NEEDED_DEDUP_ALL`; `NEEDED_DEDUP_ALL=0` in the sweep script's environment turns it off).

**Where "needed" is slower, and by how much.** `needed_sweeps.py perf` times the families the
fourth review found slow, at four sizes (seconds; 30 s limit for the reference matcher, 10 s for
upstream):

| family (text) | n | "needed" | empty check only | rule B | upstream |
|---|---|---|---|---|---|
| `fullmatch (?:(?:a\|b\|c\|d)*){3<=d<=3}` (`a`*n + `x`) | 4 / 8 / 16 / 32 | 0.001 / 0.002 / 0.004 / 0.007 | 0.002 / 0.004 / 0.013 / 0.046 | 0.000 / 0.000 / 0.000 / 0.001 | 0.001 / 0.003 / 0.025 / 0.27 |
| `fullmatch (?:(?:(?:a\|b\|c\|d)*)*){3<=d<=3}` (`a`*n + `x`) | 4 / 8 / 16 / 32 | 0.007 / 0.024 / 0.087 / 0.34 | 0.069 / 4.5 / timeout / timeout | 0.001 / 0.012 / 2.9 / timeout | MemoryError at every size (7 s) |
| `fullmatch (?:(?:a\|b\|c\|d)*(?:a\|b\|c\|d)*){4<=d<=4}` (`a`*n + `x`) | 4 / 8 / 16 / 32 | 0.014 / 0.046 / 0.17 / 0.64 | 0.021 / 0.12 / 0.97 / 10.5 | 0.000 / 0.001 / 0.004 / 0.013 | 0.024 / 0.28 / 6.1 / timeout |
| `search (?:(?:(?:a\|b)*?)+?){2<=d<=2}c` (`ab`*(n/2)) | 4 / 8 / 16 / 32 | 0.005 / 0.024 / 0.15 / 1.06 | 0.019 / 0.94 / timeout / timeout | 0.001 / 0.012 / 3.2 / timeout | 0.000 at every size |
| `fullmatch (?:(?:(a)\|b\|c\|d)*(?(1)q\|r)){2<=d<=2}` (`a`*n + `x`) | 4 / 8 / 16 / 32 | 0.001 / 0.002 / 0.005 / 0.009 | up to 0.087 | up to 0.001 | up to 0.011 |
| `fullmatch (?:(?:(?:a\|b\|c\|d){1<=d<=1})*){4<=d<=4}` (`a`*n + `x`) | 4 / 8 / 16 / 32 | 0.000 | 0.000 | 0.000 | MemoryError at every size |

Read plainly:

- With the memo, every family grows polynomially: roughly linear for the first and fifth,
  quadratic (four times per doubling) for the second and third, and cubic for the lazy `search`
  (about seven times per doubling, because `search` also tries every start). Without the memo, the
  second, third and fourth grow exponentially.
- Rule B is faster on the third family by a factor of about 50 at n = 32 (0.013 s against 0.64 s),
  and on the first and fifth by a few milliseconds. Rule B is fast there because it refuses the
  deleting iterations that the `{4<=d<=4}` minimum needs; on these texts both find no match, but
  in the sweeps rule B lost 1,801 matches this way. On the second and fourth families rule B is
  itself exponential and times out at n = 32, where "needed" takes 0.34 s and 1.06 s.
- Upstream is faster on the lazy `search` family (0.000 s at every size, against 1.06 s at n = 32).
  On the second, third and last families upstream runs out of memory or time where "needed"
  answers.
- The remaining super-linear growth has one cause: the memo belongs to one run of a repeat, and an
  inner or following repeat is entered afresh from every path of the outer one, so its memo starts
  empty each time. Making the memo span those runs would need the key to include where the match
  continues afterwards, that is, the whole backtracking context; that is a general memoisation
  design, not a cheap change, and nothing finer can be dropped from the key without losing
  exactness (every field changes what the rest of the match can do). No cheap exact improvement was
  found.
- All of these are timings of a slow Python model. They show how many branches each rule makes
  the search explore, not what the port will cost. The port's real cost has to be measured in the
  engine, with the benchmark suite, once the rule and memo are implemented.

**What the port's engine needs to key the state check.** At the end of a repeat iteration
(`RE_OP_END_GREEDY_REPEAT` and its lazy twin), with the repeat's `RepeatData` at hand:

- `state.TextPos`;
- `RepeatData.Count`, clipped to `min(count, min)` when the repeat has no maximum;
- `state.FuzzyCounts` for the open section, plus the saved outer counts, which are fixed for a run
  of the repeat and so can be left out of a per-run set;
- the spans of the referenced groups. `CaptureChange` cannot stand in for them, because two paths
  can reach the same spans through different numbers of changes; the key needs the spans
  themselves, or a hash of them checked against the spans on a hit;
- the (c) guard's recorded group states.

The set must belong to one run of the repeat: created when the repeat is entered, saved and
restored with the rest of `RepeatData` when an outer backtrack re-enters it, and discarded on exit,
as the body guard lists are today. Its size is bounded by positions times reachable error counts
times tested spans; the port should cap it (a SHORTCUT with the 1 GB backtracking limit as the
ceiling) and fall back to plain search if the cap is hit, which is always correct, only slower.

"Needed" answers several cases where upstream runs out of memory or time:
`(?:(?:x){d<=1})+y` over `y`, `fullmatch("(?:(?:b?)*){d<=1}", "a")`, upstream's `(?b)` on
`(?:(?:x){d<=1}){1,3}y` over `y`, the `(a|b|c|d)*` families above.

**Minimum cost (option M)** would be the expensive choice: it must explore alternatives after the
first fit, which is what `(?b)` already does on request.

## Recommendation

Adopt **N, the "needed" rule**, for iterations that matched no text and spent errors, in both
greedy and lazy repeats:

- allow it below the repeat's minimum count, as every engine does;
- allow it when its deletions raise an enclosing fuzzy section's unmet `d` or `e` minimum,
  which is POSIX's "necessary to satisfy the minimum" applied to error counts;
- allow it when it changed a group the pattern tests, which is upstream's own progress test;
- otherwise fail it;
- drop any path whose state after an iteration (empty or not) an earlier path through the same
  run of the repeat already reached: the repeat memo, which is exact and removes duplicate work
  only. It is upstream's repeat guard keyed by state instead of position, so it also works for
  fuzzy patterns.

Measure the cost in the port's engine with the benchmark suite; the reference timings above show
where to look (nested repeats under a minimum error count, and lazy nested repeats under
`search`).

Keep upstream's behaviour for error-free empty iterations (decided under D12, at the end). Record the
change as a port-right divergence in the ledger alongside entry 33, replacing the narrower entry 33
stop, with the sweeps above as its evidence and the rows above as pinned tests.

Reasons: it is the only candidate that lost no match in any sweep (0 of about 55,700 cases compared
with unrestricted, 0 of 33,312 checked by brute force) while never looping; it removes errors that nothing in the pattern or text required, which is what users of a
first-match mode can reasonably expect; it keeps first-match semantics, so the README's `cats|cat`
example still holds. What it does not do: it does not return the minimum-cost
match (use `(?e)` or `(?b)` for that), and its counts differ from upstream's plain counts wherever
upstream took an unneeded deleted iteration.

## Decision (D12, 2026-09-28): a changed tested group is progress

The question: in exact matching, an iteration of a repeat reads no text but changes the span of a
group that a conditional or a backreference tests. Does the repeat go round again? Upstream says
yes (`_regex.c:12550-12553`, `:12787-12793`; a referenced group bumps `capture_change` only when
its span changes, `same_span_as_group`, `:12726-12728`). Most backtracking engines check the
position only.

**The survey.** `capture_progress.py`, search, run 2026-09-28 (Python 3.14.7 `re`, `regex`
2026.9.10, PCRE2 10.47 via pip `pcre2` 0.7.1, Perl 5.42.3, Node 24.16.0, OpenJDK 26.0.2.1, .NET
10.0.12, Ruby 3.2.3 in WSL, and the port through `port-probe.cs`). Each cell is the match span, or
`none`, or `err` where the engine rejects the syntax (no conditionals in Java or JavaScript, no
possessive `*+` in .NET or JavaScript, `re` rejects `\1` before group 1 is defined). `k` rows test
the group with a conditional, `b` rows with a backreference, `u` rows are controls whose group
nothing tests.

| id | pattern | subject | re | regex | PCRE2 | perl | node | java | .NET | ruby | port |
|---|---|---|---|---|---|---|---|---|---|---|---|
| k01 | `^(?:(?(1)c\|z)\|())*$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k02 | `(?:(?(1)c\|z)\|())*$` | `c` | 1,1 | 0,1 | 1,1 | 1,1 | err | err | 1,1 | 0,1 | 0,1 |
| k03 | `^(?:(?(1)c\|z)\|())+$` | `c` | 0,1 | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k04 | `^(?:(?(1)c\|z)\|()){2,}$` | `c` | 0,1 | 0,1 | 0,1 | 0,1 | err | err | 0,1 | 0,1 | 0,1 |
| k05 | `^(?:(?(1)c\|z)\|()){0,5}$` | `c` | none | 0,1 | 0,1 | none | err | err | none | 0,1 | 0,1 |
| k06 | `^(?:(?(1)c\|z)\|())*$` | `cc` | none | 0,2 | none | none | err | err | none | 0,2 | 0,2 |
| k07 | `^(?:(?(2)c\|z)\|(a)\|())*$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k08 | `^(?:()\|(?(1)c\|z))*$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k09 | `^(?:(?(1)c\|z)\|())*?$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k10 | `^(?:(?(1)c\|z)\|())+?$` | `c` | 0,1 | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k11 | `^(?:(?(1)c\|z)\|())*+$` | `c` | none | 0,1 | none | none | err | err | err | 0,1 | 0,1 |
| k12 | `^(?>(?:(?(1)c\|z)\|())*)$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k13 | `^(?:(?:(?(1)c\|z)\|())*)*$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k14 | `^(?:(?:(?(1)c\|z)\|())+)*$` | `c` | 0,1 | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k15 | `^(?:(?(1)c\|z)\|(()))*$` | `c` | none | 0,1 | none | none | err | err | none | 0,1 | 0,1 |
| k16 | `^(?:(?(1)c\|z)\|())*$` | `` | 0,0 | 0,0 | 0,0 | 0,0 | err | err | 0,0 | 0,0 | 0,0 |
| b01 | `^(?:\1c\|())*$` | `c` | err | 0,1 | none | none | 0,1 | none | none | 0,1 | 0,1 |
| b02 | `^(?:(?=(c))\|\1)*$` | `c` | none | 0,1 | none | none | none | 0,1 | none | none | 0,1 |
| b03 | `^(?:\1\|(?=(c)))*$` | `c` | err | 0,1 | none | none | none | none | none | none | 0,1 |
| b04 | `^(?:\1\|(?=(c)))*?$` | `c` | err | 0,1 | none | none | none | none | none | none | 0,1 |
| b05 | `^(?:\1\|(?=(c)))*+$` | `c` | err | 0,1 | none | none | err | none | err | none | 0,1 |
| b06 | `^(?:(?:\1\|(?=(c)))*)*$` | `c` | err | 0,1 | none | none | none | none | none | none | 0,1 |
| b07 | `^(?:\1\|(?=(c))){2,}$` | `c` | err | 0,1 | 0,1 | 0,1 | none | none | 0,1 | 0,1 | 0,1 |
| b08 | `^(?:\1\|(?=(c)))+$` | `c` | err | 0,1 | none | none | none | none | none | 0,1 | 0,1 |
| u01 | `^(?:(c)\|())*$` | `c` | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 |
| u02 | `(?:(b\|))*` | `bba` | 0,2 | 0,2 | 0,2 | 0,2 | 0,2 | 0,2 | 0,2 | 0,2 | 0,2 |
| u03 | `^(?:(?=(c))\|c)*$` | `c` | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 | 0,1 |

Over the 23 `k` and `b` rows (k16 and the `u` rows are controls, where every engine that runs them
agrees):

| engine | rows it runs | same answer as upstream | different |
|---|---|---|---|
| upstream `regex` | 23 | 23 | 0 |
| the port | 23 | 23 | 0 |
| Onigmo (Ruby) | 23 | 18 | 5 (b02-b06: a group set inside a lookahead does not count) |
| PCRE2 | 23 | 3 | 20 |
| Perl | 23 | 2 | 21 |
| .NET | 21 | 2 | 19 |
| Python `re` | 16 | 4 | 12 |
| Java | 8 | 1 | 7 |
| Node | 7 | 1 | 6 (rule B, and it clears a repeated atom's groups on every pass) |

So two engines count the change as progress and five do not. A head count is not the answer,
though: the position-only engines do not agree with each other (PCRE2 matches k05 and Perl does
not; `re` matches k03, k10 and k14 and PCRE2 does not), and they disagree with themselves across
quantifiers.

**The principle.** Every engine's empty-iteration check exists to stop an infinite loop (perlre
"Repeated Patterns Matching a Zero-length Substring"; ECMA-262 22.2.2.3.1 Note 4; pcre2pattern). A
termination check is exact when it drops only paths that cannot lead anywhere new. After an
empty pass that changed nothing the rest of the pattern can observe, the next pass starts from the
same position with the same tested groups, so it can only repeat itself, and dropping it loses
nothing. After an empty pass that changed a tested group, the next pass starts from a different
state and can match something new: in k01 the second pass can read the `c` that the first could
not. Dropping it loses a real match.

The check also has to respect a basic law of repetition: whatever `X{2,}`, `X+` or `X{0,5}`
matches, `X*` matches too, since `*` allows every count those allow. Position-only checking breaks
that law. Over `c`, Perl, PCRE2 and .NET match `^(?:(?(1)c|z)|()){2,}$` (k04) and
`^(?:\1|(?=(c))){2,}$` (b07) but not the same bodies under `*` (k01, b03); PCRE2 matches the
`{0,5}` form (k05) but not `*`; `re` matches `+` (k03) but not `*`. Upstream and the port give the
same answer for every quantifier on every row.

Perl documents its behaviour: `(?: NON_ZERO_LENGTH | ZERO_LENGTH )*` "is made equivalent to"
`(?: NON_ZERO_LENGTH )* (?: ZERO_LENGTH )?`. That equivalence is exactly what loses k01's match,
because the zero-length branch is what makes the non-zero-length branch possible. So Perl's answer
is a deliberate design, but one that trades correctness for simplicity; .NET's "never repeat after
an empty match when the minimum number of captures has been found" is the same trade.

**The decision.** Keep upstream's rule: in exact matching, an empty iteration that changed the
span of a tested group is progress, and the repeat may go round again. It is the rule that loses
no match through its termination check, keeps `X*` a superset of `X{n,}`, and is also the rule the
fuzzy "needed" recommendation above builds on (its part (c)). The port already implements it
(`src/FuzzyRegex/Engine/Matcher.cs:7606`, greedy, and `:7817`, lazy), so D12 needs no engine
change. The port differs from Perl, PCRE2, .NET and `re` on these patterns and says so in
`docs/COMPARISON.md`; it does not differ from upstream, so there is no ledger or DIVERGENCES entry.

Pinned by `tests/FuzzyRegex.Tests/Gaps/Engine/EmptyIterationGroupProgressTests.cs` (the k and b
rows, the `*`-superset law and the controls). Witness: with the group half removed from the greedy
check (`bool changed = state.TextPos != rpData.Start;` at `:7606`) 17 of its 27 cases fail; with it
removed from the lazy check (`:7817`) 4 fail (the `*?` rows and the law test); the controls pass
under both. Four rows pass under both mutants, so they pin the answer but would not catch a
position-only engine: the `+` row, the `+?` row, the `^(?:(?:(?(1)c|z)|())+)*$` row and the
`^(?:|(?=(c)))+$` row (blind review, 2026-09-28).

**What the rule still lacks: termination when a group cycles (D17).** Upstream's rule counts any
span change, so a pass that flips a tested group between two spans at one position is progress
for ever. `^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$` over `ab` sets `g` to (0,1), then (0,2),
then (0,1) again, and so on. Upstream raises MemoryError after 1.4 s; the port exhausts its 1 GB
backtrack stack after 9 s; PCRE2 (with `(?J)`) and Perl answer no match at once, which is right,
since no pass reads text and `$` cannot hold at 0. This is the lookaround case part (c) of the
"needed" rule predicted above. The right rule is the one the reference matcher already uses: a
tested group state already seen in the current run of empty iterations at this position is not a
change. It is a separate defect (D17 in `docs/KNOWN-DEFECTS.md`, red test in `OpenDefectTests`),
because it needs a per-run record of group states, which is the same machinery as the fuzzy repeat
memo and should be designed with it.

## Reproducing

- `python tools/probes/empty-iteration-survey/run_all.py`: the exact battery on every engine, and
  the fuzzy battery on `regex` and TRE (TRE, glibc and Ruby in WSL Ubuntu 24.04).
- `python tools/probes/empty-iteration-survey/needed_sweeps.py [s1 s2 s3 s4 s5 s4cap rand modes counts tre]`:
  the sweeps, the rule comparison, the count comparison (also written to `needed_counts.tsv`, not committed) and
  the TRE comparison (`tre_batch.c`, built in WSL).
- `python tools/probes/empty-iteration-survey/capture_progress.py`: the D12 table. Set `PORT_PROBE` to the
  path of `port-probe.cs` (`git show 8dda4d9:tools/probes/port-probe.cs`) to add the port's column.
- The reference matcher's modes: set `EMPTY_DELETION_ITERATIONS` to `"perl"` (the default, rule A),
  `"reject"` (C), `"minimum"` (B), `"needed"` (N) or `"unrestricted"`.

Engine versions: Python 3.14.7 `re`; `regex` 2026.9.10; PCRE2 10.47 (pip `pcre2` 0.7.1); RE2 via
`google-re2` 1.1.20251105; Perl v5.42.3; Node v24.16.0; OpenJDK 26.0.2.1; .NET 10.0.12; glibc
2.39; TRE 0.8.0; Ruby 3.2.3 (Onigmo).
