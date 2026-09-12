# The three unexpected `verbs` rows at 2000 rows (seeds 7 and 4242) after S35, 2026-09-12: all are
# reversed overlapped scans with (*SKIP) where upstream reports MORE matches than this port. For each,
# ask upstream's own match()/search() at every endpos so S36 can judge which side is right.
import regex
def spans(ms): return [m.span() for m in ms]
cases = [
    (r'(?r)(?:[a\d]*(*SKIP)\D|\p{Nd})(?:[\p{L}\p{N}]{1,3}(*SKIP)\S|.)((?>\s+(*PRUNE)A))', 'İİAAA AS', 0),
    (r'(?r)(?:[^\d]{2,4}(*SKIP)A|\U0001F600)$', '\U0001F600\U0001F600\U00010400\U00010400\U0001F600\r\n\U0001F600A', regex.M),
    (r'(?r)([^a]{2,4}(*SKIP)[a\d])((?:[^\d]++(*SKIP)\s|\ ))', 'b0 0\n A', 0),
]
for p, s, f in cases:
    c = regex.compile(p, f)
    print(repr(p)[:70]); print("  overlapped finditer:", spans(c.finditer(s, overlapped=True)))
    print("  plain finditer:     ", spans(c.finditer(s)))
    print("  search(pos=0,endpos=e) for each e:", [(e, c.search(s, 0, e).span() if c.search(s, 0, e) else None) for e in range(len(s), 0, -1)])
    print("  match(endpos=e) for each e:      ", [(e, c.match(s, 0, e).span() if c.match(s, 0, e) else None) for e in range(len(s), 0, -1)])
