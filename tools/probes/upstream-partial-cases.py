# Probes upstream regex 2026.7.19 on the S31 partial-match divergence families (2026-09-12). Results: docs/plan/2026-09-12-divergence-research.md
import regex
def t(p,s,**k):
    m=regex.compile(p).match(s,partial=True,**k) if 'pos' in k or 'endpos' in k else regex.match(p,s,partial=True)
    print(f"{p!r:14} {s!r:8} {k or ''} -> {(m.span(), m.partial) if m else None}")
print("# family 3: bounded lazy repeat - can 'baa' be a prefix of any match?")
for p in ["ba??x","ba?x","ba{0,1}?x","b(a??)x","(?:ba??)x",".{0,2}?x","b.??x","ba*?x"]: t(p,"baa")
for p in ["ba??x"]: t(p,"ba"); t(p,"b"); t(p,"bab")
print("# family 1: search_start partial on empty subject")
for p in [r"(?r)\b$", r"\b$", r"$", r"(?r)$", r"\b"]:
    m=regex.compile(p).search("",partial=True); n=regex.compile(p).match("",partial=True)
    print(f"{p!r:10} search={(m.span(),m.partial) if m else None} match={(n.span(),n.partial) if n else None}")
print("# family 2: narrowed slice, reversed")
c=regex.compile(r"(?r)a(bc)*")
for pos,end in [(1,1),(0,1),(1,3),(0,0),(2,2)]:
    m=c.match("abc",pos,end,partial=True); print(f"pos={pos} endpos={end} -> {(m.span(),m.partial) if m else None}")
c2=regex.compile(r"a(bc)*")
for pos,end in [(1,1),(0,1),(2,2)]:
    m=c2.match("abc",pos,end,partial=True); print(f"fwd pos={pos} endpos={end} -> {(m.span(),m.partial) if m else None}")
