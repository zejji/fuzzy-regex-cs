# DRAFT - NOT FILED

Ledger entry 70. Written on 2026-10-04 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A word boundary over the empty text, and `\Z` at the edge, in a partial call

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.match(r'\b', '', partial=True)
>>> regex.search(r'\b', '', partial=True)
>>> regex.match(r'a\Zb', 'a', partial=True)
<regex.Match object; span=(0, 1), match='a', partial=True>
```

Over the empty text `\b` holds for any text that starts with a word character, so by the README's
definition of a partial match (`README.rst:270`) the first two should be partial matches. In the
third, `\Z` holds only when no text follows, and a partial call is asking about text that does follow,
so `a\Zb` over 'a' has no completion; upstream reports a partial because the `b` runs out of text
and the `\Z` plays no part in the answer. PCRE2 also answers a partial to the third.

A possible fix: decide a `\b`, `\B`, `\Z` or `$` at the edge of a partial call for the text to come,
and report a partial only for the continuations that pass it.
