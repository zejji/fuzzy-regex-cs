"""Does BESTMATCH rank by error count or by cost? Brute-force check against upstream.

Run with the upstream interpreter, e.g. `.venvs/regex-2026.9.10/Scripts/python.exe`.

Evidence recorded 2026-09-12 on regex 2026.9.10 (docs/plan/slices/S42-bestmatch.md):
  - Issue 470's example: `(?b)(voices){1i+1d+2s<=2}` on 'voixes voicees' -> (0, 6) 'voixes',
    fuzzy_counts (1, 0, 0), cost 2. 'voicees' is (0, 1, 0), cost 1. Both are one error, so the
    count-ranked search ties and takes the earlier; a cost-ranked one would take 'voicees'.
  - Upstream's only weighted (?b) test, test_regex.py:2713, gives (34, 39) under BOTH rules: the
    lowest-cost candidate and the fewest-errors candidate are the same substring.
"""
import regex

print("regex", regex.__version__)

for p in [r"(voices){1i+1d+2s<=2}", r"(voices){1i+1d+2s<=1}"]:
    m = regex.search("(?b)" + p, "voixes voicees")
    print(p, m.span(), m.group(), m.fuzzy_counts)


def candidates(pattern, subject, cost):
    out = []
    for i in range(len(subject) + 1):
        for j in range(i, len(subject) + 1):
            m = regex.fullmatch(pattern, subject[i:j])
            if m:
                s, ins, d = m.fuzzy_counts
                out.append((cost(s, ins, d), s + ins + d, i, j, subject[i:j], m.fuzzy_counts))
    return out


s = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro"
p = r"(foobar){i<=1,d<=2,s<=3,2d+1s<4}"
m = regex.search("(?b)" + p, s)
print("upstream (?b):", m.span(), m.group(), m.fuzzy_counts)
c = candidates(p, s, lambda sub, ins, d: 2 * d + sub)
print("lowest cost :", sorted(c)[0])
print("fewest errs :", sorted(c, key=lambda t: (t[1], t[2]))[0])
