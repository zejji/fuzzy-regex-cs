# Three open semantic questions from the complete matrix (for owner review)

Written 2026-09-30, revised the same day after the blind review of the answer key and a Boost
survey. The complete-matrix answer key (`matrix/answer-key`, 3e255f4e) found three places where
reputable engines disagree. For each one this note gives:

- the plain background and a worked example;
- measured answers from six engines;
- what the engines' documentation says;
- the options, with their pros and cons;
- a recommendation derived from regex principles. It is not decided by counting engines, since the
  owner asked for the most correct approach.

Every result was measured on this machine on 2026-09-30, not recalled:

| Engine | Version |
|---|---|
| upstream, python `regex` | 2026.9.10 |
| PCRE2, via pip `pcre2` | 10.47 |
| Perl, cygwin | 5.42.3 |
| Boost.Regex, standalone headers, g++ 13 in WSL, perl syntax | git a640597, 2026-05-22 |
| .NET 10 BCL `Regex` | .NET 10 |
| this port | main 3def5eba |

The documentation quotes come from PCRE2 10.47's `pcre2pattern`, from perlre (Perl's `blead`
source) and from upstream's `README.rst`. "No match" means the search found nothing. A dash means
the engine has no syntax for the construct.

Corrections to the answer key that this note records:

- **OPEN-1 is not new.** The owner already ruled on it on 2026-09-26, in ledger entry 47
  (`docs/plan/DECISIONS.md`, line 1585 onwards).
