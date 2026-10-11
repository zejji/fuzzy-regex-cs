"""D11 chunk 9 fix round, re-review finding 4 (VERIFICATION.md rule 11): rows that reach D38's
`WMarkKind.Held` (a condition's test that holds for every continuation after its yes-branch failed)
and the D64 review's must-advance change (a scan's attempt from inside the text to come runs with
must-advance lifted). Writes held-advance.jsonl; run d11-port-judge-grid.cs on it in Debug and read
the standard error's "rows with HeldFailedTheCondition" and "rows with MustAdvanceLifted", which
must be non-zero.

    python tools/probes/d11-held-advance-grid.py out.jsonl
"""
import itertools
import json
import sys

# Condition tests whose alternatives succeed with and without reading the edge, so the second
# success repeats the first one's continuation (the dedup), blocked or not.
TESTS = [
    r"ab$|a", r"a\b|a", r"ab\b|a", r"a|ab$", r"(?>ab$|a)", r"ab$(*PRUNE)|a", r"a$|a", r"a\B|a",
    r"ab|a", r".*z|a", r"a(?=b$)|a",
]
YES = ["ac", "a", "b"]
NO = ["abc", "a", ""]
WRAP = ["{}", "(?:{})*", "(?:{})+?", "c?{}", "(?:{}){{e<=1}}", "(?:{}){{i<=1}}"]
SUBJECTS = ["a", "ab", "abc", "b"]

# Zero-width patterns for the scans: after an empty match, the next attempt has must-advance set.
ZERO = [
    r"(?<=a)", r"(?<!b)", r"\b", r"\B", r"(?=a)", r"(?=\X)", r"(?=[ac])", "$", r"\m", r"\M", "^",
    r"(?=ab|a$)", r"(?!b)",
]
SCAN_SUBJECTS = ["", "a", "b", "aa", "ab", "ba", "aab", "abb"]

rows = []
for test, yes, no, wrap in itertools.product(TESTS, YES, NO, WRAP):
    pattern = wrap.format(f"(?(?={test}){yes}|{no})")
    for subject in SUBJECTS:
        for op in ["match", "search", "fullmatch"]:
            rows.append({"pattern": pattern, "subject": subject, "operation": op})
for first, second in itertools.product(ZERO, [""] + ZERO):
    for subject in SCAN_SUBJECTS:
        rows.append({"pattern": first + second, "subject": subject, "operation": "finditer"})
with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as f:
    for r in rows:
        f.write(json.dumps(r) + "\n")
print(len(rows))
