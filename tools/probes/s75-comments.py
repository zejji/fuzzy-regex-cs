"""What a `(?#...)` comment does to a fuzzy budget beside it.

The demo decides from the pattern text alone whether a budget has a bound
(`demo/web/src/lib/budget.ts`), so it has to read a comment the way the engine does. Two questions
the grammar alone does not settle (S75, item 1):

* does a backslash escape the `)` that ends the comment, or is the comment over at the first `)`?
* is a comment something a budget can apply to?

Both are measured here. `parse_comment` is upstream line 978.

Run: python tools/probes/s75-comments.py
"""

import regex

CASES = [
    # An escaped `)` stays inside the comment, so the comment ends at the second one.
    (r"(?#\)zzz)a", "a"),
    (r"(?#\))colour", "colour"),
    (r"(?#\)x{e})y", "zzzzzyzzzzz"),
    (r"(?#a)b)colour", "colour"),
    # A comment is not something a budget can apply to.
    (r"(?#c){e}", "z"),
    (r"((?#c){e})", "z"),
    (r"(?#a)(?#b){e}", "z"),
    (r"(?#a)x{e}", "zzzzzzzzz"),
    # A comment nobody closed is a pattern upstream refuses.
    (r"(?#a{e}", "z"),
    (r"(?#x", "z"),
    # The comment is text, so the pattern beside it is exact.
    (r"(?#{e})colour", "czozlzozuzzr"),
]


def main() -> None:
    print(f"regex {regex.__version__}")
    for pattern, subject in CASES:
        try:
            match = regex.search(pattern, subject)
        except Exception as error:  # noqa: BLE001
            print(f"{pattern:18} {subject!r:14} ERROR {error}")
            continue
        answer = "no match" if match is None else f"{match.span()} counts={match.fuzzy_counts}"
        print(f"{pattern:18} {subject!r:14} {answer}")


if __name__ == "__main__":
    main()
