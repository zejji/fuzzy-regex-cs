r"""Seed 20260921 row 72790: what upstream's change list says inside a scan and on its own.

The row is `(?r)(?=(?:[^\d]?\sß){1<=e<=2})\L<w1>{d<=1}` scanned over 'ss\r\n' (both s spelt
U+00DF) under IGNORECASE, as a `finditer`. Two of its five matches diverge from this port:

  span (3, 3)   upstream deletions [4, 5, 5]      this port [3, 5, 6]
  span (1, 2)   upstream counts (1, 0, 0) with its ONE position in DELETIONS, this port in
                SUBSTITUTIONS

The second is the self-contradiction `fuzzy-changes-of-the-wrong-kind-for-their-own-counts` pins:
one substitution counted, none listed. The first is a position disagreement, and this probe exists
because NEITHER engine's list for it is obviously the right one - so the row stays unjudged rather
than being filed on the strength of its neighbour.

What the probe shows: asked ON ITS OWN, upstream answers the (3, 3) match with deletions
[3, 4, 5], a clean run of three. Inside the scan it answers [4, 5, 5], which is two thirds of the
PREVIOUS match's [4, 5, 6] with the last position repeated. So upstream contradicts itself between
the scan and the same question asked alone. This port's [3, 5, 6] matches neither, and whether it
is right is the open question: it shares its first position with upstream's own anchored answer and
its last two with the previous match, which is the shape of a carry-over on THIS side.

Run: python tools/probes/s57b-row72790-changes-in-a-scan.py
Measured 2026-09-21 on the pinned regex 2026.9.10.
"""

import regex

IGNORECASE = 0x2

PATTERN = r"(?r)(?=(?:[^\d]?\sß){1<=e<=2})\L<w1>{d<=1}"
SUBJECT = "ßß\r\n"
W1 = ["ß", "İﬁİ"]


def show(label: str, match) -> None:
    if match is None:
        print(f"    {label:<28} None")
    else:
        print(f"    {label:<28} {match.span()}  counts={match.fuzzy_counts}  changes={match.fuzzy_changes}")


def main() -> None:
    print(f"regex {regex.__version__}")
    compiled = regex.compile(PATTERN, IGNORECASE, w1=W1)

    print("  the scan, match by match")
    for index, match in enumerate(compiled.finditer(SUBJECT)):
        show(f"match {index}", match)

    print("  the (3, 3) match asked on its own")
    show("search endpos=3", compiled.search(SUBJECT, 0, 3))
    show("match endpos=3", compiled.match(SUBJECT, 0, 3))
    show("fullmatch (3, 3)", compiled.fullmatch(SUBJECT, 3, 3))
    show("search endpos=4", compiled.search(SUBJECT, 0, 4))


if __name__ == "__main__":
    main()
