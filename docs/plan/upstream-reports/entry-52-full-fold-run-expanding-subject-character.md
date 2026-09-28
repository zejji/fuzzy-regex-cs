# DRAFT - NOT FILED

Ledger entry 52. Written on 2026-09-28 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A fuzzy full-folded run cannot substitute one subject character that expands under folding

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?fi)(?:s){s<=1}sx', 'ǰsx')
<regex.Match object; span=(0, 3), match='ǰsx', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(?fi)(?:ssx){s<=1}', 'asx')
<regex.Match object; span=(0, 3), match='asx', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(?fi)(?:ssx){s<=1}', 'ǰsx')
>>> regex.search(r'(?fi)(?:ssx){s<=1}', 'sǰx')
>>> regex.fullmatch(r'(?fi)(?:fst){i<=1}', 'fßst')
>>>
```

The first and third patterns differ only in how far the fuzzy section reaches. The third allows
every error placement the first allows, so it should match 'ǰsx' at least as well; and the second
shows that replacing one letter of the run with an ordinary character costs one substitution. With
'ǰ' (U+01F0) in its place it finds nothing.

The cause is in `_regex.c`. `STRING_FLD` compares the pattern with the full case folding of each
subject character, and 'ǰ' folds to two characters, 'j' and U+030C. `next_fuzzy_match_string_fld`
(lines 10580-10633) moves one folded character per edit, so replacing a pattern letter with 'ǰ'
costs a substitution for the 'j' and another edit for the U+030C. The same happens to 'ß', the
ligatures, 'ΐ' and 'ŉ' wherever the run's letters take part in some multi-character folding: the
last row above needs two insertions for the one inserted 'ß'. Where the letter is compiled on its
own, the edit is one character and the match is found.

A possible fix: at the start of a subject character's folding, when the folding is longer than one
character, also offer a substitution and an insertion of the whole subject character, after the
existing three kinds. Offer them too when the first folded character matched, so that matching
half of the character does not rule out editing all of it. No match the current edits find is
lost, although the first answer's mix of edits can change: `(?fi)(?:ss){e<=3}` fullmatched over
'jßsß' then reports one substitution and two insertions instead of three insertions.

None of PCRE2, Python `re`, .NET, JavaScript or Perl has fuzzy matching, so there is no second
answer to compare with.
