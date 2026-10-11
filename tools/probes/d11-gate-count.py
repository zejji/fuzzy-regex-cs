"""D11's merge gate count (2026-10-04): per shape and variant of d11-runout-gate-grid.py, the rows
main answers right and the branch wrong, split into the accepted classes and the rest, and the
branch's misses. Accepted: decision (a)'s run-out answered the partial (the engine's causal events
RunOutAnsweredByGreedyBodyKept / RunOutAnsweredByLazyPastTheEdge), or a possessive repeat inside
the lookahead (the `*+` core: limit C2, the second pre-merge review's item 4), or a row of the
"read-before" variant that answers as its prefix-free twin does on both engines (main's answer moves
only because `()\1` in front leaves the pattern no first set, so main never tries a start inside
the text to come, D70). Pass: 0 in "rest".

    python tools/probes/d11-gate-count.py gate.jsonl branch-grid.txt main-answers.txt [--list]

branch-grid.txt is d11-port-judge-grid.cs output (Debug); main-answers.txt has the answer in its
fourth column, one line per row in the same order.
"""
import collections
import json
import sys

rows = [json.loads(line) for line in open(sys.argv[1], encoding="utf-8")]
branch = [line.rstrip("\r\n").split("\t") for line in open(sys.argv[2], encoding="utf-8")]
main = [line.rstrip("\r\n").split("\t") for line in open(sys.argv[3], encoding="utf-8")]
assert len(rows) == len(branch) == len(main), (len(rows), len(branch), len(main))
twin = {(r["shape"], r["core"], r["pattern"], r["subject"], r["operation"]): i
        for i, r in enumerate(rows) if r["variant"] == "plain"}
table = collections.defaultdict(collections.Counter)
listed = []
for row, b, m in zip(rows, branch, main):
    key = (row["shape"], row["variant"])
    table[key]["rows"] += 1
    if b[3].startswith("ERR") or b[4] == "?":
        table[key]["err"] += 1
        continue
    judge = b[4]
    if b[3] == "None" and judge != "None":
        table[key]["miss"] += 1
    if m[3] == judge and b[3] != judge:
        events = b[5] if len(b) > 5 else ""
        if "RunOutAnsweredBy" in events:
            table[key]["accepted (a)"] += 1
        elif row["core"].endswith("*+"):
            table[key]["accepted C2"] += 1
        elif (
            row["variant"] == "read-before"
            and (j := twin.get((row["shape"], row["core"], row["pattern"][4:], row["subject"], row["operation"]))) is not None
            and branch[j][3] == b[3]
            and main[j][3] == b[3]
        ):
            table[key]["accepted twin"] += 1
        else:
            table[key]["rest"] += 1
            listed.append(row | {"branch": b[3], "main": m[3], "judge": judge, "events": events})
    elif b[3] == judge and m[3] != judge:
        table[key]["fixed"] += 1
cols = ["rows", "rest", "accepted (a)", "accepted C2", "accepted twin", "fixed", "miss", "err"]
print("| shape | variant | " + " | ".join(cols) + " |")
print("|---|---|" + "---|" * len(cols))
total = collections.Counter()
for key in sorted(table):
    total.update(table[key])
    print(f"| {key[0]} | {key[1]} | " + " | ".join(str(table[key][c]) for c in cols) + " |")
print("| all | | " + " | ".join(str(total[c]) for c in cols) + " |")
if "--list" in sys.argv:
    for item in listed:
        print(json.dumps(item, ensure_ascii=True))
