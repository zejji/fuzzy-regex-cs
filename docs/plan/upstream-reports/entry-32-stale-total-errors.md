# DRAFT - NOT FILED

Ledger entry 32. Written by S87 on 2026-09-23 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## BESTMATCH search never returns after a nested fuzzy section is rejected

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')
# never returns
>>> regex.search(r'(?b)(?:(?:a(?:x+?)){e<=2}|2)', '2y')
<regex.Match object; span=(0, 1), match='2'>
```

The only difference between the two patterns is the inner `{s<=1}`, which cannot change what the
pattern matches: the answer is the exact '2' either way. A constrained form hangs as well:
`(?b)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)` over `'2\n'`.

ENHANCEMATCH gives a worse answer instead of hanging:

```python
>>> regex.search(r'(?e)(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')
<regex.Match object; span=(0, 2), match='2y', fuzzy_counts=(2, 0, 0)>
>>> regex.search(r'(?e)(?:(?:a(?:x+?)){e<=2}|2)', '2y')
<regex.Match object; span=(0, 1), match='2'>
>>> regex.search(r'(?e)(?:2|(?:a(?:x+?){s<=1}){e<=2})', '2y')
<regex.Match object; span=(0, 1), match='2'>
```

**Cause.** `state->total_errors` is written at the end of a fuzzy section and not put back when
that section is undone. In the `RE_OP_END_FUZZY` forward arm (`_regex.c:12484`) the total is set
from the section's counts; when it exceeds `max_errors` the arm pushes the outer counts and
backtracks, leaving the total as it is. The backtrack arm (`:15569-15571`) subtracts the inner
counts from `state->fuzzy_counts` and also leaves the total. A match that then succeeds through
`|2` reports a `total_errors` of 2 while its `fuzzy_counts` are (0, 0, 0).

`do_best_fuzzy_match` compares that total with the fewest errors so far (`:17653`). The stale 2 is
no better, so `start_pos` does not advance, and the loop finds the same match again for ever.
`do_enhanced_fuzzy_match` makes the same comparison (`:17939`) and stops trying to improve.

**Suggested fix.** In the END_FUZZY forward arm, keep the old `total_errors` (and `total_cost`)
before overwriting them. Restore both before `goto backtrack` on the over-budget path, and push
them on the bstack with the inner counts, so the backtrack arm can pop and restore them after
subtracting the counts. Recomputing the total from `state->fuzzy_counts` in the backtrack arm is
not enough when sections nest: the counts then still hold the enclosing section's errors, and
nothing resets the total when that section is itself backtracked out of.

With this change a C# port of the module answers (0, 1) with no errors on every pattern above,
and the whole of its ported copy of `test_regex.py` still passes.
`tools/probes/s87-stale-total-errors.py` in the reporter's repository prints every case above,
running each in a child process with a time limit.
