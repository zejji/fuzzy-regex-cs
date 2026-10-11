# DRAFT - NOT FILED

Ledger entry 47. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A `(*PRUNE)` or `(*SKIP)` inside an atomic group that has not finished fails only the group, where PCRE2 and Perl fail the attempt

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?>a(*PRUNE)b)|a', 'ac').span()
(0, 1)
>>> regex.search(r'(?>aa(*SKIP)b)|a', 'aaca').span()
(0, 1)
>>> regex.search(r'(?=a(*PRUNE)b)..|a', 'ac').span()
(0, 1)
>>> regex.search(r'(?!(?>a(*PRUNE)b)|a)a', 'ac')
>>>
```

PCRE2 10.47 and Perl 5.42.3 answer `None`, `(3, 4)`, `None` and `(0, 1)`. In the first row, 'a'
passes `(*PRUNE)`, then 'b' fails against 'c' and the match backtracks onto the verb. The group has
not finished, so nothing has yet promised that backtracking will not enter it, and the verb does what
it does anywhere else: the attempt at 0 ends, the second alternative is not tried there, and nothing
matches at 1.

The README says a verb "used in an atomic group or a lookaround ... won't affect the enclosing
pattern". pcre2pattern ("Verbs that act after backtracking") explains that confinement differently:
"its effect is confined to that group, because once the group has been matched, there is never any
backtracking into it". That covers a group that has finished. For one that has not, PCRE2 unwinds to
the innermost negative assertion, which becomes true, or conditional test, which becomes false if
positive ("Backtracking verbs in assertions"), and otherwise fails the attempt. The fourth row shows
the difference: the verb should cross the atomic group and make the negative lookahead true, but
`regex` stops at the atomic group, so the lookahead's second branch `a` matches and the lookahead
fails.

**Cause.** `push_bstack` marks the backtrack stack at every atomic group, conditional and lookaround
(`_regex.c:12050`, `:12238`, `:13787`), and `RE_OP_PRUNE` and `RE_OP_SKIP` cut back to the innermost
mark (`top_bstack`, `:2811`), whatever kind it is.

**Suggested fix.** When the innermost mark belongs to an atomic group or positive lookaround, let the
verb push a backtrack entry instead of cutting, and when backtracking reaches it, drop marks until
one belongs to a negative lookaround, a conditional or the attempt, restore the slice the crossed
lookarounds widened, and cut to that mark. Patterns without verbs are unaffected.

If the README's composable scopes are the intended design, the README could instead say so
explicitly, including that it differs from PCRE2 and Perl for groups that have not finished.
