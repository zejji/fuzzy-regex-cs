r"""`(?b)` makes upstream lose a fuzzy partial that the same pattern without the flag still finds.

S43, found by the composed `interactions` wave at the Phase 5 close. Five rows of a 6000-row
three-seed default wave are this family: seed 7 rows 74938 (`match`) and 77937 (`search`), seed 4242
rows 76251 (`fullmatch`) and 76681 (`search`), seed 20260913 row 76593 (`search`).

THE JUDGEMENT NEEDS NO SECOND ENGINE, and it rests on TWO arguments of different strength. Keeping
them apart matters, because a first draft of this docstring claimed the stronger one for all five
rows and its blind review disproved that on three of them.

  * **Holds on all five.** Deleting `(?b)` gives upstream a match it refused with the flag present:
    the five rows become (0,3)P, (0,7)P, (0,1)P, (4,5)P and (8,8)P, and those are this port's
    answers. `BESTMATCH` is documented as finding the *best* match rather than the first, so a flag
    that turns a match into no match at all is upstream contradicting its own documentation. `(?e)`
    in the same place does not do it, so it is `do_best_fuzzy_match` and not fuzzy ranking at large.

  * **Holds on two of the five, and on the minimised shape below.** The SAME compiled pattern - the
    flag still on - answers None from `search(partial=True)` and a partial from
    `match(subject, pos, partial=True)` at a position inside the searched region. That is the
    stronger form, because no reading of any ranking rule lets a search miss what its own anchored
    match finds. Rows 76681 and 76593 show it. Rows 74938, 77937 and 76251 do NOT: with `(?b)` on,
    upstream finds nothing from any door at any position, so for those three only the first
    argument applies.

The conditions, each necessary on the minimised shape:
  * `(?b)`;
  * a fuzzy section;
  * a `(*SKIP)` - the same pattern without the verb keeps its match under `(?b)`;
  * `partial=True`.

Minimum:  (?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)  over 'ab.'
          upstream search(partial=True) -> None
          upstream match('ab.', 2, partial=True) -> (2, 3) partial     <- same compiled pattern
          the same pattern without (?b): search -> (0, 3) partial      <- this port's answer

The faulting function is `do_best_fuzzy_match` (upstream/src/_regex.c:17584); the exact line is not
pinned here, and Phase 6's upstream report owns finishing that.

Pinned in tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyBestMatchTests.cs.

Measured 2026-09-13 against regex 2026.7.19.
"""

import sys

import regex

CASES = [
    (r"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", "ab.", "the minimum - upstream loses it"),
    (r"(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", "ab.", "no (?b) - upstream keeps it"),
    (r"(?e)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", "ab.", "(?e) instead - upstream keeps it"),
    (r"(?b)(?:ab){e<=1}(?:\S\w|\W)", "ab.", "no verb - upstream keeps it"),
    (r"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", ".ab.", "a longer subject"),
    (r"(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", ".ab.", "the same without (?b)"),
    (r"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", "xaby", "a third subject"),
    (r"(?:ab){e<=1}(?:\S(*SKIP)\w|\W)", "xaby", "the same without (?b)"),
    # The wave's own row, reduced only by dropping (?e), which changes nothing.
    (r"(?b)(?:a\w){s<=1,i<=1,d<=1}(?:\S(*SKIP)[\p{L}\p{N}]|\W)", "\r\na\U0001d518\n", "seed 4242 row 76681"),
    (r"(?:a\w){s<=1,i<=1,d<=1}(?:\S(*SKIP)[\p{L}\p{N}]|\W)", "\r\na\U0001d518\n", "the same without (?b)"),
]


def say(text):
    sys.stdout.buffer.write(text.encode("utf-8", "backslashreplace") + b"\n")


for pattern, subject, why in CASES:
    compiled = regex.compile(pattern)
    found = compiled.search(subject, partial=True, timeout=10.0)

    # The same compiled pattern, asked at every start. This is the contradiction.
    anchored = []
    for start in range(len(subject) + 1):
        m = compiled.match(subject, start, partial=True, timeout=10.0)
        if m is not None:
            anchored.append(f"{start}:{m.span()}{'P' if m.partial else ''}")

    shown = f"{found.span()}{'P' if found.partial else ''}" if found else "None"
    lost = "   *** search LOST what its own match finds ***" if found is None and anchored else ""
    say(f"{pattern!r:56} {subject!r:12} search={shown:11} anchored[{' '.join(anchored)}]{lost}")
    say(f"    {why}")
