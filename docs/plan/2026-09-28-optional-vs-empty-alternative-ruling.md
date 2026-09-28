# `X?` against `(?:X|)` under fuzzy matching: ruling

Written 2026-09-28 for the F-A branch (`maint/fuzzy-exact-deletion`, 54b1358). Every result below
comes from a run made that day: F-A and main (8dda4d9, from `git archive`) through the port probe,
upstream `regex` 2026.9.10 on Python 3.14.7, and the other engines named in each table. The survey
behind ledger entry 44 (`2026-09-26-empty-iteration-survey.md`) is not repeated here.

## Summary

**Ruling: option A.** A written empty alternative is the exit of an optional, the same as the zero
iterations of `X?`. So ledger 44's "needed" rule covers it: a pass through a non-empty alternative
that consumed no text and spent errors fails unless something needs those errors (a section
minimum or a tested group), and the empty alternative then matches with none. After the fix,
`(?:a|){d<=1}` over `''` gives 0 errors, the same as `(?:a?){d<=1}`. Choices between two non-empty
alternatives stay first-match, so the README's `(?:cats|cat){e<=1}` example and upstream's two
tests of it keep their answer.

Why:

- **The identity is universal.** `X?` and `(?:X|)` give the same answer (1,200 cases) in Python
  `re`, PCRE2, Perl, .NET, upstream's exact mode and the port's exact mode. They also agree in
  upstream's fuzzy modes, plain, `(?e)` and `(?b)` (26,880 pairs). F-A is the only engine measured
  that breaks it: 600 pairs, 28 of them with a different span.
- **Option B would move the break, not remove it.** Narrowing ledger 44 so that `a?` gives upstream's
  one deletion would break the equally basic identities `X{0,2}` = `(?:XX?)?` and `X*` = `(?:XX*)?`.
  It would also bring back the unneeded deletion that ledger 44 removed, now for `\d?`.
- **Option A is the rule the owner already approved.** Ledger 44 fails an iteration whose errors
  nothing needs, because the repeat's exit reaches the same place with fewer errors. An empty
  alternative is that same exit, spelt differently.

## 1. The finding, and how far it reaches

On F-A, ledger 44's rule applies only to repeats, so the same pattern written two ways gives two
answers (search and fullmatch agree on every row):

| pattern | subject | F-A | main | upstream |
|---|---|---|---|---|
| `(?:a?){d<=1}` | `''` | (0,0) (0,0,0) | (0,0) (0,0,1) | (0,0) (0,0,1) |
| `(?:a\|){d<=1}` | `''` | (0,0) **(0,0,1)** | (0,0) (0,0,1) | (0,0) (0,0,1) |
| `(?:a??){d<=1}`, `(?:\|a){d<=1}` | `''` | (0,0) (0,0,0) | (0,0) (0,0,0) | (0,0) (0,0,0) |
| `(?:a?b){d<=1}` | `b` | (0,1) (0,0,0) | (0,1) (0,0,1) | (0,1) (0,0,1) |
| `(?:(?:a\|)b){d<=1}` | `b` | (0,1) **(0,0,1)** | (0,1) (0,0,1) | (0,1) (0,0,1) |
| `(?:ab?){d<=1}`, `(?:a(?:b\|)){d<=1}` | `b` | (0,1) (0,0,1) | same | same |
| `(?:(?:a\|b)?){d<=1}` | `b` | **(0,1)** (0,0,0) | (0,0) (0,0,1) | (0,0) (0,0,1) |
| `(?:a\|b\|){d<=1}` | `b` | (0,0) (0,0,1) | (0,0) (0,0,1) | (0,0) (0,0,1) |
| `(?e)(?:(?:a\|b)?){d<=1}` | `b` | **(0,1)** (0,0,0) | (0,0) (0,0,0) | (0,0) (0,0,0) |
| `(?e)(?:a\|b\|){d<=1}` | `b` | (0,0) (0,0,0) | (0,0) (0,0,0) | (0,0) (0,0,0) |
| `(?:a?){e<=1}` and `(?:a\|){e<=1}` | `''` | 0 and **1** deletions | 1 and 1 | 1 and 1 |
| `(?:a?){s<=1}`, `(?:a\|){s<=1}` | `''` | 0 | 0 | 0 |
| `(?:a?){e<=1}`, `(?:a\|){e<=1}` | `b` | (0,1) (1,0,0) | same | same |

