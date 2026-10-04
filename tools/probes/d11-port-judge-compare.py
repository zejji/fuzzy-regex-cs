"""Compares d11-port-judge-grid.cs output with d11-brute-judge.py verdicts (one per line):
    python d11-port-judge-compare.py port.txt upstream-judge.txt [examples]
    python d11-port-judge-compare.py port.txt - [examples]      (the port's own judge only)
A miss is a None where a judge finds a completion; the port judge is authoritative where the
port's fuzzy rules differ from upstream's (ledger entries 12 and 51). A finditer row compares the
whole list. The event column's per-row counts are summed as "rows with <event>" at the end, so a
grid that never reaches a mechanism shows it (VERIFICATION.md rule 11).
"""
import sys, collections
port = [l.rstrip("\n").split("\t") for l in open(sys.argv[1], encoding="utf-8")]
up = ["-"] * len(port) if sys.argv[2] == "-" else [l.strip() for l in open(sys.argv[2], encoding="utf-8")]
c = collections.Counter(); ex = collections.defaultdict(list); ev = collections.Counter()
for p, u in zip(port, up):
    op, pat, subj, ans, pj = p[:5]
    for e in (p[5].split(",") if len(p) > 5 and p[5] else []):
        ev[e.split("=")[0]] += 1
    if ans.startswith("ERR"): k = "error"
    elif op == "finditer": k = "ok" if ans == pj else "finditer-diff(port-judge)"
    elif pj.startswith("F"): k = "ok" if ans == pj else "complete-mismatch"
    elif pj.startswith("P"):
        k = "miss(port-judge)" if ans == "None" else ("ok" if ans == pj else "span-diff(port-judge)")
    else:
        k = "phantom(port-judge)" if ans != "None" else "ok"
    if k == "ok" or k.startswith("phantom"):
        if u.startswith("P") and ans == "None": k = "miss(upstream-judge only)"
    c[k] += 1; ex[k].append(f"{op} {pat} {subj!s} port={ans} pj={pj} uj={u}")
for k, v in sorted(c.items()): print(k, v)
for k in ex:
    if k != "ok":
        for e in ex[k][:int(sys.argv[3]) if len(sys.argv) > 3 else 4]: print("  ", k, e)
for k, v in sorted(ev.items()): print("rows with", k, v)
