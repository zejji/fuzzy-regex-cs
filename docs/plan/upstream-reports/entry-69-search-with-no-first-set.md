# DRAFT - NOT FILED

Ledger entry 69. Written on 2026-10-04 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A partial search of a pattern with no first set makes no start inside the text to come

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?<=ab)(?:b|$)', 'b', partial=True)
>>> regex.search(r'(?<=ab)\Z', 'b', partial=True)
>>> regex.search(r'(?(DEFINE)(?P<g1>b))(?<=ab)(?1)', 'b', partial=True)
>>> regex.search(r'(?(DEFINE)(?P<g1>b))(?<=ab)(?&g1)', 'x', partial=True)
>>> regex.search(r'(?(DEFINE)(?P<g1>b))\b(?1)', '', partial=True)
```

Each answers `None`. A partial search is meant to say whether a longer text could give a match
(`README.rst:270`), and a brute-force check (upstream without `partial`, over every continuation of
one to four characters) finds a completion for each of these subjects. A pattern with no first set
has no character to test the next position against, and upstream makes no start inside the text to
come for it.

A possible fix: a partial search that finds nothing should make one more attempt from a start at the
edge of the text, with the text to come unknown, whether or not the pattern has a first set.
