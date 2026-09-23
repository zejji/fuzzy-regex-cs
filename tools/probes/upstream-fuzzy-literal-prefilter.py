"""Upstream's answers for the fuzzy-literal prefilter's pinned rows (S60b item 10).

The prefilter is this port's own and upstream has none, so every row is a plain search whose
answer the prefilter must not change. Each row is chosen so that one specific mistake in the
prefilter would change it: a case fold the piece search cannot see, an offset or error budget left
out of the start bound, a slice bound read wrongly, or a partial match refused. The expected values
in tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyLiteralPrefilterTests.cs are this script's output.

Run: python tools/probes/upstream-fuzzy-literal-prefilter.py
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8")


def show(label, pattern, subject, flags=0, partial=False, pos=None, endpos=None):
    """Print one row's search answer: span, text and fuzzy counts."""
    kwargs = {}
    if pos is not None:
        kwargs["pos"] = pos
    if endpos is not None:
        kwargs["endpos"] = endpos
    if partial:
        kwargs["partial"] = True
    m = regex.search(pattern, subject, flags | regex.VERSION1, **kwargs)
    answer = "None" if m is None else f"span={m.span()} counts={m.fuzzy_counts} partial={m.partial}"
    print(f"{label:<34} {pattern!r:<44} {subject!r}")
    print(f"  search: {answer}")


def show_all(label, pattern, subject, flags=0):
    """Print every span finditer yields."""
    spans = [m.span() for m in regex.finditer(pattern, subject, flags | regex.VERSION1)]
    print(f"{label:<34} {pattern!r:<44} {subject!r}")
    print(f"  finditer: {spans}")


print(f"regex {regex.__version__}\n")

print("=== folds the piece search cannot see ===")
show("KELVIN SIGN folds to k", r"(?i)(?:kelvin works){e<=1}", "Kelvin wxrks")
show("sharp s folds to ss", r"(?i)(?:strasse lane){e<=1}", "straße lxne")
show("fi ligature folds to fi", r"(?i)(?:fine lanterns){e<=1}", "ﬁne lantxrns")
show("long s folds to s", r"(?i)(?:sunset boulevard){e<=1}", "ſunset boulevxrd")

print("\n=== the start bound ===")
show("piece offset", r"(?:abcdefgh){e<=1}", "zzabXdefgh")
show("error budget", r"(?:abcdefghi){e<=2}", "zzaYbcdefghi")
show("pos", r"(?:abcdefgh){e<=1}", "abcdefgh zz abcdXfgh", pos=5)
show("endpos", r"(?:abcdefgh){e<=1}", "abcdefgh zz abcdXfgh", endpos=6)
show("no piece at all", r"(?i)(?:amber lantern works){e<=2}", "a record about something else")

print("\n=== modes ===")
show("partial", r"(?:amber lantern works){e<=2}", "xx amb", partial=True)
show("reverse", r"(?r)(?:amber lantern works){e<=2}", "one amber lantxrn works two amber lantern wurks")
show("reverse, piece at the end", r"(?r)(?:amber lantern works){e<=2}", "nothing here but amber lantern wurks")
show("reverse, full-folded chain", r"(?fi)(?r)(?:stone fine){s<=1,i<=1}", "xebaxsizdrfkSTone Fineoociokw r lo")
show("reverse, no piece", r"(?r)(?:amber lantern works){e<=2}", "nothing to see here at all")
show("bestmatch", r"(?b)(?:amber lantern works){e<=2}", "amber lantxrn wxrks and amber lantern works")
show("enhancematch", r"(?e)(?:amber lantern works){e<=2}", "amber lantxrn wxrks and amber lantern works")
show("non-ASCII elsewhere", r"(?i)(?:amber lantern works){e<=2}", "café amber lantern wxrks")
show_all(
    "finditer",
    r"(?i)(?:amber lantern works){e<=2}",
    "x amber lantern works yy AMBER LANTRN WORKS zz amberlantern work! qq amber",
)
