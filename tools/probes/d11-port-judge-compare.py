"""Compares d11-port-judge-grid.cs output with d11-brute-judge.py verdicts (one per line):
    python d11-port-judge-compare.py port.txt upstream-judge.txt [examples]
A miss is a None where a judge finds a completion; the port judge is authoritative where the
port's fuzzy rules differ from upstream's (ledger entries 12 and 51).
"""
import sys, collections
port = [l.rstrip("\n").split("\t") for l in open(sys.argv[1], encoding="utf-8")]
up = [l.strip() for l in open(sys.argv[2], encoding="utf-8")]
c = collections.Counter(); ex = collections.defaultdict(list)
for p, u in zip(port, up):
    op, pat, subj, ans, pj = p[:5]
    if ans.startswith("ERR"): k = "error"
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
