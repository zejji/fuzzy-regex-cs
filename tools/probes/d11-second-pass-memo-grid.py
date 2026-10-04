"""D11 chunk 9: partial rows of (?:X)QY for the failure memo in the second pass (amendment 1).
Writes rows2.jsonl; run d11-port-judge-grid.cs on it before and after a change and diff the answers.
"""
import json, itertools, random
X = ["a|a", "a|ab", "(?=a)a", "(?(?=a)a|b)", "(?(?=.*c)a|a)", "(?>a|ab)", r"\ba", r"a\B", "(?!b)a", "a|b", "(?=a$)a|a", "ab|a"]
Q = ["*", "+", "*?", "+?", "{0,}"]
Y = ["", "b", "$", r"\b", "c", "(?=c)", "a$", "bc"]
subs = [""] + ["".join(p) for n in range(1, 6) for p in itertools.product("abc", repeat=n)]
random.seed(9)
rows = []
for x, q, y in itertools.product(X, Q, Y):
    pat = f"(?:{x}){q}{y}"
    for s in random.sample(subs, 4):
        for op in ["match", "search", "fullmatch"]:
            rows.append({"pattern": pat, "subject": s, "operation": op, "partial": True})
with open("rows2.jsonl", "w", encoding="utf-8", newline="\n") as f:
    for r in rows: f.write(json.dumps(r) + "\n")
print(len(rows))
