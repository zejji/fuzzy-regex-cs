# Probes upstream regex 2026.7.19 on the S31 partial-match divergence families (2026-09-12). Results: docs/plan/2026-09-12-divergence-research.md
import regex
def show(label, m): print(f"{label:48} -> {m.span() if m else None}")
for p in [r"(?r)(ab)+", r"(?r)(?:ab)+", r"(?r)(ab)*", r"(?r)ab", r"(?r)(ab){1,2}", r"(?r)a+", r"(?r)(a)+", r"(ab)+"]:
    c = regex.compile(p)
    show(f"{p} fullmatch('xabz',1,3)", c.fullmatch("xabz", 1, 3))
    show(f"{p} fullmatch('ab')", c.fullmatch("ab"))
    show(f"{p} match('xabz',1,3)", c.match("xabz", 1, 3))
    show(f"{p} fullmatch('xab',1)", c.fullmatch("xab", 1))
    show(f"{p} fullmatch('abz',0,2)", c.fullmatch("abz", 0, 2))
    print()
