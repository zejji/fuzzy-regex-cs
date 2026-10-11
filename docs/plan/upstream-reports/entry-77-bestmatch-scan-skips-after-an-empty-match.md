# DRAFT - NOT FILED

Ledger entry 77. Written on 2026-10-10 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## After an empty match, a BESTMATCH scan skips positions and ENHANCEMATCH keeps a worse fit

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> [(m.span(), m.fuzzy_changes) for m in regex.finditer(r'(?b)(?:\ ){d<=1}', ' xyz')]
[((0, 1), ([], [], [])), ((1, 1), ([], [], [1])), ((3, 3), ([], [], [2]))]
>>> [m.span() for m in regex.finditer(r'(?:\ ){d<=1}', ' xyz')]
[(0, 1), (1, 1), (2, 2), (3, 3), (4, 4)]
>>> [m.span() for m in regex.finditer(r'(?b)(?:\ ){d<=1}', ' xyz', overlapped=True)]
[(0, 1), (1, 1), (2, 2), (3, 3), (4, 4)]
>>> regex.split(r'(?b)(?:ab){d<=2}', 'xyz')
['', 'xy', 'z']
>>> regex.split(r'(?:ab){d<=2}', 'xyz')
['', 'x', 'y', 'z', '']
>>> [(m.span(), m.fuzzy_counts) for m in regex.finditer(r'(?e)(?:abc|d){d<=3}', 'xy')]
[((0, 0), (0, 0, 1)), ((1, 1), (0, 0, 3)), ((2, 2), (0, 0, 3))]
>>> [(m.span(), m.fuzzy_counts) for m in regex.finditer(r'(?e)(?:abc|d){d<=3}', 'xy', overlapped=True)]
[((0, 0), (0, 0, 1)), ((1, 1), (0, 0, 1)), ((2, 2), (0, 0, 1))]
```

With `(?b)` the scan skips position 2 and position 4, and the (3, 3) match reports a deletion at 2,
outside its own span. The same pattern without the flag, and the flagged pattern with
`overlapped=True`, find all five positions. `BESTMATCH` is documented as choosing the best match,
so at equal cost it should change which fit is reported, not which positions match. With `(?e)` the
match at (1, 1) keeps three deletions although one deletion (the `d` branch) fits there, as the
overlapped scan shows.

The cause seems to be the rule against two empty matches in a row. After an empty match the scan
sets `must_advance`, and the success test (`_regex.c:15164`) refuses an empty match that ends at
`search_anchor`. `do_best_fuzzy_match` and `do_enhanced_fuzzy_match` re-run the pattern from later
starts while they look for a better fit, and `init_match` sets `search_anchor` to each run's own
start, while `must_advance` stays set. So the rule becomes "no empty match at any candidate's start"
rather than "no empty match where the scan resumed". In the widened-slice fallback of
`do_best_fuzzy_match` the re-run can then slide to a later position, and the change list copied from
the first candidate no longer belongs to the reported span, which is where the deletion at 2 comes from.

A possible fix: in both functions, set `must_advance` for a re-run only when it starts at the
position the scan resumed from. An overlapped scan clears `must_advance` and is unaffected.
