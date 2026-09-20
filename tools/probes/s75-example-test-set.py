"""The worked example for a fuzzy test set, measured before it is written into the demo.

S75 item 3 adds an example for `{e<=2:[a-z]}` - a budget that may only spend its errors on
lowercase letters. The example claims that "color" matches and that "col our" and "col0ur" do not,
and this is where that claim is checked. Every candidate subject is searched on its own, so a
match found in one cannot be credited to another.

Run: python tools/probes/s75-example-test-set.py
"""

import regex

PATTERN = "(?:colour){e<=2:[a-z]}"

SUBJECTS = [
    "colour",
    "color",
    "col our",
    "col0ur",
    "colour, color, col our and col0ur",
]


def main() -> None:
    print(f"regex {regex.__version__}. Pattern {PATTERN!r}")
    for subject in SUBJECTS:
        found = [
            (match.span(), match.group(), match.fuzzy_counts)
            for match in regex.finditer(PATTERN, subject)
        ]
        print(f"{subject!r:36} {found}")


if __name__ == "__main__":
    main()
