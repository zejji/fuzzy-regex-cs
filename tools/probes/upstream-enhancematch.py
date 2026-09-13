"""What ENHANCEMATCH answers, measured rather than reasoned about (S41, 2026-09-13).

Covers the six shapes the S41 gap tests pin: a match '(?e)' improves, one it cannot, a cost
equation where ranking by cost and ranking by error count disagree, '(?e)' with '(?r)', '(?e)'
inside a scan (which is what the restored slice makes correct), and the counts and changes of an
improved match.

Run:  python tools/probes/upstream-enhancematch.py
"""

import regex

print("regex version:", regex.__version__)
print()


def show(label, m):
    if m is None:
        print(f"{label}: no match")
        return
    print(
        f"{label}: span={m.span()} value={m.group()!r} counts={m.fuzzy_counts} "
        f"changes={m.fuzzy_changes} groups={[m.span(g) for g in range(1, m.re.groups + 1)]}"
    )


# --- a match ENHANCEMATCH improves, and the same pattern without the flag ---------------------
show("A improves    ", regex.fullmatch(r"(?e)(?:x|xyq){e<=2}", "xyz"))
show("A plain       ", regex.fullmatch(r"(?:x|xyq){e<=2}", "xyz"))

# --- a match it cannot improve: one substitution is already the best this pattern can do ------
show("B cannot      ", regex.match(r"(?e)(?:[ab][cd][ef]){e<=1}", "acx"))
show("B plain       ", regex.match(r"(?:[ab][cd][ef]){e<=1}", "acx"))

# --- cost ranking versus count ranking ---------------------------------------------------------
# Two insertions cost 1 each; one substitution costs 9. Upstream ranks by error COUNT and so
# takes the single, more expensive substitution. This port ranks by cost (DECISIONS 2026-09-12).
show("C cost        ", regex.fullmatch(r"(?e)(?:x|xyq){1i+9s+9d<=20}", "xyz"))
show("C plain       ", regex.fullmatch(r"(?:x|xyq){1i+9s+9d<=20}", "xyz"))
show("C captured    ", regex.fullmatch(r"(?e)((?:x)|(?:xyq)){1i+9s+9d<=20}", "xyz"))

# --- '(?e)' with '(?r)' ------------------------------------------------------------------------
show("D reverse     ", regex.fullmatch(r"(?er)(?:x|xyq){e<=2}", "xyz"))
show("D forward     ", regex.fullmatch(r"(?e)(?:x|xyq){e<=2}", "xyz"))
print(
    "D reverse scan:",
    [m.span() for m in regex.finditer(r"(?er)(?:q|xyq){e<=1}", "xyzxyz")],
)

# --- '(?e)' in a scan: the slice the loop narrowed has to go back before the next match ---------
print(
    "E scan spans  :",
    [(m.span(), m.fuzzy_counts) for m in regex.finditer(r"(?e)(?:x|xyq){e<=2}", "xyzxyz")],
)
print(
    "E scan values :",
    regex.findall(r"(?e)(?:x|xyq){e<=2}", "xyzxyz"),
)

# --- counts and changes of an improved match ---------------------------------------------------
show("F changes     ", regex.match(r"(?e)(?:bc){e}", "c"))
show("F plain       ", regex.match(r"(?:bc){e}", "c"))
