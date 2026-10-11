# DRAFT - NOT FILED

Ledger entry 43. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## `\X` under REVERSE, or in a lookbehind, matches one codepoint instead of a grapheme cluster

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.findall(r'\X', 'e\u0301a')
['e\u0301', 'a']
>>> regex.findall(r'(?r)\X', 'e\u0301a')
['a', '\u0301']
>>> regex.search(r'(?r)\X{2}', 'e\u0301a').span()
(1, 3)
>>> print(regex.search(r'(?r)^\X', '\r\n'))
None
>>> print(regex.search(r'(?<=^\X)b', '\r\nb'))
None
```

A grapheme cluster is a property of the text, so the reversed search should find the same
clusters in the opposite order, `['a', 'e\u0301']`, and the 'e' should not go missing. The next
three should be `(0, 3)`, `(0, 2)` and `(2, 3)`: CR LF is one cluster, so `^\X` covers it and
there is one cluster before the 'b'. The README says `\X` conforms to UAX #29, and the comment in
`Grapheme._compile` says the match "is the same whether matching forwards or backwards". Run over
the 766 lines of Unicode 17.0.0's `GraphemeBreakTest.txt`, forward `\X` agrees with every line and
`(?r)\X` disagrees with 465.

**Cause.** `Grapheme._compile` (`regex/_regex_core.py`) builds

```python
Atomic(Sequence([LazyRepeat(AnyAll(), 1, None), GraphemeBoundary()]))
```

for both directions, and `Sequence._compile` reverses its items when `reverse` is true. Going
backwards the boundary test therefore comes first, at the position the match starts from, and the
lazy repeat then takes one codepoint; the atomic group never lets it take more. A lookbehind
compiles its body in reverse, so `\X` inside one has the same fault.

**Suggested fix.** List the items the other way round when compiling in reverse, so that after the
reversal the boundary is tested where the cluster stops:

```python
class Grapheme(RegexBase):
    def _compile(self, reverse, fuzzy):
        if reverse:
            items = [GraphemeBoundary(), LazyRepeat(AnyAll(), 1, None)]
        else:
            items = [LazyRepeat(AnyAll(), 1, None), GraphemeBoundary()]

        return Atomic(Sequence(items)).compile(reverse, fuzzy)
```

With `Grapheme._compile` replaced by this in a running regex 2026.9.10, `\X` and `(?r)\X` both agree
with all 766 lines, and the lookbehind and `{2}` examples above give the expected answers. The
forward code is unchanged.
