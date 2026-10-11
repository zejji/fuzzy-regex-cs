# DRAFT - NOT FILED

Ledger entry 80. Written on 2026-10-11 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A greedy repeat before a full-folded literal misses it when the text holds `ß` or a ligature

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.fullmatch(r'(?fi)\w*ßa', 'ßa')
>>> regex.search(r'(?fi)\w*ßa', 'xxßa')
>>> regex.fullmatch(r'(?fi)\w*ffi', 'ﬃ')
>>> regex.fullmatch(r'(?fi)(?r)ßa\w*', 'ßa')
>>> regex.fullmatch(r'(?fi)ßa', 'ßa')
<regex.Match object; span=(0, 2), match='ßa'>
>>> regex.fullmatch(r'(?fi)\w*?ßa', 'ßa')
<regex.Match object; span=(0, 2), match='ßa'>
>>> regex.fullmatch(r'(?fi)\w*ßa', 'ssa')
<regex.Match object; span=(0, 3), match='ssa'>
```

`\w*` can match nothing, so `\w*ßa` must match wherever `ßa` does. The bare literal and the lazy
repeat both find it; the greedy repeat does not, and only when the text holds the expanding
character itself. With the folded text 'ssa' it matches.

The cause seems to be the GREEDY_REPEAT_ONE backtrack arm for a STRING_FLD tail
(`_regex.c:16118-16129`; the reversed arm is at `:16066-16080`). It adds up the tail's folded
length, 3 for `ßa`, and uses it as a text offset: `pos = max(pos + 1, slice_start + folded_length)`,
then searches from `pos - folded_length`. In the text 'ßa' the literal spans two positions, not
three, so `pos` passes `limit` and the search never runs.

A possible fix: bound the start by the fewest text positions the tail can occupy (each pattern
character's folding can match one text character), not by its folded length.

The lazy arm has a related problem with the same tail, reported separately (the lazy repeat misses
a full-folded literal that starts at its maximum).

---

## Notes (not part of the report)

- Found by the confirmation sweep of 2026-10-11 (seed 554621207 row 27187, `(?fi)(?r)ﬃﬃ\p{Lu}*`
  over 'ﬃﬃ', fullmatch: upstream None, here (0, 2)). Judged by a subagent and re-run on both engines
  by the orchestrator. The line numbers were read, not traced in a debugger.
- This port answers right on every line; pinned by
  `RepeatTests.A_greedy_repeat_gives_back_to_a_full_folded_literal_whose_text_is_shorter_than_its_folding`.
- Python `re` (simple folding only) matches `(?i)\w*ßx` over 'aßx'. PCRE2 was not available for a
  survey; the argument stands on the bare-literal equivalence above.
