"""A POSIX search of a fuzzy pattern crashes the upstream C engine.

Run with the upstream interpreter. It does not print a result: the process dies.

    $ python tools/probes/upstream-posix-fuzzy-crash.py
    regex 2026.7.19 '(?p)(?:[ab][bc]){e<=1}' 'ax'
    Segmentation fault  (exit 139)

Measured 2026-09-13 on regex 2026.7.19 (CPython 3.14, Windows). Ledger entry 9. This port answers
the row rather than crashing, so there is no divergence to pin - only the absence of ground truth
for `(?p)` combined with a fuzzy section, which is why the oracle's `fuzzy` generator draws no
`(?p)` and `ExpectedDivergences` has nothing for it.

Pass a case number to run one of the others.
"""

import sys

import regex

CASES = {
    "1": (r"(?p)(?:[ab][bc]){e<=1}", "ax"),
    "2": (r"(?p)(?:[ab][bc][wx]){e<=2}", "qabx"),
    "3": (r"(?p)(?:[ab][bc]){e<=1}", "qax"),
    # The control: the same pattern without `(?p)` answers normally.
    "4": (r"(?:[ab][bc]){e<=1}", "ax"),
    # And the same pattern without the fuzzy section answers normally.
    "5": (r"(?p)(?:[ab][bc])", "ab"),
}

pattern, subject = CASES[sys.argv[1] if len(sys.argv) > 1 else "1"]
print("regex", regex.__version__, repr(pattern), repr(subject), flush=True)
m = regex.search(pattern, subject)
print("ok", None if m is None else (m.span(), m.fuzzy_counts, m.fuzzy_changes), flush=True)
