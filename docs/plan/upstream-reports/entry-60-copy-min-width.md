# DRAFT - NOT FILED

Ledger entry 60. Written on 2026-09-30 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A group called from a lookbehind counts twice towards `min_width`

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?P<g1>\w)(?<=(?&g1))\W', 'a ')
>>> regex.search(r'(?P<g1>\w)(?<=\w)\W', 'a ')       # the call written out
<regex.Match object; span=(0, 2), match='a '>
>>> regex.search(r'(a)b(?<=(?1)b)', 'ab')
>>> regex.search(r'(a)b(?<=ab)', 'ab')
<regex.Match object; span=(0, 2), match='ab'>
```

The call in the lookbehind needs a backwards copy of the group, which `_compile` appends after the
pattern's `SUCCESS`. `build_CALL_REF` adds the copy's width to `args->min_width`
(`_regex.c:24560`), so the pattern's `min_width` is 3 where 2 characters match, and the width
early-out in `do_exact_match` refuses the two-character subject before matching starts. With
`partial=True` the same early-out skips the non-partial pass, so a complete match is reported as a
partial one.

A possible fix: record `min_width` when `SUCCESS` is built and use that as the pattern's, so the
copies, which are only reached through calls, add nothing.
