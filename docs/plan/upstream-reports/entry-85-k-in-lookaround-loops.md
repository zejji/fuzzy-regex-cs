# DRAFT - NOT FILED

Ledger entry 85. Written on 2026-10-15 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## `\K` inside a lookaround makes `finditer`, `split` and `sub` loop for ever

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex, itertools
>>> [m.span() for m in itertools.islice(regex.finditer(r'(?=y\K)', 'y'), 4)]
[(1, 0), (1, 0), (1, 0), (1, 0)]
>>> [m.span() for m in itertools.islice(regex.finditer(r'(?r)(?=y\K)', 'y'), 4)]
[(0, 1), (0, 1), (0, 1), (0, 1)]
>>> [m.span() for m in itertools.islice(regex.finditer(r'(?<=\Ky)', 'y'), 4)]
[(0, 1), (0, 1), (0, 1), (0, 1)]
>>> [m.span() for m in itertools.islice(regex.finditer(r'(?<=\Ka)b', 'abab', overlapped=True), 4)]
[(0, 2), (0, 2), (0, 2), (0, 2)]
>>> regex.split(r'(?=y\K)', 'yy', timeout=2)
TimeoutError: regex timed out
>>> regex.search(r'(?er)(?:Cyy){1i+1d+1s<=2}(?=z\K)', 'bx-axyz-𝒜', timeout=2)
TimeoutError: regex timed out
>>> regex.search(r'(?r)(?:Cyy){1i+1d+1s<=2}(?=z\K)', 'bx-axyz-𝒜')
<regex.Match object; span=(3, 7), match='axyz', fuzzy_counts=(2, 0, 0)>
```

`list(regex.finditer(r'(?=y\K)', 'y'))` never returns and runs out of memory. Each scan above has
one match (the overlapped one two, (0, 2) and (2, 4)), and the search under `(?e)` should answer
as plain `(?r)` does; without the `\K`, or with the `\K` outside the lookaround, they behave.

**What goes wrong.** In `(?=y\K)` over 'y' the attempt at 0 runs the lookahead, which reads the 'y'
and moves the reported start to 1, and then puts the text position back to 0. The match ends where
its attempt began, so it consumed nothing, but its span (1, 0) is not empty. The scan's rule
against two empty matches in a row tests the span (`state->must_advance = state->text_pos ==
state->match_pos`, `_regex.c:20932`, and the copies at `:21207`, `:22087`, `:22361`, `:22510`), so
it bans nothing, and the next search from 0 finds the same match.

The other loops step on from `match_pos` as if it were where the attempt began. A `\K` in a
lookbehind (or in a lookahead under `(?r)`) leaves it behind the attempt. The overlapped step
(`state->text_pos = state->match_pos + step`, `:20928`, `:22506`) then lands on the same attempt
again, and so does the advance after a failed attempt (`state->text_pos = state->match_pos`,
`:15695`), which loops inside a single search. The `\K` survives the failed attempt because a
lookaround that succeeds discards the backtrack entry that would undo it (`RE_OP_END_LOOKAROUND`,
`:12918`).

**Other engines.** Perl (5.32 onwards; checked on 5.42.3) and PCRE2 (10.38 onwards; checked on
10.47) refuse `\K` inside a lookaround at compile time. PCRE2 keeps an option to allow it,
`PCRE2_EXTRA_ALLOW_LOOKAROUND_BSK`, and its `pcre2_next_match` helper (`src/pcre2_match_next.c`)
handles exactly this case: a match that ends where the search began but whose start a `\K` moved is
"a non-empty match which nonetheless fails to make progress", and it bumps along instead of
searching from the same place.

**A possible fix**, as made in a .NET port of this module: record where each attempt began. Count a
match that ends where its attempt began as empty for `must_advance`, and step on from the attempt's
start, not `match_pos`, whenever `match_pos` lies behind it (the overlapped step and the advance
after a failure). Patterns without a `\K` inside a lookaround are unaffected, because there the two
positions agree.
