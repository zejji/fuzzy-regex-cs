# DRAFT - NOT FILED

Ledger entry 78. Written on 2026-10-10 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## Under BESTMATCH and ENHANCEMATCH, `\G` matches at later positions than where the search began

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> m = regex.search(r'(?b)\G(?:abc){e<=2}', 'xabc'); m.span(), m.fuzzy_counts
((1, 4), (0, 0, 0))
>>> m = regex.match(r'(?b)(?:abc){e<=2}', 'xabc'); m.span(), m.fuzzy_counts
((0, 4), (0, 1, 0))
>>> m = regex.search(r'(?b)\G(?:abc){e<=2}', 'zxabc', pos=1); m.span()
(2, 5)
>>> [m.span() for m in regex.finditer(r'(?b)\G(?:abc){e<=2}', 'abcxabc')]
[(0, 3), (4, 7)]
>>> m = regex.search(r'(?e)(?<=\G.)(?:bcd|bc){e<=1}', 'abcx'); m.span(), m.fuzzy_counts
((1, 4), (1, 0, 0))
>>> m = regex.search(r'(?e)(?<=^.)(?:bcd|bc){e<=1}', 'abcx'); m.span(), m.fuzzy_counts
((1, 3), (0, 0, 0))
```

`\G` matches where the search began, so a search from 0 cannot return a match that needs `\G` at
1, and a `finditer` whose second search began at 3 cannot return a second match at 4. With `(?b)`
or `(?e)` it does both. The `^` spelling, which means the same thing at 0, answers as expected.

The cause seems to be that `\G` tests `state->search_anchor` (`try_match_SEARCH_ANCHOR`,
`_regex.c:7288`), which `init_match` sets to the current position (`_regex.c:3410`).
`do_best_fuzzy_match` and `do_enhanced_fuzzy_match` call `init_match` again for every candidate
they re-run, from that candidate's own start, so `\G` then holds at each candidate's start.

A possible fix: record the position of the first `init_match` of the operation and let `\G` (and
the `search_start` check for a leading `\G`) test that. The rule against an insertion where a
search began also reads `search_anchor` (`_regex.c:10214`); it is a separate question whether it
should move with each candidate.
