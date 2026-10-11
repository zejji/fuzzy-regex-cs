# DRAFT - NOT FILED

Ledger entry 39. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A zero-error constraint such as `{e<=0}` is ignored beside another fuzzy section, but `{d<=0}` is not

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.match(r'(?:(?:ab){s<=1,d<=1}){e<=0}', 'x')
<regex.Match object; span=(0, 1), match='x', fuzzy_counts=(1, 0, 1)>
>>> regex.match(r'(?:(?:ab){s<=1,d<=1}){d<=0}', 'x')
>>> regex.match(r'(?:c(?:ab){e<=0}){e<=1}', 'cax')
<regex.Match object; span=(0, 3), match='cax', fuzzy_counts=(1, 0, 0)>
>>> regex.match(r'(?:c(?:ab){d<=0}){e<=1}', 'cax')
>>>
```

`{d<=0}` permits no errors at all, since "if a certain type of error is specified, then any type
not specified will **not** be permitted" (README), so it and `{e<=0}` are the same constraint, but
they give different answers. `{e<1}` and `{s<=0,i<=0,d<=0}` behave like `{e<=0}`; `{1s+1i+1d<=0}`
behaves like `{d<=0}`.

It also means the pattern from issue 306 still matches where the inner sections should forbid it:

```python
>>> regex.search(r'(dogf(((oo){e<1})|((00){e<1}))d){e<2}', 'dogfxod')
<regex.Match object; span=(0, 7), match='dogfxod', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(dogf(((oo){d<1})|((00){d<1}))d){e<2}', 'dogfxod')
>>>
```

and a group call carries the outer budget into a group constrained to zero errors:

```python
>>> regex.search(r'(ab){e<=0}x(?:(?1)){e<=1}', 'abxax')
<regex.Match object; span=(0, 5), match='abxax', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(ab){d<=0}x(?:(?1)){e<=1}', 'abxax')
>>>
```

**Cause.** `parse_sequence` (`_regex_core.py:527-533`) applies a fuzzy constraint only if
`is_actually_fuzzy` (`:548-556`) says it permits errors, and that function reads the constraints as
written, before `Fuzzy.__init__` fills in the defaults. It returns False when `e` is written as
`(0, 0)` or when `s`, `i` and `d` all are, so no `Fuzzy` node is built. On its own that is only an
optimisation, but the missing node is also what would cap the errors of a fuzzy section inside it,
keep an outer section's budget off its subpattern, and hold inside a group that a fuzzy section
calls. `{d<=0}` passes the check, because `s` and `i` are only set to zero later, so its node is
built and it works.

**Suggested fix.** Remove the `is_actually_fuzzy` check so the constraint is always applied. To keep
the optimisation for a zero-error constraint that no other fuzzy section can reach, drop the node
after parsing only where no fuzzy section encloses it, none is inside it, and no group call in a
fuzzy pattern could carry a budget into it. A C# port of the module does exactly this: every
pattern above then answers as its `{d<=0}` twin does, the compiled code of a lone `a{e<=0}` is
unchanged, and the whole of its ported copy of `test_regex.py` still passes, including the issue
306 rows, which use `(?e)` over 'dogfood' and 'dogfoot' and pass either way. Over 77,004 generated
rows of nested, sibling, repeated and called zero-error sections, it agrees with the `{d<=0}`
spelling on every one.
