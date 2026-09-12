#!/usr/bin/env python
"""The reversed half of upstream's stale-`(*SKIP)`-slice defect - ledger entry 5.

Three facts about the same row, in the order they settle the verdict:

1. Upstream's overlapped scan reports a capture OUTSIDE the match it belongs to, in a pattern
   with no lookaround that could put one there.
2. Upstream's own single-shot ``search`` over the same slice puts that capture inside the match,
   which is this port's answer.
3. The scan is memory-unsafe rather than merely wrong: printing each match as it arrives segfaults
   the interpreter, where the quiet list comprehension in step 1 completes. That is the same
   instability ``upstream-overlapped-skip-instability.py`` records forward as a ``gc.collect()``
   between iterations changing the answer.

Tracked rather than left in ``.scratch/``: S18's controls are permanently unreproducible because
their scripts were scratch, and a figure nobody can re-run is not evidence. Measured against
``regex`` 2026.7.19 and re-run against 2026.9.10 on 2026-09-12; the row is the one S29's ``verbs``
wave drew at seed 20260913.

Usage::

    python tools/probes/upstream-reversed-overlapped-skip.py          # steps 1 and 2, no crash
    python tools/probes/upstream-reversed-overlapped-skip.py --crash  # step 3, exits 139
"""

import sys

import regex

PATTERN = r'(?r)(?:\p{L}+(*SKIP)\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\w\s]))'
SUBJECT = 'AAAA00'


def main() -> int:
    print('regex', regex.__version__)

    # 1. The quiet scan. Expected: [((0, 6), (5, 6)), ((0, 5), (5, 6))] - the second capture is
    #    outside the second match.
    spans = [
        (m.span(), m.span('g1'))
        for m in regex.compile(PATTERN, cache_pattern=False).finditer(SUBJECT, overlapped=True)
    ]
    print('overlapped scan:', spans)
    for span, capture in spans:
        inside = span[0] <= capture[0] and capture[1] <= span[1]
        print('   match %s capture %s inside: %s' % (span, capture, inside))

    # 2. Upstream's own single-shot door over the same slice. Expected: (0, 5) with g1 at (4, 5).
    m = regex.compile(PATTERN, cache_pattern=False).search(SUBJECT, 0, 5)
    print('search(0, 5):', (m.span(), m.span('g1')) if m else None)

    if '--crash' not in sys.argv:
        print("re-run with --crash for step 3; it segfaults the interpreter on purpose")
        return 0

    # 3. The same scan with a side effect between iterations. Expected: exit 139 after the first
    #    line, on Windows 11 / CPython 3.14 / regex 2026.7.19 and 2026.9.10.
    for m in regex.compile(PATTERN, cache_pattern=False).finditer(SUBJECT, overlapped=True):
        print('  ', m.span(), m.span('g1'))
        sys.stdout.flush()

    print('DID NOT CRASH - upstream may have fixed it; re-judge ledger entry 5')
    return 0


if __name__ == '__main__':
    sys.exit(main())
