# DRAFT - NOT FILED

Ledger entry 55. Written on 2026-09-30 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A call to the whole pattern inside a pattern that is one fuzzy section never returns

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> m = regex.fullmatch(r'(?:b||b(?0)*){e<=2}', 'azx'); m.span(), m.fuzzy_counts
((0, 3), (2, 1, 0))
>>> m = regex.fullmatch(r'(?:|z?(?R)?a){e<=3}', 'aaba'); m.span(), m.fuzzy_counts
((0, 4), (1, 3, 0))
>>> regex.search(r'(?:z(?R)|){e<=1}', 'bz')
MemoryError
>>> m = regex.fullmatch(r'(?:|z?(?R)?a){e<=3}x', 'aabax'); m.span(), m.fuzzy_counts
((0, 5), (2, 1, 0))
```

The first two matches report three and four errors under limits of two and three. In the first,
only `b` can match any of 'a', 'z' and 'x', so no match of 'azx' fits in two errors and the answer
should be None. With `(?b)` added the same call reports (0, 3) with one error. The third recurses
until it runs out of memory. The fourth is the second with a character after the section, and it
is right.

The cause is that `_compile` and `_check_group_features` disagree on whether the pattern is a fuzzy
section. `_main.py:577` computes `fuzzy = isinstance(parsed, _Fuzzy)` before `parsed.optimise`,
when the pattern is still a one-item `Sequence`, so `fuzzy` is False and the whole-pattern key
`(0, reverse, fuzzy)` at line 627 is not the call's key. `_check_group_features`
(`_regex_core.py:4436`) runs after optimising, sees the `Fuzzy`, and decides that the pattern
already has the call's features, so it adds no copy. The call is left with no `CALL_REF` node at
all: `RE_OP_GROUP_CALL` falls back to `start_node` (`_regex.c:13394`), and the code there ends in
`SUCCESS`, not `GROUP_RETURN`. So a match can end inside the call, with the outer instance of the
fuzzy section still open. Its errors are never added to the total and its limits are never checked.

A possible fix, in `_main._compile`: compute `fuzzy` after optimising.

```python
    parsed = parsed.optimise(info, reverse)
    parsed = parsed.pack_characters(info)
    fuzzy = isinstance(parsed, _Fuzzy)
```

Then the pattern gets the same `CALL_REF ... END` wrapper as a pattern with no fuzzy section. Tried
on a copy of 2026.9.10 with only that change (2026-09-30): the second pattern gives (0, 4) with
counts (1, 2, 0), three errors, and the fourth is unchanged. The first and third then recurse
without end (a 2-second timeout, and MemoryError), because nothing stops a call from re-entering
itself at the same position - the separate infinite-recursion problem of issues 551 and 554. Where
the recursion is bounded, they give None and (0, 2) with counts (1, 0, 0). The compiled code of a
pattern that is not one fuzzy section is unchanged.
