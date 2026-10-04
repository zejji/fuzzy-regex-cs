"""D11 run-out rule (2026-10-04, review F1/S1): repeats, a branch and fuzzy sections inside
lookarounds whose tail is decidable on the held text, the shape the earlier repeat grids lacked
(VERIFICATION.md rule 11). Run d11-port-judge-grid.cs on the output in Debug and read the standard
error's "rows with RunOutKeptAlternatives"; classify moved rows with d11-classify-runout.py.

    python tools/probes/d11-runout-look-grid.py out.jsonl
"""
import json, sys
W = ["(?!{c}{t})ab", "(?!{c}{t})", "(?!{c}{t}).*", "(?={c}{t})(*FAIL)", "(?={c}{t})ab",
     "(?!(?>{c}){t})ab", "(?!{c}{t}|b)a", "a(?!{c}{t})"]
xs = ["ab", "a", "[ab]", "(?:a|b)", "(?:a|ab)", "."]
qs = ["*", "+", "{1,3}", "*?", "+?", "?", "*+"]
ts = ["(?<=b)", "(?<=a)", "(?<!b)", "(?<=^ab)", ""]
fz = ["(?:ab){e<=1}", "(?:ab){i<=1}", "(?:ab){s<=1}", "(?:ab){d<=1}", "a{e<=1}", "(?:abc){e<=1}"]
subs = ["", "a", "ab", "aa", "abab", "ba"]
rows = set()
for w in W:
    for t in ts:
        cores = ["(?:" + x + ")" + q for x in xs for q in qs] + fz
        for c in cores:
            for s in subs:
                for op in ["match", "fullmatch", "search"]:
                    rows.add((w.format(c=c, t=t), s, op))
with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as f:
    for p, s, o in sorted(rows):
        f.write(json.dumps({"pattern": p, "subject": s, "operation": o}) + "\n")
print(len(rows))
