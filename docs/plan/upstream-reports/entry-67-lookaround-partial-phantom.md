# DRAFT - NOT FILED

Ledger entry 67. Written on 2026-10-04 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A partial match is reported when a lookaround's body runs out of text, whatever follows

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.match(r'(?=..)[ab]', 'x', partial=True)
<regex.Match object; span=(0, 1), match='x', partial=True>
>>> regex.match(r'(?!.a|x)x', 'x', partial=True)
<regex.Match object; span=(0, 1), match='x', partial=True>
>>> regex.match(r'(?!(?:ab)+)ab', 'ab', partial=True)
<regex.Match object; span=(0, 2), match='ab', partial=True>
>>> regex.match(r'(?!ab|a)', 'a', partial=True)
<regex.Match object; span=(0, 1), match='a', partial=True>
```

The README defines a partial match as one where "a complete match could be possible if the string
had not been truncated" (`README.rst:270`). None of these can be completed by any longer text. In
`(?=..)[ab]` the `[ab]` after the lookahead fails on the held 'x' whatever follows. In `(?!.a|x)x`
the branch `x` matches the held text, so the negative lookahead fails for every continuation.

The cause is that every `return RE_ERROR_PARTIAL` path in `basic_match` (the try-match sites at
`_regex.c:12571-12597`, `:13235-13258` and `:12755-12780` and their lazy twins) ends the whole match
with a partial, with no notion of the lookaround the node sits in. A repeat's or a branch's test
node reaches the same return, which is why `(?=(?:ab)+$)(*FAIL)` over 'ab' is a partial too.

A possible fix: a run-out inside a lookaround body should not end the match. Treat the body's result
as unknown, go on after the lookaround, and report a partial only if the rest of the pattern can
succeed for some continuation of the text.
