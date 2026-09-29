# DRAFT - NOT FILED

Ledger entry 50. Written on 2026-09-28 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A lookaround inside a fuzzy section is never passed by an insertion, although `\b` and `$` are

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?:b\b){i<=1}', 'bx c')
<regex.Match object; span=(0, 2), match='bx', fuzzy_counts=(0, 1, 0)>
>>> print(regex.search(r'(?:b(?=c)){i<=1}', 'bxc'))
None
>>> print(regex.search(r'(?:b(?!x)){i<=1}', 'bxc'))
None
>>> print(regex.search(r'(?:b(?<=x)c){i<=1}', 'bxc'))
None
>>> regex.search(r'(?:ab(?=c)){i<=1}', 'abxc')
<regex.Match object; span=(0, 3), match='abx', fuzzy_counts=(0, 1, 0)>
```

In the first line the inserted `x` moves the word boundary into place, which is one insertion. The
next three are the same situation with a lookaround instead of `\b`: inserting the `x` after the
`b` puts the lookahead in front of the `c` (or the lookbehind after the `x`), so each has a match
with one insertion, `bx` or `bxc`. The last line shows the insertion is found when the lookaround
follows a string of two characters, but not one.

The same holds under `{e<=1}`: `regex.search(r'(?:b(?=c)){e<=1}', 'bxc')` returns (1, 2) with a
substitution, where (0, 2) with one insertion starts earlier.

**Cause.** A failing zero-width item in a fuzzy section goes to `fuzzy_match_item` with a step of 0,
which leaves an insertion as the only possible error (`RE_OP_BOUNDARY`, `_regex.c` 12060-12075;
`RE_OP_END_OF_STRING`, 13052-13062). A lookaround never does: when a positive lookaround's body has
no choices left, the `RE_OP_LOOKAROUND` case of the backtrack switch restores the state and carries
on backtracking (17115-17168), and when a negative lookaround's body matches, `RE_OP_END_LOOKAROUND`
restores the state and goes to `backtrack` (12918-13000).

**Suggested fix.** At both places, after the lookaround's state is restored, call
`fuzzy_match_item(state, search, &node, 0)` with `node` the lookaround node when it has
`RE_STATUS_FUZZY`, and on success match the lookaround again at the new position. The retry entry
needs its own tag, because `RE_OP_LOOKAROUND` already tags the lookaround's own backtrack entry,
and that tag belongs in the zero-width block of the backtrack switch (15330-15344). A C# port of
this engine that does this agrees with a small reference backtracking matcher on 6,000 generated
fuzzy lookaround cases.
