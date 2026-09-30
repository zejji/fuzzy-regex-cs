"""D11 build chunk 8: run-outs inside lookaround bodies and condition tests in patterns that read a
capture while matching (a backreference or a group condition), the rows the unknown captures decide.

Prints JSON-lines rows (partial match and search) for the port probe and d11-judge-rows.py:

    python tools/probes/d11-unknown-capture-grid.py > rows.jsonl
"""
import json, itertools
tests = [r"(?=a(.)b)", r"(?=a(b)?c)", r"(?=(a)b)", r"(?=a(.))", r"(?!a(.)b)", r"(?(?=a(.)b)a|b)",
         r"(?=a(?:(.)|x)b)", r"(?=(?:a(.))+b)", r"(?<=(a)b)", r"(?=a(?>(.)b))"]
tails = [r"(?(1)x|y)", r"(?(1)a|b)", r"\1", r"a\1", r"a(?(1)b|c)", r"a", r"(?(1)a|a)\1?", r"(x)?(?(2)y|a)"]
rows = []
for t, tail in itertools.product(tests, tails):
    p = t + tail
    for s in ["a", "ab", "ax", "ba"]:
        for op in ["match", "search"]:
            rows.append({"pattern": p, "subject": s, "operation": op, "partial": True})
for r in rows:
    print(json.dumps(r))
