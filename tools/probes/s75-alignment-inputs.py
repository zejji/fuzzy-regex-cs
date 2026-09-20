"""What `fuzzy_changes` gives an alignment view, and what it does not.

S75 asks for a letter-by-letter alignment of the selected match: the pattern on one row, the
subject on the next, the edit kinds between them. This probe is the evidence for what such a view
can honestly draw. `fuzzy_changes` is a triple of SUBJECT positions - substitutions, insertions,
deletions - and carries nothing about which part of the pattern each error was spent against.

The third case is the one that settles it: a pattern with an optional character and an alternation
has no fixed sequence of characters to put on a pattern row at all.

Run: python tools/probes/s75-alignment-inputs.py
"""

import regex

CASES = [
    # A literal pattern, where a pattern row would be tempting.
    ("(?:colour){e<=2}", "calor"),
    ("(?:foobar){i<=1,d<=1,s<=1}", "xfoobat"),
    # A pattern whose characters are not a sequence: `u?` may or may not be there, and the
    # alternation means the engine's own answer depends on which branch it took.
    ("(?:colou?r|couleur){e<=2}", "calor"),
]


def main() -> None:
    print(f"regex {regex.__version__}")
    for pattern, subject in CASES:
        match = regex.search(pattern, subject)
        if match is None:
            print(f"{pattern!r:32} {subject!r:10} no match")
            continue
        print(
            f"{pattern!r:32} {subject!r:10} span={match.span()} matched={match.group()!r} "
            f"fuzzy_counts={match.fuzzy_counts} fuzzy_changes={match.fuzzy_changes}"
        )


if __name__ == "__main__":
    main()
