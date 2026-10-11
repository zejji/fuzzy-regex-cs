# DRAFT - NOT FILED

Ledger entry 74. Written on 2026-10-08 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A partial match leaves a group unset when the repeat inside it stops at a tail that runs out

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> m = regex.search(r'(?<o>b(?<i>a)?)b', 'ba', partial=True)
>>> m.span(), m.partial, m.span('o')
((0, 2), True, (-1, -1))
>>> regex.search(r'(?<o>b(?<i>a)?)b', 'bab').span('o')
(0, 2)
```

Every text that completes the partial match ('bab', 'babx', ...) sets `o` to (0, 2), so the partial
match could report it. `o` is unset because `END_GREEDY_REPEAT` tests the repeat's tail with
`try_match` and returns `RE_ERROR_PARTIAL` as soon as that test runs out of text
(`_regex.c:12591-12594`), before the end of group `o`, which comes first in the tail, is reached.
The other repeat arms (`END_LAZY_REPEAT`, `GREEDY_REPEAT`, `LAZY_REPEAT` and the one-character
repeats' backtracking) do the same: `(?<o>a??)b` over 'a' and `(?r)b(?<o>(?<i>a)?)` over 'a' also
leave `o` unset.

A possible fix: treat a partial from the tail test as "the tail may match" and enter the tail, so the
item that ran out runs out again after the group has closed. The span and the partial flag do not
change.
