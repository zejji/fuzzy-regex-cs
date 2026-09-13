#!/usr/bin/env python
"""S40a item 2: upstream never returns from a `(*SKIP)` inside an atomic group after an optional item.

Found by S40 at 6000 rows a generator - `verbs` row 5944, seed 4242 - where it killed the whole wave
silently, because `tools/record-oracle.py` had no per-row deadline at the time. Minimised by hand
from

    [^a]?\\U0001f600(?>[a\\d]{1,3}(*SKIP)\\p{Ll})  on  '\\U00010400\\r_\\U0001f600\\xdf\\U0001f600aA'

to the four-character subject below. Every row is bounded by `timeout=`, so this probe cannot hang:
a row that prints TIMEOUT is a row upstream never finishes.

Run it against whichever interpreter's `regex` you want to judge:

    python tools/probes/upstream-skip-in-atomic-hang.py
    python tools/probes/upstream-skip-in-atomic-hang.py --grid
    .venvs/regex-2026.9.10/Scripts/python tools/probes/upstream-skip-in-atomic-hang.py

Verdict, measured 2026-09-13: 2026.7.19 hangs on the row, 2026.9.10 answers None. The fix is
`b77694a`, "Git issue 613: (*SKIP) inside an atomic group, plus an equality-only scan stop",
released 2026.8.30 - past our pin. It adds two clamps to the GREEDY_REPEAT_ONE backtrack arm
(`if (pos < limit) limit = pos;` and its reversed twin), which is what stops the retreat loop
running away once a `(*SKIP)` has moved `slice_start` above the repeat's own start. Nothing to
file. Ledger entry 10.
"""

from __future__ import annotations

import sys
import time

import regex

TIMEOUT = 5.0

# The minimised row, then each of its three parts removed in turn. All three controls return, so
# the hang needs every one of a leading optional item, an atomic group, and `(*SKIP)` inside it.
CASES = (
    (r".?x(?>a(*SKIP)z)", "xzxa", "the minimised row"),
    (r".?x(?>a(*PRUNE)z)", "xzxa", "control: (*PRUNE) instead of (*SKIP)"),
    (r".?x(?:a(*SKIP)z)", "xzxa", "control: a non-atomic group"),
    (r"x(?>a(*SKIP)z)", "xzxa", "control: no leading optional item"),
    (
        "[^a]?\U0001f600(?>[a\\d]{1,3}(*SKIP)\\p{Ll})",
        "\U00010400\r_\U0001f600\xdf\U0001f600aA",
        "the original wave row",
    ),
)


# `--grid`: the same shape swept, which is what makes this port's answer mean anything. This port
# carries the PRE-FIX backtrack clamp (Matcher.cs's GreedyRepeatOne arm has upstream's slice clamp
# and not the `pos` clamp b77694a added), so "it does not hang on the one minimised row" is not
# "it cannot hang". `.scratch/probe-port-repeatone-skip-grid.ps1` puts the identical grid to this
# port; the comparison, measured 2026-09-13, is 70 of 1296 calls hanging on 2026.7.19, 0 of 1296
# in this port, 0 of 1296 on 2026.9.10. A grid that never reached the shape would give this port
# the same zero, which is why the upstream half is run at all.
GRID_REPEATS = (".?", ".*", ".+", ".{0,2}", ".{1,3}", "a?", "[ab]*", r"\w{0,3}", ".{2,4}")
GRID_BODIES = (
    "(?>a(*SKIP)z)",
    "(?>a(*SKIP)z)?",
    "(?>[ab](*SKIP)z)",
    "(?>a{1,2}(*SKIP)z)",
    "a(*SKIP)z",
    "(?>(?:a(*SKIP))z)",
)
GRID_SUBJECTS = ("xzxa", "xzxaa", "azxa", "aazz", "xaz", "zaxa", "aaaa", "xzxazz")
GRID_OPERATIONS = ("search", "match", "fullmatch")


def grid() -> None:
    ran = hangs = 0
    for repeat in GRID_REPEATS:
        for body in GRID_BODIES:
            pattern = repeat + "x" + body
            try:
                compiled = regex.compile(pattern)
            except regex.error:
                continue
            for subject in GRID_SUBJECTS:
                for operation in GRID_OPERATIONS:
                    ran += 1
                    try:
                        getattr(compiled, operation)(subject, timeout=TIMEOUT)
                    except TimeoutError:
                        hangs += 1
                        print(f"  TIMEOUT  {pattern:<26} {subject!r:<10} {operation}")

    print(f"  ran {ran} calls, {hangs} timed out")


def main() -> None:
    print("regex", regex.__version__)
    if "--grid" in sys.argv:
        grid()
        return

    for pattern, subject, note in CASES:
        started = time.perf_counter()
        try:
            answer = repr(regex.compile(pattern).search(subject, timeout=TIMEOUT))
        except TimeoutError:
            answer = f"TIMEOUT after {TIMEOUT}s"
        elapsed = time.perf_counter() - started
        # ascii(), not !r: the last case carries astral characters and a Windows console is cp1252,
        # so repr() raises UnicodeEncodeError and the probe dies after printing the finding.
        print(f"  {ascii(pattern)} on {ascii(subject)}\n    -> {answer}  [{elapsed:.2f}s]  # {note}")


if __name__ == "__main__":
    main()
