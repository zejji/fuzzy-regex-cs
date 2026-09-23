"""S89: the three partial rows over a full-folded ligature, handed to S90.

Each row's pattern, its literal form (the group's text in place of the reference), and the forms
without full folding or without (?e), asked with and without partial=True. Then the smallest
literal relative of row 3874, where upstream loses a deletion right after a pattern-side ligature
(the port finds it: (0, 4) with a deletion at 2).

Run: python tools/probes/s89-partial-fold-rows.py   (regex 2026.9.10, 2026-09-23)
"""

import regex


def show(p, s, **kw):
    m = regex.match(p, s, **kw)
    if not m:
        return "None"
    return f"{m.span()} partial={m.partial} {m.fuzzy_counts} {m.fuzzy_changes}"


T = "\U0001D7EE"
cases = [
    ("9010", r"(?fi)(st)(?:[^a](?:a(?:\1)){2i+1d+1s<=2}){e<=2}", "stat"),
    ("9010 literal", r"(?fi)(st)(?:[^a](?:ast){2i+1d+1s<=2}){e<=2}", "stat"),
    ("9010 simple fold", r"(?i)(st)(?:[^a](?:a(?:\1)){2i+1d+1s<=2}){e<=2}", "stat"),
    ("8938", r"(?fi)(?r)(?:\A(?:(?:\1)a){e<=2,s<=1}[^a]){e<=3,1i+1d+2s<=3:[a-f]}(f)", "ﬀAf"),
    ("8938 literal", r"(?fi)(?r)(?:\A(?:fa){e<=2,s<=1}[^a]){e<=3,1i+1d+2s<=3:[a-f]}(f)", "ﬀAf"),
    ("8938 simple fold", r"(?i)(?r)(?:\A(?:(?:\1)a){e<=2,s<=1}[^a]){e<=3,1i+1d+2s<=3:[a-f]}(f)", "ﬀAf"),
    ("3874", "(?e)(?fi)\\m(ﬆx)(?:(?:\\1)(?:" + T + ".){s<=1}){1i+1d+1s<=1}", "ﬆxST" + T + "a"),
    ("3874 literal", "(?e)(?fi)\\m(ﬆx)(?:(?:ﬆx)(?:" + T + ".){s<=1}){1i+1d+1s<=1}", "ﬆxST" + T + "a"),
    ("3874 without (?e)", "(?fi)\\m(ﬆx)(?:(?:\\1)(?:" + T + ".){s<=1}){1i+1d+1s<=1}", "ﬆxST" + T + "a"),
    ("ligature, delete x", "(?fi)(?:ﬆxba){d<=1}", "STba"),
    ("st, delete x", "(?fi)(?:stxba){d<=1}", "STba"),
]
for name, p, s in cases:
    print(f"{name:20}| partial: {show(p, s, partial=True)} | plain: {show(p, s)}")
