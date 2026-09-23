# DRAFT - NOT FILED

Ledger entry 31. Written by S85 on 2026-09-23 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

It builds on entry 28 (`entry-28-full-fold-fuzzy-deletion.md`), whose fix changes the same loop,
and on entry 29 (`entry-29-30-full-fold-backreference.md`), whose suggested deletion refusal this
one replaces.

---

## A fuzzy full-folded item cannot end part-way into a subject folding

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?:sss){d<=1}', 'ß', regex.I | regex.V1)
<regex.Match object; span=(0, 1), match='ß', fuzzy_counts=(0, 0, 1)>
>>> regex.search(r'(?:sss){d<=1}', 'ßß', regex.I | regex.V1)
<regex.Match object; span=(1, 2), match='ß', fuzzy_counts=(0, 0, 1)>
>>> regex.search(r'(?:sss){0d+1s+1i<=1:[x]}', 'ßß', regex.I | regex.V1)
Traceback (most recent call last):
  ...
MemoryError
```

Under full case folding 'ßß' is s-s-s-s. The pattern needs s-s-s with one deletion, and the first
'ß' alone gives that, as the first line shows. A second 'ß' should not move the match to position
1. With free deletions the search never ends.

The same happens reversed (`(?r)(?:sss){d<=1}` over 'ßß' gives (0, 1), where (1, 2) is the
mirror of the first line), with ligatures (`(?:xfff){i<=1,d<=2}` over 'ﬀﬃfi' gives (1, 3),
where (0, 1) with two deletions also fits), and in a backreference:

```python
>>> regex.search(r'(s)(?:\1){d<=1}', 'sß', regex.I | regex.V1)
None
>>> regex.search(r'(s)(?:s){d<=1}', 'sß', regex.I | regex.V1)
<regex.Match object; span=(0, 1), match='s', fuzzy_counts=(0, 0, 1)>
```

**Cause.** When a `RE_OP_STRING_FLD` item's letters run out part-way through a subject folding,
the leftovers loop (`_regex.c:14856`, reversed `:14962`) asks `fuzzy_match_string_fld` for an
edit. The only one open to a deletion-only pattern is `RE_FUZZY_DEL`, and
`next_fuzzy_match_string_fld` (`:10590`) handles it by moving `new_string_pos` on, which is
already at the end. The edit is charged and `folded_pos` stays where it was, so the loop cannot
leave the folding, and with free deletions it repeats until memory runs out. The first line
matches only because the folding there is the last character of the slice. `RE_OP_REF_GROUP_FLD`
has no leftovers loop and backtracks (`:14154`).

**Suggested fix.** In the leftovers, a deletion takes back the last comparison into the folding:
when `new_string_pos` is at the end of the item (at 0 when reversed), move `new_folded_pos` back
by `step` instead. Repeated, it returns the folding to its start, so the item ends before that
subject character, and every comparison given back costs one deletion. For the loop to stop
there, its condition has to be entry 28's `0 < folded_pos && folded_pos < folded_len`. Refuse the
take-back when an insertion or substitution was made in the same folding; that edit was charged
for a character that would then fall outside the match. Because `text_pos` moves only when a
folding is used up, those edits are the entries the item added to `state->fuzzy_changes` whose
position is the current `text_pos`. Only the item's own entries count: in
`(s)(?=(?:x){s<=1})(?:\1){d<=1}` over 'sß' the lookahead's substitution is recorded at the same
position, before the backreference starts. So the item needs the change count it began with. The
same deletion in the backreference leftovers loop entry 29
suggests replaces the refusal suggested there.

With this change a C# port of the module gives (0, 1) with one deletion over 'ßß', the mirror
answer reversed, (0, 1) for the backreference, and a single deletion for the free-deletion case.
The whole of its ported copy of `test_regex.py` still passes. `tools/probes/s85-leftover-take-back.py`
in the reporter's repository prints every case above.
