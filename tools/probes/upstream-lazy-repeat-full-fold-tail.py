"""Ledger entry 38 (2026-09-26): a lazy bounded repeat before a full-folded literal misses matches.

On backtrack, the LAZY_REPEAT_ONE arm looks ahead for the literal that follows the repeat. Its
STRING arm calls `string_search(state, test, pos + 1, limit + length, ...)`
(upstream/src/_regex.c:16709), but its STRING_FLD arm calls
`string_search_fld(state, test, pos + 1, limit, ...)` (:16764), and `string_search_fld` treats
`limit` as the end of the readable text (:6674). `limit` is the last position the repeat may
reach, so a literal starting there can never be read to its end. The reversed twin is at :16808
and :16819.

It is not a search skip: `regex.match` refuses the whole subject on the first case too. CPython's
re and Perl 5.42.3 both give (0, 3) for `(?i)[^k]??ss` over 'ass' and (0, 4) for `(?i)a{0,2}?ss`
over 'aass'; Perl also gives (0, 2) over U+00E9 U+1E9E 'S' and U+00E9 U+FB01.

    python tools/probes/upstream-lazy-repeat-full-fold-tail.py
"""

import re
import sys

import regex

SHARP_S_CAPITAL = "\N{LATIN CAPITAL LETTER SHARP S}"
FI_LIGATURE = "\N{LATIN SMALL LIGATURE FI}"

# (pattern, subject, the port's span)
CASES = [
    (r"(?V1)(?i)[^k]??ss", "\xe9" + SHARP_S_CAPITAL + "S", (0, 2)),
    (r"(?V1)(?i)[^k]??ss", "ass", (0, 3)),
    (r"(?V1)(?i)a{0,2}?ss", "aass", (0, 4)),
    (r"(?V1)(?i)(?:a{0,2}?ss|q)", "aass", (0, 4)),
    (r"(?V1)(?i)[^k]??fi", "\xe9" + FI_LIGATURE, (0, 2)),
    (r"(?V0)(?fi)[^k]??ss", "ass", (0, 3)),
    (r"(?r)(?V1)(?i)ss[^k]??", "ssa", (0, 3)),
    # Controls: greedy, simple folding, and no folding. Both engines agree on these.
    (r"(?V1)(?i)[^k]?ss", "ass", (0, 3)),
    (r"(?V0)(?i)[^k]??ss", "ass", (0, 3)),
    (r"(?V1)[^k]??ss", "ass", (0, 3)),
]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"regex {regex.__version__}, CPython {sys.version.split()[0]}")
    for pattern, subject, port in CASES:
        m = regex.search(pattern, subject)
        upstream = m.span() if m else None
        mark = "" if upstream == port else "   <- differs from the port"
        print(f"{pattern!r:28} {ascii(subject):16} upstream {upstream!s:8} port {port!s:8}{mark}")
        if pattern == r"(?V1)(?i)[^k]??ss" and subject == "ass":
            print(f"{'':28} {'':16} match    {regex.match(pattern, subject)}")
    for pattern, subject in [(r"(?i)[^k]??ss", "ass"), (r"(?i)a{0,2}?ss", "aass")]:
        m = re.search(pattern, subject)
        print(f"re {pattern!r:25} {subject!r:16} {m.span() if m else None}")


if __name__ == "__main__":
    main()
