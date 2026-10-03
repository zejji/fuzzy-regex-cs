# DRAFT - NOT FILED

Ledger entry 66. Written on 2026-10-03 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A called group's copy is compiled before `optimise` and `pack_characters`

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?fi)(?(DEFINE)(?P<g1>SS))(?<=(?&g1))', 'ß')
>>> regex.search(r'(?fi)(?<=(?:SS))', 'ß')                    # the call written out
<regex.Match object; span=(1, 1), match=''>
>>> regex.search(r'(?fi)(?(DEFINE)(?P<g1>SS))(?:(?&g1)){i<=1}', 'ß')
>>> regex.search(r'(?fi)(?:(?:SS)){i<=1}', 'ß')
<regex.Match object; span=(0, 1), match='ß'>
>>> regex.search(r'(?(DEFINE)(?P<g1>(?>a|b)c))(?:(?&g1)){d<=1}', 'b')
<regex.Match object; span=(0, 1), match='b', fuzzy_counts=(0, 0, 1)>
>>> regex.search(r'(?:(?:(?>a|b)c)){d<=1}', 'b')
```

A call in a lookbehind or a fuzzy section needs a copy of the group with those features, which
`_compile` builds from `info.defined_groups` (`_main.py:637`, `_regex_core.py:4449`). That records
the group as the parser built it, because `Group.optimise` returns a new `Group` (`:3073`) and
`pack_characters` then runs on the new one. So the copy's `S`, `S` stay two characters and never
meet the full case fold of 'ß'. The parsed tree has also been partly rewritten by then:
`Atomic.optimise` replaces its body in place (`:2083`), so the fuzzy copy in the third example runs
`(?>a|b)` as the set `[ab]`, where the written-out group, inside a fuzzy section, is never
optimised and keeps the deletion of `a` that the atomic group commits to.

A possible fix: run `optimise` and `pack_characters` on the pattern from a copy of the parsed tree,
and build each additional group from the untouched tree, optimised in the copy's own direction
(not at all when the copy is fuzzy) and then packed.
