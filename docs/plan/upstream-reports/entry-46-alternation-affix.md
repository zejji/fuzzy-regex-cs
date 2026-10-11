# DRAFT - NOT FILED

Ledger entry 46. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## Two alternatives that start with the same `(*SKIP)` or `(*PRUNE)` match differently from two that start with different verbs

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(*SKIP)[ab]+|(*PRUNE)\b', 'ccb').span()
(2, 3)
>>> regex.search(r'(*SKIP)[ab]+|(*SKIP)\b', 'ccb').span()
(0, 0)
>>> regex.search(r'(*PRUNE)[ab]+|(*PRUNE)\b', 'ccb').span()
(0, 0)
```

The first answer is the right one, and PCRE2 10.47 and Perl 5.42 give `(2, 3)` for all three. At
positions 0 and 1, `[ab]+` fails, the match backtracks into the verb, and the verb ends the attempt
at that position, so the `\b` alternative is never tried.

**Cause.** `Branch._split_common_prefix` (`regex/_regex_core.py`) moves the items that every
alternative starts with out in front of the branch, so the second and third patterns compile as
`(*SKIP)(?:[ab]+|\b)`. Now the verb is reached only after both alternatives have failed, and `\b`
matches at 0. The guard, `can_be_affix`, is `True` for `Skip` and `Prune`.

The same guard lets through three other kinds of item that can match in more than one way, where
moving the item out changes which way is tried first:

```python
>>> regex.search(r'(?:a\K|ab)c|(?:a\K|ab)', 'abc').span()     # a nested branch
(1, 1)
>>> regex.search(r'(?:(?1)c|(?1))|(a|ab)', 'abc').span()      # a group call
(0, 1)
>>> m = regex.search(r'(?:a){e<=1}c|(?:a){e<=1}', 'abc')     # a fuzzy section
>>> m.span(), m.fuzzy_counts
((0, 1), (0, 0, 0))
```

These should be `(0, 3)`, `(0, 3)`, and `(0, 3)` with one insertion (which is what
`(?:a){e<=1}c|(?:a){e<=2}` gives, the two sections no longer being equal). PCRE2 and Perl agree on
the first two. `_split_common_suffix` does the same under REVERSE:
`regex.search(r'(?r)[ab]+(*SKIP)|\b(*SKIP)', 'bcc')` is `(3, 3)` where `(0, 1)` is expected.

**Suggested fix.** Refuse those five kinds as affixes:

```python
class Branch(RegexBase):
    def can_be_affix(self):
        return False

class CallGroup(RegexBase):
    def can_be_affix(self):
        return False

class Fuzzy(RegexBase):
    def can_be_affix(self):
        return False

class Prune(ZeroWidthBase):
    def can_be_affix(self):
        return False

class Skip(ZeroWidthBase):
    def can_be_affix(self):
        return False
```

`StringSet` inherits `Branch`'s and loses nothing, since its alternatives are sequences, which
were never affixes. With these overrides in a running regex 2026.9.10, every example above gives
the expected answer, and regex agrees with PCRE2 on 4,312 of 4,312 generated rows over 28 kinds of
item; without them it disagrees on 415.
