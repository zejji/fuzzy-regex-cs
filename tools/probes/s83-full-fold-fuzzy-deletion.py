"""Ledger entry 28: a fuzzy deletion that finishes a full-case-folded item is charged twice upstream.

Run: python tools/probes/s83-full-fold-fuzzy-deletion.py

Each case is asked under Version 1 (full case folding, which builds STRING_FLD and REF_GROUP_FLD
items) and under Version 0 (simple folding, which never does). The cases hold no ligature, so the
same pattern means the same under both. The last four lines are wave row 6250, asked under V1 only.
"""
import regex

CASES = [
    (r"(?:fi){d<=1}", "fe"),
    (r"(?:fi){d<=2}", "fe"),
    (r"(?r)(?:fi){d<=1}", "ei"),
    (r"(?:sta){d<=1}", "sa"),
    (r"(?:a fie){e<=2}", "x a fe"),
    (r"(?:copper field studio){e<=2}", "COPPER FILD SUDIO HARBOUR CANVAS FALCON 1499452310"),
    (r"(fi)(?:\1){d<=1}", "fife"),
    (r"(?r)(?:\1){d<=1}(fi)", "eifi"),
]


def show(m):
    if m is None:
        return "None"
    return f"span={m.span()} fuzzy_counts={m.fuzzy_counts} fuzzy_changes={m.fuzzy_changes}"


print("regex", regex.__version__)
for pattern, subject in CASES:
    for name, flags in (("V1", regex.I | regex.V1), ("V0", regex.I | regex.V0)):
        print(f"{name} search({pattern!r}, {subject!r}) -> {show(regex.search(pattern, subject, flags))}")

# Upstream contradicting itself: a looser budget loses the match a tighter one finds.
# Seed 7 row 6250 of the fuzzy wave, asked with match().
subject = "ßasa0a\U0001f600"
for best in ("", "(?b)"):
    for budget in ("{d<=1}", "{s<=1,i<=1,d<=1}"):
        pattern = best + "(?fi)(ßa)(?:(?:\\1)\\B0a\U0001f600)" + budget
        print(f"V1 match({ascii(pattern)}, {ascii(subject)}) -> {show(regex.match(pattern, subject, regex.V1))}")
