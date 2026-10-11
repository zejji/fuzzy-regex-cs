# DRAFT - NOT FILED

Ledger entry 45. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## `(*SKIP)` takes effect when it runs, so a `(*PRUNE)` after it cannot decide the next start

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> print(regex.search(r'aa(*SKIP)x(*PRUNE)y|a', 'aaxz'))
None
>>> print(regex.search(r'aa(*SKIP)b(*PRUNE)(*F)|a', 'aab'))
None
>>> print(regex.search(r'(?>aa(*SKIP))x', 'aaax'))
None
```

PCRE2 10.47 (with and without `PCRE2_NO_START_OPTIMIZE`) and Perl 5.42.3 answer `(1, 2)`,
`(1, 2)` and `(1, 4)`.

In the first example the attempt at 0 matches `aa`, passes `(*SKIP)` at 2, matches `x`, passes
`(*PRUNE)`, and fails on `y`. Backtracking reaches the `(*PRUNE)` first, so the attempt ends and
the next one starts one character on, at 1, where `a` matches. The `(*SKIP)` is never
backtracked onto, so it has no effect. pcre2pattern states both rules: these verbs "do nothing
when they are encountered ... if there is a subsequent match failure, causing a backtrack to the
verb, a failure is forced", and "if more than one backtracking verb is present in a pattern, the
one that is backtracked onto first acts". perlre describes `(*SKIP)` the same way: it acts "on
failure". In the third example the `(*SKIP)` is inside an atomic group that has finished, so
nothing can backtrack onto it and the next attempt starts at 1. The README says the same of
`(*SKIP)` "used in an atomic group": "it won't affect the enclosing pattern".

**Cause.** `RE_OP_SKIP` in `_regex.c` sets `state->slice_start` (or `slice_end` when reversed)
as soon as the verb runs, and the search loop starts the next attempt at `slice_start`. By the
time the `(*PRUNE)` acts, the next start is already 2, and position 1 is never tried.

**Suggested fix.** Record the position instead of moving the slice: after `top_bstack`, push the
text position and `RE_OP_SKIP` on the backtracking stack, and set `slice_start` (or `slice_end`)
in a new `RE_OP_SKIP` case of the backtracking switch. A later `(*PRUNE)` or `(*SKIP)` prunes
the entry away and decides for itself, the end of an atomic group or lookaround discards it, and
a successful match never pops it. A `(*SKIP)` that backtracking does reach behaves as it does
today: `regex.search(r'aa(*SKIP)x|a', 'aab')` stays `None`.
