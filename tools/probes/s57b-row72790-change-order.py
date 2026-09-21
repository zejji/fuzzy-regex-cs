r"""Seed 20260921 row 72790, minimised: the ORDER a reversed match records its fuzzy changes in.

Row 72790's two engines report three deletions each for the same zero-width match and the lists
look nothing alike - upstream [4, 5, 5], this port [3, 5, 6]. They are the same three deletions.
`match_fuzzy_changes` (`upstream/src/_regex.c:20504-20560`) walks ONE list of changes in the order
they were recorded and adds to each DELETION the number of deletions already emitted, so the same
raw positions in a different order print as different numbers:

    raw [3, 4, 4] -> 3, 4+1, 4+2 -> [3, 5, 6]     raw [4, 4, 3] -> 4, 4+1, 3+2 -> [4, 5, 5]

This probe un-shifts both engines' lists back to the raw positions they were recorded at. On row
72790 the two multisets are EQUAL - the same three deletions, {3, 4, 4}, in a different order:
upstream puts the body's last, this port puts it first, which is the order a reversed sequence runs
in, the body being the rightmost element and so the first the engine reaches.

WHERE UPSTREAM CONTRADICTS ITSELF, which is what settles who is right. Take the minimised row
`(?r)(?=(?:a\s){1<=e<=2})b{d<=1}` over 'a'. Both engines answer the same zero-width match at 0 with
the same counts, (0, 0, 2): the lookahead deletes `\s` at 1, the body deletes `b` at 0. Now write
the lookahead's section without its MINIMUM error count and ask upstream again:

    (?r)(?=(?:a\s){d<=1})b{d<=1}     upstream raw deletions [0, 1]   = this port
    (?r)(?=(?:a\s){e<=1})b{d<=1}     upstream raw deletions [0, 1]   = this port
    (?r)(?=(?:a\s){1<=e<=2})b{d<=1}  upstream raw deletions [1, 1]

Same span, same counts, same fit - and the fit already spends 2 errors, so a floor of 1 rejects
nothing. A minimum error count decides whether a fit is ACCEPTED; it cannot move where a character
was deleted. Upstream's body deletion moves from 0 to 1 anyway, onto the position the lookahead
reached, and its own two other spellings answer what this port answers.

WHAT NARROWS IT. The ladder below is `tools/probes/s57b-row72790-ladder-rows.jsonl`, replayed
through both engines with `pwsh -File tools/run-oracle.ps1 -Rows <that file>`. Only two of its ten
rows diverge, and they are the two whose lookahead holds a section with a minimum error count. A
non-fuzzy body agrees, and the section on its own outside a lookahead agrees. Row 72790 needs a
fuzzy lookahead and a fuzzy body too, but not the minimum: its `{d<=2}` spelling diverges the same
way, so the minimum is what exposes the misplacement on the small row, not what causes it.

WHAT THIS IS NOT. It is not ledger entry 11 mechanism A, the leak `start_match` leaves behind.
Removing this port's own change-list clear (`Matcher.cs:5155`) makes it reproduce upstream's leaked
answer on that family's own row - `(?:[ab][bc](*PRUNE)[wx]){e<=2}` over 'qab' goes from `[d:3]` to
`[s:0]` - and does not move row 72790 by a single position. Measured 2026-09-21.

Run: python tools/probes/s57b-row72790-change-order.py
Measured 2026-09-21 on the pinned regex 2026.9.10.
"""

import regex

IGNORECASE = 0x2

# Each case is the label, the pattern, the flags, the subject and the named lists. The first is the
# gate row itself; the rest are the ladder, smallest first.
CASES = [
    (
        "row 72790",
        r"(?r)(?=(?:[^\d]?\sß){1<=e<=2})\L<w1>{d<=1}",
        IGNORECASE,
        "ßß\r\n",
        {"w1": ["ß", "İﬁİ"]},
    ),
    (
        "row 72790, no minimum",
        r"(?r)(?=(?:[^\d]?\sß){d<=2})\L<w1>{d<=1}",
        IGNORECASE,
        "ßß\r\n",
        {"w1": ["ß", "İﬁİ"]},
    ),
    ("minimum, diverges  ", r"(?r)(?=(?:a\s){1<=e<=2})b{d<=1}", 0, "a", {}),
    ("minimum, diverges  ", r"(?r)(?=(?:a\s){1<=e<=2})b{d<=1}", 0, "ab", {}),
    ("no minimum, agrees ", r"(?r)(?=(?:a\s){d<=1})b{d<=1}", 0, "a", {}),
    ("no minimum, agrees ", r"(?r)(?=(?:a\s){e<=1})b{d<=1}", 0, "a", {}),
    ("no fuzzy body      ", r"(?r)(?=(?:a\s){1<=e<=2})b", 0, "ab", {}),
    ("no lookahead       ", r"(?r)(?:a\s){1<=e<=2}", 0, "ab", {}),
]


def unshift(deletions: list[int]) -> list[int]:
    """The raw position each deletion was recorded at, undoing `match_fuzzy_changes`'s running shift."""
    return [position - index for index, position in enumerate(deletions)]


def main() -> None:
    print(f"regex {regex.__version__}")
    for label, pattern, flags, subject, lists in CASES:
        compiled = regex.compile(pattern, flags, **lists)
        print(f"  {label}  {pattern}  over {subject!r}")
        for match in compiled.finditer(subject):
            substitutions, insertions, deletions = match.fuzzy_changes
            print(
                f"      {str(match.span()):<8} counts={match.fuzzy_counts} "
                f"[s:{substitutions}][i:{insertions}][d:{deletions}] "
                f"raw deletions {unshift(deletions)}"
            )


if __name__ == "__main__":
    main()
