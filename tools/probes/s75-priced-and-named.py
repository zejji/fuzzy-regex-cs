"""Does a cost equation bound a kind that the same budget also names without a bound?

`{i,1i+1d<3}` names insertions twice: once as a constraint with no bound, once with a price. The
demo's "this budget has no bound" line has to decide which one wins before it can say anything
about the pattern (S75, item 1). Upstream accepts the pair, and which way round they are written is
not obviously the same question, so the answer has to be measured.

Each case pairs a pattern with a subject that only an unlimited budget of that kind reaches:
"czozlzozuzzr" is "colour" with six characters pushed into it, the empty subject needs six
deletions, and "zzzzzzzzz" shares no character with "colour".

Run: python tools/probes/s75-priced-and-named.py
"""

import regex

CASES = [
    ("(?:colour){i}", "czozlzozuzzr"),
    ("(?:colour){i,1i+1d<3}", "czozlzozuzzr"),
    ("(?:colour){1i+1d<3,i}", "czozlzozuzzr"),
    ("(?:colour){i<=2,1i+1d<3}", "czozlzozuzzr"),
    ("(?:colour){d}", ""),
    ("(?:colour){d,1d+1i<3}", ""),
    ("(?:colour){s}", "zzzzzzzzz"),
    ("(?:colour){s,1i+1s<3}", "zzzzzzzzz"),
    # An equation binds only the kinds it prices, and prices of zero buy any number of errors.
    ("(?:colour){d,1i+1s<3}", ""),
    ("(?:colour){0d+1i<3}", ""),
]


def main() -> None:
    print(f"regex {regex.__version__}")
    for pattern, subject in CASES:
        try:
            match = regex.search(pattern, subject)
        except Exception as error:  # noqa: BLE001
            print(f"{pattern:28} {subject!r:16} ERROR {error}")
            continue
        answer = "no match" if match is None else f"{match.span()} counts={match.fuzzy_counts}"
        print(f"{pattern:28} {subject!r:16} {answer}")


if __name__ == "__main__":
    main()
