"""Generates `(?e)` rows an ALTERNATION can improve, which the fuzzy generator cannot draw (S41).

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
    pattern = "(?e)(?:" + body + "){%di+%ds+%dd<=%d}" % (ci, cs, cd, rng.choice([4, 6, 9, 12, 20, 30]))
    if rng.random() < 0.15:
        pattern = "(?e)(?r)" + pattern[4:]

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
