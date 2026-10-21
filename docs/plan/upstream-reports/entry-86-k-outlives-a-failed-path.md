# DRAFT - NOT FILED

Ledger entry 86. Written on 2026-10-16 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## `\K` inside a lookaround or atomic group is not undone when the path fails

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?=ab\K)x|b', 'ab')
>>> regex.search(r'(?=ab\K)x|b', 'ab', 1)
<regex.Match object; span=(1, 2), match='b'>
>>> regex.search(r'(?=a\K)x|a', 'a')
<regex.Match object; span=(1, 1), match=''>
>>> regex.search(r'(?!a\K)x|a', 'a')
<regex.Match object; span=(1, 1), match=''>
>>> regex.search(r'(?(?=ab\K)x|b)', 'ab')
>>> regex.search(r'(?>a\K)x|a', 'a')
<regex.Match object; span=(1, 1), match=''>
```

In each pattern the alternative or branch that holds the `\K` fails, so the answer should be the
other alternative's: (1, 2), (0, 1), (0, 1), (1, 2) and (0, 1). The first search contradicts the
second: the failed attempt at 0 makes the search skip position 1.

**What goes wrong.** `\K` saves `match_pos` on the backtrack stack so that backtracking can put it
back. A lookaround, a condition's test or an atomic group that succeeds discards everything its body
pushed (`RE_OP_END_LOOKAROUND`, `_regex.c:12918`; the negative case at `:12985-13009` restores the
captures and fuzzy counts but not `match_pos`; `RE_OP_END_ATOMIC` likewise), so the saved start goes
too. When the path after the construct fails, the moved start remains: a later alternative reports
it, and if the attempt fails, the advance (`state->text_pos = state->match_pos`, `:15695`) steps on
from it.

**Other engines.** Ruby 3.2.3 (Onigmo), which accepts `\K` in a lookaround, undoes it in every case
above. PCRE2 10.47 and Ruby undo it in atomic groups and possessive repeats; Perl 5.42.3 keeps it
there. Perl and PCRE2 refuse `\K` in a lookaround at compile time.

**A possible fix**, as made in a .NET port of this module: when the pattern has a `\K` inside such
a construct, push the current `match_pos` with a `KEEP` tag below the construct's frame on entry.
The cut stops at the frame, so the entry survives, and backtracking past the construct restores
the start whatever the body did. A negative lookaround whose body matched restores it at once.
Verbs are unaffected: their cut to the attempt discards the entry, so `a\K(*PRUNE)x|a` stays None,
which PCRE2 agrees with.
