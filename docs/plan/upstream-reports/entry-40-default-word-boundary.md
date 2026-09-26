# DRAFT - NOT FILED

Ledger entry 40. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## The WORD flag's word boundary fails 268 lines of WordBreakTest.txt

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> [m.start() for m in regex.finditer(r'(?w)\b', 'a:\u0308a')]
[0, 1, 4]
>>> [m.start() for m in regex.finditer(r'(?w)\b', '1,\u03081')]
[0, 1, 4]
>>> [m.start() for m in regex.finditer(r'(?w)\b', '\u0300A')]
[0, 2]
>>> [m.start() for m in regex.finditer(r'(?w)\b', '\U0001F1E6A')]
[0, 2]
>>> [m.start() for m in regex.finditer(r'(?w)\b', "'A")]
[0, 2]
```

The README says the `WORD` flag gives "a default Unicode word boundary", and the module's tables
are Unicode 17.0.0. That version's `WordBreakTest.txt` expects `[0, 4]` for the first two and
`[0, 1, 2]` for the other three. Run over the whole file, `(?w)\b` disagrees on 268 of its 1,944
lines. Every one of them comes from `unicode_at_default_boundary` and one of three things:

1. **WB4 is applied to one side only.** UAX #29 says "Ignore Format and Extend characters, except
   after sot, CR, LF, and Newline" (`X (Extend | Format | ZWJ)* → X`). The function skips them
   leftwards from the character before the position, but WB6, WB7, WB7b, WB7c, WB11 and WB12 read
   the character two away without skipping them (`_regex.c:1615` and the reads after it), so the
   U+0308 in `'a:\u0308a'` stops WB6 from seeing the second 'a'. And when the leftward skip reaches
   the start of the text it returns "no break" (`:1595`), where "except after sot" means the
   ignored characters stand for themselves: WB999 breaks between U+0300 and 'A'.
2. **WB15/WB16 do not check the right-hand character.** The rules are `sot (RI RI)* RI × RI` and
   `[^RI] (RI RI)* RI × RI`. The code counts the regional indicators to the left and returns "no
   break" on an odd count whatever follows, so a single regional indicator joins a following
   letter.
3. **WB5a.** Added for Hg issue 219 (2016.8.27), it keeps an apostrophe and a following vowel
   together. It is not one of UAX #29's default rules. The report's notes offer
   `apostrophe ÷ vowels` as an optional tailoring for French and Italian, and that tailoring
   breaks there; the code returns "no break".

**Suggested fix.** Skip Extend, Format and ZWJ when finding the characters two away in either
direction; let a run of them that reaches the start of the text, CR, LF or a Newline stand for
itself instead of returning; require a regional indicator on the right in WB15/WB16; and remove
WB5a, or turn it into the tailoring UAX #29 describes behind its own flag. A C# port of the module
with those four changes passes all 1,944 lines, for `(?w)\b`, `(?w)\B`, `(?w)(?r)\b` and `\m`/`\M`
under `(?w)`.
