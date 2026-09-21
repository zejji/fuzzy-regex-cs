"""Which fuzzy budgets are unbounded, measured rather than reasoned about.

The demo says so in one line under the pattern when a budget has no bound (S75, item 1), and the
line is decided by `demo/web/src/lib/budget.ts` reading the pattern text. This probe is where the
table that file is tested against comes from: for each pattern, whether the Python `regex` module
matches a subject that shares nothing with it. An unbounded budget allows any number of errors, so
it matches anywhere; a bounded one does not.

Run: python tools/probes/s75-fuzzy-budget.py
"""

import regex

# A subject per pattern, because each kind of error needs a subject it can reach. Unlimited
# insertions cannot turn "colour" into "zzz" - an insertion adds a character, it does not change
# one - so the `{i}` row is measured against "colour" with characters pushed into it.
FAR = "zzzzzzzzz"
PADDED = "czozlzozuzzr"

CASES = [
    # Unbounded: one letter, no comparison. A kind that is not named is zero, so `{s}` allows any
    # number of substitutions and no insertions or deletions at all.
    ("(?:colour){e}", FAR),
    ("(?:colour){s}", FAR),
    ("(?:colour){i}", PADDED),
    ("(?:colour){d}", FAR),
    # Unbounded, mixed with a bounded constraint.
    ("(?:colour){s<=1,e}", FAR),
    ("(?:colour){s<=1,e}", "colouur"),
    # Bounded.
    ("(?:colour){e<=2}", FAR),
    ("(?:colour){e<2}", FAR),
    ("(?:colour){s<=1,i<=1,d<=1}", FAR),
    ("(?:colour){1i+1d<3}", FAR),
    ("(?:colour){e<=2:[a-z]}", FAR),
    # Unbounded, with a test on which characters an edit may touch.
    ("(?:colour){e:[a-z]}", FAR),
    # The minimum-and-maximum form.
    ("(?:colour){1<=e<=3}", FAR),
    ("(?:colour){2i+2d+1s<=4}", FAR),
    # Not a fuzzy budget at all.
    ("colou?r", FAR),
    ("a{2}", FAR),
    ("[{e}]", FAR),
    (r"colour\{e\}", FAR),
    # A class that holds a closing bracket, then a brace pair that is literal text.
    ("[]{e}]x", "]x"),
    ("[]{e}]x", "}x"),
    # A kind constrained twice is re-read as a cost equation (`parse_fuzzy_item`, line 679), so
    # `{s,s<=1}` prices substitutions at one and `{d,d<=1,i}` leaves insertions unlimited. When the
    # second reading fails too, as for `e`, the whole budget is not one and the braces are literal.
    ("(?:colour){s,s<=1}", "colouu"),
    ("(?:colour){s,s<=1}", "colzuu"),
    ("(?:colour){d,d<=1,i}", PADDED),
    ("(?:colour){i,i<=1,d}", FAR),
    ("(?:colour){e<=1,e}", FAR),
    ("(?:colour){e<=1,e}", "colour{e<=1,e}"),
    ("(?:colour){s<=1,s}", "colour{s<=1,s}"),
]


def main() -> None:
    print(f"regex {regex.__version__}")
    for pattern, subject in CASES:
        try:
            match = regex.search(pattern, subject)
        except regex.error as error:  # a pattern this table got wrong
            print(f"{pattern!r:30} {subject!r:14} ERROR {error}")
            continue
        found = "no match" if match is None else f"match {match.span()} {match.group()!r}"
        counts = "" if match is None else f" fuzzy_counts={match.fuzzy_counts}"
        print(f"{pattern!r:30} {subject!r:14} {found}{counts}")


if __name__ == "__main__":
    main()