The `{s<=1}` and `{i<=1}` forms never diverge: a substitution or insertion consumes text, so the
pass is not empty. BESTMATCH gives 0 for every form in all three builds. The lazy forms `a??` and
`(?:|a)` already agree, because both try the empty way first.

**The sweep.** Seven bodies X (`a`, `ab`, `[ab]`, `(a)`, `a+`, `a|b`, `ab|c`) were written three
ways, each pair against its alternation spelling:

- greedy: `(?:X)?` against `(?:X|)`;
- lazy: `(?:X)??` against `(?:|X)`;
- middle: `(?:(?:X)?|c)` against `(?:X||c)`.

Each pair went into four contexts (alone, followed by `b`, `x…b`, and doubled), under eight
constraints (`d<=1`, `d<=2`, `e<=1`, `e<=2`, `s<=1`, `i<=1`, `1<=e<=2`, `2i+2d+1s<=2`) and three
modes (plain, `(?e)`, `(?b)`). Every combination ran over ten subjects with search and fullmatch:
80,640 rows, 26,880 pairs per engine.

| engine | pairs that differ |
|---|---|
| upstream 2026.9.10 | 0 |
| main 8dda4d9 | 0 |
| F-A with `UpstreamEmptyIterations` set (reflection) | 0 |
| F-A | 600: 572 counts only (the greedy spelling cheaper), 15 spans in plain mode, 13 spans in `(?e)`; 0 in `(?b)` and 0 in the lazy pairs |

Setting the switch removes every difference, so ledger 44 is the only cause. The differing pairs
fall under every constraint except `s<=1` and `i<=1`. Under `1<=e<=2` the needed rule works as
designed: `(?:(?:a)?(?:a)?){1<=e<=2}` over `''` takes one deletion, the minimum, where the
alternation spelling takes two.

## 2. Principles

**Is `X?` equivalent to `(?:X|)`?** Yes, by definition. A regular-expression textbook defines `X?`
as X or the empty string. Thompson's construction builds it as a choice between X and an empty
path. A backtracking engine tries X first and the empty path second, which is the order of
`(?:X|)`. `X??` reverses that order, as `(?:|X)` does. Every engine measured keeps the identity in
exact matching:

| engine | version | cases | differ |
|---|---|---|---|
| Python `re` | 3.14.7 | 1,200 | 0 |
| PCRE2 (python `pcre2` 0.7.1) | 10.47 | 1,200 | 0 |
| Perl | 5.42.3 | 1,200 | 0 |
| .NET `System.Text.RegularExpressions` | 10.0.12 | 1,200 | 0 |
| `regex`, exact | 2026.9.10 | 1,200 | 0 |
| this port, exact (F-A) | 54b1358 | 1,200 | 0 |
| ECMAScript (V8) | Node 24.16.0 | 1,200 | 48, all captures only |

The battery covered bodies `a`, `ab`, `(a)`, `(a*)`, `a*`, `(a)|b`, `(a|b)`, `(?:a|(b))`, `(a?)` and
`(a)(b)?`. It used the greedy, lazy and middle spellings, four contexts and ten subjects, and
compared the span and every group. The only exception is JavaScript, and it proves the point. All
48 of its differences are a group that X can match empty: `/(?:(a*))?/` over `b` leaves group 1
undefined, while `/(?:(a*)|)/` sets it to `''`. The span never differs. The cause is ECMA-262's
empty-iteration rule (22.2.2.3.1 RepeatMatcher step 2.b), which rejects an empty iteration beyond
the minimum. So when an empty-iteration rule applies only to repeats, this identity is the thing it
breaks. That is the F-A defect.

