# DRAFT - NOT FILED

Ledger entry 33. Written by S88 on 2026-09-23 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## MemoryError from a repeated fuzzy group that can match by deleting

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?:(?:x){d<=1})+y', 'y', regex.V1)
MemoryError
>>> regex.search(r'(?:(?:x){d<=1}){1,3}y', 'y', regex.V1)
<regex.Match object; span=(0, 1), match='y', fuzzy_counts=(0, 0, 3)>
>>> regex.search(r'(?:(?:x){d<=1})+', '', regex.V1)
<regex.Match object; span=(0, 0), match='', fuzzy_counts=(0, 0, 2)>
```

The bounded repeat and the repeat at the end of the string both answer, so the unbounded search
has a match to find. `(x)(?:(?:x){d<=2})+$` over `'xy'` raises MemoryError as well, and so do the
same searches under V0, `(?e)` and `(?b)`, reversed with `(?r)y(?:(?:x){d<=1})+`, and inside an
outer section, `(?:(?:(?:x){d<=1})+y){e<=5}`.

**Cause.** Each fuzzy edit increments `state->capture_change` (`_regex.c:10487`), and
`is_repeat_guarded` returns FALSE when the pattern is fuzzy (`:9596`). `RE_OP_END_GREEDY_REPEAT`
(`:12552`) treats a changed `capture_change` as progress, so an iteration whose body matched only
by deleting counts as progress even though the text position has not moved. The fuzzy check that
follows turns this off only when the text position is at the end of the slice (the start when
reversed). Elsewhere the repeat goes round again. Because each entry to the inner fuzzy section
starts its counts at zero, the `{d<=1}` never runs out, and the loop continues until the backtrack
stack exhausts memory.

**Suggested fix.** In the fuzzy check of `RE_OP_END_GREEDY_REPEAT`, once the minimum is met, also
treat an iteration that did not move the text position as no progress when the repeat has no
maximum. That is too broad on its own. A pass that does not move can still set a capture group
that a later pass tests, and upstream answers those correctly today:
`(?:(?(1)c|z)|()(?:x){d<=1})*$` over `'c'` is (0, 1) with one deletion. Inside an enclosing fuzzy
section, the section's own budget usually ends the loop, and `(?:\d+a0b+?){d<=2}` over `'67a0bab'`
changes answer if the rule applies there. A C# port of the module applies the rule only outside
any fuzzy section and only when the repeat's body holds no capture group. With that rule it
answers `(?:(?:x){d<=1})+y` over `'y'` with (0, 1) and two deletions, the same count upstream gives
at the end of the string, and the whole of its ported copy of `test_regex.py` still passes. A
complete fix would tell a pass that changed a group from one that only made fuzzy edits, which
`capture_change` alone cannot do.
`tools/probes/s88-fuzzy-empty-iteration.py` in the reporter's repository prints every case above,
running each in a child process with a time limit.
