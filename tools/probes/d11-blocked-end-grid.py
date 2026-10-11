"""D11 amendment 2: the grid of blocked lookaround and condition ends inside called groups.

Prints JSON-lines rows (partial match) for the port probe and for tools/probes/d11-judge-rows.py:
five tests (lookahead, condition, negative lookahead, negative condition, lookbehind) over six
bodies and six tails, each inline, after a prefix, in a called group, in a nested call, in a
numbered call and under (?R). The inline and prefixed rows are the called rows' twins.

    python tools/probes/d11-blocked-end-grid.py > rows.jsonl
"""
import json, itertools
bodies = [r"a\b|a", r"a$|a", r"(a)\b|(a)", r"a\b", r"(?:a|ab)\b", r"a(?&d)?\b"]
tests = {"la": "(?={B})", "cond": "(?(?={B})a|b)", "nla": "(?!{B})", "ncond": "(?(?!{B})b|a)", "lb": "(?<={B})"}
conts = [r"((((b))))", r"b", r"\n", r"(a)(\n)", r"a\n", r"a"]
rows = []
for B, (tk, T), C in itertools.product(bodies, tests.items(), conts):
    t = T.format(B=B)
    d = "(?<d>b)" if "(?&d)" in B else ""
    pats = {
        "inline": f"(?(DEFINE){d}){t}{C}" if d else f"{t}{C}",
        "xpre": f"(?(DEFINE){d})x{t}{C}" if d else f"x{t}{C}",
        "call": f"(?(DEFINE)(?<c>{t}){d})(?&c){C}",
        "xcall": f"(?(DEFINE)(?<c>{t}){d})x(?&c){C}",
        "nest": f"(?(DEFINE)(?<c>{t}){d}(?<e>(?&c)))(?&e){C}",
    }
    if not d:
        pats["num"] = f"(?:({t})|x(?1)){C}"
        pats["rec"] = f"(?:x(?R)|{t}){C}"
    for k, p in pats.items():
        subs = ["xa", "xab"] if k in ("xpre", "xcall", "num", "rec") else ["a", "ab"]
        if tk == "lb":
            subs = [s + "" for s in subs]
        for s in subs:
            rows.append({"kind": k, "test": tk, "pattern": p, "subject": s, "operation": "match", "partial": True})
for r in rows: print(json.dumps(r))
