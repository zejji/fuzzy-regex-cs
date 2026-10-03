# DRAFT - NOT FILED

## Should a failed negative condition keep the captures its test made?

regex 2026.9.10, CPython 3.14, Windows.

A question rather than a bug report: the README does not say what happens to the captures made
inside a conditional's lookaround test, and the two forms of the same question answer differently.

```python
>>> import regex
>>> regex.search(r'(?(?=(a))\1b|x)', 'ab')
<regex.Match object; span=(0, 2), match='ab'>
>>> print(regex.search(r'(?(?!(a))x|\1b)', 'ab'))
None
```

Both ask "is `(a)` here?" with the branches swapped. When the positive test holds, group 1 is kept
for the yes branch. When the negative test fails, because `(a)` matched, the no branch runs but group
1 has been reset, so `\1` fails. Nothing has backtracked at that point.

PCRE2 documents keeping them (pcre2pattern, "Assertions"): "If such an assertion is being used as a
condition in a conditional group, captured substrings are retained, because matching continues
with the 'no' branch of the condition." PCRE2 10.47 and Perl 5.42.3 both answer (0, 2) with group 1
= 'a' for the second search, and the same for the negative lookbehind form
`regex.search(r'(?(?<!(a))x|\1b)', 'aab')`, which is (1, 3) there and None here.

The reset is in `END_CONDITIONAL` in `_regex.c` (around line 12423): for a negative test whose body
matched, it pops the captures saved at `CONDITIONAL` together with the repeats and fuzzy counts
before going to the no branch. Leaving the captures in place, and keeping the saved copy on the
backtrack stack for when the no branch fails, would give PCRE2's answer. If the current behaviour is
intended, a line in the README would settle it.
