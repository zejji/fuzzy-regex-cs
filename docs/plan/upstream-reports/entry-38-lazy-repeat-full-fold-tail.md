# DRAFT - NOT FILED

Ledger entry 38. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A lazy repeat before a full-case-folded literal misses matches

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex, re
>>> regex.search(r'(?V1)(?i)[^k]??ss', 'ass')
<regex.Match object; span=(1, 3), match='ss'>
>>> regex.match(r'(?V1)(?i)[^k]??ss', 'ass')
>>> re.search(r'(?i)[^k]??ss', 'ass')
<re.Match object; span=(0, 3), match='ass'>
>>> regex.search(r'(?V1)(?i)a{0,2}?ss', 'aass')
>>> regex.search(r'(?V1)(?i)[^k]?ss', 'ass')
<regex.Match object; span=(0, 3), match='ass'>
```

The lazy repeat may take the 'a', so the match at 0 exists, and the greedy form finds it. So does
the same pattern under V0 without FULLCASE. `regex.match` returns None, so this is not a search
optimisation. The reversed form misses too: `(?r)(?V1)(?i)ss[^k]??` over `'ssa'` gives (0, 2),
where (0, 3) is the answer. Perl 5.42.3 and `re` give (0, 3) for the first pattern and (0, 4) for
the second.

**Cause.** When the lazy repeat takes one more character, the `RE_OP_LAZY_REPEAT_ONE` backtrack arm
looks ahead for the literal that follows it. The `RE_OP_STRING` arm calls
`string_search(state, test, pos + 1, limit + length, ...)` (`_regex.c:16709`). The
`RE_OP_STRING_FLD` arm calls `string_search_fld(state, test, pos + 1, limit, ...)` (`:16764`),
without the length, and `string_search_fld` treats `limit` as the end of the text it may read
(`:6674`). `limit` is the last position the repeat may reach, so a literal that starts there is
never read to its end. The reversed arm (`:16808` against `:16819`) has the same gap.

**Suggested fix.** Let the STRING_FLD arms read past `limit` by the extent of the literal, as the
STRING arms do, still clamped to the slice end (`:16752`). A C# port of the module, which tries
the tail at each position in turn rather than searching for it, answers every case above as `re`
and Perl do.
`tools/probes/upstream-lazy-repeat-full-fold-tail.py` in the reporter's repository prints every
case above.