A fuzzy engine does not have to preserve every identity of exact matching: first-match fuzzy
answers depend on alternative order, as `cats|cat` against `cat|cats` shows. But this identity
gives one construct two spellings, and users, generators and optimisers move freely between them.
If the error count or the span depends on the spelling, it is a defect unless a principle demands
it. None does here. Upstream keeps the identity, and so does TRE (section 3).

**What error count is right, outside BESTMATCH, for a part that can match empty with no errors?**
Two answers are possible:

1. **First found in backtracking order**, which is upstream's answer: `(?:a|){d<=1}` over `''`
   tries `a`, deletes it, and succeeds with one deletion.
2. **Do not charge errors that an error-free path to the same place avoids.** Deleting `a` leaves
   the matcher at the same position, with the same groups, as the empty alternative. The only
   difference is one more error. That path is dominated.

Ledger 44 chose answer 2 for repeats, and the owner approved it on 2026-09-26. Its grounds were
that an iteration which consumed nothing and spent errors that nothing needs is dominated by the
repeat's exit, and that dropping it never loses a match, because the exit is always there. An
empty alternative after X is that exit, spelt as an alternative. Every step of the argument
carries over:

- the exit is always there (it matches empty with no condition);
- the dominated path ends in the same state apart from errors, or is kept when it is not, because
  the rule keeps passes that change a tested group;
- the needed exceptions (a section minimum, a tested group) apply unchanged.

So answer 2 is what the port already decided. The only question left is the construct it covers.

Where the rule stops matters as much. Answer 2 rests on dominance by an empty exit. It gives no
licence to prefer one non-empty alternative over another because it spends fewer errors: that is
option M (minimum cost), which the owner rejected for the default mode. So these stay first-match,
as in upstream:

- `(?:cats|cat){e<=1}` over `cat` has one deletion. The README pins it (`upstream/README.rst:609`),
  as do `upstream/regex/tests/test_regex.py:2784` and `:3380`.
- `(?:a|b){d<=1}` over `b` gives (0,0) with one deletion, because branch `a` is deleted before
  branch `b` is tried.
- `(?:c|a?){d<=1}` over `''` has one deletion: `c` is deleted before `a?` is tried, and `a?` is
  a nullable alternative, not an empty one.

The line is short to state: **an optional part never spends errors to match nothing unless
something needs them; a choice between non-empty alternatives is first-match.** "Optional part"
means `X?`, `X??`, `X{0,n}`, `X*`, or an alternative written empty.

## 3. Survey of fuzzy engines

| engine | how run | `a?` against `(a\|)`, and the other pairs | answer |
|---|---|---|---|
| `regex` 2026.9.10, plain | Python | 26,880 pairs, 0 differ | 1 deletion for both greedy forms over `''` (first found) |
| `regex`, ENHANCEMATCH `(?e)` | Python | 0 differ | 0 for both |
| `regex`, BESTMATCH `(?b)` | Python | 0 differ | 0 for both |
| TRE 0.8.0 (`libtre5` 0.8.0-7) | `tre_regaexec` from C in WSL (probe below) | 105 pairs under `d`, `e` and `s` budgets: equal cost in every pair; 3 ties differ in edit kind | 0 for `a?`, `(a\|)`, `a??`, `(\|a)`, `(a\|b)?` and `(a\|b\|)` over `''` and `b`: minimum cost |
| tre-agrep 0.8.0 (the agrep interface) | WSL CLI, `-D1 -I9 -S9 -E1` | `a?b` and `(a\|)b` over `b` both match | same as TRE |
| ugrep 7.8.5 `-Z` | `winget install Genivia.ugrep` | rejects an empty alternative (`x(a\|)`: "error at position 8"). With the empty alternative written `b{0}`, `x(a\|b{0})` and `xa?` give cost 0 over `x`, `xy` and `xb` under `-Z-1`, `-Z1` and `-Z~1` | 0 for both |
| fuzzysearch 0.8.1 | `pip install fuzzysearch` | literal patterns only; `a?` is two literal characters | not applicable |
| F-A | port probe | 600 of 26,880 differ | 0 for `a?`, 1 for `(?:a\|)` |

