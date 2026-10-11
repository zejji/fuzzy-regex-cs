# DRAFT - NOT FILED

## An alternation at the start of a negative condition's yes branch keeps only its first alternative

regex 2026.9.10, CPython 3.14, Windows.

When a condition's test is a negative lookaround and the yes branch starts with an alternation, only
the first alternative is ever tried:

```python
>>> import regex
>>> print(regex.search(r'(?(?!a)(?:x|))x', 'x'))
None
>>> print(regex.search(r'(?(?<!q)(?:xz|x))c', 'xc'))
None
>>> regex.search(r'(?(?=x)(?:x|))x', 'x')
<regex.Match object; span=(0, 1), match='x'>
```

The test holds in the first two, so the yes branch runs, and its second alternative (empty, or 'x')
leaves the subject needed by what follows. PCRE2 10.47 and Perl 5.42.3 answer (0, 1) and (0, 2).

The cause is in `skip_one_way_branches`. The check for a CONDITIONAL's true branch reads

```c
next = node->nonstring.true_node;
if (next && next->op == RE_OP_BRANCH &&
  !next->nonstring.true_node) {
```

The second `true_node` is the BRANCH's own, which is never set, so every BRANCH there counts as
1-way and is skipped to its first exit. The checks for `next_1` and `next_2` above it test
`!next->nonstring.next_2.node`. A positive test is unaffected because it reaches the yes branch
through `next_1` of the END_CONDITIONAL node.

A possible fix is to test `!next->nonstring.next_2.node` there too.
