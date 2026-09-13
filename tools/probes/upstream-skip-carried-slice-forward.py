r"""A forward `(*SKIP)` leaves `slice_start` moved for a partial search's second pass.

S43, found by the seed-99991 `fuzzy,interactions` wave at the Phase 5 close - row 6897 of
`pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions -Seeds 99991`.

This is `partial-retry-reversed-slice`'s mechanism in a pattern that runs LEFT TO RIGHT. A `partial`
search runs two passes over one match attempt (upstream `do_match`, `upstream/src/_regex.c:18160`):
a non-partial pass, then a partial one from the same `text_pos`. Upstream restores `text_pos` and
nothing else, so a bound the verb moved in the first pass is still moved in the second. S40b
restores both bounds in this port; upstream does not.

WHAT THE MOVED BOUND COSTS IS DIFFERENT THIS WAY ROUND, which is why the reversed entry's own
argument does not transfer and why the two have separate entries. Both engines answer a partial at
the SAME span, codepoints (1, 4). They differ in which ALTERNATIVE the partial pass could still
enter, and so in which error was spent and which group captured.

THE JUDGEMENT IS THE `(*PRUNE)` CONTROL, and here it is the whole of the evidence rather than a
corroboration of an anchored sweep. `(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and
moves NO bound, so replacing one with the other isolates the bound move from the pattern's meaning:

    as the wave drew it        (1,4)P insertion at 3, no captures     <- upstream
    first verb -> (*PRUNE)     (1,4)P substitution at 1, (3,4)        <- this port's answer
    first verb deleted         (1,4)P substitution at 1, (3,4)        <- this port's answer
    second verb (*PRUNE) gone  (1,4)P insertion at 3, no captures     <- unchanged

The last line is a control this family has not had before: the pattern carries a SECOND verb, and
deleting it changes nothing, so the divergence is the first verb's and not "a verb somewhere in the
pattern".

Every span above is in CODEPOINTS, which is what Python counts; the oracle report renders this
port's answer in UTF-16 code units, and this subject's leading emoji is a surrogate pair.

Pinned in tests/FuzzyRegex.Tests/Gaps/Engine/PartialMatchingTests.cs; classified as
`partial-retry-carried-slice-forward` in tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.

Measured 2026-09-13 against regex 2026.7.19.
"""

import sys

import regex

# Row 6897 as the wave drew it. The first verb is the one under test; the second is the control.
PATTERN = (
    r"\b(?:(?:\ _(\W)){e<=1}(*SKIP)[A-Z]|[^a])"
    r"(?:.?(?:(\w+?)){i<=1:.}){e<=2,s<=1:[^a-z]}"
    r"(?:(?:[abz]([abz])){2i+1d+1s<=2}(*PRUNE)[\w\s]|\W)"
)
SUBJECT = "\U0001f600ß_ "

CASES = [
    (PATTERN, "as the wave drew it"),
    (PATTERN.replace("(*SKIP)", "(*PRUNE)", 1), "first verb (*SKIP) -> (*PRUNE), which moves no bound"),
    (PATTERN.replace("(*SKIP)", "", 1), "first verb deleted"),
    (PATTERN.replace("(*PRUNE)", ""), "second verb deleted, the (*SKIP) left alone - the control"),
]


def say(text):
    sys.stdout.buffer.write(text.encode("utf-8", "backslashreplace") + b"\n")


def show(m):
    if m is None:
        return "None"
    spans = [m.span(i) for i in range(1, m.re.groups + 1)]
    return f"{m.span()}{'P' if m.partial else ''} spans={spans} fuzzy={m.fuzzy_counts} changes={m.fuzzy_changes}"


say(f"subject {SUBJECT!r}, MULTILINE")
say("asked with partial=True, as the wave asked it:")
for pattern, why in CASES:
    m = regex.compile(pattern, flags=regex.M).search(SUBJECT, partial=True, timeout=10.0)
    say(f"  {show(m)}")
    say(f"      {why}")

# Without the partial there is no second pass to carry a bound into, so every case must agree - and
# they do, all None. That is what says the two passes are where the divergence lives.
say("")
say("the same four with no partial asked for, so there is no second pass to carry a bound into:")
for pattern, why in CASES:
    m = regex.compile(pattern, flags=regex.M).search(SUBJECT, timeout=10.0)
    say(f"  {show(m):24} {why}")