TRE documents a minimum-cost search. Its README (the `libtre-dev` copy in WSL; upstream
<https://github.com/laurikari/tre>) says: "TRE can also be used to search for matches with the
lowest cost." The page `laurikari.net/tre/documentation/regaexec/` returned HTTP 500 on
2026-09-28. Every engine that minimises cost gives 0
for both spellings. Upstream's plain mode gives 1 for both. Only F-A mixes the two answers, one per
spelling.

These installs failed:

- `sudo -n apt-get install ugrep agrep` in WSL: "sudo: a password is required". The original
  Wu-Manber agrep was not run; tre-agrep stands in for it.
- `pip download tre` (pyTRE): "Failed to build 'tre' when getting requirements to build wheel".
  The C library was used instead.

## 4. The options

| option | answer for `(?:a\|){d<=1}` over `''`, and for `a?` | identities kept (out of the six below) | cost |
|---|---|---|---|
| F-A today | 1 and 0 | I2, I4, I5 | - |
| **A: an empty alternative is an optional's exit** | 0 and 0 | I1, I2, I3, I4 (I5 and I6 as below) | see section 5 |
| A, also for empty alternatives the compiler makes when it factors | 0 and 0 | all six, but the README's `cats\|cat` becomes 0 and two upstream tests fail, and the answer then depends on whether the optimiser factored (`cats\|cat\|dog` is not factored and stays 1) | rejected |
| B: narrow ledger 44 so `{0,1}` gives upstream's answer | 1 and 1 | I1, I5, I6 | brings back the unneeded deletion for `\d?`: `(?:[0-9]?){d<=1}` over `k` would have 1 error, which is ledger 44's `42kg` complaint |
| M: minimum cost everywhere | 0 and 0 | all | the owner rejected it for the default mode (survey, "The options") |

The identities, measured on F-A and upstream:

| id | identity | example (search) | upstream | F-A | under A |
|---|---|---|---|---|---|
| I1 | `X?` = `(?:X\|)` | `(?:a?){d<=1}` / `(?:a\|){d<=1}` over `''` | 1 / 1 | 0 / **1** | 0 / 0 |
| I2 | `X{0,2}` = `(?:XX?)?` | `(?:a{0,2}){d<=1}` / `(?:(?:aa?)?){d<=1}` over `''` | 1 / 1 | 0 / 0 | 0 / 0 |
| I3 | `X*` = `(?:XX*\|)` | `(?:a*){d<=1}` / `(?:aa*\|){d<=1}` over `''` | 1 / 1 | 0 / **1** | 0 / 0 |
| I4 | `X*` = `(?:XX*)?` | `(?:a*){d<=1}` / `(?:(?:aa*)?){d<=1}` over `''` | 1 / 1 | 0 / 0 | 0 / 0 |
| I5 | `XY\|X` = `X(?:Y\|)` | `(?:cats\|cat){e<=1}` / `(?:cat(?:s\|)){e<=1}` over `cat` | 1 / 1 | 1 / 1 | 1 / **0** |
| I6 | `XY\|X` = `XY?` | `(?:cats\|cat){e<=1}` / `(?:cats?){e<=1}` over `cat` | 1 / 1 | 1 / **0** | 1 / **0** |

Under B, I2 to I4 break (for example `a{0,2}` gives 0 while `(?:aa?)?` gives 1), and so does ledger
44 for every `{0,1}`. Under A, I5 and I6 break, but on the line section 2 draws: `cats|cat` chooses
between two non-empty alternatives, and `cat(?:s|)` and `cats?` have an optional `s`. I6 is already
broken on F-A today. A adds I5 and repairs I1 and I3. The identities A keeps are the ones that
change only how an optional is spelt: `?`, `{0,n}`, `*` and the empty alternative. The ones it
gives up rewrite a choice between non-empty alternatives. First-match semantics keeps those
distinct in any case: `cats|cat` and `cat|cats` already differ in upstream.

**Decision: A, for alternatives written empty in the pattern.** The mark has to be set where the
parser builds the alternation, before `Branch.Optimise`. Otherwise the compiler's own factoring
(`SplitCommonPrefix`, `src/FuzzyRegex/Parsing/Nodes.cs:1297` and `:1459`, which rewrites `cats|cat`
as `cat` followed by an alternation of `s` and an empty alternative) would carry the mark. That
would change the README answer and make the result depend on whether an optimisation fired. The
builder must check whether that factoring runs inside fuzzy sections. If it does, the mark must not
survive onto the empty alternative it creates. The mark must survive the optimisations that keep
the alternation's meaning (`FlattenBranches` at `:1438`, `ReduceToSet` at `:1652`): for example,
`(?:a|b|)` may become an alternation of `[ab]` and the empty alternative.

The needed conditions are ledger 44's, applied unchanged:

- the pass is admitted if its deletions raise an open section's unmet `d` or `e` minimum
  (`RaisesUnmetMinimum`);
- the pass is admitted if it changed a group that a backreference or conditional tests
  (`EmptyIterationAdmitted`, `src/FuzzyRegex/Engine/Matcher.cs:4716`).

Passes without errors are never touched, so exact matching is unchanged.

## 5. Performance

Measured on F-A and main in Release, best of 7 over a 200,000-character random text from the
alphabet `xyab` (seed 42), counting all matches. The upstream figures are the best of 3 with
`findall`.

| pattern | F-A | main | upstream |
|---|---|---|---|
| `(?:x(?:a\|)y){e<=1}` | 31.3 ms | 31.4 ms | 27.0 ms |
| `(?:x(?:a)?y){e<=1}` | 58.7 ms | 52.3 ms | 37.7 ms |
| `(?:x(?:a\|b\|)y){e<=1}` | 41.4 ms | 31.9 ms | 28.2 ms |
| `(?:x(?:a\|b)?y){e<=1}` | 65.7 ms | 55.3 ms | 40.4 ms |
| `x(?:a\|)y` (exact) | 5.5 ms | 7.5 ms | - |
| `x(?:a)?y` (exact) | 6.9 ms | 7.3 ms | - |

What this means for the build:

- **Do not implement A by rewriting `(?:X|)` into a `{0,1}` repeat.** The answers would be right,
  but a fuzzy repeat costs about 1.9 times a fuzzy alternation here (58.7 against 31.3 ms). The
  rewrite would put that cost on every fuzzy pattern that has an empty alternative.
- **Put the check where ledger 44 puts it, and nowhere else.** Ledger 44 has two parts:
  - an early exit at the deletion point, `Matcher.cs:5170-5181`, which skips pushing the deletion
    when the item is a repeat's whole body and the pass began here;
  - the general check at the repeat's end, `Matcher.cs:8404`, under `state.IsFuzzy`.

  The alternation needs the same two parts:
  - the early exit, for an item that is a whole alternative with a marked empty alternative after
    it (this covers `(?:a|)`, `(?:[0-9]|)` and `(?:a|b|)`);
  - a check where a non-empty alternative of a marked alternation joins the continuation (this
    covers multi-item alternatives such as `(?:ab|){d<=2}` and `(?:aa*|)`).

  Only alternations the compiler marked may run new code. The start position and change count
  that the check needs must be saved only for those alternations. BRANCH itself, unmarked
  alternations and exact matching must not change.
- **Performance gate:**
  - `(?:x(?:a|)y){e<=1}` and `(?:x(?:a|b|)y){e<=1}` stay within 10% of the F-A figures above
    (31.3 and 41.4 ms);
  - the exact rows do not move beyond noise;
  - the benchmark suite shows no regression on patterns without an empty alternative.
- **Not caused by this ruling:**
  - F-A is already 30% slower than main on `(?:x(?:a|b|)y){e<=1}` (41.4 against 31.9 ms);
  - F-A is 12 to 19% slower than main on the `?` forms.

  These come from ledgers 42 and 44. They should be checked on their own.

## 6. Tests

**Red tests.** Each row must fail on F-A today and pass after the fix. Expected values are in the
port's `(span) (s,i,d)` form. Every row holds for both search and fullmatch unless noted, and the
comment at the end is F-A's answer today.

```
search    (?:a|){d<=1}                    ''    (0,0) (0,0,0)            # F-A (0,0,1)
search    (?:a|){e<=1}                    ''    (0,0) (0,0,0)            # (0,0,1)
search    (?:(?:a|)b){d<=1}               'b'   (0,1) (0,0,0)            # (0,0,1)
search    (?:(?:a|)b){e<=1}               'b'   (0,1) (0,0,0)            # (0,0,1)
search    (?:x(?:a|){d<=1})               'x'   (0,1) (0,0,0)            # (0,0,1)
search    (?:a|){d<=1}b                   'b'   (0,1) (0,0,0)            # (0,0,1)
search    (?:a|b|){d<=1}                  'b'   (0,1) (0,0,0)            # (0,0) (0,0,1): span changes; search only (fullmatch is already (0,1) (0,0,0))
search    (?e)(?:a|b|){d<=1}              'b'   (0,1) (0,0,0)            # (0,0) (0,0,0): span changes; search only
search    (?:a||c){d<=1}                  ''    (0,0) (0,0,0)            # (0,0,1)
search    (?:[0-9]|){d<=1}                'k'   (0,0) (0,0,0)            # (0,0,1); search only (fullmatch is None in every spelling)
search    (?:a|){d<=1}(?:b|){d<=1}        ''    (0,0) (0,0,0)            # (0,0,2)
search    (?:(?:a|)(?:a|)){d<=2}          ''    (0,0) (0,0,0)            # (0,0,2)
search    (?:aa*|){d<=1}                  ''    (0,0) (0,0,0)            # (0,0,1)   multi-item alternative
search    (?:a(?:a|)|){d<=1}              ''    (0,0) (0,0,0)            # (0,0,1)   multi-item alternative
search    (?:(a)|){d<=1}                  ''    (0,0) (0,0,0) g1 unset   # (0,0,1) g1=(0,0)
search    (?:(?:a|)(?:a|)){1<=e<=2}       ''    (0,0) (0,0,1)            # (0,0,2): the minimum needs one, not two
search    (?:cat(?:s|)){e<=1}             'cat' (0,3) (0,0,0)            # (0,0,1)
```

**Witness rows.** These must keep their answer. The first group is the spelling each red row must
now equal. The second group sits outside the rule.

```
# the forms the red rows must equal (already these values on F-A)
search    (?:a?){d<=1}  (?:a{0,1}){d<=1}  (?:a??){d<=1}  (?:|a){d<=1}      ''    (0,0) (0,0,0)
search    (?:a?){e<=1}                    ''    (0,0) (0,0,0)
search    (?:a?b){d<=1}  (?:(?:|a)b){d<=1} 'b'  (0,1) (0,0,0)
search    (?:(?:a|b)?){d<=1}              'b'   (0,1) (0,0,0)
search    (?e)(?:(?:a|b)?){d<=1}          'b'   (0,1) (0,0,0)
search    (?:(?:a)?|c){d<=1}              ''    (0,0) (0,0,0)
search    (?:(a?)){d<=1}                  ''    (0,0) (0,0,0) g1=(0,0)
search    (?:(?:(a))?){d<=1}              ''    (0,0) (0,0,0) g1 unset
search    (?:cats?){e<=1}                 'cat' (0,3) (0,0,0)
# needed by a minimum or a tested group: the error stays, in both spellings
search    (?:a|){1<=d<=1}  (?:a?){1<=d<=1} ''   (0,0) (0,0,1)
search    (?:(a)|){d<=1}(?(1)x|y)         'x'   (0,1) (0,0,1) g1=(0,0)
search    (?:(?:(a))?){d<=1}(?(1)x|y)     'x'   (0,1) (0,0,1) g1=(0,0)
# outside the rule: no empty alternative, or the pass consumed text
search    (?:cats|cat){e<=1}              'cat' (0,3) (0,0,1)            # README :609; test_regex.py:2784, :3380
fullmatch (?:cats|cat){e<=1}              'cat' (0,3) (0,0,1)
search    (?e)(?:cats|cat){e<=1}          'cat' (0,3) (0,0,0)
search    (?:a|b){d<=1}                   'b'   (0,0) (0,0,1)            # first-match between non-empty alternatives
search    (?:c|a?){d<=1}                  ''    (0,0) (0,0,1)            # a? is nullable, not an empty alternative
search    (?:a|(?=x)){d<=1}  (?:(?=x)|a){d<=1} '' (0,0) (0,0,1)          # a lookaround is not an empty alternative
search    (?:a|){e<=1}  (?:a?){e<=1}      'b'   (0,1) (1,0,0)            # substitution consumes text
search    (?:a|){s<=1}  (?:a?){s<=1}      'x'   (0,1) (1,0,0)
search    (?:a|){s<=1}  (?:a|){i<=1}      ''    (0,0) (0,0,0)
search    (?b)(?:a|){d<=1}  (?b)(?:a?){d<=1} '' (0,0) (0,0,0)
search    (?:a|)                          ''    (0,0) (0,0,0)            # exact matching untouched
```

**Equivalence property test.** Regenerate the sweep from section 1: seven bodies, the greedy, lazy
and middle pairs, four contexts, eight constraints, three modes, ten subjects, search and
fullmatch. Assert that the two spellings of every pair give the same span, counts and group 1.
Expect 0 of 26,880 to differ, where F-A today has 600. Hold this pair invariant rather than
upstream's answers: for `(?e)` and `(?b)` the builder's measured value is whatever the `?` spelling
gives.

**Oracle.** Upstream answers 1 deletion for the red rows. The oracle entry
`fuzzy-empty-iteration-needed-rule` (`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs:3222`)
claims a row when its ablation, `UpstreamEmptyIterations` plus `SkipExactDeletionRetry`
(`OracleComparer.cs:660`), reproduces upstream. So the switch must also turn off the
empty-alternative rule, or the oracle will report these rows as unclaimed divergences. Record the
change as an amendment to ledger 44 in `docs/DIVERGENCES.md:78`, with I1 and the red rows as its
examples, and pin it in `Gaps/Engine/FuzzyNeededEmptyIterationTests.cs`.

## 7. Unsettled

- **I5 and I6.** Under A, `(?:cats|cat){e<=1}` (1 deletion) and `(?:cat(?:s|)){e<=1}` (0) differ.
  The ruling accepts this on the first-match line of section 2. Removing it would take option M or
  a change to the README's documented semantics, and either is the owner's call.
- **Nullable alternatives that are not empty.** `(?:c|a?)`, `(?:a|b*)` and `(?:a|(?=x))` stay
  first-match. The dominance argument would reach them only if the matcher tried later
  alternatives before a deletion, and that is option M for alternation.
- **Where factoring runs.** Whether `SplitCommonPrefix` fires inside fuzzy sections was not
  confirmed. The builder must check it before choosing where to set the mark (section 4).
- **The `(?b)` answer for `(?b)(?:a|b|){d<=1}` over `b`.** Today every build gives (0,0) (0,0,0).
  Once the first match found becomes (0,1) (0,0,0), BESTMATCH may report either span. The builder
  must measure it and make the `?` spelling agree.

## Reproducing

The probes are in the worktree's `.scratch/ovea/` and are not committed:

- `rows.jsonl`, `sweep.jsonl`, `ident*.jsonl`: the rows;
- `fa/`, `mp/`, `fu/`: the port probe against F-A, against main from `git archive`, and against F-A
  with the switch set by reflection;
- `up.py`: upstream;
- `swan.py`, `cmp.py`: the comparisons;
- `exact_py.py`, `exact.js`, `exact.pl`, and `exact-net.cs` (run from outside the repo, because the
  repo's analysers reject the script): the exact-matching battery;
- `tre_ovea.c`: built with `gcc tre_ovea.c -ltre` in WSL;
- `perf/`, `perfm/`: the timings.
