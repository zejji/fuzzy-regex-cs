"""Partial-contract judge: checks a partial search's start against this port's own full search.

The partial contract (DECISIONS 2026-09-30) makes a partial search report the least start from
which some continuation of the subject matches (reversed: the greatest end). This tool asks the
port's own non-partial search over t + w for every continuation w over a small alphabet ('abx ',
plus 'c' or 'A' when the pattern needs them) and compares that least start with two answers to the
same rows, usually main's and a branch's. Written for the D100 blind review (2026-10-17).

    python partial-contract-judge.py gen rows.jsonl out-base.txt out-fix.txt cases.jsonl jrows.jsonl [maxw]
    dotnet run -c Release tools/probes/port-probe.cs -- jrows.jsonl 5 | sed 's/	[0-9]*ms$//' > jout.txt
    python partial-contract-judge.py fold cases.jsonl jout.txt out-base.txt out-fix.txt [maxw]

rows.jsonl holds the rows in port-probe's shape; out-base.txt and out-fix.txt are port-probe's
answers to them (trailing timings stripped), one line per row. `gen` keeps the search rows where
either answer is partial and writes one full-search row per continuation of up to maxw characters
(default 3). `fold` prints each case where either side disagrees with the judge, tagged B/b (base
right/wrong) and F/f (fix right/wrong), and the tag counts on stderr. A judge answer is a bound:
a continuation longer than maxw, or outside the alphabet, is not tried.
"""
import itertools
import json
import re
import sys

SPAN = re.compile(r"^\((\d+),(\d+)\)")


def words(alpha, maxw):
    yield ""
    for n in range(1, maxw + 1):
        for w in itertools.product(alpha, repeat=n):
            yield "".join(w)


def alpha_for(p):
    a = "abx "
    if "c" in p.replace("\\", ""):
        a += "c"
    if "(?i)" in p:
        a += "A"
    return a


def ans(line):
    return line.rstrip("\n").split("\t")[3]


if sys.argv[1] == "gen":
    _, _, rows_p, base_p, fix_p, cases_p, jrows_p = sys.argv[:7]
    maxw = int(sys.argv[7]) if len(sys.argv) > 7 else 3
    rows = [json.loads(l) for l in open(rows_p, encoding="utf-8")]
    base = open(base_p, encoding="utf-8").readlines()
    fix = open(fix_p, encoding="utf-8").readlines()
    with open(cases_p, "w", encoding="utf-8", newline="\n") as cf, open(jrows_p, "w", encoding="utf-8", newline="\n") as jf:
        for i, r in enumerate(rows):
            if r["operation"] != "search":
                continue
            if "partial" not in ans(base[i]) and "partial" not in ans(fix[i]):
                continue
            p, s = r["pattern"], r["subject"]
            rev = "(?r)" in p
            cf.write(json.dumps({"i": i, **r}) + "\n")
            for w in words(alpha_for(p), maxw):
                jr = {"pattern": p, "subject": w + s if rev else s + w, "operation": "search"}
                if "pos" in r:
                    jr["pos"] = r["pos"]
                jf.write(json.dumps(jr) + "\n")
else:
    _, _, cases_p, jout_p, base_p, fix_p = sys.argv[:6]
    maxw = int(sys.argv[6]) if len(sys.argv) > 6 else 3
    base = open(base_p, encoding="utf-8").readlines()
    fix = open(fix_p, encoding="utf-8").readlines()
    jl = open(jout_p, encoding="utf-8").readlines()
    k = 0
    stats = {}
    for line in open(cases_p, encoding="utf-8"):
        c = json.loads(line)
        p, s, i = c["pattern"], c["subject"], c["i"]
        rev = "(?r)" in p
        best, wit = None, None
        for w in words(alpha_for(p), maxw):
            m = SPAN.match(ans(jl[k]))
            k += 1
            if not m:
                continue
            st, en = int(m[1]), int(m[2])
            if rev:
                v = max(en - len(w), 0)
                if best is None or v > best:
                    best, wit = v, w
            else:
                v = min(st, len(s))
                if best is None or v < best:
                    best, wit = v, w

        def key(a):
            if "partial" not in a:
                return "full" if SPAN.match(a) else "None"
            m = SPAN.match(a)
            return int(m[2]) if rev else int(m[1])

        b, f = key(ans(base[i])), key(ans(fix[i]))
        j = "None" if best is None else best
        tag = ("B" if b == j else "b") + ("F" if f == j else "f")
        stats[tag] = stats.get(tag, 0) + 1
        if tag != "BF":
            print(f"{tag}\t{p}\t{s!r}\tpos={c.get('pos')}\tbase={ans(base[i]).split(' ')[0]}\tfix={ans(fix[i]).split(' ')[0]}\tjudge={j} wit={wit!r}")
    print(stats, file=sys.stderr)
