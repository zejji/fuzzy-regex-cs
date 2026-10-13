# DRAFT - NOT FILED

Ledger entry 82. Written on 2026-10-12 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A lazy repeat before a full-folded literal reports a partial match where none can start

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?if)a*?b', '-', partial=True)
<regex.Match object; span=(0, 1), match='-', partial=True>
>>> regex.match(r'(?if)a*?b', '-', partial=True)
<regex.Match object; span=(0, 1), match='-', partial=True>
>>> regex.search(r'(?i)a*?b', '-', partial=True)
<regex.Match object; span=(1, 1), match='', partial=True>
>>> regex.match(r'(?if)a*?b', '-c', partial=True)
>>>
```

Neither `a` nor `b` matches '-', so no longer text that begins with '-' can complete `a*?b` from
position 0. The partial match at 0 promises a completion that cannot exist. Without FULLCASE the
answer is (1, 1), and with one more character ('-c') the match is None.

The cause seems to be the LAZY_REPEAT_ONE backtrack arm for a STRING_FLD tail
(`_regex.c:16764-16767`). It looks for the tail from `pos + 1` and returns RE_ERROR_PARTIAL as soon
as `string_search_fld` reports running out of text, before the loop below it (`:16776-16786`)
checks that the repeat can take the characters between `pos` and the tail. Over '-' that search
starts at the end of the text. The STRING arm clamps `limit` to `slice_end - length` first, so its
search is empty there. The reversed arm (`:16819-16822`) has the same order.

A possible fix: when the search reports a partial, run the same `match_one` check over
`pos .. found` before returning RE_ERROR_PARTIAL.

The same arm has two other problems with a full-folded tail, reported separately (the lazy repeat
misses a literal that starts at its maximum; the greedy repeat misses one whose text is shorter
than its folding).

---

## Notes (not part of the report)

- Found by oracle seed 7 row 35060 (`(\p{L}*?)(?P<g2>[\U0001F3FB])\2\K$` under
  IGNORECASE|FULLCASE over '‍', partial search sliced [0, 1)), red on the schedule on
  2026-09-28, 2026-10-05 and 2026-10-12. Minimised and judged 2026-10-12.
- PCRE2 10.47 under PARTIAL_SOFT gives no partial for `(?i)a*?b` over '-'.
- This port answers (1, 1); pinned by
  `RepeatTests.A_lazy_repeat_before_a_full_folded_literal_reports_a_partial_only_where_one_can_start`.
- The line numbers were read, not traced in a debugger.
