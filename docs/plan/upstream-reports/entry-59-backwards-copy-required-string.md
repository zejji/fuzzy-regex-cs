# DRAFT - NOT FILED

Ledger entry 59. Written on 2026-09-30 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A backwards `(?R)` copy skips its required run to the run's forward end

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'aa(?:(?<=a(?R)){i<=1}a|)', 'aaaa')
<regex.Match object; span=(0, 3), match='aaa', fuzzy_counts=(0, 1, 0)>
>>> regex.search(r'aa(?:(?<=a(?R)){i<=1}a|)', 'aaab')
<regex.Match object; span=(0, 3), match='aaa', fuzzy_counts=(0, 1, 0)>
```

The only error is one insertion, yet the match is three characters long: 'aa', one inserted
character, and the final `a`, which would end at 4. The final `a` was read at 2, so the text
position went back after the lookbehind. The `(?R)` inside the lookbehind compiles the whole
pattern again, backwards, and its `aa` is the pattern's required string, so it carries
`RE_STATUS_REQUIRED`. At `state->req_pos` the `STRING_REV` arm skips the comparison and sets
`text_pos = state->req_end` (`_regex.c:15109`), the forward end of the run, where a backwards match
of it must end at `req_pos - 2`.

A possible fix: set `required` only for the compile in the pattern's own direction, for example by
clearing it before `info.additional_groups` are compiled in `_compile`, or by testing the node's
direction beside `RE_STATUS_REQUIRED` in the skip.
