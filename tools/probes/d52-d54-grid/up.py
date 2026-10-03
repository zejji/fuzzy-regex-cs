"""Upstream's answers to the grid, three ways: U as released; A with each conditional's test
compiled exact (the D52 fix, monkeypatched in memory); J = A on the row's posForm (the D54 ruling:
a negative conditional answers as its positive twin with the branches swapped).
Output: one JSON line per row {id, U, A, J} in port-runner's Describe format."""
import json, sys
import regex
from regex import _regex_core as core

orig = core.LookAroundConditional._compile


def exact_test(self, reverse, fuzzy):
    code = [(core.OP.CONDITIONAL, int(self.positive), int(not self.behind))]
    code.extend(self.subpattern.compile(self.behind, False))
    code.append((core.OP.NEXT,))
    code.extend(self.yes_item.compile(reverse, fuzzy))
    add = self.no_item.compile(reverse, fuzzy)
    if add:
        code.append((core.OP.NEXT,))
        code.extend(add)
    code.append((core.OP.END,))
    return code


def describe(m):
    if m is None:
        return "None"
    s = f"({m.start()},{m.end()})" + ("P" if m.partial else "")
    c = m.fuzzy_counts
    ch = m.fuzzy_changes
    s += f" {c[0]},{c[1]},{c[2]} [" + ",".join(map(str, ch[0])) + "|" + ",".join(map(str, ch[1])) + "|" + ",".join(map(str, ch[2])) + "]"
    n = len(m.regs) - 1
    s += " g" + "".join(f"({m.start(g)},{m.end(g)})" if m.start(g) >= 0 else "(-)" for g in range(1, n + 1))
    s += " c" + "".join("[" + "".join(f"({a},{b})" for a, b in m.spans(g)) + "]" for g in range(1, n + 1))
    return s


def ask(pattern, row):
    try:
        regex.purge()
        p = regex.compile(pattern)
        subj, op, part = row["subject"], row["operation"], row.get("partial", False)
        if op == "finditer":
            return "[" + " ; ".join(describe(m) for m in p.finditer(subj, partial=part, timeout=2)) + "]"
        f = {"search": p.search, "match": p.match, "fullmatch": p.fullmatch}[op]
        return describe(f(subj, partial=part, timeout=2))
    except TimeoutError:
        return "ERR Timeout"
    except Exception as e:
        return "ERR " + type(e).__name__ + " " + str(e).split("\n")[0][:120]


for line in open(sys.argv[1], encoding="utf-8"):
    row = json.loads(line)
    core.LookAroundConditional._compile = orig
    u = ask(row["pattern"], row)
    core.LookAroundConditional._compile = exact_test
    a = ask(row["pattern"], row)
    j = ask(row["posForm"], row)
    print(json.dumps(dict(id=row["id"], U=u, A=a, J=j)), flush=True)
