"""Gate row 104366 through `greedy-lazy-existence-agree`, on UPSTREAM.

S52c scope item 7. The row is a reversed partial over an EMPTY slice at the end of the subject:

    regex.compile(r'(?r)\\xdf\\ufb01(.*?)\\b').match('\\ufb01\\u0131', 2, 2, partial=True)

Upstream answers a zero-width partial at (2, 2); this port answers no match. It must NOT be pinned
by agreement, because NEITHER ENGINE IS SELF-CONSISTENT on the axis around it: over the 33 cells of
`tools/probes/{upstream,port}-reversed-partial-ignores-the-slice-start.*` the two agree on 23 and
differ on 10, and the 10 split both ways. So "do they agree" has no useful answer here, and what
separates right from wrong is a metamorphic property applied to ONE engine at a time.

`greedy-lazy-existence-agree` (`docs/ORACLE-INVARIANTS.md` group F) is that property: greediness
ORDERS the candidate set, it does not change its membership, so the greedy and lazy spellings of one
quantifier must agree on WHETHER a match exists at a given start - they may differ only on the span.
A partial call may not deny what both spellings allow.

This asks upstream. `tools/probes/port-gate-row-greedy-lazy.ps1` asks this port with the same grid.

Run: `python tools/probes/upstream-gate-row-greedy-lazy.py`
"""

import regex

SUBJECT = "ﬁı"

# The gate row's pattern, and the same pattern with the quantifier respelled. `(.*?)` is lazy,
# `(.*)` greedy, `(.*+)` possessive - the third is not part of the invariant and is printed only
# because a possessive spelling that AGREES with the other two says the disagreement is about
# ordering rather than about the repeat's own bounds.
SPELLINGS = (
    ("lazy      (.*?)", r"(?r)\xdfﬁ(.*?)\b"),
    ("greedy    (.*)", r"(?r)\xdfﬁ(.*)\b"),
    ("possessive(.*+)", r"(?r)\xdfﬁ(.*+)\b"),
)

# The cells: the gate row itself first, then the same question without each element that might
# explain it, so a disagreement can be attributed rather than merely observed.
CELLS = (
    ("the gate row", r"(?r)\xdfﬁ{q}\b", (2, 2), True),
    ("without the reversal", r"\xdfﬁ{q}\b", (2, 2), True),
    ("without the \\b", r"(?r)\xdfﬁ{q}", (2, 2), True),
    ("without the unmatchable prefix", r"(?r){q}\b", (2, 2), True),
    ("over the whole subject", r"(?r)\xdfﬁ{q}\b", (0, 2), True),
    ("not partial", r"(?r)\xdfﬁ{q}\b", (2, 2), False),
)

QUANTIFIERS = (("lazy", "(.*?)"), ("greedy", "(.*)"), ("possessive", "(.*+)"))


def answer(pattern: str, pos: int, endpos: int, partial: bool) -> str:
    try:
        m = regex.compile(pattern).match(SUBJECT, pos, endpos, partial=partial)
    except Exception as e:  # noqa: BLE001 - a refusal is an answer
        return f"{type(e).__name__}"
    if m is None:
        return "no match"
    return f"{m.span()}{' partial' if m.partial else ' complete'}"


print(f"regex {regex.__version__}, subject {ascii(SUBJECT)} ({len(SUBJECT)} codepoints)")
print()
print(f"{'cell':34} {'lazy':22} {'greedy':22} {'possessive':22} invariant")
violations = 0
for label, shape, (pos, endpos), partial in CELLS:
    answers = {
        name: answer(shape.replace("{q}", quantifier), pos, endpos, partial)
        for name, quantifier in QUANTIFIERS
    }
    # EXISTENCE ONLY. The spans are allowed to differ - that is what greediness is for - so the
    # invariant compares "did a match exist" and nothing else.
    exists = {name: value != "no match" for name, value in answers.items()}
    broken = exists["lazy"] != exists["greedy"]
    violations += broken
    print(f"{label:34} {answers['lazy']:22} {answers['greedy']:22} "
          f"{answers['possessive']:22} {'BROKEN' if broken else 'holds'}")

print()
print(f"greedy-lazy-existence-agree is broken on {violations} of {len(CELLS)} cells, on upstream.")
