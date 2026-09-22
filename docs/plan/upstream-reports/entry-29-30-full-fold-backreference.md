# DRAFT - NOT FILED

Ledger entries 29 and 30. Written by S84 on 2026-09-22 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

The two defects sit in the same opcode and would likely be fixed together, so this is one report.
If the maintainer prefers two issues, the sections split cleanly.

---

## A fuzzy full-folded backreference loses matches that the equivalent literal finds

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

### 1. A group that ends half-way through a folding

```python
>>> import regex
>>> regex.search(r'(s)(?:\1){e<=1}', 'sß', regex.I | regex.V1)
None
>>> regex.search(r'(?:sss){e<=1}', 'ßß', regex.I | regex.V1)
<regex.Match object; span=(0, 2), match='ßß', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(s)(?:\1){e<=1}', 'sß', regex.I | regex.V0)
<regex.Match object; span=(0, 2), match='sß', fuzzy_counts=(1, 0, 0)>
```

Under full case folding `'sß'` is s-s-s. The pattern needs s-s plus one edit for the third s, and a
literal with the same folded text finds exactly that. The backreference does not. `{i<=1}` fails the
same way, where `(?:sss){i<=1}` over `'ßß'` finds the one insertion; so do the reversed form
`(?r)(?:\1){e<=1}(s)` over `'ßs'` and a longer group, `(as)(?:\1){e<=1}` over `'asaß'`.

**Cause.** `RE_OP_REF_GROUP_FLD` loops while group text remains (`_regex.c:14102`). If the group
runs out while the subject character's folding is part-used, the check at `:14154` backtracks.
`RE_OP_STRING_FLD` in the same state runs `while (folded_pos < folded_len)` (`:14855`) and offers
each remaining folded character to `fuzzy_match_string_fld`. `RE_OP_REF_GROUP_FLD_REV` has the
mirror image at `:14255`.

**Suggested fix.** After the loop, when the node is fuzzy, run the same leftovers loop through
`fuzzy_match_group_fld`, advancing `text_pos` when a folding is used up, as `RE_OP_STRING_FLD`
does. Mirror it in the reversed arm. One difference from the literal loop is needed: in the
leftovers loop the group has nothing left to delete, so `next_fuzzy_match_group_fld` should refuse
`RE_FUZZY_DEL` when `gfolded_pos` is at the end of the group's folding (at 0 when reversed). The
check has to sit there rather than in the loop, because `retry_fuzzy_match_group_fld` offers the
deletion again when a later item fails. Without it the loop never ends when deletions are free, as
the literal loop shows today:

```python
>>> regex.search(r'(?:sss){0d+1s+1i<=1:[x]}', 'ßß', regex.I | regex.V1)
Traceback (most recent call last):
  ...
MemoryError
```

### 2. A retried edit compares a character it already used

```python
>>> regex.search(r'(ab)(?:\1){e<=1}', 'abxab', regex.I | regex.V1)
None
>>> regex.search(r'(ab)(?:\1){e<=1}', 'abxab', regex.V1)
<regex.Match object; span=(0, 5), match='abxab', fuzzy_counts=(0, 1, 0)>
>>> regex.search(r'(ab)(?:\1){e<=1}', 'abxab', regex.I | regex.V0)
<regex.Match object; span=(0, 5), match='abxab', fuzzy_counts=(0, 1, 0)>
```

No special character is involved: under `I | V1` every fuzzy backreference compiles to
`RE_OP_REF_GROUP_FLD`. The substitution of x for a is tried first and fails at the b; the retry
should insert the x instead, and does not.

It also affects `BESTMATCH`, which reports two edits where one suffices:

```python
>>> regex.search(r'(?b)(?f)(ßa)(?:\1){s<=1,i<=1,d<=1}', 'ßasa', regex.I | regex.V1)
<regex.Match object; span=(0, 4), match='ßasa', fuzzy_counts=(1, 0, 1)>
>>> regex.search(r'(?b)(?f)(ßa)(?:ßa){s<=1,i<=1,d<=1}', 'ßasa', regex.I | regex.V1)
<regex.Match object; span=(0, 4), match='ßasa', fuzzy_counts=(0, 0, 1)>
```

**Cause.** `retry_fuzzy_match_group_fld` re-enters the arm with `string_pos >= 0`, and the re-entry
branch (`_regex.c:14094`) reloads both foldings and goes straight into the loop. It skips the two
steps the loop body takes after an edit: `:14145` moves `text_pos` past a used-up subject folding
and `:14148` moves `string_pos` past a used-up group folding. A retried insertion that used up the
x therefore compares the x again. `RE_OP_STRING_FLD` takes the subject's step on re-entry
(`:14801`, reversed `:14907`).

**Suggested fix.** In the re-entry branch, after reloading the foldings, repeat the two steps:

```c
if (folded_pos >= folded_len && folded_len > 0) {
    ++state->text_pos;
    folded_pos = 0;
    folded_len = 0;
}
if (gfolded_pos >= gfolded_len && string_pos < span->end) {
    ++string_pos;
    gfolded_pos = 0;
    gfolded_len = 0;
}
```

and the mirror image in `RE_OP_REF_GROUP_FLD_REV`. With the first fix in place, the re-entry can
also find the group used up, so fold the group character only while `string_pos < span->end`.

With both changes a C# port of the module agrees with the written-out literal on each of the cases
above and on the six rows of its differential test that the change moved, and the whole of its
ported copy of `test_regex.py` still passes. `tools/probes/s84-full-fold-backreference.py` in the
reporter's repository prints every case above.
