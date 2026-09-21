r"""Seed 20260921 row 74201: which flag makes upstream spend the extra deletion.

The row is `(?e)(?p)^[abz]+?\L<w1>{1<=e<=2:[A-Za-z_]}$` searched over 'AA\n\U0001F3FB' under
FULLCASE, IGNORECASE and MULTILINE. Upstream answers the span (0, 2) at a cost of two deletions
and this port answers the same span at a cost of one, so the question is whether POSIX chose a
dearer fit of a span its own flagless engine can fit more cheaply - which is what
`posix-fuzzy-contradicts-its-own-flagless-answer` pins - or whether `(?e)`'s improvement loop is
what stopped early, which would be `enhancematch-loses-a-candidate` instead.

The batch instrument, `tools/probes/gate-divergence-doors.py`, asks the `(?e)` door and not the
`(?p)` one, so this asks both, and both removed together.

Run: python tools/probes/s57b-posix-overcharges-a-named-list-section.py
Measured 2026-09-21 on the pinned regex 2026.9.10.
"""

import regex

FULLCASE, IGNORECASE, MULTILINE = 0x4000, 0x2, 0x8

PATTERN = r"(?e)(?p)^[abz]+?\L<w1>{1<=e<=2:[A-Za-z_]}$"
SUBJECT = "AA\n\U0001f3fb"
W1 = ["A", "A\U0001f3fb", "aa\U0001f600", "\U0001d7ee\U0001f3fb\U00010400"]


def answer(pattern: str) -> str:
    match = regex.compile(pattern, FULLCASE | IGNORECASE | MULTILINE, w1=W1).search(SUBJECT)
    return "None" if match is None else f"{match.span()} counts={match.fuzzy_counts}"


def main() -> None:
    print(f"regex {regex.__version__}")
    for label, pattern in [
        ("as drawn", PATTERN),
        ("without (?p)", PATTERN.replace("(?p)", "")),
        ("without (?e)", PATTERN.replace("(?e)", "")),
        ("without either", PATTERN.replace("(?e)", "").replace("(?p)", "")),
    ]:
        print(f"    {label:<16} {answer(pattern)}")


if __name__ == "__main__":
    main()
