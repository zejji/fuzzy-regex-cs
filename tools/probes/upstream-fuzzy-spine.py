"""Ground truth for S38's fuzzy spine: counts and change positions for one-character items.

Run with the upstream interpreter, e.g. `.venvs/regex-2026.9.10/Scripts/python.exe`, or with the
PyPI fallback the oracle itself uses. The version it ran on is the first line of its output.

Every pattern here has a fuzzy section made ONLY of one-character and zero-width items, which is
the subset S38 delivers. That is why no two literal characters ever sit next to each other:
`Sequence.pack_characters` (upstream/regex/_regex_core.py:3526) packs a run of two or more
`Character` items into one STRING node, and a fuzzy STRING is S39's. Anything that is not a
positive, non-zero-width `Character` - a class, a property, a dot, an assertion - flushes the run,
so `(?:[ab][cd])` is two one-character items where `(?:ab)` is one string.

Upstream issues 607 and 608 were crashes in exactly these shapes (upstream/changelog.txt:7-8), so
the run itself is evidence: it prints a version and does not crash.
"""

import regex


def show(label, m):
    if m is None:
        print(f"{label}: None")
        return
    print(
        f"{label}: span={m.span()} group={ascii(m.group())} counts={m.fuzzy_counts} "
        f"changes={m.fuzzy_changes} partial={m.partial}"
    )


print("regex", regex.__version__)

# One substitution, one insertion, one deletion, each on its own.
show("sub    match('(?:[ab][cd][ef]){e<=1}', 'acx')", regex.match(r"(?:[ab][cd][ef]){e<=1}", "acx"))
show("ins    fullmatch('(?:[ab][cd]){e<=1}', 'axc')", regex.fullmatch(r"(?:[ab][cd]){e<=1}", "axc"))
show("ins    fullmatch('(?:[ab][cd]){e<=1}', 'acx')", regex.fullmatch(r"(?:[ab][cd]){e<=1}", "acx"))
show("del    match('(?:[ab][cd][ef]){e<=1}', 'ae')", regex.match(r"(?:[ab][cd][ef]){e<=1}", "ae"))

# Each error kind constrained on its own, so the engine cannot pick a cheaper one.
show("s<=1   match('(?:[ab][cd][ef]){s<=1}', 'acx')", regex.match(r"(?:[ab][cd][ef]){s<=1}", "acx"))
show("s<=0   match('(?:[ab][cd][ef]){s<=0}', 'acx')", regex.match(r"(?:[ab][cd][ef]){s<=0}", "acx"))
show("i<=1   fullmatch('(?:[ab][cd]){i<=1}', 'axc')", regex.fullmatch(r"(?:[ab][cd]){i<=1}", "axc"))
show("d<=1   match('(?:[ab][cd][ef]){d<=1}', 'ae')", regex.match(r"(?:[ab][cd][ef]){d<=1}", "ae"))

# A cost equation: a substitution costs 2 and the budget is 1, so only a deletion fits.
show("cost   match('(?:[ab][cd][ef]){2s+1d<=1}', 'acx')", regex.match(r"(?:[ab][cd][ef]){2s+1d<=1}", "acx"))
show("cost   match('(?:[ab][cd][ef]){2s+1d<=1}', 'ae')", regex.match(r"(?:[ab][cd][ef]){2s+1d<=1}", "ae"))

# Properties, ranges and the dot, all one-character items.
show(r"prop   match('(?:\w\w\w){e<=1}', 'ab!')", regex.match(r"(?:\w\w\w){e<=1}", "ab!"))
show(r"range  match('(?:[a-c][a-c]){e<=1}', 'ax')", regex.match(r"(?:[a-c][a-c]){e<=1}", "ax"))
show(r"neg    match('(?:[^x][^x]){e<=1}', 'ax')", regex.match(r"(?:[^x][^x]){e<=1}", "ax"))
show("dot    fullmatch('(?:...){e<=1}', 'abcd')", regex.fullmatch(r"(?:...){e<=1}", "abcd"))

# A zero-width item inside the fuzzy section. A zero-width item consumes nothing, so it can be
# neither deleted nor substituted; an insertion is the only error left, and it moves the position.
show(r"zw     match(r'(?:\b[fg][op]){e<=1}', 'xfo')", regex.match(r"(?:\b[fg][op]){e<=1}", "xfo"))
show(r"zw     search(r'(?:\b[fg][op]){e<=1}', 'xfo')", regex.search(r"(?:\b[fg][op]){e<=1}", "xfo"))
show(r"zw     match(r'(?:[fg][op]\b){e<=1}', 'fox')", regex.match(r"(?:[fg][op]\b){e<=1}", "fox"))
show(r"zw     match(r'(?:^[fg][op]){e<=1}', 'fo')", regex.match(r"(?:^[fg][op]){e<=1}", "fo"))
show(r"zw     search(r'(?:[ab]$[cd]){e<=1}', 'ac')", regex.search(r"(?:[ab]$[cd]){e<=1}", "ac"))
show(r"zw     search(r'(?:[fg][op]$){e<=1}', 'fox')", regex.search(r"(?:[fg][op]$){e<=1}", "fox"))

