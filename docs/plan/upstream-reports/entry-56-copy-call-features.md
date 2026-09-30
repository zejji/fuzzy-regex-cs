# DRAFT - NOT FILED

Ledger entry 56. Written on 2026-09-30 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A call inside a called group's copy keeps the features of where it is written (segfault)

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?&g4)(?:(?P<g4>(?&g3))){s<=1}(?P<g3>a)', 'ba')
Windows fatal exception: access violation
>>> regex.search(r'a(?:(?P<g4>a)){s<=1}(?P<g3>a)', 'ba')      # the calls written out
>>> regex.search(r'(?P<g3>a)(?P<g4>(?&g3))(?:(?&g4)){s<=1}', 'aab')
>>> regex.search(r'(?P<g3>a)(?P<g4>a)(?:(?&g4)){s<=1}', 'aab')  # the call to g3 written out
<regex.Match object; span=(0, 3), match='aab', fuzzy_counts=(1, 0, 0)>
```

A group called from a place with other features than where it is written is compiled again with
the caller's direction and fuzziness (`_check_group_features`, `_regex_core.py:4420-4457`), which is
what makes `(?&g4)` above run g4 exactly although g4 is written inside `{s<=1}`. The calls inside
that copy are not looked up again: `call.call_ref` is set once, for where the call is written
(`:4454`), and the copy compiles the same `CallGroup` node (`:2557`). So the exact copy of g4 calls
the fuzzy compile of g3, whose `a` fails on 'b' with no fuzzy section in force, and
`any_error_permitted` dereferences a NULL `state->fuzzy_node` (`_regex.c:9667`). The mirror case
does not crash but loses the match: the fuzzy copy of g4 called from `(?:(?&g4)){s<=1}` calls the
exact g3, so the substitution the section allows is never tried. Writing the calls out gives the
answers the calls should give. The same holds for `(?R)`:
`regex.search(r'(?P<g>a)(?:b(?:(?R)){s<=1}|c(?&g))', 'abacb')` is None, and with `c(?&g)` written
as `ca` it is (0, 5) with one substitution.

A possible fix: before compiling each entry of `info.additional_groups`, walk it with `fix_groups`
from its own `(reverse, fuzzy)`, look each call it reaches up in `call_refs` again (adding a copy
for any new key), and set `call_ref` for that compile. Compiling the copies one at a time makes the
in-place update safe.
