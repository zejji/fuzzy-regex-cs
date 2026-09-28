# DRAFT - NOT FILED

Ledger entry 51. Written on 2026-09-28 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A fuzzy section below its minimum error count never tries the insertion that would meet it

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.match(r'(?:ab){1<=e<=2}c', 'abxc')
<regex.Match object; span=(0, 4), match='abxc', fuzzy_counts=(0, 1, 0)>
>>> print(regex.match(r'(?:a){1<=e<=1}c', 'axc'))
None
>>> print(regex.match(r'(?:a){1<=e<=2}b', 'aab'))
None
>>> print(regex.fullmatch(r'(?:a){1<=e<=2}', 'aa'))
None
>>> regex.search(r'(?:a){1<=e<=2}b', 'aab')
<regex.Match object; span=(2, 3), match='b', fuzzy_counts=(0, 0, 1)>
```

The first two lines are the same situation: the section matches exactly, and inserting the `x`
after it gives the one error the minimum asks for. Upstream finds it after a two-character string
but not after a single character. The third and fourth lines are the same again, with an `a`
inserted after the section's `a`, so `match` should give (0, 3) and `fullmatch` (0, 2), each with
one insertion. The last line is the same pattern as the third, and `search` passes over that match
at 0 and reports a deletion at 2.

**Cause.** The forward `RE_OP_END_FUZZY` case checks the section's constraints, minimums included,
with `fuzzy_within_constraints` (`_regex.c` 12461-12462) and goes to `backtrack` when they fail.
The entry whose backtrack case tries one more insertion after the section's last item
(15512-15563) is pushed only after that check (12500-12511). So a section that ends below its
minimum is failed before the one error that could still meet the minimum has been tried. A string
avoids this because it pushes its own insertion retry before the section ends (`fuzzy_insert`,
14764-14768).

**Suggested fix.** In the forward `RE_OP_END_FUZZY` case, when the constraints fail only because
an insertion or error minimum is unmet, and would hold with enough more insertions, push the
entry as usual and then go to `backtrack`, so that its backtrack case tries a trailing insertion.
In that backtrack case, after each insertion, check the constraints again and go to `backtrack`
while they still fail, which tries one more. A substitution or deletion minimum cannot be met by
insertions, and a maximum already passed cannot come back, so those still fail at once. A C#
port of this engine that does this agrees with a small reference backtracking matcher on 6,000
generated cases with minimum error counts.
