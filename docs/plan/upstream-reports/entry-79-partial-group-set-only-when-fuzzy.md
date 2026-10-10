# DRAFT - NOT FILED

Ledger entry 79. Written on 2026-10-10 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A partial match sets a group when the pattern is fuzzy, and leaves it unset when it is not

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> m = regex.match(r'(?:a(\w*))x', 'ab', partial=True); m.span(), m.span(1), m.partial
((0, 2), (-1, -1), True)
>>> m = regex.match(r'(?:a(\w*)){e<=0}x', 'ab', partial=True); m.span(), m.span(1), m.partial
((0, 2), (-1, -1), True)
>>> m = regex.match(r'(?:a(\w*)){s<=1}x', 'ab', partial=True); m.span(), m.span(1), m.partial
((0, 2), (1, 2), True)
>>> m = regex.match(r'(?:a(\w*)){d<=0}x', 'ab', partial=True); m.span(), m.span(1), m.partial
((0, 2), (1, 2), True)
>>> m = regex.match(r'(?:a(\w*)){s<=1}x', 'abcx'); m.span(), m.span(1), m.fuzzy_counts
((0, 4), (1, 3), (0, 0, 0))
```

The four partial matches have the same span and use no errors, yet group 1 is unset in two of
them and (1, 2) in the other two. Only the error constraint differs, and `{d<=0}` permits no
deletion at all, so an allowance that is never used changes the captures.

The unset answer looks like the right one. The text 'ab' is completed by 'abx', where group 1 is
(1, 2), but also by 'abcx', where it is (1, 3), so (1, 2) is a value the rest of the text can still
change. The exact pattern already answers this way.

The cause has not been traced in `_regex.c`.

---

## Notes (not part of the report)

- Found by the oracle sweep of 2026-10-09 (seed 774958321, row 25315, judged row cause "G10 + ledger
  5"). A blind review on 2026-10-09 judged the unset answer right.
- This port leaves group 1 unset in all four spellings, by D62's rule (a partial reports only what
  every completion of its path would report) and the D67 fix (entry 74). No port change is needed;
  the judged row pins both answers.
- SHORTCUT: no upstream root cause yet. Before filing, trace which fuzzy path reaches the end of the
  text after the group closes (likely the fuzzy retry of `\w*`'s tail), so the report can name it.
