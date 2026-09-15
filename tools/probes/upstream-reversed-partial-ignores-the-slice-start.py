"""Upstream's two rules for a reversed partial that runs out of text, and where they disagree.

S52 sitting 10, 2026-09-15, regex 2026.9.10. The row this judges is seed 20260915 row 104366 of
the 6000-row gate: `(?r)\xdfﬁ(.*?)\b` asked as `match(subject, 2, 2, partial=True)` over
'ﬁı', where upstream answers a zero-width partial at (2, 2) and this port answers no
match.

Upstream holds TWO rules for "has a reversed match run out of text on the left", and they disagree
about whether the slice start counts:

  * `init_match` (upstream/src/_regex.c:18442-18446) sets `text_end = end` - the SLICE end - but
    `text_start = 0`, the real string start, under the comment "Open start and closed end bounds,
    like in re module". Five lines above (:18435-18437) sits the contract the asymmetry breaks: "The
    documentation says that the end of the slice behaves like the end of the string."
  * Every node handler then asks `text_pos <= state->text_start && partial_side == RE_PARTIAL_LEFT`
    (:6747, :12173, :13854, :14502 and about thirty more), so a reversed match that runs out at a
    NON-ZERO slice start reports no match at all.
  * `search_start` - the optimiser's entry, taken only for the pattern shapes it is enabled for -
    asks `start_pos < state->slice_start` and returns `RE_ERROR_PARTIAL` positioned at
    `slice_start` (:8400-8405); so do the `search_start_STRING*_REV` helpers, which pass
    `state->slice_start` as the limit and let `string_search_rev` set `is_partial` there
    (:8335-8382 - `_FLD_REV` at :8335, `_IGN_REV` at :8361, `_REV` at :8373).

So whether a reversed partial is reported at a non-zero slice start depends on whether the
optimiser picked the pattern's leading string as the search test - a decision about SPEED, which
must not change the answer. Upstream's own test suite never asks a reversed partial at a non-zero
pos: the suite makes 72 `partial=True` calls and not one of them passes a `pos`. That is why the
two rules have never met.

Run: python tools/probes/upstream-reversed-partial-ignores-the-slice-start.py
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

SUBJECT = "ﬁı"
DRAWN = r"(?r)\xdfﬁ(.*?)\b"


def answer(pattern, subject, pos, endpos):
    match = regex.compile(pattern).match(subject, pos, endpos, partial=True)
    if match is None:
        return "None"
    return f"({match.start()}, {match.end()}) partial={match.partial}"


def block(title, rows):
    print(f"\n=== {title}")
    for label, pattern, subject, pos, endpos in rows:
        print(f"  {label:<46} {answer(pattern, subject, pos, endpos)}")


print(f"regex {regex.__version__}")

# THE CONTROL. Three patterns over the SAME one visible character, 'a'. They differ only in a
# trailing group that can match nothing but the empty string where the match ends, so on this
# question they are the same regex. Upstream answers all three the same way when that character is
# the whole subject, and two of the three differently when it is a slice of a longer one.
block(
    "the control: one visible character, as the whole subject and as a slice",
    [
        ("(?r)ya          over 'a', no slice", r"(?r)ya", "a", 0, 1),
        ("(?r)ya(.*?)\\b   over 'a', no slice", r"(?r)ya(.*?)\b", "a", 0, 1),
        ("(?r)ya(.*)\\b    over 'a', no slice", r"(?r)ya(.*)\b", "a", 0, 1),
        ("(?r)ya          over 'xya' slice (2,3)", r"(?r)ya", "xya", 2, 3),
        ("(?r)ya(.*?)\\b   over 'xya' slice (2,3)", r"(?r)ya(.*?)\b", "xya", 2, 3),
        ("(?r)ya(.*)\\b    over 'xya' slice (2,3)", r"(?r)ya(.*)\b", "xya", 2, 3),
    ],
)

# The greedy/lazy pair on its own. Preference order chooses AMONG matches; it cannot decide whether
# one exists, and over the one character the slice shows, `(.*)` and `(.*?)` have the same single
# possible behaviour - match nothing.
block(
    "greedy against lazy, same slice, same visible text",
    [
        ("(?r)ya(.*?)\\b   'xya' slice (2,3)  LAZY", r"(?r)ya(.*?)\b", "xya", 2, 3),
        ("(?r)ya(.*)\\b    'xya' slice (2,3)  GREEDY", r"(?r)ya(.*)\b", "xya", 2, 3),
        ("(?r)ya(.?)\\b    'xya' slice (2,3)  BOUNDED", r"(?r)ya(.?)\b", "xya", 2, 3),
    ],
)

# The drawn row, and the same pattern cut back one construct at a time. Four of the five cuts
# answer None, which is this port's answer to all six; the fifth - dropping only the trailing
# `\b` - still answers the partial, so it is the LAZY REPEAT after the two-character literal
# that carries it and not the boundary.
block(
    "the drawn row and its cuts, empty slice (2, 2)",
    [
        ("as drawn   (?r)\\xdf\\ufb01(.*?)\\b", DRAWN, SUBJECT, 2, 2),
        ("no trailing \\b", r"(?r)\xdfﬁ(.*?)", SUBJECT, 2, 2),
        ("no lazy group", r"(?r)\xdfﬁ\b", SUBJECT, 2, 2),
        ("neither", r"(?r)\xdfﬁ", SUBJECT, 2, 2),
        ("one literal only", r"(?r)\xdf(.*?)\b", SUBJECT, 2, 2),
        ("greedy instead of lazy", r"(?r)\xdfﬁ(.*)\b", SUBJECT, 2, 2),
    ],
)

# An empty slice shows the matcher zero characters, so nothing inside it can tell one position from
# another - and yet upstream's answer moves with the position, in OPPOSITE directions for two
# patterns.
block(
    "an empty slice is an empty slice: (?r)ab(.*?)\\b at every empty slice of 'xyz'",
    [(f"pos = endpos = {p}", r"(?r)ab(.*?)\b", "xyz", p, p) for p in range(4)]
    + [("the empty subject", r"(?r)ab(.*?)\b", "", 0, 0)],
)
block(
    "and (?r)a at every empty slice of 'xyz' - the same question, inverted",
    [(f"pos = endpos = {p}", r"(?r)a", "xyz", p, p) for p in range(4)],
)

# The min-width inversion, read off the two blocks above: `(?r)a` needs ONE character and reports
# no match; `(?r)ab(.*?)\b` needs TWO and reports a partial, at the very same empty slice. A
# partial is "the available text ran out", so needing more of it cannot make it run out less.
block(
    "the min-width inversion, side by side at the empty slice (2, 2) of 'xyz'",
    [
        ("(?r)a          needs 1 character", r"(?r)a", "xyz", 2, 2),
        ("(?r)ab         needs 2", r"(?r)ab", "xyz", 2, 2),
        ("(?r)ab(.*?)\\b  needs 2", r"(?r)ab(.*?)\b", "xyz", 2, 2),
        ("(?r)abc(.*?)\\b needs 3", r"(?r)abc(.*?)\b", "xyz", 2, 2),
    ],
)

# The forward side, for contrast: it honours the slice end exactly as init_match's own comment
# promises, at every position and for every one of these patterns. The asymmetry is the reverse
# side's alone.
block(
    "forward, the same shapes - the slice end behaves like the string end, uniformly",
    [
        ("a              'xyz' slice (2,2)", r"a", "xyz", 2, 2),
        ("ab(.*?)\\b      'xyz' slice (2,2)", r"ab(.*?)\b", "xyz", 2, 2),
        ("ab(.*)\\b       'xyz' slice (2,2)", r"ab(.*)\b", "xyz", 2, 2),
        # The exact forward mirror of the control: one visible character, 'a', with the pattern
        # running out of text one character later. Forward it is a partial whether that character
        # is the whole subject or a slice of a longer one.
        ("ay             'ay'  no slice", r"ay", "ay", 0, 1),
        ("ay             'xya' slice (2,3)", r"ay", "xya", 2, 3),
    ],
)
