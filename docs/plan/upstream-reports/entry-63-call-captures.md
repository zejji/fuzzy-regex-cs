# DRAFT - NOT FILED

## After a group call, a group can report no value but list a capture

regex 2026.9.10, CPython 3.14, Windows.

A group call such as `(?&name)` or `(?1)` restores the called group's value when it returns, but
leaves the call's captures on the group's capture list. So the value and the list can disagree:

```python
>>> import regex
>>> m = regex.search(r'(?(DEFINE)(?<c>a))(?&c)b', 'ab')
>>> m.group('c'), m.captures('c')
(None, ['a'])
>>> m = regex.search(r'(?<c>a)(?&c)b', 'aab')
>>> m.group('c'), m.captures('c')
('a', ['a', 'a'])
>>> m = regex.fullmatch(r'(?P<x>.)*(?&x)', 'abc')
>>> m.group('x'), m.captures('x')
('b', ['a', 'b', 'c'])
```

In the first, the group has no value but one capture. In the third, the value is not the last
capture. PCRE2 10.47 and Perl 5.42 treat a call as a subroutine and restore the called group on
return, so they report c unset, c = 'a' at 0 and x = 'b', with no trace of the calls' captures.

This looks deliberate: Hg issue 78 asked for captures made in recursive calls, and
`test_hg_bugs` asserts `['(1+0)', '((1+0)+1)', '(((1+0)+1)+1)']` for
`(?<rec>\((?:[^()]++|(?&rec))*\))`. The mechanism is that `GROUP_CALL` saves only each group's
`current` (`push_groups`, `_regex.c:2490`, called at :13405) and `GROUP_RETURN` restores only that
(`pop_groups` at :13516), leaving `count` where the callee left it.

The question is only whether the combination in the first example is intended: a group that
`group()` says did not participate, while `captures()` lists something for it. If captures from
calls are wanted, one option is to document that `captures()` includes them and that the value is
the caller's; another is to restore `count` on return as well, as PCRE2 and Perl do, which would
change the Hg issue 78 test.
