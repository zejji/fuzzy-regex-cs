r"""Row 14 of the S57b gate, and the limit of the anchor-reachability instrument.

`upstream-partial-anchor-reachability.py` asks upstream's own `match(pos, endpos, partial=True)`
at every anchor in the searched region and collects the spans it will produce. When upstream's
`search` answers a span that is in none of them, upstream has reported something its own matcher
cannot make - the argument `search-start-partial` and both `partial-retry-*` entries rest on.

Seed 20260920 row 99286 is the row that shows where the instrument stops working::

    python tools/probes/s57b-anchor-sweep-reads-past-the-anchor.py

The pattern is reversed and ends in `(?(?=[^a])[^\d]|\p{ASCII})`. Matching right to left, that
conditional is tried FIRST, at the right-hand end of the span, and its lookahead reads to the
right of it. An anchored call truncates the subject at `endpos`, so the lookahead sees
end-of-text instead of the U+200D that is really there and the conditional takes its other
branch. The anchored call is then asking a different question, and its None says nothing about
the span the search reported.

Forcing the branch the untruncated subject takes puts the span back in the grid, at the highest
answering endpos - which is the anchor a reversed search owes.

Written by S57b against regex 2026.9.10, 2026-09-20.
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

SUBJECT = "\U0001f3fb\U0001f600\U0001d518‍‍\U0001f600\U0001f600"
SKIP = r"(?r)^(?:.(*SKIP)[^\d]|[a-f])\U0001f600(?(?=[^a])[^\d]|\p{ASCII})"
HEAD = r"(?r)^(?:.(*PRUNE)[^\d]|[a-f])\U0001f600"
FLAGS = regex.MULTILINE

SPELLINGS = (
    ("(*SKIP), as recorded", SKIP),
    ("(*PRUNE)", SKIP.replace("(*SKIP)", "(*PRUNE)")),
    ("verb deleted", SKIP.replace("(*SKIP)", "")),
)


def span(match):
    return "None" if match is None else str(match.span())


def sweep(compiled):
    return [
        (endpos, compiled.match(SUBJECT, 0, endpos, partial=True).span())
        for endpos in range(len(SUBJECT) + 1)
        if compiled.match(SUBJECT, 0, endpos, partial=True) is not None
    ]


print("regex", regex.__version__)
print()
print("the row, and its two controls")
for name, pattern in SPELLINGS:
    compiled = regex.compile(pattern, FLAGS)
    print(f"  {name:22} search {span(compiled.search(SUBJECT, partial=True))}")

print()
print("the anchor sweep, which answers at endpos 0 and nowhere else under every spelling")
for name, pattern in SPELLINGS:
    print(f"  {name:22} {sweep(regex.compile(pattern, FLAGS))}")

print()
print("why: the lookahead at position 3 reads past the anchor")
lookahead = regex.compile(r"(?=[^a])")
print("  full subject        ", lookahead.match(SUBJECT, 3) is not None)
print("  truncated at endpos ", lookahead.match(SUBJECT, 3, 3) is not None)

print()
print("force the branch the untruncated subject takes, and the span is back in the grid")
for name, tail in (
    ("conditional as written", r"(?(?=[^a])[^\d]|\p{ASCII})"),
    ("then-branch forced", r"[^\d]"),
    ("else-branch forced", r"\p{ASCII}"),
):
    compiled = regex.compile(HEAD + tail, FLAGS)
    print(
        f"  {name:24} search {span(compiled.search(SUBJECT, partial=True))}"
        f"  anchors {sweep(compiled)}"
    )
