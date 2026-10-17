"""Tests for screen.py's pure parts: the twins and the claim rules. No engine runs here.

    python tools/battery/test_screen.py

The answers are in abl.cs's descriptor. The engine-backed proof is the re-judge of battery run
20261016-2 (tools/battery/README.md, "The second screen").
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import screen  # noqa: E402

FAILED = []


def check(name, got, want):
    if got != want:
        FAILED.append(f"{name}: got {got!r}, want {want!r}")


# greedy: lazy quantifiers lose their `?`; escapes, sets and group openers are left alone.
check("greedy *?", screen.greedy("a*?b"), "a*b")
check("greedy {1,2}?", screen.greedy("(?:ab){1,2}?c"), "(?:ab){1,2}c")
check("greedy ??", screen.greedy("x??y"), "x?y")
check("greedy none", screen.greedy("(?:a)?b+"), None)
check("greedy escape", screen.greedy("\\*?a+?"), "\\*?a+")
check("greedy set", screen.greedy("[*?]+?"), "[*?]+")
check("greedy fuzzy braces", screen.greedy("(?:a){e<=1}?"), None)

# start_moved_only: the \K twin's answer with only the start moved on.
check("moved start", screen.start_moved_only("(2,4) (0,1,0) [;3;] g1=(2,3)", "(0,4) (0,1,0) [;3;] g1=(2,3)", False), True)
check("moved start, counts differ", screen.start_moved_only("(2,2) (0,1,0) [;0;]", "(1,2)", False), False)
check("moved start, end differs", screen.start_moved_only("(2,3)", "(0,4)", False), False)
check("moved start backwards", screen.start_moved_only("(0,4)", "(2,4)", False), False)
check("reversed: end moved back", screen.start_moved_only("(0,2)", "(0,4)", True), True)
check("finditer", screen.start_moved_only("[(1,2) | (3,4)]", "[(0,2) | (2,4)]", False), True)
# A \K that moved nothing (it sat at the start) leaves the twin's answer as it is.
check("moved by nothing", screen.start_moved_only("(0,4)", "(0,4)", False), True)
# A sub or split is judged by its finditer form, which must itself differ from upstream's.
sub = {"pattern": "(?b)a\\Kb", "subject": "ab", "operation": "split", "flags": 0}
check("scan form", screen.as_scan(sub)["operation"], "finditer")
check("split moved", screen.classify(sub, 'split "a" ""', 'split "x"', [], {"nok": "[(0,2)]", "scan": "[]"},
                                     {"nok": "[(0,2)]", "scan": "[(1,2)]"})[0], "twin")
check("split, scan agrees", screen.classify(sub, 'split "a" ""', 'split "x"', [], {"nok": "[(0,2)]", "scan": "[(1,2)]"},
                                            {"nok": "[(0,2)]", "scan": "[(1,2)]"}), None)
# A count cuts the scan to the matches the sub or split makes.
check("first", screen.first("[(0,1) | (2,3) | (4,5)]", 2), "[(0,1) | (2,3)]")
check("split with a count", screen.classify({**sub, "count": 1}, 'split "a" ""', 'split "x"', [],
                                            {"nok": "[(0,2) | (3,4)]", "scan": "[]"},
                                            {"nok": "[(0,2) | (2,4)]", "scan": "[(1,2) | (2,4)]"})[0], "twin")
# A twin the engines agree on bar upstream's self-contradicting change list still counts.
kb = {"pattern": "(?:(?:ab|a)\\K(?:ba){1,2}+){e<=2}", "subject": "aabbaa", "operation": "fullmatch", "flags": 4096}
check("twin leak", screen.classify(kb, "(4,6) (1,0,1) [4;;3]", "(3,6) (0,1,1) [;1;5]", [],
                                   {"nok": "(0,6) (0,1,1) [1;1;]"}, {"nok": "(0,6) (0,1,1) [;1;5]"})[0], "twin")

row = {"pattern": "(ab)(*SKIP)a{1,2}", "subject": "aab", "operation": "search", "partial": True}
# A pair of switches that gives upstream's answer claims the row, naming both entries.
got = screen.classify(row, "(0,1)", "None", ["DropNegativeTestCaptures+upstreamFuzzyConditionTest"], {}, {})
check("pair claim", got and got[0], "ablation")
check("pair names", "ledger 64" in got[1] and "ledger 65" in got[1], True)
# A twin both engines agree on, equal to the port's answer, claims it.
check("prune twin", screen.classify(row, "P(3,3) g1=(1,3)", "P(1,3) g1=(1,3)", [], {"prune": "P(1,3) g1=(1,3)"},
                                    {"prune": "P(1,3) g1=(1,3)"})[0], "twin")
# ... but not when the port's own answer differs from the twin's ...
check("prune twin, port differs", screen.classify(row, "P(3,3)", "P(2,3)", [], {"prune": "P(1,3)"}, {"prune": "P(1,3)"}),
      None)
# ... nor when the engines disagree on the twin.
check("prune twin, engines split", screen.classify(row, "P(3,3)", "P(1,3)", [], {"prune": "P(0,3)"}, {"prune": "P(1,3)"}),
      None)
# D96: the H'-off switch on a \K row is the open defect, reported, never claimed.
keep = {"pattern": "(?:a){i<=1}\\K", "subject": "ba", "operation": "search", "flags": 0x8000}
check("D96 stays open", screen.classify(keep, "(2,2)", "(2,2) (0,1,0) [;0;]", ["noLift"], {}, {})[0], "open")
# ... and the \K twin does not claim it either: the port's counts differ from the twin's.
check("D96 not a moved start", screen.classify(keep, "(2,2)", "(2,2) (0,1,0) [;0;]", [], {"nok": "(1,2)"}, {"nok": "(1,2)"}),
      None)
# The same switch on a row without \K is H' (D61), decided.
check("H' claimed", screen.classify({**keep, "pattern": "(?:a){i<=1}"}, "(1,2)", "(0,2) (0,1,0) [;0;]", ["noLift"], {}, {})[0],
      "ablation")
# A hit bar change positions counts only where upstream's list contradicts its own counts.
cond = {"pattern": "(?:(?(?!x)xba|a)){e<=2}c", "subject": "c", "operation": "search"}
pair = ["~DropNegativeTestCaptures+upstreamFuzzyConditionTest"]
check("leaked list", screen.classify(cond, "(0,1) (0,0,1) [0;;]", "None", pair, {}, {})[0], "ablation")
check("consistent list", screen.classify(cond, "(0,1) (0,0,1) [;;1]", "None", pair, {}, {}), None)
check("contradiction", screen.contradicts_its_counts("[(0,1) | (2,3) (1,0,0) [;;2]]"), True)
check("no contradiction", screen.contradicts_its_counts("(0,3) (1,0,1) [0;;1] g1=(0,1)"), False)
# The row's own answer is upstream's bar positions: ledger 11 alone, never a switch.
check("base leak", screen.classify(cond, "(0,1) (0,0,1) [0;;]", "(0,1) (0,0,1) [;;0]", ["~base"], {}, {})[0], "leak")
check("base, consistent list", screen.classify(cond, "(0,1) (0,0,1) [;;1]", "(0,1) (0,0,1) [;;0]", ["~base"], {}, {}), None)
# An exact hit is preferred to a loose one, and a single switch to a pair.
check("exact first", screen.classify(cond, "(0,1) (0,0,1) [0;;]", "None", ["~KeepCallCaptures", "UpstreamEmptyIterations+d85"],
                                     {}, {})[1], screen.SWITCHES["UpstreamEmptyIterations"] + " + " + screen.SWITCHES["d85"])
# POSIX: the POSIX-free fullmatch over each of the port's spans is the control.
px = {"pattern": "(?b)(?e)(?p)(a+)b", "subject": "aab", "operation": "search", "flags": 2}
check("posix found", screen.is_posix(px), True)
check("posix removed", screen.without_posix(px)["pattern"], "(?b)(?e)(a+)b")
check("posix flag removed", screen.without_posix({**px, "pattern": "(a+)b", "flags": 0x10002})["flags"], 2)
check("posix questions", [(q["operation"], q["pos"], q["endpos"]) for q in screen.posix_span_questions(px, "(0,3) g1=(0,2)")],
      [("fullmatch", 0, 3)])
check("overcharge", screen.posix_overcharge("(0,3) (1,0,0) [posix] g1=(0,1)", "(0,3) g1=(0,2)", ["(0,3) g1=(0,2)"],
                                            ["(0,3) g1=(0,2)"]), True)
check("same cost", screen.posix_overcharge("(0,3) g1=(0,1)", "(0,3) g1=(0,2)", ["(0,3) g1=(0,2)"], ["(0,3) g1=(0,2)"]),
      False)
check("other span", screen.posix_overcharge("(0,2) (1,0,0) [posix]", "(0,3)", ["(0,3)"], ["(0,3)"]), False)
check("engines split on the control", screen.posix_overcharge("(0,3) (1,0,0) [posix]", "(0,3)", ["(0,3) (0,0,1) [;;1]"],
                                                              ["(0,3)"]), False)
# Upstream crashing or refusing is never a claim.
check("upstream crash", screen.classify(row, "RESOURCE-KILLED", "None", ["KeepCallCaptures"], {}, {}), None)

if FAILED:
    print("\n".join(FAILED))
    sys.exit(1)
print("screen tests passed")
