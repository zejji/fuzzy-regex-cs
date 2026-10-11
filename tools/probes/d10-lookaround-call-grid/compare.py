"""D10 grid comparison: upstream vs the port without the fix (before) and with it (after)."""
import collections, json, re, sys
stem = sys.argv[1]
rows = [json.loads(l) for l in open(stem + '.jsonl', encoding='utf-8') if l.strip()]
up, bf, af = ([l.rstrip('\r\n') for l in open(f'{stem}.{k}.txt', encoding='utf-8')] for k in ('up', 'before', 'after'))
assert len(rows) == len(up) == len(bf) == len(af)
tally = collections.Counter(); kinds = collections.Counter(); kinds_matched = collections.Counter()
def strip_caps(a):  # everything but the capture lists
    return a.rsplit(' ', 1)[0] if a.startswith('(') else a
def sub_list(small, big):  # is every capture list in small a subsequence of big's?
    ls = re.findall(r'\[([^\]]*)\]', small.rsplit(' ', 1)[1]); lb = re.findall(r'\[([^\]]*)\]', big.rsplit(' ', 1)[1])
    def subseq(x, y):
        it = iter(re.findall(r'\(\d+,\d+\)', y)); return all(e in it for e in re.findall(r'\(\d+,\d+\)', x))
    return len(ls) == len(lb) and all(subseq(x, y) for x, y in zip(ls, lb))
changed = []; new_diff = []
for r, u, b, a in zip(rows, up, bf, af):
    for k in r['kinds']:
        kinds[k] += 1
        if u.startswith('('): kinds_matched[k] += 1
    tally[('before==upstream' if u == b else 'before!=upstream', 'after==before' if a == b else 'after!=before')] += 1
    if a != b:
        ok = strip_caps(a) == strip_caps(b) and a.startswith('(') and sub_list(a, b)
        changed.append((ok, r, u, b, a))
    if a != u and b == u:
        new_diff.append((r, u, a))
print('rows', len(rows)); print('matched upstream rows', sum(1 for u in up if u.startswith('(')))
for k, v in sorted(tally.items()): print(' ', k, v)
print('rows per construct (all / upstream matched):', {k: (kinds[k], kinds_matched[k]) for k in sorted(kinds)})
print('changed rows', len(changed), 'of which captures-only removals', sum(1 for c in changed if c[0]))
print('new differences from upstream', len(new_diff), 'of which in changed set', sum(1 for d in new_diff if any(d[0] is c[1] for c in changed)))
with open(stem + '.changed.txt', 'w', encoding='utf-8') as f:
    for ok, r, u, b, a in changed:
        f.write(json.dumps({'ok': ok, 'pattern': r['pattern'], 'subject': r['subject'], 'partial': r['partial'], 'up': u, 'before': b, 'after': a}) + '\n')
for ok, r, u, b, a in changed:
    if not ok: print('NOT CAPTURE-ONLY', r['pattern'], r['subject'], '\n  before', b, '\n  after ', a)
