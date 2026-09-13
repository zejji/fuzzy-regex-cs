"""Generates `(?e)` and `(?b)` rows an ALTERNATION can improve, which no wave draws (S41, S42).

The committed `fuzzy` generator builds a section by concatenating atoms, so the `ENHANCEMATCH`
improvement loop almost never has two candidates of different shape to choose between and its chain
is one or two runs long. That is enough to exercise the loop and not enough to tell a cost ranking
from an error-count ranking, nor to catch a loop that stops walking the chain too early. This writes
rows that do both: an alternation body, and a cost equation whose coefficients are not all equal.

S41's blind review found the defect this exists to catch, and the numbers below are this file's
reason to be committed rather than left in a session's scratch - a control figure nobody can re-run
is not evidence (the port-slice skill's rule, learned from S18 and S19).

    python tools/probes/enhancematch-cost-rows.py .scratch/cost.jsonl 777
    pwsh -File tools/run-oracle.ps1 -Rows .scratch/cost.jsonl

    committed code     agree 2457  expected 12  diverge 31   - every one of the 31 a cheaper match
                                                               at a different span, none dearer
    ranking and termination merged into one cost test (the defect):
                       agree 2434  expected 12  diverge 54
    ranking reverted to upstream's error count:
                       agree 2500  expected 0   diverge 0    - which is what says the whole family
                                                               is the ranking rule and nothing else

The middle row is the one to read. Merging the two tests does not merely re-rank the chain, it CUTS
it at the first run that costs more, and some of those 54 are strictly worse than upstream on the
cost this port ranks by AND on the error count upstream ranks by. Row 455, which both of S41's blind
reviews found independently: `(?e)(?:(?:cat|x)){1i+2s+9d<=30}` over 'acx' answers `(1, 0, 0)`, cost
2, upstream (verified directly, regex 2026.7.19) and `(3, 0, 0)`, cost 6, with the tests merged. HOW
MANY of the 54 are strictly worse depends on how you score a `sub` or a `split` row, whose report
line carries no counts at all, so no single number is quoted here: S41's two reviews scored 7 and 19
of them by different rules and both are right about what they counted. The 54 is the figure to
re-run against.

S42 ADDED THE `(?b)` HALF AND THE FIGURES BELOW, and this probe is now the ONLY instrument that
draws either cost-ranking family: `record-oracle.py`'s `_has_weighted_cost` deliberately refuses to
put `(?e)` or `(?b)` on a row whose equation prices the three error kinds differently, because an
oracle cannot judge a comparison the two engines are defined to answer differently. So the report
this writes is read for the INVARIANT rather than for a green light. The invariant is that this
port's answer is never DEARER than upstream's under the pattern's own equation.

    python tools/probes/enhancematch-cost-rows.py .scratch/cost.jsonl 777
    pwsh -File tools/run-oracle.ps1 -Rows .scratch/cost.jsonl
    python .scratch/analyse-probe.py TestResults/oracle/report.txt   # scoring script, see below

    committed code     agree 2216  expected 32  diverge 252
      of the 252:      128 this port cheaper, 0 dearer, 3 equal cost, 9 no match at all,
                       112 `sub`/`split` rows whose report line carries no counts to score
    BESTMATCH's cost walk turned off (`FuzzyCount == 2` in Matcher.DoBestFuzzyMatch, which no
    pattern satisfies with one section):
                       agree 2475  expected  6  diverge  19 - 12 cheaper, 0 dearer, 0 lost,
                       and every one of the 19 an `(?e)` row, which is what says the other 233
                       are BESTMATCH's budget and nothing else

The three EQUAL-COST rows are the then-earliest half of the owner's rule, not a defect: both
engines spend the same errors at the same price and this port's match starts earlier - row 137,
`(?b)(?:(?:ba+x|ab?)){5i+9s+1d<=4}` finditer over 'yzxyz', where upstream's second match is (4, 4)
and this port's is (2, 2), both one deletion.

THE NINE THAT MATCH NOTHING ARE AN INHERITED UPSTREAM BUG, ledger entry 12, and they are the honest
price of this change. All nine are `fullmatch` rows whose cheapest fit needs TRAILING insertions,
and upstream loses those under `(?b)` on its own: `regex.fullmatch(r'(?b)(?:x){e<=3}', 'xyz')` is
None where the same pattern without `(?b)` answers (0, 2, 0). The cost budget does not cause the bug,
it steers more rows onto it. Pinned by
Gaps/Engine/FuzzyBestMatchTests.Bestmatch_loses_a_match_that_needs_two_trailing_insertions.

The scoring script is four lines of report parsing and is NOT committed, because the report format
is the thing that would rot. It reads each `DIVERGE` block, takes the `<n>i+<n>s+<n>d` coefficients
out of the pattern and the `fuzzy=(s,i,d)` triple out of each side's FIRST match, and compares the
two costs. First match, not the sum over a scan: a scan's later matches start where its earlier ones
ended, so summing compares two different decompositions and reports a cheaper first match as a
dearer row (measured - it called 2 rows dearer that are not).

`run-oracle.ps1 -Rows` records these against the real Python module, so they are ground truth like
any other wave; they are simply not drawn at random.
"""

import json
import random
import sys

rng = random.Random(int(sys.argv[2]) if len(sys.argv) > 2 else 20260913)

ATOMS = ["a", "b", "x", "y", "ab", "xyq", "cat", "cats", "[ab]", "\\w", "a+", "b?", "(?:a|ab)"]
OPS = ["search", "match", "fullmatch", "finditer", "sub", "split"]
SUBJ = ["xyz", "yzxyz", "abcd", "acx", "cats", "cts", "ab", "aabb", "xaybz", "c", "abab", ""]


def equation():
    """A cost equation the two rankings can disagree under - so never all-equal coefficients."""
    while True:
        cs, ci, cd = (rng.choice([1, 2, 3, 5, 9]) for _ in range(3))
        if not (cs == ci == cd):
            return cs, ci, cd


rows = []
for _ in range(2500):
    body = "".join(rng.choice(ATOMS) for _ in range(rng.randint(1, 3)))
    if rng.random() < 0.6:
        # The alternation: a second branch of a different length, which is what gives the
        # improvement loop a chain with more than one shape on it.
        body = "(?:" + body + "|" + "".join(rng.choice(ATOMS) for _ in range(rng.randint(1, 3))) + ")"

    cs, ci, cd = equation()
    # S42: half the rows carry `(?b)` instead of `(?e)`, so the one probe covers both cost-ranking
    # families. They are the same decision reached by different code - `ENHANCEMATCH` moved its
    # tie-break and `BESTMATCH` moved its BUDGET - so they fail differently and both want drawing.
    flag = "(?b)" if rng.random() < 0.5 else "(?e)"
    pattern = flag + "(?:" + body + "){%di+%ds+%dd<=%d}" % (ci, cs, cd, rng.choice([4, 6, 9, 12, 20, 30]))
    if rng.random() < 0.15:
        pattern = flag + "(?r)" + pattern[4:]

    operation = rng.choice(OPS)
    row = {
        "generator": "fuzzy",
        "pattern": pattern,
        "flags": 0,
        "namedLists": {},
        "subject": rng.choice(SUBJ),
        "operation": operation,
    }
    if operation == "sub":
        row["template"] = "<>"
        row["count"] = 0
    if operation == "split":
        row["count"] = 0

    rows.append(row)

with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as f:
    for row in rows:
        f.write(json.dumps(row, ensure_ascii=False) + "\n")

print(len(rows), "rows")
