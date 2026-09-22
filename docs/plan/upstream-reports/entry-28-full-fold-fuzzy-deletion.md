# DRAFT - NOT FILED

Ledger entry 28. Written by S83 on 2026-09-22 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A fuzzy deletion next to a foldable pair is charged twice under full case folding

**Version:** regex 2026.9.10, CPython 3.14.6, Windows x64.

### Reproduction

```python
>>> import regex
>>> regex.search(r'(?:fi){d<=1}', 'fe', regex.I | regex.V1)
None
>>> regex.search(r'(?:fi){d<=1}', 'fe', regex.I | regex.V0)
<regex.Match object; span=(0, 1), match='f', fuzzy_counts=(0, 0, 1)>
```

Deleting the `i` from `fi` leaves `f`, which matches the first character of `'fe'`: one edit, within
`{d<=1}`. Version 0 finds it and Version 1 does not. The same happens on realistic text:

```python
>>> regex.search(r'(?:copper field studio){e<=2}',
...              'COPPER FILD SUDIO HARBOUR CANVAS FALCON', regex.I | regex.V1)
None
```

`FILD SUDIO` is `field studio` with the E and the T deleted, two edits. Version 0 finds it at (0, 17)
with fuzzy_counts (0, 0, 2).

Allowing more edits does not help, and can make the answer worse:

```python
>>> regex.search(r'(?:fi){d<=2}', 'fe', regex.I | regex.V1)
<regex.Match object; span=(2, 2), match='', fuzzy_counts=(0, 0, 2)>
```

The one-deletion match at position 0 is skipped for a two-deletion empty match at the end. And a
looser budget can lose a match a tighter one finds:

```python
>>> s = '\xdfasa0a\U0001f600'
>>> regex.match('(?fi)(\xdfa)(?:(?:\\1)\\B0a\U0001f600){d<=1}', s, regex.V1)
<regex.Match object; span=(0, 7), match='\xdfasa0a\U0001f600', fuzzy_counts=(0, 0, 1)>
>>> regex.match('(?fi)(\xdfa)(?:(?:\\1)\\B0a\U0001f600){s<=1,i<=1,d<=1}', s, regex.V1)
None
```

The reversed direction and a full-folded backreference fail the same way:
`(?r)(?:fi){d<=1}` over `'ei'`, `(fi)(?:\1){d<=1}` over `'fife'` and `(?r)(?:\1){d<=1}(fi)` over
`'eifi'` are `None` under `regex.I | regex.V1`, and (1, 2), (0, 3) and (1, 4) with one deletion
under `regex.I | regex.V0`. `tools/probes/s83-full-fold-fuzzy-deletion.py` in the reporter's
repository prints every case above under both versions.

### Cause

Under full case folding, a literal that holds a pair one character can fold to (fi, ff, st, ss)
compiles to a `RE_OP_STRING_FLD` item, which compares the pattern against each subject character's
folding. `folded_pos` and `folded_len` say how much of the current subject character's folding has
been used.

When a pattern character fails to match, the next subject character's folding has already been
loaded, so `folded_pos` is 0 and `folded_len` is 1 or more. A fuzzy deletion then advances only the
pattern position. If that deletion finishes the item, the leftovers loop after it
(`_regex.c:14856`, `while (folded_pos < folded_len)`) and the backtrack check at `:14874` read the loaded
folding as a half-matched subject character. Nothing of it was used, but it is charged as a further
edit, or the path backtracks. A one-deletion match therefore costs two edits or fails.

`RE_OP_STRING_FLD_REV` has the mirror image (`folded_pos > 0`, where a reversed walk consumes a
folding from its end), and `RE_OP_REF_GROUP_FLD` and `RE_OP_REF_GROUP_FLD_REV` test the same way at
`:14154` and `:14255`.

### Suggested fix

Count leftovers only when the folding is part-used. Forward:

```c
while (0 < folded_pos && folded_pos < folded_len) {
```

and `if (0 < folded_pos && folded_pos < folded_len)` for the backtrack test; reversed,
`0 < folded_pos && folded_pos < folded_len` in place of `folded_pos > 0`. A half-matched `ß` or
ligature at the end of an item is still charged: `(?fi)(?:sst){e<=1}` over `'\xdfﬆ'` still
needs its insertion.

With that change a C# port of the module agrees with Version 0 on every one of 300,000 fuzzy phrase
searches over short records (0 span differences, 106 before it), and the whole of its ported copy of
`test_regex.py` still passes.
