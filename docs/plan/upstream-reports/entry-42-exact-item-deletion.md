# DRAFT - NOT FILED

Ledger entry 42. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A fuzzy item that matched exactly is never tried as a deletion, so matches within the budget are missed

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> print(regex.match(r'(?:a){d<=1}a', 'a'))
None
>>> regex.match(r'(?:a|b){d<=1}a', 'a')
<regex.Match object; span=(0, 1), match='a', fuzzy_counts=(0, 0, 1)>
>>> print(regex.fullmatch(r'(?:ab){d<=1}b', 'ab'))
None
>>> regex.search(r'(?:ab){d<=1}b', 'abxabb').span()
(3, 6)
>>> regex.search(r'(?:(?:a){d<=1}ab|a)', 'ab').span()
(0, 1)
```

In the first line, deleting the fuzzy `a` costs one deletion and leaves the literal `a` to match,
so there is a match within the constraint. The second line finds it, but only because its `b`
branch fails and so tries the deletion. The third and fourth are the same: `ab` with its `b`
deleted is a match at 0. The last returns the second branch where the first branch matches with
one deletion.

The README defines a deletion as a pattern item absent from the text, and its own example,
`fullmatch('(?:cats|cat){e<=1}', 'cat')`, returns the first branch with one deletion rather than
the exact second branch: an item's errors are tried before an earlier choice is retried. It also
promises "the first match that meets the given constraints". Issues #248 and #370 reported
matches missed in this way.

**Cause.** `fuzzy_match_item` tries the errors of an item only when the item fails to match
(`_regex.c` 10185-10258). An item that matches pushes nothing onto the backtracking stack (the
one-character arms around line 11924, the string arms around line 14742), so when the rest of the
pattern then fails, the deletion of that item is never tried.

**Suggested fix.** When a fuzzy item matches exactly and a deletion is still permitted, push the
entry `fuzzy_match_item` would push, marked so that `retry_fuzzy_match_item` goes straight to the
deletion. The search is then the complete, ordered one the README describes. A C# port of this engine
that does this agrees with a small
reference backtracking matcher on 29,125 generated cases and misses no match that an edit-distance
brute force finds. The cost can be kept down by not pushing the entry where it provably cannot
lead to a match not already tried: if a later item of the same run uses the character, matching
the item and deleting the later one instead is an earlier match with no more errors.

Note that this changes some answers that currently look right, towards the ones the README's
order gives: `regex.match(r'(?:ab){e<=2}b', 'bb')` gives (0, 0, 1) today and would give (1, 0, 1),
because deleting the exactly matched `b` comes before going back to the substitution of `a`.
