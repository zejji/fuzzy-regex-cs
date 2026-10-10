# DRAFT - NOT FILED

Ledger entry 76. Written on 2026-10-10 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A (*PRUNE) or (*SKIP) after a lazy leading repeat loses matches that start inside the repeat's run

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> print(regex.search(r'.*?(*PRUNE)b', 'ab'))
None
>>> regex.search(r'x?.*?(*PRUNE)b', 'ab')
<regex.Match object; span=(1, 2), match='b'>
>>> regex.search(r'a*?(*SKIP)(?:b|$)', 'aab')
<regex.Match object; span=(3, 3), match=''>
>>> print(regex.search(r'(?r)b(*PRUNE).*?', 'ba'))
None
```

At 0 the lazy `.*?` takes nothing, `b` fails against 'a', and backtracking reaches `(*PRUNE)`, so
the attempt ends. The next attempt should start at 1, where `b` matches; the `x?` spelling, which
changes nothing about what can match, finds it. PCRE2 10.47 gives (1, 2) for the first pattern and
(2, 3) for the third ("Backtracking control" in pcre2pattern: after `(*PRUNE)` the match advances to
the next start, and a `(*SKIP)` at the attempt's own start is an ordinary advance).

The cause seems to be the skip in `do_match` after a failed attempt (`_regex.c:15705-15723`): when
the pattern starts with a single-character repeat, the next start moves past the whole run the
repeat could match. That is safe when the attempt tried the rest of the pattern after every length
of the run, but a lazy repeat tries the short lengths first, so a verb can end the attempt before the
long ones. `.{0,2}?(*PRUNE)b` finds (1, 2) over 'ab' and `.{0,3}?(*PRUNE)b` finds nothing, which
fits: the skip is taken only when the run is shorter than the repeat's maximum, and over 'ab' the run
of two reaches the first pattern's maximum but not the second's. PCRE2 turns off its own similar shortcut,
`PCRE2_DOTSTAR_ANCHOR`, for any pattern that holds `(*PRUNE)` or `(*SKIP)` (pcre2api).

A possible fix: take the skip after a lazy leading repeat only when the pattern has no `(*PRUNE)` or
`(*SKIP)`. A greedy repeat tries the longest length first, so it can keep the skip.
