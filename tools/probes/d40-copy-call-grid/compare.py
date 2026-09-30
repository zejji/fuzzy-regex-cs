"""D40 grid comparison. Reads STEM.{up,inline,before,after,portinline}.txt: upstream, upstream on
the written-out form, the port before and after the fixes, and the port on the written-out form.
A call means its body written out, so the port's answer must equal its own written-out answer on
every row that has one. Every row whose answer (span and fuzzy counts) differs from upstream's is
classified; pass --show to print the rows of each class."""
import collections, json, sys
stem = sys.argv[1]
rows = [json.loads(l) for l in open(stem + '.jsonl', encoding='utf-8') if l.strip()]
up, il, bf, af, pi = ([l.rstrip('\r\n') for l in open(f'{stem}.{k}.txt', encoding='utf-8')]
                      for k in ('up', 'inline', 'before', 'after', 'portinline'))
assert len(rows) == len(up) == len(il) == len(bf) == len(af) == len(pi)
# Optional: main's written-out answers (STEM.beforeinline.txt) and the ablated port (STEM.ablate.txt).
def optional(k):
    try: return [l.rstrip('\r\n') for l in open(f'{stem}.{k}.txt', encoding='utf-8')]
    except FileNotFoundError: return None
bi, ab = optional('beforeinline'), optional('ablate')
def head(a):  # span and fuzzy counts: the written-out form's captures differ by construction
    return ' '.join(a.split(' ')[:2]) if a.startswith('(') else a
answers = lambda a: a == 'None' or a.startswith('(')
kinds, kinds_matched, tally, cls = (collections.Counter() for _ in range(4))
shown = collections.defaultdict(list)
for n, (r, u, i, b, a, p) in enumerate(zip(rows, up, il, bf, af, pi)):
    for k in r['kinds']:
        kinds[k] += 1
        kinds_matched[k] += u.startswith('(')
    tally['exceptions after'] += a.startswith('ERR') and a != 'ERR Timeout'
    tally['exceptions before'] += b.startswith('ERR') and b != 'ERR Timeout'
    tally['asserts after'] += a.startswith('ERR ASSERT')
    tally['timeouts after'] += a == 'ERR Timeout'
    tally['upstream answers'] += answers(u)
    tally['upstream segfaults'] += u == 'ERR Segfault'
    tally['rows with a written-out form the port answers'] += answers(p)
    if answers(p) and head(a) != head(p):
        tally['DIFFER FROM THE WRITTEN-OUT FORM'] += 1
        shown['DIFFER FROM THE WRITTEN-OUT FORM'].append(n)
    if bi is not None and answers(bi[n]) and head(b) == head(bi[n]) and head(a) != head(p):
        tally['NEW WRONG AGAINST MAIN'] += 1
        shown['NEW WRONG AGAINST MAIN'].append(n)
    if bi is not None and head(bi[n]) != head(p):
        tally['written-out answer moved from main'] += 1
    if ab is not None and ab[n].startswith('ERR') and ab[n] != 'ERR Timeout' and answers(u):
        tally['ablation crashes where upstream answers'] += 1
        shown['ablation crashes where upstream answers'].append(n)
    if answers(u) and head(a) != head(u):
        if not answers(p): c = 'no written-out form (recursion, or it does not compile)'
        elif head(a) != head(p): c = 'UNEXPLAINED'
        elif head(i) == head(p): c = 'upstream disagrees with its own written-out form'
        else: c = 'the two written-out forms already differ (not a call defect)'
        c += '; before the fixes ' + ('the same' if head(b) == head(a) else 'agreed with upstream' if head(b) == head(u) else 'a third answer')
        cls[c] += 1
        shown[c].append(n)
print('rows', len(rows), 'upstream matched', sum(1 for u in up if u.startswith('(')))
for k, v in sorted(tally.items()): print(f'  {k}: {v}')
print('rows per construct (all / upstream matched):', {k: (kinds[k], kinds_matched[k]) for k in sorted(kinds)})
print('differences from upstream, by class:')
for k, v in sorted(cls.items()): print(f'  {v:4}  {k}')
if '--show' in sys.argv:
    for c, ns in shown.items():
        print(f'[{c}]')
        for n in ns:
            print(f'  row {n} {rows[n]["pattern"]!r} {rows[n]["subject"]!r}\n'
                  f'     up {head(up[n])} | up written-out {head(il[n])} | port written-out {head(pi[n])} | before {head(bf[n])} | after {head(af[n])}')
