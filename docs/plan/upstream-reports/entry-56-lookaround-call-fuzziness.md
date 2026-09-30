# DRAFT - NOT FILED

Ledger entry 56. Written on 2026-09-30 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A call inside a lookaround in a fuzzy section runs its group fuzzily

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?:(?=(?&g))..){s<=1}(?P<g>ab)?', 'xb')
<regex.Match object; span=(0, 2), match='xb', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(?:(?=ab)..){s<=1}', 'xb')                  # the call written out
>>> regex.search(r'(?:..(?<=(?&g))){s<=1}(?P<g>ab)?', 'xb')
<regex.Match object; span=(0, 2), match='xb', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(?:..(?<=ab)){s<=1}', 'xb')
```

A lookaround's body is compiled exact even inside a fuzzy section
(`LookAround._compile`, `_regex_core.py:3201`: `self.subpattern.compile(self.behind)`), so
`(?=ab)` does not accept 'xb'. But `LookAround.fix_groups` (`:3160`) passes the caller's `fuzzy` to
the body, so a call there is registered as fuzzy and runs the group's fuzzy copy, which does
accept it. A call should match what its body written out matches.

A possible fix, in `LookAround.fix_groups`:

```python
    def fix_groups(self, pattern, reverse, fuzzy):
        self.subpattern.fix_groups(pattern, self.behind, False)
```
