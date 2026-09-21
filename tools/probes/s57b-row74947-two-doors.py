r"""Seed 20260921 row 74947: the two doors that answer, and the one that cannot.

Upstream answers None to this reversed `fullmatch(partial=True)`. Two independent ablations give it
back this port's answer, which is `bestmatch-loses-a-partial`'s own pair of arguments:

    (?b) deleted        a flag documented to pick the BEST match cannot empty the set of matches
    (*SKIP) -> (*PRUNE) `(*PRUNE)` prunes exactly what `(*SKIP)` prunes, and the only extra thing
                        `(*SKIP)` does is move a slice bound (upstream/src/_regex.c:14553 reversed,
                        :14555 forward), so a difference between them is about the bound

The usual third door does not answer: with the verb deleted the pattern exhausts memory, which is
ledger entry 14's shape - a self-recursive call round a fuzzy section that can match empty - and
says nothing about this row either way. It is printed rather than hidden.

Run: python tools/probes/s57b-row74947-two-doors.py
Measured 2026-09-21 on the pinned regex 2026.9.10.
"""

import regex

PATTERN = (
    r"(?b)(?r)(\d*)+?(?P<g2>(?:\p{ASCII}\U00010428[[:alpha:]]{0,2}){e<=1}(?&g2)?)"
    r"(?:(?:A\U00010428(?:A){e<=2,s<=1}){e<=1}(*SKIP)\p{Ll}|[[:alpha:]])"
)
SUBJECT = "\U00010428\U00010428"
FLAGS = 0x8  # MULTILINE, the row's own flag word


def show(label: str, pattern: str) -> None:
    """Ask upstream the row's own question of one spelling of the pattern."""
    try:
        match = regex.compile(pattern, FLAGS).fullmatch(SUBJECT, partial=True)
    except MemoryError as error:
        print(f"  {label:<22} MemoryError: {error}")
        return

    if match is None:
        print(f"  {label:<22} None")
        return

    print(
        f"  {label:<22} {match.span()} "
        f"{'PARTIAL' if match.partial else 'complete'} "
        f"groups={match.groups()} counts={match.fuzzy_counts} changes={match.fuzzy_changes}"
    )


def main() -> None:
    print(f"regex {regex.__version__}, spans in CODEPOINTS (the subject is astral)")
    show("as drawn", PATTERN)
    show("(?b) deleted", PATTERN.replace("(?b)", "", 1))
    show("(*SKIP) -> (*PRUNE)", PATTERN.replace("(*SKIP)", "(*PRUNE)", 1))
    show("verb deleted", PATTERN.replace("(*SKIP)", "", 1))


if __name__ == "__main__":
    main()
