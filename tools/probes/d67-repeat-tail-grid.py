"""D67 grid: repeats inside a group under partial matching (2026-10-08).

    python d67-repeat-tail-grid.py gen            # writes grid-rows.jsonl (51,200 rows)
    python d67-repeat-tail-grid.py judge out.tsv  # judges a port-probe run of those rows

The port's answers come from the port probe (`git show 8dda4d9:tools/probes/port-probe.cs`), changed
to print every group as ` g<n>=(s,e)` or ` g<n>=unset`. For each partial answer the judge runs
upstream `regex` without partial over every continuation of 1 to MAXN characters (default 5) from
the partial's anchor, and compares each group the held text decides (its common value lies inside
the partial's span) with the port's. Groups whose completions disagree are not judged. Spans are
compared between two port runs with `diff`. Measured: main 4,424 disagreements, D67 none, no span
or partial flag moved.
"""
import itertools
import json
import os
import re
import sys

import regex

BODIES = ["a", "(?<i>a)", "(?:aa)", "(?<i>aa)", "(?:a|c)", "(?<i>a|c)", "[ac]", "(?<i>[ac])"]
QUANTS = ["?", "*", "+", "{0,2}", "{1,2}", "??", "*?", "+?", "{0,2}?", "{1,2}?"]
TAILS = ["b", "bc"]
PREFIXES = ["", "b"]
SUBJECTS = [""] + ["".join(p) for n in range(1, 4) for p in itertools.product("abc", repeat=n)]


def patterns():
    for body, q, tail, pre in itertools.product(BODIES, QUANTS, TAILS, PREFIXES):
        yield f"(?<o>{pre}{body}{q}){tail}"
        # Mirror: the reversed pattern reads right to left, so put the literals the other way round.
        yield f"(?r){tail[::-1]}(?<o>{body}{q}{pre})"


def gen():
    with open("grid-rows.jsonl", "w", encoding="utf-8", newline="\n") as f:
        for p in patterns():
            for s in SUBJECTS:
                for op in ("search", "match"):
                    f.write(json.dumps({"pattern": p, "subject": s, "operation": op, "partial": True}) + "\n")


ANS = re.compile(r"^\((\d+),(\d+)\) .*?( partial)?((?: g\d+=\S+)*)$")


def parse(answer):
    m = ANS.match(answer)
    if not m:
        return None
    groups = {}
    for g in m.group(4).split():
        k, v = g.split("=")
        groups[k] = None if v == "unset" else tuple(int(x) for x in v.strip("()").split(","))
    return int(m.group(1)), int(m.group(2)), bool(m.group(3)), groups


def judge(path):
    bad = agree = nocons = 0
    for line in open(path, encoding="utf-8"):
        op, pat, subj, answer, _ = line.rstrip("\n").split("\t")
        pat, subj = json.loads(pat), json.loads(subj)
        a = parse(answer)
        if not a or not a[2]:
            continue
        s, e, _, groups = a
        rev = pat.startswith("(?r)")
        comp = regex.compile(pat)
        seen = {}
        for n in range(1, int(os.environ.get("MAXN", "5")) + 1):
            for w in itertools.product("abc", repeat=n):
                w = "".join(w)
                if rev:
                    t = w + subj
                    m = comp.match(t, endpos=len(w) + e)
                    shift = len(w)
                else:
                    t = subj + w
                    m = comp.match(t, pos=s)
                    shift = 0
                if not m:
                    continue
                for gi in range(1, comp.groups + 1):
                    sp = m.span(gi)
                    sp = None if sp == (-1, -1) else (sp[0] - shift, sp[1] - shift)
                    seen.setdefault(f"g{gi}", set()).add(sp)
        if not seen:
            continue
        for g, vals in seen.items():
            if len(vals) != 1:
                nocons += 1
                continue
            want = next(iter(vals))
            if want is not None and not (s <= want[0] and want[1] <= e):
                continue  # the group reaches into the text to come: the held text does not decide it
            if groups.get(g) != want:
                bad += 1
                print(f"BAD\t{op}\t{pat}\t{subj!r}\t{answer}\t{g} want {want}")
            else:
                agree += 1
    print(f"agree={agree} bad={bad} noconsensus={nocons}", file=sys.stderr)


if __name__ == "__main__":
    gen() if sys.argv[1] == "gen" else judge(sys.argv[2])
