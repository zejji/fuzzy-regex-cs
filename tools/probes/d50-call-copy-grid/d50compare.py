"""D50 grid: rows whose answer moved from main, and whether each now equals the written-out form.

The run, from this directory, for a stem such as .scratch/g (main's port from a `git archive` of
main's src with this script beside it; ../d40-copy-call-grid/upstream-answer.py for upstream):

    python gen-grid.py 20261003 3000 > STEM.jsonl      # also: gen-grid.py 7 2000, gen-r.py 3 4000,
                                                        # gen-atomic.py 11 4000
    dotnet run -c Debug port-answer.cs -- STEM.jsonl > STEM.after.txt
    dotnet run -c Debug port-answer.cs -- STEM.jsonl --field inline > STEM.portinline.txt
    dotnet run -c Debug port-answer.cs -- STEM.jsonl --ablate > STEM.ablate.txt   # Info.UpstreamCallCopies
    (main's port) STEM.before.txt and, with --field inline, STEM.beforeinline.txt
    python ../d40-copy-call-grid/upstream-answer.py STEM.jsonl pattern > STEM.up.txt   # and inline
    python d50compare.py STEM

Measured 2026-10-03 (Debug, two million steps and two seconds per row): g 20261003/3000 and 7/2000
changed 6 rows, r 3/4000 changed 2, a 11/4000 changed 12; every changed row equals the port's
written-out answer, the ablation equals main on every row, and the written-out answers did not move.
"""
import json, sys, collections
def lines(p): return [l.rstrip('\r\n') for l in open(p, encoding='utf-8')]
head = lambda a: ' '.join(a.split(' ')[:2]) if a.startswith('(') else a
tot = collections.Counter()
for s in sys.argv[1:]:
    rows = [json.loads(l) for l in open(s + '.jsonl', encoding='utf-8')]
    b, a, ab, pi, bi, up, ui = (lines(f'{s}.{k}.txt') for k in ('before', 'after', 'ablate', 'portinline', 'beforeinline', 'up', 'inline'))
    c = collections.Counter()
    for n, r in enumerate(rows):
        p = r['pattern']
        for k, t in (('full-fold', p.startswith('(?f')), ('reversed', p[:6].find('r)') >= 0), ('atomic', '(?>' in p),
                     ('alternation', '|' in p.replace('(?(', '')), ('any', True)):
            if t: c['rows ' + k] += 1
        c['ablate != main (full answer)'] += ab[n] != b[n]
        c['written-out moved (full answer)'] += bi[n] != pi[n]
        if a[n] != b[n]:
            c['CHANGED (full answer)'] += 1
            if head(a[n]) != head(b[n]): c['changed span/counts'] += 1
            same = head(a[n]) == head(pi[n])
            c['changed and now equals written-out' if same else 'CHANGED BUT NOT WRITTEN-OUT'] += 1
            c['changed; upstream gave main\'s answer' if head(up[n]) == head(b[n]) else 'changed; upstream gave another'] += 1
            print(f'{s} row {n} {"OK " if same else "BAD"} {ascii(p)} {ascii(r["subject"])}\n    main {head(b[n])} | now {head(a[n])} | written-out {head(pi[n])} | up {head(up[n])} | up written-out {head(ui[n])}')
        if pi[n].startswith('(') or pi[n] == 'None':
            c['rows with written-out answer'] += 1
            if head(a[n]) != head(pi[n]): c['NOW DIFFERS FROM WRITTEN-OUT'] += 1
            if head(b[n]) != head(bi[n]): c['main differed from written-out'] += 1
        c['ERR Steps after'] += a[n] == 'ERR Steps'
        c['ERR Timeout after'] += a[n] == 'ERR Timeout'
    print(s, dict(sorted(c.items())))
    tot.update(c)
print('TOTAL', dict(sorted(tot.items())))
