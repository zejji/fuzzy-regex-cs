# DRAFT - NOT FILED

Ledger entry 49. Written on 2026-09-28 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A fuzzy `ß` can be substituted as one character on its own, but not next to another letter

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?fi)(?:ß){s<=1}x', 'ax')
<regex.Match object; span=(0, 2), match='ax', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(?fi)(?:ßx){s<=1}', 'ax')
>>> regex.search(r'(?fi)(?:ßx){d<=1}', 'x')
>>> regex.search(r'(?fi)(?:ﬁx){s<=1}', 'ax')
>>>
```

The first two patterns differ only in how far the fuzzy section reaches. The second allows every
error placement the first allows, since the `x` matches exactly in both, so it should match 'ax' at
least as well. It finds nothing.

The cause is in `_regex_core.py`. A lone character that expands under full case folding compiles to
a choice between the character and its folding (`Character._compile`, lines 2629-2632), so the fuzzy
matcher can substitute or delete the whole `ß`. When `Sequence.pack_characters` packs `ß` together
with a neighbouring letter, the resulting `String` compiles to `STRING_FLD` holding only the folded
characters, `ssx` (lines 4017-4025 and 4041-4048). Each fuzzy edit there changes one folded
character, so replacing the `ß` with one character costs a substitution and a deletion. Whether the
one-edit answer is available therefore depends on packing: `(?fi)(?:xß){s<=1}` over 'xa' does match,
because `_fix_full_casefold` happens to leave that `ß` as a lone character.

A possible fix: in a fuzzy section, compile a full-folded `String` that holds an expanding character
as `Branch([String, Sequence of its characters])`, which is the lone character's own choice extended
to the run. Keeping the packed string first leaves every existing exact match unchanged.

For comparison, PCRE2 10.47, Python `re`, .NET and JavaScript do not fold `ß` to `ss`; Perl 5.42.3
does and agrees on the exact matches (`ßx` matches 'ssx', `sß` matches 'ßs'). None of them has
fuzzy matching.
