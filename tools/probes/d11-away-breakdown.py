"""D11 chunk 11's gate classifier and breakdown (pre-merge reviews P2 and R5, 2026-10-04): the rows
of the port grid (d11-port-grid.py) that moved away from the judge between main (answers only, the
answer in the fourth column) and the branch (d11-port-judge-grid.cs output, Debug). The judge's
horizon is the MAXW the grid ran with; at MAXW 2 some rows counted here are fixes (a completion of 3
characters exists), so say which MAXW the counts are at.

A label is given by cause only when the call's events leave no other cause:
  C2-mech  the run-out that answered was produced by a mechanism (RunOutAnsweredBy*, recorded at it);
  C2       a run-out outside every body answered and the call blocked nothing (no blocked cut or end,
           no undetermined negative, no condition taken both ways, no skipped verb, no run-out in a
           body), so the only presumption on its path is the rest after that run-out;
  C3       a second-pass success answered, the call read the text to come at least twice and
           blocked nothing.
The other rows get an event class (not a proof of cause): C2 in a body, C2 in a body then an
answering run-out, C2 after a blocked construct, C3 after a blocked construct, C4 (a blocked
construct and one read).

    python tools/probes/d11-away-breakdown.py main.txt branch.txt [--list out.jsonl]

The list is written ASCII-safe (JSON with ensure_ascii), one row per line.
"""
import collections
import json
import sys

BLOCK = ["CutBlockedAtomic", "CutBlockedLookaround", "CutBlockedCondition", "NegativeUndetermined",
         "ConditionBothWays", "VerbSkipped", "RunOutInBody"]


def label(answer, judge, ev):
    n = lambda k: int(ev.get(k, 0))
    clean = not any(n(k) for k in BLOCK)
    reads = n("UndeterminedRead") + n("DecidedReadDiffering")
    if answer == "None" and judge != "None":
        return "MISS"
    if n("RunOutAnsweredByLazyPastTheEdge") or n("RunOutAnsweredByGreedyBodyKept"):
        return "C2-mech"
    if n("RunOutAnswered") and clean:
        return "C2"
    if n("SuccessTurnedPartial") and clean and reads >= 2:
        return "C3"
    if n("RunOutInBody") and n("RunOutAnswered"):
        return "events: C2 in a body, then an answering run-out"
    if n("RunOutInBody"):
        return "events: C2 in a body"
    if n("RunOutAnswered"):
        return "events: C2 after a blocked construct"
    if reads >= 2:
        return "events: C3 after a blocked construct"
    if any(n(k) for k in BLOCK):
        return "events: C4"
    return "events: other"


def main():
    main_rows = [line.rstrip("\r\n").split("\t") for line in open(sys.argv[1], encoding="utf-8")]
    branch = [line.rstrip("\r\n").split("\t") for line in open(sys.argv[2], encoding="utf-8")]
    assert len(main_rows) == len(branch)
    listing = sys.argv[sys.argv.index("--list") + 1] if "--list" in sys.argv else None
    out = open(listing, "w", encoding="ascii", newline="\n") if listing else None
    count = collections.Counter()
    for m, b in zip(main_rows, branch):
        if b[3].startswith("ERR") or b[4] == "?" or m[3] == b[3]:
            continue
        judge = b[4]
        kind = "onto" if b[3] == judge else "away" if m[3] == judge else "other"
        if kind == "onto":
            count["onto"] += 1
            continue
        ev = dict(e.split("=") for e in b[5].split(",")) if len(b) > 5 and b[5] else {}
        lab = label(b[3], judge, ev)
        count[kind + " | " + lab] += 1
        if out:
            row = {"kind": kind, "label": lab, "operation": b[0], "pattern": json.loads(b[1]),
                   "subject": json.loads(b[2]), "main": m[3], "branch": b[3], "judge": judge, "events": b[5] if len(b) > 5 else ""}
            out.write(json.dumps(row, ensure_ascii=True) + "\n")
    for k, v in sorted(count.items()):
        print(f"{v}\t{k}")


if __name__ == "__main__":
    main()
