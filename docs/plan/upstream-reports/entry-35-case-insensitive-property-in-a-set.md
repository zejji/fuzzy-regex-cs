# DRAFT - NOT FILED

Ledger entry 35, parts C and H. Written on 2026-09-28 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A case-insensitive `\p{Ll}` refuses a capital with no lower-case partner inside a set, and in the first-set check

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

Under IGNORECASE a bare `\p{Ll}` matches any cased letter, including a capital that has no
lower-case form, such as U+2102 DOUBLE-STRUCK CAPITAL C:

```python
>>> import regex
>>> regex.fullmatch(r'(?i)\p{Ll}', '\u2102')
<regex.Match object; span=(0, 1), match='ℂ'>
>>> regex.fullmatch(r'(?i)[\p{Ll}x]', '\u2102')
>>> regex.search(r'(?i)\p{Ll}?a{2}', '\u2102aa').span()
(1, 3)
>>> regex.search(r'(?i)\p{Ll}?a{2}', 'Aaa').span()
(0, 3)
```

The second answer should be a match and the third `(0, 3)`. The third pattern has no set in it.

**Cause.** A property inside a case-insensitive set is checked by `matches_member_ign`
(`_regex.c`), which tests the plain property against each case variant of the character. The bare
property goes through `matches_PROPERTY_IGN`, which treats Lu, Ll and Lt as "any cased letter".
U+2102 has no case variants and is not Ll, so only the bare form accepts it.

The third pattern reaches the set rule because its first item is optional. `_check_firstset`
(`_regex_core.py`) then collects every item that could start the match, here `\p{Ll}` and `a`, into
one case-insensitive set, and the compiled pattern checks each start position against it before
matching. That check refuses U+2102, so the search starts one character later. Stopping
`_compile_firstset` from returning a check gives `(0, 3)`:

```python
>>> import regex._main
>>> regex._main._compile_firstset = lambda info, fs: []
>>> regex.purge()
>>> regex.search(r'(?i)\p{Ll}?a{2}', '\u2102aa').span()
(0, 3)
```

The same happens reversed (`regex.search(r'(?ri)a{2}\p{Ll}?', 'aa\u2102')` is `(0, 2)`, not
`(0, 3)`), and to `\p{Lu}` and `\p{Lt}`: `regex.fullmatch(r'(?i)[\p{Lu}x]', '\u0138')` and
`regex.fullmatch(r'(?i)x?\p{Lt}', 'a')` are None, while the bare forms match.

Perl 5.42, PCRE2 10.47 and .NET 10 answer the bare and the set form the same way, accepting U+2102
for a case-insensitive `\p{Ll}`. JavaScript's `/\p{Ll}/iu` refuses it in both forms. Either rule
could be defended, but a pattern should not get a different answer depending on whether the
property sits in brackets, or on whether an optimisation hoisted it into a set.

**Suggested fix.** In `matches_member_ign`, answer a `PROPERTY` member the way
`matches_PROPERTY_IGN` answers a bare property, once for the character itself, instead of testing
the plain property against each case variant:

```c
case RE_OP_PROPERTY:
    return matches_PROPERTY_IGN(encoding, locale_info, member, ch);
```

This needs the character itself passed down beside `cases`. The first-set check then agrees with
the matcher with no change of its own.
