# S34 item 1: upstream's overlapped scanner versus its own match/search at each position (2026-09-12).
import regex
p = r'(?:[^\d](*SKIP)){2,3}'; s = '\r\naabb '
print("upstream overlapped:", [m.span() for m in regex.finditer(p, s, regex.M, overlapped=True)])
print("upstream plain:     ", [m.span() for m in regex.finditer(p, s, regex.M)])
c = regex.compile(p, regex.M)
print("upstream match at each pos:", [(i, c.match(s, i).span() if c.match(s, i) else None) for i in range(len(s))])
print("upstream search from each pos:", [(i, c.search(s, i).span() if c.search(s, i) else None) for i in range(len(s))])
print("no-verb overlapped: ", [m.span() for m in regex.finditer(r'(?:[^\d]){2,3}', s, regex.M, overlapped=True)])