- **Perl is an inconsistent witness for OPEN-1.** It scopes the verb to the call in one form and
  not in another (the blind review's finding, re-measured here).
- **The key misdescribed Perl in OPEN-3.** Perl numbers the groups by position, but its name lookup
  still gives the text the name labels.

---

## OPEN-1. A backtracking verb inside a called group

### Background

`(*PRUNE)` is an instruction written inside a pattern. perlre defines it like this: once matching
has passed it, "should B not match, then no further backtracking will take place, and the pattern
will fail outright at the current starting position". In short: if the engine ever backtracks past
this point, give up on this starting position. `(*SKIP)` does the same and also moves the next
starting position forward.

A call such as `(?&g)` runs the pattern of group `g` at the place of the call, much like calling a
function. The question is how far "give up" reaches when the verb sits inside the called group:

- **Model A:** the whole attempt at this starting position, exactly as if the group's text were
  written in place of the call;
- **Model B:** only the call. The call then fails, and the rest of the pattern can still try other
  routes. The call is a sealed unit.

### Measured

The first row is the key example. The group is called, the verb fails the call, and the question is
whether the outer alternative `ac` still gets tried.

| Pattern | Subject | Upstream | PCRE2 | Perl | Boost | Port |
|---|---|---|---|---|---|---|
| `(?:(?&g)c\|ac)(?(DEFINE)(?<g>a(*PRUNE)b))` | 'ac' | no match | (0,2) | no match | no match | no match |
| the same with `(*SKIP)` | 'ac' | no match | (0,2) | no match | no match | no match |
| written out: `(?:(?:a(*PRUNE)b)c\|ac)` | 'ac' | no match | no match | no match | no match | no match |
| `(?:(?&g)x\|abc)(?<g>a(*PRUNE)b)?` (group called and also used in place) | 'abc' | no match | (0,3) | **(0,3)** | no match | no match |
| written out: `(?:(?:a(*PRUNE)b)x\|abc)(?<g>a(*PRUNE)b)?` | 'abc' | no match | no match | no match | no match | - |
| `(a(*PRUNE)b)?(?:(?1)x\|abc)` | 'abc' | no match | no match | **(0,3)** | no match | - |
| `(?:(?>(?&g))c\|ac)(?(DEFINE)(?<g>a(*PRUNE)b))` (call inside an atomic group) | 'ac' | (0,2) | (0,2) | no match | no match | - |
| `(?:(?&g)c\|ac)(?(DEFINE)(?<g>a(*ACCEPT)b))` | 'ac' | - | - | - | (0,2) | - |

What this shows:

- **Upstream and Boost** follow Model A on every row.
- **PCRE2** follows Model B on every row.
- **Perl** follows Model A in the `(?(DEFINE)...)` form but Model B in row 4, disagreeing with its
  own written-out form in row 5. In row 6 it matches even though the verb is reached by backtracking
  out of an ordinary group, not through a call. Its own definition of `(*PRUNE)` forbids that, so
  row 6 looks like a Perl defect. Perl is therefore weak evidence either way.
- **Row 7:** Model A, together with the owner's 2026-09-26 ruling that a verb reaches past an
  unfinished atomic group, gives no match, as Perl and Boost do. Upstream gets (0,2) by confining
  the verb to the atomic group (ledger 47), and PCRE2 by confining it to the call.
- **Row 8:** Boost scopes `(*ACCEPT)`, a verb that ends matching successfully, to the call. It still
  lets the failure verbs reach the whole attempt. That is exactly the split perlre documents (next
  section). Upstream and the port do not support `(*ACCEPT)`.

.NET's BCL has neither verbs nor calls.

### What the engines document

- **PCRE2** ("Backtracking verbs in subroutines"): "(\*COMMIT), (\*SKIP), and (\*PRUNE) cause the
  subroutine match to fail when triggered by being backtracked to in a group called as a
  subroutine. There is then a backtrack at the outer level." It adds: "Perl's treatment of the
  other verbs in subroutines is different in some cases."
- **Perl** defines `(*PRUNE)` against the attempt ("fail outright at the current starting
  position"), and scopes only `(*ACCEPT)` to the innermost call: "When inside of a nested pattern,
  such as recursion ... only the innermost pattern is ended immediately."
- **Upstream** does not document verbs in calls.

### Deriving the answer

- **What a failure verb talks about.** `(*PRUNE)` and `(*SKIP)` make statements about the *starting
  position*: "do not look for a match starting here" and "do not start again before this point".
  A starting position belongs to the match attempt, not to a subpattern. A call does not start a new
  attempt; it runs a group's text at a point inside the current one. So a statement about the
  starting position means the same inside a call as anywhere else. That is Model A.
- **Why `(*ACCEPT)` is scoped differently.** `(*ACCEPT)` says "this pattern has succeeded here".
  Inside a call, "this pattern" is naturally the called group, which is why Perl and Boost scope it
  to the call. The split is principled, not arbitrary: success verbs speak about a pattern, while
  failure verbs speak about the attempt.
- **The case for Model B, and why it is weaker.** PCRE2 treats a call like a function whose
  internal control flow cannot escape. That is a coherent design. But it redefines what the verbs
  mean inside a call, and PCRE2 has to state it as a special rule. Under Model A no special rule is
  needed. Model A also keeps a property the port relies on throughout, adopted for D40: a call
  answers as the group written out in its place, with the group's own flags (the blind review
  confirmed that flags come from where the group is defined).

### Options

- **(a) Model A: the verb ends the whole attempt.**
  - For: follows from what the verbs are defined to mean.
  - For: upstream and Boost follow it consistently, and it matches perlre's definitions.
  - For: it keeps written-out equivalence, and it is the owner's 2026-09-26 ruling. No change is
    needed.
  - Against: PCRE2 documents the opposite, and Perl sometimes does the opposite.
- **(b) Model B: the verb ends only the call.**
  - For: a coherent "sealed unit" design, documented by PCRE2.
  - Against: it needs a special rule for calls, breaks written-out equivalence, and reverses a
    ruling. Only PCRE2 follows it consistently.

### Recommendation: (a), the most correct, which is also the existing ruling

---

## OPEN-2. Captures made inside a conditional's negative test

### Background

A conditional `(?(test)yes|no)` runs the test first, then picks a branch. The test can be a
lookahead:

- `(?(?=X)yes|no)` takes `yes` when X matches here, and `no` otherwise;
- `(?(?!X)yes|no)` takes `yes` when X does not match here, and `no` when it does.

While X is being matched, it may capture groups. The disputed case is the negative test that fails:
X matched, so the `no` branch runs. Are X's captures still set?

### Measured (subject 'ab')

| Pattern | Upstream | PCRE2 | Perl | Boost | .NET BCL | Port |
|---|---|---|---|---|---|---|
| negative: `(?(?!(a))x\|\1b)` | no match | (0,2) | (0,2), 1='a' | (0,2), 1='a' | no match | no match |
| positive: `(?(?=(a))\1b\|x)` | (0,2), 1='a' | (0,2) | (0,2), 1='a' | (0,2), 1='a' | no match | (0,2), 1='a' |
| plain lookahead: `(?=(a))\1b` | - | - | - | (0,2), 1='a' | (0,2), 1='a' | - |

The rule decides whether a match exists at all, not only what a group reports. The two conditionals
ask the same question, "is `(a)` here?", with the branches swapped.

### What the engines document

- **PCRE2** ("Assertions"): "For a negative assertion, a matching branch means that the assertion
  is not true. If such an assertion is being used as a condition in a conditional group, captured
  substrings are retained, because matching continues with the 'no' branch of the condition. For
  other failing negative assertions, control passes to the previous backtracking point, thus
  discarding any captured strings within the assertion."
- **Perl and Boost** do not document the case; both keep the captures when measured.
- **.NET** drops a conditional test's captures in both forms, although its plain lookahead keeps
  them (last row). Within .NET's own rules, the conditional is therefore a special case.
- **Upstream's** README documents lookaround tests in conditionals (Hg issue 163) but says nothing
  about their captures.

### Deriving the answer

- **Captures are undone by backtracking, and only by backtracking.** Every engine here works this
  way, and it is why a standalone failing negative lookahead leaves no captures: its failure makes
  the engine backtrack.
- **A failing negative test inside a conditional causes no backtracking.** The engine carries on
  into the `no` branch. So nothing undoes what X captured, and the captures stand. PCRE2 gives
  exactly this reasoning.
- **Negation with swapped branches cannot change the answer.** `(?(?!X)A|B)` and `(?(?=X)B|A)` must
  agree, captures included. Upstream's mix breaks this. .NET's "never capture in a test" keeps it,
  but only by making conditionals an exception to .NET's own lookahead rule.

### Options

- **(a) Keep the captures, as PCRE2, Perl and Boost do.**
  - For: follows from the two principles above, is documented by PCRE2, and three engines agree.
  - Against: a divergence from upstream, to be pinned with a draft upstream report.
- **(b) Drop them in both forms, as .NET does.**
  - For: consistent within itself.
  - Against: it changes the positive form, where upstream, PCRE2, Perl and Boost all agree, and it
    makes conditionals an exception to how lookaheads capture.
- **(c) Keep upstream's mix.**
  - Against: two spellings of the same question give different answers.

### Recommendation: (a), the most correct. It needs a fix and a new register row

---

## OPEN-3. A branch reset in which one name sits at different positions

### Background

In a branch reset `(?|A|B)`, each alternative numbers its groups from the same starting number, so
the first group of A and the first group of B are both group 1. Names are labels on groups. When a
name sits first in one alternative and second in another, four reasonable rules cannot all hold:

- **R1.** Two groups in the same alternative never share a number. If they did, one capture would
  overwrite the other and be lost.
- **R2.** A name identifies its group, and looking it up gives the text that group matched.
- **R3.** Each alternative numbers its groups by position, from the same start. This is what "branch
  reset" literally promises.
- **R4.** One name, one number. This is PCRE2's stated invariant, upstream's documented rule, and
  the shape of .NET's API, which gives each name exactly one number.

On 2026-09-22 (S82) the owner chose upstream maintainer's "option 3": a group never takes a number
another group in the same alternative will use. It is shipped and pinned in `docs/DIVERGENCES.md`
(line 69).

### Measured

| Pattern | Subject | Upstream | PCRE2 | Perl | Boost | Port (option 3) |
|---|---|---|---|---|---|---|
| `(?\|(?P<bug>xxx)(!)\|(!)(?P<bug>BUG))` | '!BUG' | 1='BUG' (= bug), 2 unset: the '!' is lost | refuses to compile | 1='!', 2='BUG', bug='BUG' | 1='!', 2='BUG' | 1='BUG' (= bug), 2='!' |
| the same followed by `-\1` | '!BUG-!' | no match | refuses | (0,6) | - | no match |
| the same followed by `-\1` | '!BUG-BUG' | (0,8) | refuses | no match | - | (0,8) |
| `(?\|(?P<a>x)\|(?P<b>y))` | 'y' | a=1, b=2; 'y' is group 2 | refuses | a and b both name group 1 | - | a=1, b=2; 'y' is group 2 |

PCRE2's error is "two named subpatterns have the same name (PCRE2_DUPNAMES not set)".

Upstream's README documents its rule and gives an example of this very case, from which its own
defect follows: "Groups with the same group name will have the same group number, and groups with a
different group name will have a different group number." For
`(\s+)(?|(?P<foo>[A-Z]+)|(\w+) (?P<foo>[0-9]+))` it says `(\w+)` "is group 2 because of the branch
reset" and `(?P<foo>[0-9]+)` "is group 2 because it's called 'foo'". Two groups in one alternative
then share group 2, which breaks R1. Measured over ' abc 123', upstream's groups are (' ', '123'),
and the 'abc' is lost. Option 3 is the maintainer's own repair: it keeps the documented rule for
names and moves `(\w+)` to group 3.

### Which rules each answer keeps

| Answer | R1 no lost capture | R2 name finds its text | R3 positional numbers | R4 one name, one number |
|---|---|---|---|---|
| Upstream today | broken | kept | broken | kept |
| Perl / Boost | kept | kept (Perl) | kept | broken |
| PCRE2: refuse | n/a | n/a | n/a | enforced by refusing |
| Option 3 (port) | kept | kept | broken where a name forces it | kept |

### Deriving the answer

- **No answer keeps all four rules.** R3 and R4 contradict each other on these patterns. Whichever
  answer is chosen gives up one of them, or refuses the pattern.
- **This port's rules are upstream's documented rules.** Where upstream is self-consistent, the
  port follows them; where they contradict themselves, the port repairs them. Upstream's README
  chooses R4 over R3 explicitly, and documents both examples:
  - different names get different numbers, "eg. `(?|(?P<foo>first)|(?P<bar>second))` has group 1
    ('foo') and group 2 ('bar')";
  - a name keeps its number across alternatives.

  Under those documented rules, the pattern is not ambiguous. It is defined, and upstream defines
  it with an R1 defect that option 3 repairs. Option 3 is therefore the correct completion of this
  engine's documented semantics.
- **PCRE2's refusal is correct for PCRE2,** because PCRE2's documented rules make the pattern
  invalid. Refusing here would reject a construct upstream documents as supported, with worked
  examples. That is a different engine's rule, not a more correct reading of this one's.

### The owner's question: should the user get a message telling them how to write it unambiguously?

Best practice in language tools is:

- **reject** input that has no defined meaning, with an error that says how to fix it;
- **warn** about input that is valid but often a mistake.

Compilers do this with warnings. Python's own `re` does it for regex syntax: `re.compile('[[a]')`
compiles, but emits "FutureWarning: Possible nested set at position 1" (measured). This pattern
falls in the second class: it is valid under this engine's documented rules, but its numbers differ
from Perl's and Boost's, and numbered references (`\1`, `Groups[1]`) will surprise anyone who
expects positional numbers.

So an error is wrong here, but guidance is right. The question is what channel carries it:

- **A compile error** (PCRE2's approach) rejects a documented, supported pattern. For this engine
  that is a defect, not guidance.
- **A runtime warning.** .NET's regex APIs have no warning channel, and this port's API mirrors
  them. A trace or `EventSource` message would reach almost nobody, so it would be noise rather
  than help.
- **Documentation**, at the point a user reads about branch reset: the GUIDE and the XML doc
  comment. It should state the rule, give the '!BUG' example, and show an unambiguous rewrite.
  Measured on every engine: drop the branch reset and name every group, as in
  `(?:(?P<bug>xxx)(?P<bang>!)|(?P<bang>!)(?P<bug>BUG))`. Access by name then gives bug='BUG' and
  bang='!' in upstream, Perl, Boost, the .NET BCL and the port, and in PCRE2 with `(?J)` (which
  upstream rejects). That is also perlre's own advice: "it's best to use the same names, in the
  same order, in each of the alternations".
- **A static analyzer** (a Roslyn diagnostic on a pattern literal) is the idiomatic .NET channel for
  "valid but suspicious". It is worth recording as a later upgrade for the whole API, not building
  for this one case.

### Recommendation: keep option 3, the most correct for this engine, and add the documentation

Do not refuse the pattern. Add the GUIDE section and doc comment with the rewrite above. Record a
`SHORTCUT:` note naming the analyzer as the upgrade path for warnings on valid but suspicious
patterns.

---

## Summary

| Question | Most correct answer | Change needed |
|---|---|---|
| OPEN-1, verb inside a called group | (a) the verb ends the whole attempt: it speaks about the starting position, which a call does not change | None; confirms the 2026-09-26 ruling, with this evidence added |
| OPEN-2, captures after a failed negative conditional test | (a) keep them: only backtracking undoes captures, and none happens | A fix, a pinned divergence from upstream, and a new register row |
| OPEN-3, branch-reset name at different positions | Keep option 3: the repaired form of upstream's documented rule | Documentation with the unambiguous rewrite; analyzer noted as a later upgrade |

## Appendix: how to reproduce

Every row in the tables above was run through each engine as follows:

- upstream: `regex.search(p, s)`;
- PCRE2: `pcre2.compile(p).search(s)`;
- Perl: `$s =~ /$p/`, reading `@-`, `@+` and `%+`, with `(?P<n>` spelled `(?<n>`;
- Boost: `boost::regex_search` with `boost::regex::perl`, standalone headers from
  github.com/boostorg/regex at a640597;
- .NET BCL: `Regex.Match` with a 1 s timeout;
- the port: `new FuzzyRegex(p).Match(s)`, printing every group and `GroupNumberFromName`.

The matrix survey tool, `tools/matrix/survey.py` on `matrix/answer-key`, runs rows of this kind
across the engines it supports. It does not include Boost.
