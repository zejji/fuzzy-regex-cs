"""D40 grid comparison: upstream, the port before the fix and after it, and upstream's answer to the
inlined pattern (every call written out as its body, which is what a call means). Prints the
tallies, the rows per construct, and every row the fix changed, classified."""
import collections, json, sys
stem = sys.argv[1]
rows = [json.loads(l) for l in open(stem + '.jsonl', encoding='utf-8') if l.strip()]
up, bf, af, il = ([l.rstrip('\r\n') for l in open(f'{stem}.{k}.txt', encoding='utf-8')] for k in ('up', 'before', 'after', 'inline'))
assert len(rows) == len(up) == len(bf) == len(af) == len(il)
def head(a):  # span and fuzzy counts only: the inlined pattern's captures differ by construction
    return ' '.join(a.split(' ')[:2]) if a.startswith('(') else a
answers = lambda a: a == 'None' or a.startswith('(')
kinds, kinds_matched = collections.Counter(), collections.Counter()
tally = collections.Counter()
for r, u, b, a, i in zip(rows, up, bf, af, il):
    for k in r['kinds']:
        kinds[k] += 1
        if u.startswith('('): kinds_matched[k] += 1
    tally['after exception'] += a.startswith('ERR') and a not in ('ERR Timeout',)
    tally['before exception'] += b.startswith('ERR') and b not in ('ERR Timeout',)
    tally['after timeout'] += a == 'ERR Timeout'
    tally['upstream answers'] += answers(u)
    tally['upstream segfault'] += u == 'ERR Segfault'
    tally['changed by the fix'] += a != b
    tally['new difference from upstream'] += answers(u) and a != u and b == u
print('rows', len(rows)); print('upstream matched', sum(1 for u in up if u.startswith('(')))
for k, v in sorted(tally.items()): print(f'  {k}: {v}')
print('rows per construct (all / upstream matched):', {k: (kinds[k], kinds_matched[k]) for k in sorted(kinds)})
print('exceptions after:', collections.Counter(a for a in af if a.startswith('ERR')))
print('exceptions before:', collections.Counter(b for b in bf if b.startswith('ERR')))
cls = collections.Counter()
for n, (r, u, b, a, i) in enumerate(zip(rows, up, bf, af, il)):
    if a == b: continue
    if b.startswith('ERR') and not b == 'ERR Timeout':
        c = 'was an exception; now ' + ('agrees with inlined' if head(a) == head(i) else 'upstream-inlined differs' if answers(i) else 'no inlined answer')
    elif head(a) == head(i) and head(b) != head(i):
        c = 'moved to the inlined answer'
    else:
        c = 'OTHER'
    cls[c] += 1
    if c == 'OTHER' or 'differs' in c or len(sys.argv) > 2:
        print(f'[{c}] row {n} {r["pattern"]!r} {r["subject"]!r}\n   up     {u}\n   inline {i}\n   before {b}\n   after  {a}')
print('changed rows by class:', dict(cls))
