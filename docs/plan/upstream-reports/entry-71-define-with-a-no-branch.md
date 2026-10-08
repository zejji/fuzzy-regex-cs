# DRAFT - NOT FILED

Ledger entry 71. Written on 2026-10-08 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A `(?(DEFINE)...)` group with a no-branch compiles, and the pattern never matches

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?(DEFINE)a|b)x', 'bx')
>>> regex.search(r'(?(DEFINE)a|b)x', 'x')
>>> regex.search(r'(?(DEFINE)a)x', 'bx')
<regex.Match object; span=(1, 2), match='x'>
```

The first two answer `None`: adding a no-branch to a DEFINE group stops the whole pattern from
matching anything, with no error. A DEFINE group is never matched in place, so a no-branch there
has no meaning and is most likely a typo. PCRE2 refuses the pattern at compile time ("DEFINE
subpattern contains more than one branch"), and so does Perl ("(?(DEFINE)....) does not allow
branches in regex").

A possible fix: raise `regex.error` when a `(?(DEFINE)...)` group, with no group named `DEFINE` in
the pattern, has a non-empty no-branch. (Refusing an empty one too, as PCRE2 does, would break
patterns that work today, such as `(?(DEFINE)a|)x`.)
