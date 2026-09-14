"""Under `(?r)`, upstream reports a LOOKAHEAD's fuzzy change at the MATCH START, not where it tested.

The evidence `ExpectedDivergences.reversed-lookahead-change-at-the-match-start` rests on, and the
only thing that can settle it: **upstream's own FORWARD matching of the same pattern answers the
position this port answers**, and adding `(?r)` - which moves no bound and picks the same candidate
on every row here, same span and same counts - moves upstream's answer to the match start while
this port's stays put.

Written by S48b's second sitting on 2026-09-14, minimising seed-7 gate row 73463 from a
nine-character subject and nine constructs to three characters and four.

THE CONDITION IS A GENERAL REPEAT AFTER THE LOOKAHEAD, and it was measured rather than guessed - a
first draft of this probe claimed the shift was the lookahead's OFFSET from the match start, and
row 3 below kills that: its offset is 2 and upstream still answers 0. What separates the rows that
move from the rows that do not is whether the atom after the lookahead is a general repeat (`A+`,
`A{1,2}`) or a fixed count (`A`, `A{1}`).

Run it::

    python tools/probes/upstream-reversed-lookahead-change-position.py

Expected on 2026.9.10: rows 1 and 3 CONTRADICT - forward answers where the lookahead tested,
reversed answers 0 - and rows 2 and 4 AGREE. Row 2 is row 1 with the repeat made a fixed count and
nothing else; row 4 puts the lookahead AT the match start, so there is nowhere for the position to
move to.

The engine-side half - that this port answers the FORWARD position in both directions - is asserted
by the oracle entry itself and by
`Gaps.Engine.FuzzyCountsAndChangesTests.A_reversed_lookahead_reports_its_substitution_where_the_lookahead_tested`.
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

# (label, pattern WITHOUT the reverse flag, subject, whether the two directions are expected to
# contradict). No flags at all: the wave row carried IGNORECASE, FULLCASE and MULTILINE and none of
# the three is part of the mechanism.
ROWS = [
    ("general repeat after the lookahead, offset 1", r"A(?=[^A]{e<=1})A+\D", "AAA", True),
    ("the same with a FIXED count instead", r"A(?=[^A]{e<=1})A\D", "AAA", False),
    ("general repeat, offset 2 - still answers 0", r"AA(?=[^A]{e<=1})A+\D", "AAAA", True),
    ("lookahead AT the match start, nowhere to move", r"(?=[^A]{e<=1})A+\D", "AAA", False),
]


def describe(m) -> str:
    if m is None:
        return "None"
    return f"span={m.span()} counts={m.fuzzy_counts} changes={m.fuzzy_changes}"


if __name__ == "__main__":
    print("regex", regex.__version__)
    print("A substitution belongs where the lookahead TESTED, which is what the forward line says.\n")

    failures = 0
    for label, pattern, subject, expected_contradiction in ROWS:
        forward = regex.compile(pattern).search(subject)
        reversed_ = regex.compile("(?r)" + pattern).search(subject)

        print(f"--- {label}")
        print(f"    pattern  {pattern!r}  subject {subject!r}")
        print(f"    forward  {describe(forward)}")
        print(f"    reversed {describe(reversed_)}")

        if forward is None or reversed_ is None:
            print("    -> ONE DIRECTION DID NOT MATCH, so this row says nothing")
            failures += 1
        else:
            contradicts = forward.fuzzy_changes != reversed_.fuzzy_changes
            print(f"    -> the two directions {'CONTRADICT EACH OTHER' if contradicts else 'AGREE'}")
            if contradicts != expected_contradiction:
                print("    -> NOT WHAT THIS PROBE RECORDED; upstream has moved")
                failures += 1
        print()

    raise SystemExit(1 if failures else 0)
