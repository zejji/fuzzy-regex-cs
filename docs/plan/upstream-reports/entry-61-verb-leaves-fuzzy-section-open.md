# DRAFT - NOT FILED

## A fuzzy section that (*PRUNE) or (*SKIP) cuts through stays open

regex 2026.9.10, CPython 3.13, Windows.

A verb that is backtracked onto inside a negative lookaround or a condition's test can leave a fuzzy
section opened inside that construct as the open one, and its limits then apply to the rest of the
enclosing section:

```python
>>> import regex
>>> regex.search(r'(?:(?!(?:a(*PRUNE)b){d<=0})cd){e<=2}', 'ad')
<regex.Match object; span=(1, 2), match='d', fuzzy_counts=(1, 0, 1)>
>>> regex.search(r'(?:(?!(?:ab){d<=0})cd){e<=2}', 'ad')
<regex.Match object; span=(0, 2), match='ad', fuzzy_counts=(1, 0, 0)>
```

The lookahead body fails at 'b' with or without the verb, so the two should agree. PCRE2 and Perl
both match `(?!a(*PRUNE)b)ad` against 'ad' at (0, 2).

The cause: backtracking onto the verb cuts the backtracking stack to the lookaround's mark
(`top_bstack`), which discards the inner section's `FUZZY` entry, and that entry's backtrack arm is
the only place `state->fuzzy_node` is restored. `RE_OP_LOOKAROUND` and `RE_OP_CONDITIONAL` save the
fuzzy counts but not the node, and `start_match` clears only the counts.

A possible fix is to save `fuzzy_node` beside `fuzzy_counts` at `RE_OP_ATOMIC`, `RE_OP_CONDITIONAL`
and `RE_OP_LOOKAROUND`, restore it wherever those counts are restored, and set it to NULL in
`start_match`.
