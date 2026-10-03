# DRAFT - NOT FILED

## A fuzzy budget can remove a match when the pattern has a lookaround conditional

regex 2026.9.10, CPython 3.14, Windows.

Adding a fuzzy constraint around a conditional can turn a match into no match:

```python
>>> import regex
>>> regex.search(r'(?(?=a)ab|b)', 'b')
<regex.Match object; span=(0, 1), match='b'>
>>> print(regex.search(r'(?:(?(?=a)ab|b)){s<=1}', 'b'))
None
>>> regex.search(r'(?(?<!a)a*|a)', 'aa')
<regex.Match object; span=(0, 2), match='aa'>
>>> regex.search(r'(?:(?(?<!a)a*|a)){e<=2}', 'aa')
<regex.Match object; span=(0, 1), match='a'>
```

I expected a budget only to allow more matches, so the second search should still find 'b'.

The condition's test is compiled with the enclosing section's fuzziness, so `(?=a)` "holds" at 'b'
by a substitution. The yes branch `ab` then cannot fit, and as the test is atomic it is not tried
again exactly, so the no branch is never reached. A plain lookaround is not affected, because
`LookAround._compile` compiles its body without fuzziness:

```python
>>> regex.search(r'(?:(?=a)ab|b){s<=1}', 'b')
<regex.Match object; span=(0, 1), match='b'>
```

The difference is in `_regex_core.py`: `LookAroundConditional._compile` calls
`self.subpattern.compile(self.behind, fuzzy)` (line 3267), where `LookAround._compile` calls
`self.subpattern.compile(self.behind)` (line 3201).

A possible fix is to compile the test as a lookaround's body is compiled,
`self.subpattern.compile(self.behind)`, and to fix its group calls with `fuzzy=False` in
`fix_groups` to match. With that change the searches above give (0, 1) and (0, 2), and a fuzzy
section written inside the test, such as `(?(?=(?:ab){e<=1})...)`, still works as before.
