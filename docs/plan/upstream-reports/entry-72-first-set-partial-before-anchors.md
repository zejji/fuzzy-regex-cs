# DRAFT - NOT FILED

Ledger entry 72. Written on 2026-10-08 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A partial search reports a partial that no longer text could complete when the pattern starts with an anchor

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(\A)x', ':', partial=True)
<regex.Match object; span=(1, 1), match='', partial=True>
>>> regex.search(r'^x', ':', partial=True)
>>> regex.search(r'(?r)(?:x)?ac$', 'a', partial=True)
<regex.Match object; span=(0, 0), match='', partial=True>
>>> regex.search(r'(?r)ac$', 'a', partial=True)
```

`(\A)x` can only match at the start of the string, before the ':', so no longer string makes it
match; `^x` correctly answers `None`. Likewise `(?:x)?` cannot change which strings
`(?r)ac$` matches, yet it turns `None` into a partial. `\Gx`, `(?>\A)x` and `(?r)b+$` over ' ' do
the same.

The cause is the first-set check that `_check_firstset` (`_regex_core.py:370-409`) puts in front
of a pattern whose start is not simple. It skips zero-width items, and at the end of the text it
runs out and answers a partial before the anchors it skipped are tested. A group call gives a
pattern no first set, so `(?r)(?<o>(?<i>a|ab)c)(?:(?&o)|c)*$` over 'a' answers `None` while the
same pattern with the call written out answers a partial.

A possible fix: at the end of the text in a partial match, let a zero-width first-set (or branch
precheck) node pass through to the next node instead of returning a partial; the item that reads
the character runs out in its own place.
