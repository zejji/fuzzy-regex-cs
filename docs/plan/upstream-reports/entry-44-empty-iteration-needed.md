# DRAFT - NOT FILED

Ledger entry 44. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.
It supersedes the draft for entry 33, which reported only the MemoryError.

---

## A fuzzy repeat counts deleting iterations as progress, so its error count depends on what follows, and some repeats never stop

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?:[0-9]+){d<=2}', '42').fuzzy_counts
(0, 0, 0)
>>> regex.search(r'(?:[0-9]+){d<=2}', '42kg').fuzzy_counts
(0, 0, 2)
>>> regex.search(r'(?:(?:[0-9]+,){d<=1})+end', '12,end').fuzzy_counts
(0, 0, 1)
>>> regex.search(r'(?:(?:[0-9]+,){d<=3})+end', 'end')
MemoryError
>>> regex.search(r'(?:[0-9]+){1<=d<=2}', '42').span()
(1, 2)
>>> regex.fullmatch(r'(?:(?:b?)*){d<=1}', 'a')
MemoryError
```

Both texts in the first two lines contain the digits `42` exactly, but the second match is
charged two deletions: after `42` the repeat goes round twice more, each time pretending a digit
was missing. At the end of the text it does not. The third line reports an exact match as having an
error. The fourth runs out of memory where the answer is `end` with two deletions. The fifth skips
the match at the start of `42`, which its "at least one deletion" constraint allows with one
missing digit after `42`.

**Cause.** A fuzzy edit increments `capture_change` (`_regex.c` around line 10487), and
END_GREEDY_REPEAT counts an iteration as progress when `capture_change` or the text position moved
(around line 12550). So an iteration that matched nothing by deleting counts as progress, and so
does one whose edits were made and then undone. The only fuzzy exception stops the repeat at the
end of the slice. Where a fuzzy section inside the repeat starts each iteration with a fresh
budget, nothing stops it.

**Suggested fix.** Treat an iteration that matched no text and spent errors as progress only when
something needs it: the repeat is below its minimum, or its deletions raise a minimum error count
of an enclosing section that is not yet met, or it changed the span of a group that a
backreference or conditional tests. Otherwise fail it. Keep the existing rule for empty iterations
that spent no errors. To keep nested repeats fast, record the state after each iteration of a
repeat (text position, count clipped at the minimum when there is no maximum, the open section's
error counts, the tested groups' spans) and drop a path that reaches a recorded state: it has the
same future as the earlier path, which was explored first. That is the existing repeat guard,
which `is_repeat_guarded` turns off for fuzzy patterns because a position alone is not a state.

A C# port of this engine does this. It
agrees with a reference backtracking matcher on 79,382 generated cases without a minimum error
count, answers all the lines above at once, and keeps the families that grow exponentially here,
such as `fullmatch('(?:(?:a|b|c|d)*){n<=d<=n}', 'x')`, polynomial. The analysis of the options,
and how other engines treat empty iterations, can be supplied on request.