# Reverse matching.
show(r"rev    search('(?r)(?:[ab][cd]){e<=1}', 'ax')", regex.search(r"(?r)(?:[ab][cd]){e<=1}", "ax"))
show(r"rev    search('(?r)(?:[ab][cd][ef]){e<=1}', 'zacx')", regex.search(r"(?r)(?:[ab][cd][ef]){e<=1}", "zacx"))

# Partial matching, which reaches check_fuzzy_partial.
show(
    "part   match('(?:[ab][cd][ef][gh]){e<=1}', 'ac', partial=True)",
    regex.match(r"(?:[ab][cd][ef][gh]){e<=1}", "ac", partial=True),
)
show(
    "part   match('(?:[ab][cd][ef][gh]){e<=1}', 'acx', partial=True)",
    regex.match(r"(?:[ab][cd][ef][gh]){e<=1}", "acx", partial=True),
)

# A nested fuzzy section: the inner counts are pushed and popped around the outer ones.
show(
    "nest   match('(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}', 'axeg')",
    regex.match(r"(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}", "axeg"),
)
show(
    "nest   match('(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}', 'axey')",
    regex.match(r"(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}", "axey"),
)

# Two errors, to show the order the change list is reported in, and two deletions, to show the
# shift match_fuzzy_changes applies to each one after the first.
show("two    match('(?:[ab][cd][ef][gh]){e<=2}', 'axey')", regex.match(r"(?:[ab][cd][ef][gh]){e<=2}", "axey"))
show("twodel match('(?:[ab][cd][ef][gh]){e<=2}', 'ag')", regex.match(r"(?:[ab][cd][ef][gh]){e<=2}", "ag"))

# An exact match of a fuzzy pattern, and an atomic group inside one - the second is what says
# whether the fuzzy counts are restored when backtracking leaves the group.
show("exact  match('(?:[ab][cd]){e<=2}', 'ac')", regex.match(r"(?:[ab][cd]){e<=2}", "ac"))
show(r"atomic match('(?:(?>[ab]+)[cd]){e<=1}', 'aax')", regex.match(r"(?:(?>[ab]+)[cd]){e<=1}", "aax"))
show(r"atomic match('(?:(?>[ab])[cd]){e<=1}', 'ax')", regex.match(r"(?:(?>[ab])[cd]){e<=1}", "ax"))
show(r"atomic match('(?:(?>[ab]|[ax])[cd]){e<=1}', 'xy')", regex.match(r"(?:(?>[ab]|[ax])[cd]){e<=1}", "xy"))
show(r"look   match('(?:[ab](?=[cd])[cd]){e<=1}', 'ax')", regex.match(r"(?:[ab](?=[cd])[cd]){e<=1}", "ax"))
show(r"look   match('(?:[ab](?![cd])[ef]){e<=1}', 'ax')", regex.match(r"(?:[ab](?![cd])[ef]){e<=1}", "ax"))
show(r"cond   match('(?:([ab])(?(1)[cd]|[ef])){e<=1}', 'ax')", regex.match(r"(?:([ab])(?(1)[cd]|[ef])){e<=1}", "ax"))

# finditer, so the counts are per match rather than cumulative.
print(
    "iter   finditer('(?:[ab][cd]){e<=1}', 'ac ax'):",
    [(m.span(), m.fuzzy_counts, m.fuzzy_changes) for m in regex.finditer(r"(?:[ab][cd]){e<=1}", "ac ax")],
)

# An astral subject: upstream reports positions in codepoints, this port in UTF-16 code units.
show(r"astral match('(?:.[ab]){e<=1}', '\U0001F600c')", regex.match(r"(?:.[ab]){e<=1}", "\U0001F600c"))
show(r"astral match('(?:[ab].[cd]){e<=1}', 'a\U0001F600')", regex.match(r"(?:[ab].[cd]){e<=1}", "a\U0001F600"))

# \G inside a fuzzy section. SEARCH_ANCHOR is the one zero-width opcode that upstream's backtrack
# switch has no case for (:15330-15344 lists every other one), so a fuzzed \G that is backtracked
# into falls through to 'default: return RE_ERROR_ILLEGAL' (:17395).
try:
    show(r"anchor match(r'(?:\G[fg][op]){e<=1}', 'xfo')", regex.match(r"(?:\G[fg][op]){e<=1}", "xfo"))
except Exception as exc:  # noqa: BLE001 - the point is to see whatever comes out
    print("anchor match(r'(?:\\G[fg][op]){e<=1}', 'xfo'): raised", type(exc).__name__, exc)
