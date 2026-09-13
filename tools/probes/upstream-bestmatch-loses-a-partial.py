r"""`(?b)` makes upstream lose a fuzzy partial that the same pattern without the flag still finds.

S43, found by the composed `interactions` wave at the Phase 5 close. Five rows of a 6000-row
three-seed default wave are this family: seed 7 rows 74938 (`match`) and 77937 (`search`), seed 4242
rows 76251 (`fullmatch`) and 76681 (`search`), seed 20260913 row 76593 (`search`).

THE JUDGEMENT NEEDS NO SECOND ENGINE, and it rests on TWO arguments of different strength. Keeping
them apart matters, and the scope of each has now been stated wrongly three times: the first draft
claimed the strong form for all five rows with no evidence, a blind review cut it to two by sweeping
`pos` alone, a second draft over-corrected to all five, and the measurement says FOUR. Each argument
below carries its own scope for that reason.

  * **Holds on all five.** Deleting `(?b)` gives upstream a match it refused with the flag present:
    the five rows become (0,3)P, (0,7)P, (0,1)P, (4,5)P and (8,8)P. `BESTMATCH` is documented as
    finding the *best* match rather than the first, so a flag that turns a match into no match at
    all is upstream contradicting its own documentation. `(?e)` in the same place does not do it, so
    it is `do_best_fuzzy_match` and not fuzzy ranking at large.

    **Those flagless answers are this port's answer IN FULL on four of the five - and on row 77937
    only the SPAN agrees.** Upstream flagless spends no errors and captures nothing there; this port
    answers the same (0,7) span with `fuzzy=(1,1,1)` and group 2 set. So the argument for 77937 is
    "upstream refused a match it finds without the flag", which is the whole of the weak form, and
    NOT "upstream's flagless answer is ours". A first version of this file claimed the latter for
    all five because its comparison printed `m.span()` alone; the blind review caught it, and the
    second section below now prints the groups and the counts so the claim and the measurement are
    the same thing.

  * **Holds on FOUR of the five: the SAME compiled pattern - the flag still on - answers None from
    `search(partial=True)` and a partial from its own ANCHORED door.** No reading of any ranking
    rule lets a search miss what its own anchored match finds. Rows 76681 and 76593 answer by `pos`;
    the two `(?r)` rows, 74938 and 77937, answer by `endpos`, which is where a reversed pattern
    anchors (74938 at endpos 1, 77937 at endpos 2, 4 and 5). An earlier version of this file swept
    `pos` alone and so found only two, because a reversed row has no `pos` answer to find.

    **The fifth is 76251, and it rests on the weak form alone.** It is forward, and its only
    anchored answer is the degenerate empty slice at endpos 0 - which the caveat below disqualifies,
    so it does not count. A draft that said "all five" was counting it.

    **The caveat, and a report needs it**: truncating a FORWARD pattern's subject with `endpos`
    changes what a trailing `$`, `\Z` or lookahead means, so an endpos hit is a contradiction only
    for a pattern that reads nothing at the end. It is the natural door for a `(?r)` row, which
    anchors there, and a weaker argument for a forward one.

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

# The five wave rows WHOLE, each asked the operation the wave asked it, because two of them are
# not `search` rows at all - 74938 is a `match` and 76251 a `fullmatch` - and the docstring's
# weak-form claim is about all five. Each is replayed
# twice: as the wave drew it, and with the leading `(?b)` deleted and nothing else changed. S43's
# blind-review lesson from sitting 1 applies here - a claim about five rows that runs one row is an
# assertion, so the second argument is measured per row rather than described.
#
# Every span below is in CODEPOINTS, which is what Python counts; the oracle report renders this
# port's answer in UTF-16 code units, so an astral subject differs by the surrogate count.
WAVE_ROWS = [
    (
        "seed 7 row 74938",
        r"(?b)(?r)(?:[^\d]+(*SKIP)\p{L}|[^\d])(?:(?:(\p{Nd}{1})(?:(?P<g2>\p{ASCII})){e<=2,i<=1}){s<=1,i<=1,d<=1}(*SKIP)\p{L}|\w)\D",
        " \U00010428 ",
        "match",
    ),
    (
        "seed 7 row 77937",
        r"(?b)(?r)^(\d)(?:([^\d]{3,4}?)a(?:[[:alpha:]]{2,3}?){e<=2,s<=1:[A-Za-z_]}){s<=1,i<=1,d<=1}(?:\S*?(*SKIP)\w|[^\d])",
        "a\n\U00010400\r\na\U0001d518",
        "search",
    ),
    (
        "seed 4242 row 76251",
        r"(?b)(?e)^(?:(?:AA){s<=1,i<=1,d<=1}(*SKIP)\p{ASCII}|\s)(?:(?:A([[a-z]--[aei]])(?:(\D*)){e<=2,s<=1}){i<=1}(*SKIP)\p{ASCII}|[A-Z])",
        "A",
        "fullmatch",
    ),
    (
        "seed 4242 row 76681",
        r"(?b)(?e)(?:a\w){s<=1,i<=1,d<=1}(?:\S(*SKIP)[\p{L}\p{N}]|\W)",
        "\r\na\U0001d518\n",
        "search",
    ),
    (
        "seed 20260913 row 76593",
        "(?b)ﬁ(?:(?:(.)ﬁﬁ){s<=1:[^a-z]}(*SKIP)[A-Z]|\\p{ASCII})(?P<g2>[[:digit:]])?",
        "ﬁﬁﬁﬁßß\n ",
        "search",
    ),
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

say("")
say("--- the five wave rows whole, each asked its own operation, with and without (?b) ---")
say("SPANS ARE NOT ENOUGH HERE, and a blind review proved it: a first version of this section")
say("printed m.span() alone while the docstring claimed the flagless answer WAS this port's")
say("answer, groups and counts included. On row 77937 only the span agrees. Full answers below.")


def full(m):
    """Everything the claim is about: the span, the partial flag, the groups and the errors."""
    if m is None:
        return "None"
    spans = [m.span(i) for i in range(1, m.re.groups + 1)]
    return f"{m.span()}{'P' if m.partial else ''} spans={spans} fuzzy={m.fuzzy_counts}"


for label, pattern, subject, operation in WAVE_ROWS:
    assert pattern.startswith("(?b)"), pattern
    flagless = pattern[len("(?b)") :]

    with_flag = getattr(regex.compile(pattern), operation)(subject, partial=True, timeout=10.0)
    without = getattr(regex.compile(flagless), operation)(subject, partial=True, timeout=10.0)

    say(f"{label:24} {operation:10} (?b)={full(with_flag)}")
    say(f"{'':24} {'':10} none={full(without)}")

# THE ANCHORED DOOR OF A REVERSED PATTERN IS endpos, NOT pos, and a blind review caught this section
# sweeping only `pos` while the docstring claimed "nothing from any door at any position". Sweeping
# both, FOUR of the five have an anchored answer their own `search` refuses - 76681 and 76593 by
# `pos`, and the two `(?r)` rows by `endpos`.
#
# 76251 is the fifth and does NOT count: it is forward, and its only endpos hit is the degenerate
# empty slice at 0. Truncating a forward subject with `endpos` changes what a trailing `$`, `\Z` or
# lookahead means, so an endpos hit argues only for a pattern that reads nothing at the end; it is
# the natural door for a `(?r)` row, which anchors there. A draft that said "all five" here was
# counting the empty slice its own caveat disqualifies.
say("")
say("--- both anchored doors, swept: pos (a forward anchor) and endpos (a reversed one) ---")
for label, pattern, subject, operation in WAVE_ROWS:
    compiled = regex.compile(pattern)
    by_pos, by_end = [], []
    for i in range(len(subject) + 1):
        m = compiled.match(subject, i, partial=True, timeout=10.0)
        if m is not None:
            by_pos.append(f"pos{i}:{m.span()}{'P' if m.partial else ''}")
        m = compiled.match(subject, 0, i, partial=True, timeout=10.0)
        if m is not None:
            by_end.append(f"end{i}:{m.span()}{'P' if m.partial else ''}")
    say(f"{label:24} {'(?r)' if '(?r)' in pattern else '    '} search={full(compiled.search(subject, partial=True, timeout=10.0))}")
    say(f"{'':24}      by pos:    {' '.join(by_pos) or 'nothing'}")
    say(f"{'':24}      by endpos: {' '.join(by_end) or 'nothing'}")
