"""Upstream behaviour the C3 design note relies on. Each line prints one claim's evidence.

Run from the repo root: .venvs/regex-2026.9.10/Scripts/python.exe -I -u tools/probes/c3-fuzzy-literal-upstream-claims.py
(under 1 s). Cited by docs/plan/2026-10-13-fuzzy-literal-bitparallel-design.md. Pass --posix to
also run the POSIX rows, which crash upstream 2026.9.10 (exit 139; docs/plan/upstream-reports/LEDGER.md:1364).
"""
import sys

import regex


def show(label, pattern, subject, **kw):
    m = regex.search(pattern, subject, **kw)
    if m is None:
        print(f"{label}: {ascii(pattern)} {ascii(subject)} -> None")
    else:
        print(f"{label}: {ascii(pattern)} {ascii(subject)} -> span={m.span()} counts={m.fuzzy_counts} "
              f"changes={m.fuzzy_changes} text={ascii(m.group())}")


SENT = "the quick brown fox jumps over the lazy dog "
FUZZY = SENT + "and finds a haystakc."
print("version", regex.__version__, "- fuzzy_counts is (substitutions, insertions, deletions)")
# 1. The three bench rows.
show("B1 FuzzyBudgetThree", r"(?:haystack){e<=3}", FUZZY)
show("B2 EnhanceMatch", r"(?e)(?:haystack){e<=3}", FUZZY)
show("B3 BestMatch", r"(?b)(?:haystack){e<=3}", FUZZY)
# 2. Leftmost-first, not minimum distance: an exact match exists one place later.
show("L1 leftmost", r"(?:abc){e<=1}", "xabc")
show("L2 leftmost", r"(?:abcd){e<=2}", "abxabcd")
show("L3 bestmatch", r"(?b)(?:abcd){e<=2}", "abxabcd")
# 3. Cost equations: k is the cost budget over the cheapest permitted kind.
show("C1 cost", r"(?:abcd){2i+2d+1s<=4}", "wxyz")
show("C2 cost", r"(?:abcd){2i+2d+1s<=4}", "abcdxx")
show("C3 cost", r"(?:abcd){2i+2d+1s<=3}", "wxyz")
# 4. Per-kind limits only narrow.
show("K1 kinds", r"(?:abcd){i<=1,d<=1,s<=0}", "abxd")
show("K2 kinds", r"(?:abcd){i<=1,d<=1}", "abxd")
# 5. Budget at or over the literal's length: matches the empty string anywhere.
show("E1 k>=m", r"(?:ab){e<=2}", "zzz")
# 6. Astral text: one code point is one substitution, but two UTF-16 units.
show("A1 astral", r"(?:abc){e<=1}", "a\U0001F600c")
# 7. Full case folding changes lengths.
show("F1 fold", r"(?if)(?:strasse){e<=0}", "STRAßE")
show("F2 fold", r"(?if)(?:strasse){e<=1}", "xstraße")
show("F3 kelvin", r"(?i)(?:kab){e<=0}", "Kab")
# 8. Partial: a prefix of the literal at the end of the subject.
show("P1 partial", r"(?:abcdef){e<=1}", "xxabc", partial=True)
# 9. Reverse search.
show("R1 reverse", r"(?r)(?:abcd){e<=1}", "abcdxabcd")
# 10. Insertion class narrows only.
show("I1 inscls", r"(?:abcd){e<=1:[a-z]}", "ab9cd")
# 12. Witnesses the design's refusals rest on.
show("W1 fold literal", "(?if)(?:straße){e<=1}", "strasse")
show("W2 kelvin text", r"(?i)(?:kabc){e<=1}", "Kabx")
show("W3 kinds sum", r"(?:abcd){i<=1,d<=1,s<=1}", "xbd")
show("W4 no-tiebreak", r"(?:abcbab){e<=2}", "bcbaba")
show("W5 dense pieces", r"(?:needle){e<=1}", "nee nee dle nee")
r = regex.compile(r"(?:abcd){e<=1}")
print("W6 endpos:", r.search("xxabcd", 0, 5).span(), r.search("xxabc", 0, 4))
# 11. POSIX: leftmost-longest. Opt-in: upstream crashes on the first row.
if "--posix" in sys.argv:
    show("X1 posix", r"(?p)(?:abc){e<=1}", "xabcz")
    show("X2 plain", r"(?:abc){e<=1}", "xabcz")
