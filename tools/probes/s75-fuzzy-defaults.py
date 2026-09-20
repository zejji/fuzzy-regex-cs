"""What a fuzzy budget allows for the kinds it does not name.

`{s}` says any number of substitutions. It says nothing about insertions and deletions, and the
demo's "this budget has no bound" line is only true if what is unnamed is not quietly unlimited
too. Measured against the Python `regex` module, one subject per kind, each needing exactly one
error of that kind and none of the others (S75, item 1).

Run: python tools/probes/s75-fuzzy-defaults.py
"""

import regex

# "colour" with one error of each kind, and the exact pattern text is the only variable.
ONE_SUB = "colouu"
ONE_INS = "colourr"
ONE_DEL = "colou"

BUDGETS = [
    "{e}",
    "{s}",
    "{i}",
    "{d}",
    "{e<=1}",
    "{s<=1}",
    "{i<=1}",
    "{d<=1}",
    "{s<=1,e}",
    "{s<=0,e}",
]


def verdict(pattern: str, subject: str) -> str:
    match = regex.fullmatch(pattern, subject)
    return "-" if match is None else f"{match.fuzzy_counts}"


def main() -> None:
    print(f"regex {regex.__version__}. Columns: one substitution, one insertion, one deletion.")
    print(f"{'budget':12} {ONE_SUB:>10} {ONE_INS:>10} {ONE_DEL:>10}")
    for budget in BUDGETS:
        pattern = f"(?:colour){budget}"
        row = [verdict(pattern, subject) for subject in (ONE_SUB, ONE_INS, ONE_DEL)]
        print(f"{budget:12} {row[0]:>10} {row[1]:>10} {row[2]:>10}")


if __name__ == "__main__":
    main()
