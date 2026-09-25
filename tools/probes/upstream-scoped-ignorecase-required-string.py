"""Ledger entry 37 (2026-09-26): a scoped (?i:) loses full case folding in the required-string search.

`_get_required_string` (upstream/regex/_main.py:602) takes IGNORECASE and FULLCASE from the
literal's own case flags, but `pattern_new` (upstream/src/_regex.c:26125) removes FULLCASE whenever
the PATTERN's flags lack IGNORECASE. With `(?i:ss)` they do, so the prefilter looks for 'ss' with
simple folding and never finds U+00DF. The pattern's code is right: `(?i:ss)|q` has no required
string and matches.

Perl 5.42.3 (`use utf8; use feature 'unicode_strings'`) answers (0, 1) for `(?i:ss)` over U+00DF,
(0, 1) for `(?i:fi)` over U+FB01, (0, 2) for `(?i:ss)x` over U+00DF 'x', and no match for
`(?i:s)s`. CPython's re folds simply, so it cannot judge.

    python tools/probes/upstream-scoped-ignorecase-required-string.py
"""

import sys

import regex

# (pattern, subject, the port's span)
CASES = [
    (r"(?V1)(?i:ss)", "\xdf", (0, 1)),
    (r"(?V1)(?i:ss)x", "\xdfx", (0, 2)),
    (r"(?V1)(?i:fi)", "\N{LATIN SMALL LIGATURE FI}", (0, 1)),
    (r"(?V0)(?f)(?i:ss)", "\xdf", (0, 1)),
    # Controls: the same fold with a global IGNORECASE, with no required string, and a split
    # literal that cannot match half a folding. Both engines agree on these.
    (r"(?V1)(?i)ss", "\xdf", (0, 1)),
    (r"(?V1)(?i:ss)|q", "\xdf", (0, 1)),
    (r"(?V1)(?i:s)s", "\xdf", None),
]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"regex {regex.__version__}, CPython {sys.version.split()[0]}")
    for pattern, subject, port in CASES:
        m = regex.search(pattern, subject)
        upstream = m.span() if m else None
        mark = "" if upstream == port else "   <- differs from the port"
        print(f"{pattern!r:24} {ascii(subject):12} upstream {upstream!s:8} port {port!s:8}{mark}")


if __name__ == "__main__":
    main()
