"""S89: row 4957 of the seed-99 default wave, judged into partial-retry-reversed-slice.

Upstream's partial search answers (0, 0). Its (*PRUNE) spelling, its verb-free spelling and its
own match(0, endpos, partial=True) at the highest endpos that matches all answer (0, 5) in
codepoints, which is this port's UTF-16 (0, 9). The pattern reads nothing past the anchor (no
lookaround), so the anchor sweep is evidence here.

Run: python tools/probes/s89-partial-retry-reversed-row.py
2026-09-23, regex 2026.9.10:
    skip search ((0, 0), True)    match endpos 5 (0, 5) True
    prune search ((0, 5), True)   match endpos 5 (0, 5) True
    none search ((0, 5), True)    match endpos 5 (0, 5) True
"""

import regex

print(regex.__version__)
s = "A\U0001D518\U0001D518\U0001D518\U0001D518\r"
flags = 16650
for name, p in [
    ("skip", r"(?r)\s+(?:[[:alpha:]]+?(*SKIP)\p{L}|\W)"),
    ("prune", r"(?r)\s+(?:[[:alpha:]]+?(*PRUNE)\p{L}|\W)"),
    ("none", r"(?r)\s+(?:[[:alpha:]]+?\p{L}|\W)"),
]:
    r = regex.compile(p, flags)
    m = r.search(s, partial=True)
    print(name, "search", m and (m.span(), m.partial))
    for e in range(len(s), -1, -1):
        m = r.match(s, 0, e, partial=True)
        if m:
            print(name, " match endpos", e, m.span(), m.partial)
            break
