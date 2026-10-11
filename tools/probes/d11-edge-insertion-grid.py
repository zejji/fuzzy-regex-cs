"""D11 chunk 9, item 0: rows of fuzzy sections whose trailing or minimum-driven insertion falls at
the text edge, in and out of lookarounds. Writes rows.jsonl (3,000 sampled rows) in the working
directory; answer them with d11-port-judge-grid.cs and compare with d11-port-judge-compare.py.
"""
import json, itertools, random
bodies = ["b", "bc", "b.", "[bc]", "b|c", "bc?", "(b)", "b+"]
cons = ["e<=1", "i<=1", "1<=e<=1", "1<=i<=1", "e<=2", "1<=e<=2", "2<=e<=2", "s<=1"]
tails = ["", "$", "d", "(?=d)", r"\b"]
pats = set()
for X, C in itertools.product(bodies, cons):
    S = f"(?:{X}){{{C}}}"
    for T in tails:
        pats.add(f"a{S}{T}")                       # outside a lookaround
        pats.add(f"(?:a{S}{T}){{e<=1}}")            # nested in an outer section
    for T, Y in itertools.product(["", "$", "d"], ["", "b", "bc", "."]):
        pats.add(f"a(?={S}{T}){Y}")                 # positive lookahead
        pats.add(f"a(?!{S}{T}){Y}")                 # negative lookahead
    for T in ["", "d"]:
        pats.add(f"(?:a(?={S}{T})b.){{e<=1}}")      # lookahead inside an outer section
        pats.add(f"(?:a(?=(?:b{S}d){{0<=e<=1}})b.){{0<=e<=1}}".replace("(?:b(?:", "(?:b(?:") )
        pats.add(f"a(?<={S}){T}")                   # lookbehind
        pats.add(f"(?r){S}{T}")                     # reversed
pats.add("(?:a(?=bcd)b.){e<=1}")
subjects = ["a", "ab", "abc", "abb"]
random.seed(1)
rows = []
for p in sorted(pats):
    for s in subjects:
        for op in ["match", "fullmatch", "search"]:
            rows.append({"pattern": p, "subject": s, "operation": op, "partial": True})
random.shuffle(rows)
rows = rows[:3000]
with open("rows.jsonl", "w", encoding="utf-8", newline="\n") as f:
    for r in rows:
        f.write(json.dumps(r) + "\n")
print(len(pats), len(rows))
