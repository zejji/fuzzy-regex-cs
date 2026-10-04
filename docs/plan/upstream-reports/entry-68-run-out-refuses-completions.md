# DRAFT - NOT FILED

Ledger entry 68. Written on 2026-10-04 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A partial match is refused where a longer text completes the match

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.match(r'.*?(?<=c)', 'aa', partial=True)
>>> regex.match(r'.*?(?<=c)', 'aac')
<regex.Match object; span=(0, 3), match='aac'>
>>> regex.fullmatch(r'(?:.\b)+', 'a ', partial=True)
>>> regex.match(r'(?!(?>(?:ab)*)(?<=^ab))ab', 'ab', partial=True)
>>> regex.match(r'(?!(?>(?:ab)*)(?<=^ab))ab', 'abab', partial=True)
<regex.Match object; span=(0, 2), match='ab'>
```

`(?r)\X` fullmatch over '्क' is `None` as well. Each subject above is a prefix of a longer text the
pattern matches, so by the README's definition (`README.rst:270`) the answer is a partial match.

Upstream bounds a lazy repeat by the text held, and turns the run-out of a greedy body at the end of
the text into a failure (`_regex.c:12575`), so the attempt backtracks where the next character
would have decided it. The same happens inside a lookahead, in the last pair of calls above.

A possible fix: report the partial where the body runs out, as it already does for other shapes of
the same pattern. This can report a partial no text completes for a few patterns; a partial that
no text completes is the cheaper error, since it never takes a completion away.
