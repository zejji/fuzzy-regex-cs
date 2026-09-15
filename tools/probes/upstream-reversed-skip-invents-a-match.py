"""A reversed `(*SKIP)` makes upstream find a match that pruning alone does not find.

S52 sitting 10, 2026-09-15, regex 2026.9.10. Seed 20260915 row 74889 of the 6000-row gate, and the
row `reversed-skip-invents-a-match` is keyed on.

The argument, and it needs no reading: a verb whose entire job is to REMOVE backtracking positions
cannot create a match that does not exist without it. `(*PRUNE)` is the same opcode body but for the
two lines `(*SKIP)` has first, which are the ones that move the slice bound
(upstream/src/_regex.c:14553 reversed, :14555 forward), so it prunes identically and moves nothing.
Upstream's complete match here exists when and only when a bound was moved.

The `partial=True` half is not what is wrong. Dropping it leaves upstream on the complete match and
this port on no match, while the verb-free non-partial spelling is None on both sides - so the
divergence is in the ordinary reversed search, which is what keeps the row out of
`partial-retry-reversed-slice` and `search-start-partial`.

Run: python tools/probes/upstream-reversed-skip-invents-a-match.py
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

PATTERN = "(?r)^(?:[^a]+(*SKIP)[^a-f]|\\p{Lu})(?P<g1>\\D)(?:(?(1)(?=(?P>g1))\\w))*$"
SUBJECT = "\U0001F600ﬃ _\U00010400aﬃ\U00010400"
FLAGS = regex.I | regex.M  # the row's recorded flags, 0xa


def answer(pattern, partial):
    match = regex.compile(pattern, FLAGS, cache_pattern=False).search(SUBJECT, partial=partial)
    if match is None:
        return "None"
    kind = "PARTIAL" if match.partial else "complete"
    return f"{match.span()} g1={match.span('g1')} {kind}"


print(f"regex {regex.__version__}")
print(f"subject   {SUBJECT!a}   ({len(SUBJECT)} codepoints, 11 UTF-16 units)")
print()

SPELLINGS = [
    ("as drawn, (*SKIP)", PATTERN),
    ("(*SKIP) -> (*PRUNE)", PATTERN.replace("(*SKIP)", "(*PRUNE)")),
    ("verb deleted", PATTERN.replace("(*SKIP)", "")),
]

print("=== partial=True, as the row asks it")
for label, spelling in SPELLINGS:
    print(f"  {label:<24} {answer(spelling, True)}")

print()
print("=== partial=False - the same three, to show the partial machinery is not what differs")
for label, spelling in SPELLINGS:
    print(f"  {label:<24} {answer(spelling, False)}")

print()
print("This port answers the zero-width partial at (0, 0) to the drawn question and no match to")
print("the non-partial one, which is upstream's own (*PRUNE) answer and its own verb-free answer")
print("on both lines. That half is the gap test BacktrackingVerbTests.A_reversed_search_of_a_skip_")
print("finds_nothing_where_pruning_alone_finds_nothing, which asserts all four port cells.")
