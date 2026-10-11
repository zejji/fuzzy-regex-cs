"""D11's merge gate for the repeat run-out rule (2026-10-04, second pre-merge round): lookarounds
over repeats whose outcome a decision reads, in every shape the two pre-merge reviews found, each
row tagged with its shape so the gate counts per shape.

Lookaround shapes L, over the look grid's repeat and fuzzy cores C and five tails T:
  neg        (?!(?=C T))ab           cond   (?(?=C T)x|ab)
  atomic     (?>(?=C T)|ab)x         prune  (?=C T)(*PRUNE)x|ab
  skip       (?:(?=C T)(*SKIP)x|ab)  bare   (?!C T)ab  (F1's own negative body)
  rep+ rep* rep? rep{2}              (?>(?:(?=C T)|ab)+)x and its *, ? and {2} spellings
  poss++ poss*+ poss?+ poss{1,2}+    (?:(?=C T)|ab)++x and its *+, ?+ and {1,2}+ spellings
Pattern-wide variants: none; a group call after it (|(?P<zc>q)(?&zc)); a capture read after it
(|(?P<zz>q)(?(zz)q)); a capture read before it (()\\1 in front).

Run d11-port-judge-grid.cs (Debug, MAXW 2) on the output, the main tree's answers on the same rows,
and d11-gate-count.py over the three files.

    python tools/probes/d11-runout-gate-grid.py out.jsonl
"""
import json
import sys

XS = ["ab", "a", "[ab]", "(?:a|b)", "(?:a|ab)", "."]
QS = ["*", "+", "{1,3}", "*?", "+?", "?", "*+"]
FZ = ["(?:ab){e<=1}", "(?:ab){i<=1}", "(?:ab){s<=1}", "(?:ab){d<=1}", "a{e<=1}", "(?:abc){e<=1}"]
TS = ["(?<=b)", "(?<=a)", "(?<!b)", "(?<=^ab)", ""]
SHAPES = {
    "neg": "(?!(?={c}{t}))ab",
    "cond": "(?(?={c}{t})x|ab)",
    "atomic": "(?>(?={c}{t})|ab)x",
    "prune": "(?={c}{t})(*PRUNE)x|ab",
    "skip": "(?:(?={c}{t})(*SKIP)x|ab)",
    "bare": "(?!{c}{t})ab",
    "rep+": "(?>(?:(?={c}{t})|ab)+)x",
    "rep*": "(?>(?:(?={c}{t})|ab)*)x",
    "rep?": "(?>(?:(?={c}{t})|ab)?)x",
    "rep{2}": "(?>(?:(?={c}{t})|ab){{2}})x",
    "poss++": "(?:(?={c}{t})|ab)++x",
    "poss*+": "(?:(?={c}{t})|ab)*+x",
    "poss?+": "(?:(?={c}{t})|ab)?+x",
    "poss{1,2}+": "(?:(?={c}{t})|ab){{1,2}}+x",
}
VARIANTS = {"plain": "{p}", "call": "{p}|(?P<zc>q)(?&zc)", "read-after": "{p}|(?P<zz>q)(?(zz)q)", "read-before": "()\\1{p}"}


def main():
    cores = ["(?:" + x + ")" + q for x in XS for q in QS] + FZ
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as f:
        n = 0
        for shape, w in SHAPES.items():
            for variant, v in VARIANTS.items():
                for c in cores:
                    for t in TS:
                        p = v.format(p=w.format(c=c, t=t))
                        for s in ["a", "ab", "abab"]:
                            for op in ["match", "search"]:
                                row = {"pattern": p, "subject": s, "operation": op, "shape": shape, "variant": variant, "core": c}
                                f.write(json.dumps(row) + "\n")
                                n += 1
    print(n)


if __name__ == "__main__":
    main()
