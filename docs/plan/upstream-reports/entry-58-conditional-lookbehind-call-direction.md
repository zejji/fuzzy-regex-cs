# DRAFT - NOT FILED

Ledger entry 58. Written on 2026-09-30 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A call in a conditional's lookbehind test runs its group forwards

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'bc(?(?<=(?&g))x|y)(?P<g>bc)?', 'bcxzzz')
>>> regex.search(r'bc(?(?<=bc)x|y)', 'bcxzzz')                 # the call written out
<regex.Match object; span=(0, 3), match='bcx'>
>>> regex.search(r'bc(?(?<!(?&g))x|y)(?P<g>bc)?', 'bcyzzz')
>>> regex.search(r'bc(?(?<!bc)x|y)', 'bcyzzz')
<regex.Match object; span=(0, 3), match='bcy'>
```

`LookAroundConditional._compile` compiles the test in its own direction (`_regex_core.py:3267`:
`self.subpattern.compile(self.behind, fuzzy)`), but `fix_groups` (`:3230`) passes the caller's
`reverse`, so a call in a lookbehind test is registered as forward and runs the group's forward
compile from the current position. (The 'zzz' only keeps the subject longer than the minimum width
the call adds, which would otherwise refuse 'bcx' before matching.)

A possible fix, in `LookAroundConditional.fix_groups`:

```python
        self.subpattern.fix_groups(pattern, self.behind, fuzzy)
```
