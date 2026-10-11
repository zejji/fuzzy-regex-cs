"""S90: upstream's side of the three partial rows that S89 handed on.

Each row asked with and without partial=True, then ledger entry 11 mechanism B on its own, with no
folding: upstream reports the innermost open section's count and truncates its change list to it.
This port's side is `full-fold-fix-behind-an-innermost-count` in ExpectedDivergences.cs, which runs
the fold ablations.

Run: python tools/probes/s90-partial-fold-rows.py   (regex 2026.9.10, 2026-09-24)
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8")


def show(p, s, **kw):
    m = regex.match(p, s, **kw)
    if not m:
        return "None"
    return f"{m.span()} partial={m.partial} {m.fuzzy_counts} {m.fuzzy_changes}"


T = "\U0001D7EE"
cases = [
    ("3874", "(?e)(?fi)\\m(ﬆx)(?:(?:\\1)(?:" + T + ".){s<=1}){1i+1d+1s<=1}", "ﬆxST" + T + "a"),
    ("9010", r"(?fi)(st)(?:[^a](?:a(?:\1)){2i+1d+1s<=2}){e<=2}", "stat"),
    ("8938", r"(?fi)(?r)(?:\A(?:(?:\1)a){e<=2,s<=1}[^a]){e<=3,1i+1d+2s<=3:[a-f]}(f)", "ﬀAf"),
    ("mechanism B", r"(?:x(?:ab){s<=1}){s<=1}", "yc"),
    ("mechanism B, one char", r"(?:x(?:ab){s<=1}){s<=1}", "y"),
]
for name, p, s in cases:
    print(f"{name:22}| partial: {show(p, s, partial=True)} | plain: {show(p, s)}")
