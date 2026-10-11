"""Classify the main->branch changes. Usage: cmp2.py rows up main branch branch_pos"""
import json, sys, collections, re

L = lambda p: {r["id"]: r for r in map(json.loads, open(p, encoding="utf-8"))}
rows, up, m, b, bp = (L(p) for p in sys.argv[1:6])


def n(s):
    if s.startswith("EXC") or s.startswith("ERR"):
        return re.sub(r" @ .*", "", s)[:60]
    return s


def cat(r):
    p = r["pattern"]
    return f"{'neg' if r['neg'] else 'pos'}-{'behind' if '(?(?<' in p else 'ahead'}-{'fuzzy' if '{' in p else 'exact'}"


# 1. The ruling's equivalence inside the port: pattern == posForm on the branch.
neq = [i for i in rows if n(b[i]["base"]) != n(bp[i]["base"])]
print("branch: pattern != its positive twin:", len(neq), dict(collections.Counter(cat(rows[i]) for i in neq)))
for i in neq[:8]:
    r = rows[i]
    print("  TWIN", i, r["pattern"], "|", r["posForm"], repr(r["subject"]), r["operation"], r["partial"], "|b", b[i]["base"], "|bp", bp[i]["base"])
neqm = [i for i in rows if n(m[i]["base"]) != n(bp[i]["base"])]

changed = [i for i in rows if n(m[i]["base"]) != n(b[i]["base"])]
print("changed:", len(changed), dict(collections.Counter(cat(rows[i]) for i in changed)))
cls = collections.Counter()
other = []
for i in changed:
    J, U = n(up[i]["J"]), n(up[i]["U"])
    if n(b[i]["base"]) == J:
        cls["=J"] += 1
    elif n(m[i]["base"]) != U:
        cls["!=J, main already !=U (other divergence)"] += 1
        other.append(i)
    else:
        cls["!=J, main==U  <-- INSPECT"] += 1
        other.append(i)
print(dict(cls))
reg = [i for i in rows if n(m[i]["base"]) == n(up[i]["J"]) and n(b[i]["base"]) != n(up[i]["J"])]
print("regressions (main==J, branch!=J):", len(reg), "of which main==U:", sum(n(m[i]["base"]) == n(up[i]["U"]) for i in reg))
with open("other.txt", "w", encoding="utf-8") as f:
    for i in other + reg:
        r = rows[i]
        f.write(f"{i}\t{cat(r)}\t{r['pattern']}\t{r['subject']!r}\t{r['operation']}\t{r['partial']}\n  main {m[i]['base']}\n  br   {b[i]['base']}\n  J    {up[i]['J']}\n  U    {up[i]['U']}\n  A    {up[i]['A']}\n")
# existence changes: None <-> match
ex = collections.Counter()
for i in changed:
    a, c = m[i]["base"] == "None" or m[i]["base"] == "[]", b[i]["base"] == "None" or b[i]["base"] == "[]"
    ex[("none->match" if a and not c else "match->none" if c and not a else "match->match")] += 1
print("existence:", dict(ex))
