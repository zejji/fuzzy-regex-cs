"""D11 amendment 2, repair 1: blocked ends inside called groups under a repeat.

Backtracking into a GROUP_RETURN pushes the call's frame again with the caller's repeat guard lists
as they are then, so the frame can come back longer and every depth above it moves. This grid puts
blocked lookaround and condition tests inside called groups (direct, nested and numbered calls)
under greedy, possessive-free lazy and counted repeats. Prints JSON-lines rows (partial match and
search) for the port probe and tools/probes/d11-judge-rows.py.

    python tools/probes/d11-called-repeat-grid.py > rows.jsonl
"""
import json, itertools
tests = [r"(?=a\b)", r"(?=a$)", r"(?(?=a$)|b)", r"(?(?=a\b)a|b)", r"(?=(?:a|ab)$)a", r"(?!a\b)", r"(?>a\b|)"]
quants = ["*", "+", "{2,}", "*?", "+?", "{1,3}"]
tails = ["b", r"\n", r"a\n"]
rows = []
for t, q, tail in itertools.product(tests, quants, tails):
    pats = {
        "call": f"(?(DEFINE)(?<c>{t}))(?&c){q}{tail}",
        "nest": f"(?(DEFINE)(?<c>{t})(?<d>x?(?&c)))(?&d){q}{tail}",
        "num": f"(?:({t})|x(?1)){q}{tail}",
    }
    for kind, p in pats.items():
        for s in ["a", "aa", "xa", "xab"]:
            for op in ["match", "search"]:
                rows.append({"kind": kind, "pattern": p, "subject": s, "operation": op, "partial": True})
for r in rows:
    print(json.dumps(r))
