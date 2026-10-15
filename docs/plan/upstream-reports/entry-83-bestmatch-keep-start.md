# DRAFT - NOT FILED

Ledger entry 83. Written on 2026-10-15 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## BESTMATCH loses a fuzzy match whose start `\K` moved

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?:By){e<=1}\K', 'Cy')
<regex.Match object; span=(2, 2), match='', fuzzy_counts=(1, 0, 0)>
>>> regex.search(r'(?b)(?:By){e<=1}\K', 'Cy')
>>> regex.search(r'(?b)a\K(?:By){e<=1}', 'aCy')
>>> regex.search(r'a\K(?:By){e<=1}', 'aCy')
<regex.Match object; span=(1, 3), match='Cy', fuzzy_counts=(1, 0, 0)>
```

BESTMATCH asks for the match with the fewest errors among those fuzzy matching finds. Here fuzzy
matching finds one, with one error, and BESTMATCH returns nothing. ENHANCEMATCH (`(?e)`) returns
the match, and so does BESTMATCH when the match needs no errors (`(?b)a\K(?:By){e<=1}` over 'aBy'
gives (1, 3)).

**The cause seems to be the second pass of `do_best_fuzzy_match`.** It re-runs each best candidate
anchored at `entry->match_pos` (`_regex.c:17732`, then `basic_match(state, FALSE)` at `:17741`).
After a `\K`, `match_pos` is where the `\K` ran, not where the attempt began, so the anchored
re-run starts in the wrong place. Usually it then fails. When something else happens to match from
that place, BESTMATCH returns that instead:

```python
>>> regex.search(r'(?b)a\K(?:By){e<=1}|(?:Cyy){e<=1}', 'aCy')
<regex.Match object; span=(1, 3), match='Cy', fuzzy_counts=(0, 0, 1)>
>>> regex.search(r'a\K(?:By){e<=1}|(?:Cyy){e<=1}', 'aCy')
<regex.Match object; span=(1, 3), match='Cy', fuzzy_counts=(1, 0, 0)>
```

The deletion comes from the second alternative, the only one that can match anchored at 1. The
first pass also advances its walk to `state->match_pos` (`:17680`), which has the same problem.

A possible fix: record where each candidate's attempt began, beside `match_pos` in the best list,
and anchor the second pass and the walk there, keeping `match_pos` for the reported span.

---

## Notes (not part of the report)

- Found by scheduled-oracle row 138026906/24826 (`(?b)(?e)(?r)\K\b(?:[^a][^a]B){e<=1}` over
  '_aA AB ', match, MULTILINE, partial). Non-partial, both engines answer None; the oracle saw the
  row only because the partial answers differ (port P(0,4), upstream None).
- This port copies the second pass and answers the same on every witness above: known defect D86.
  Pinned (red until fixed) by
  `FuzzyBestMatchTests.Bestmatch_keeps_a_candidate_whose_reported_start_a_K_moved`.
- The mechanism was traced by probe (the `|(?:Cyy){e<=1}` row), not in a debugger; the C line
  numbers were read.
