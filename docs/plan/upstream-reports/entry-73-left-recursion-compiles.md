# DRAFT - NOT FILED

Ledger entry 73. Written on 2026-10-08 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A left-recursive group call compiles, then runs out of memory

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.fullmatch(r'(?(DEFINE)(?<list>(?&list),\w+|\w+))^(?&list)$', 'a,b,c')
MemoryError
>>> regex.fullmatch(r'(?(DEFINE)(?<list>\w+(?:,(?&list))?))^(?&list)$', 'a,b,c')
<regex.Match object; span=(0, 5), match='a,b,c'>
```

In the first pattern `list` calls itself before it reads anything, so each call starts another at
the same position and the matcher never returns. Whether a group can do that depends only on the
pattern, so it can be found when the pattern is compiled. PCRE2 and Perl report it as an error
while matching ("nested recursion at the same subject position", "Infinite recursion in regex").

A possible fix: when compiling, follow each group's calls that can be reached before any text is
read, and raise `regex.error` if a group can reach itself that way.
