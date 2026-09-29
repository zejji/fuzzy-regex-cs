# DRAFT - NOT FILED

Ledger entry 12. Written on 2026-09-28 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## `BESTMATCH` loses a match whose best fit ends in inserted characters

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.fullmatch(r'(?b)(?:b){e<=2}', 'bba')
>>> regex.fullmatch(r'(?:b){e<=2}', 'bba')
<regex.Match object; span=(0, 3), match='bba', fuzzy_counts=(0, 2, 0)>
>>> regex.fullmatch(r'(?e)(?:b){e<=2}', 'bba')
<regex.Match object; span=(0, 3), match='bba', fuzzy_counts=(0, 2, 0)>
>>> regex.match(r'(?b)(?:b){e<=2}$', 'bba')
>>> regex.fullmatch(r'(?b)(?:b){e<=2}', 'bb')
<regex.Match object; span=(0, 2), match='bb', fuzzy_counts=(0, 1, 0)>
```

The README says `BESTMATCH` makes fuzzy matching "search for the best match instead" of the first
one. Here there is a match that meets the constraints - the same engine finds it without the flag
and with `ENHANCEMATCH` - and with `BESTMATCH` there is none. A larger budget does not help:
`{e<=9}` is `None` too. One trailing insertion (`'bb'`) survives here and two do not, but that
boundary depends on the pattern: `regex.fullmatch(r'(?b)(a0)(?:(?:\1)){e<=3}', 'a0x0y')` is `None`
with a fit of one insertion and one substitution. It is not specific to `fullmatch`: a trailing
`$` under `match` loses it the same way, and so does `(?r)`.

The smallest form has an empty body: `regex.fullmatch(r'(?b)(?:){e<=3}', 'znz')` is `None`, and
`(0, 3)` with three insertions without the flag.

**Cause.** The trailing insertions come from the `RE_OP_END_FUZZY` backtrack case in
`basic_match`, whose guard is (`_regex.c` lines 15516-15517 in 2026.9.10):

```c
if (insertion_permitted(state, inner_node, inner_counts) &&
  total_errors(state->fuzzy_counts) + total_errors(inner_counts) <
  state->max_errors && fuzzy_ext_match(state, inner_node, state->text_pos)) {
```

By the time this runs, `END_FUZZY` has already added the inner counts into `state->fuzzy_counts`
(line 12475 onwards), so the section's errors are counted twice. With the usual unbounded
`max_errors` that never matters. `do_best_fuzzy_match` is where `max_errors` becomes finite: its
first pass finds the two-insertion match and sets `fewest_errors` to 2, and the second pass then
sets `error_limit = fewest_errors` (line 17703) and tries `max_errors` from 1 up to 2 (lines
17736-17738); the widened-slice fallback uses 2 as well (line 17832). After the first trailing
insertion the guard computes 1 + 1 < 2, refuses the second, and the second pass cannot find the
match the first pass found.

**Suggested fix.** Count the errors once, as every other `max_errors` test in the file does:

```c
if (insertion_permitted(state, inner_node, inner_counts) &&
  total_errors(state->fuzzy_counts) < state->max_errors &&
  fuzzy_ext_match(state, inner_node, state->text_pos)) {
```

`insertion_permitted` on the same line already applies the section's own limits to
`inner_counts`, so nothing is lost. Built with that one change, 2026.9.10 answers `(0, 3)` with
two insertions for every row above, leaves the flagless answers unchanged, and still refuses
`regex.fullmatch(r'(?b)(?:b){e<=1}', 'bba')`, which the budget cannot afford.

---

Evidence for the owner, not for the report: `tools/probes/bestmatch-fullmatch-trailing-insertions.py`
(with `--patched` for the patched build) and ledger entry 12, whose earlier text records the other
rows the same term explains. Re-run both against the then-current release, and re-check the line
numbers, before this is filed.
