# DRAFT - NOT FILED

Ledger entry 75. Written on 2026-10-09 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A reversed partial search over the empty text misses a match that a newline before it completes

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> print(regex.search(r'(?wr)\b$^', '', partial=True))
None
>>> regex.search(r'(?wr)\b$^', '\n')
<regex.Match object; span=(0, 0), match=''>
```

A reversed search runs out of text on the left, so a partial match asks whether some text before
the subject completes the match. Here '\n' does: at its start, `^` holds, `$` holds before the final
newline, and `\b` holds at the start of the text under the `w` flag. So by the README's definition
of a partial match (`README.rst:270`) the first call should return a partial match at (0, 0), not
`None`. The reason seems to be that a partial is reported only when a node runs out of characters to
read, and no node in this pattern reads one.

A possible fix: when a reversed partial search over an empty slice finds nothing, try the zero-width
tests at a position inside the text to come, where `$` can hold because the final newline may be
there, and report a partial if they can all pass.
