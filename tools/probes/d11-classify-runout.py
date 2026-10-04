"""D11 run-out rule (2026-10-04, review S2): classify the rows that moved between two runs of
d11-port-judge-grid.cs (columns: operation, pattern, subject, answer, judge, events), the later run
on a Debug build. A moved row is limit C2 by tool only when the run-out that ANSWERED the partial was
produced by one of decision (a)'s mechanisms: the engine records RunOutAnsweredByLazyPastTheEdge or
RunOutAnsweredByGreedyBodyKept at that run-out (MatchState.NoteRunOutTaken), so an event elsewhere in
the call no longer labels the row (the review's `(?!(?:ab)*(?<=b)).*` over 'ab').

    python tools/probes/d11-classify-runout.py before.txt after-debug.txt [--list]
"""
import sys, collections

def load(p):
    return [l.rstrip("\n").split("\t") for l in open(p, encoding="utf-8")]

a, b = load(sys.argv[1]), load(sys.argv[2])
assert len(a) == len(b), (len(a), len(b))
cnt = collections.Counter()
for x, y in zip(a, b):
    assert x[:3] == y[:3]
    if x[3] == y[3]:
        continue
    kind = "onto" if y[3] == y[4] else "away" if x[3] == x[4] else "other"
    if kind == "onto":
        cnt["onto"] += 1
        continue
    ev = y[5] if len(y) > 5 else ""
    lazy = "RunOutAnsweredByLazyPastTheEdge" in ev
    greedy = "RunOutAnsweredByGreedyBodyKept" in ev
    c2 = y[3].startswith("P") and (lazy or greedy)
    label = kind + (" C2/" + ("lazy" if lazy else "greedy") if c2 else " UNCLASSIFIED")
    cnt[label] += 1
    if not c2 or "--list" in sys.argv:
        print(label, "\t".join(y[:3]), "before=" + x[3], "after=" + y[3], "judge=" + y[4], ev, sep="\t")
print(dict(cnt))
