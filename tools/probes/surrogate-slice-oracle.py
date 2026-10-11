"""Answers the sweep's rows with upstream, over the cut-pair string: the subject's UTF-16 code
units decoded to code points, pairing every surrogate pair EXCEPT one split by the slice's
beginning or end, which becomes two lone surrogate code points. Spans are mapped back to UTF-16.
Usage: python tools/probes/surrogate-slice-oracle.py port-all.tsv out.tsv, where port-all.tsv is
tools/probes/surrogate-slice-sweep.cs run with --all. The first column of each output row says
whether upstream answers the same over the U+0001 twin, which separates the cut from everything else."""
import sys, regex

def cut_string(units, b, e):
    cps, starts = [], []  # starts[k] = UTF-16 index of code point k
    i = 0
    while i < len(units):
        u = units[i]
        if 0xD800 <= u <= 0xDBFF and i + 1 < len(units) and 0xDC00 <= units[i + 1] <= 0xDFFF and i + 1 not in (b, e):
            cps.append(chr(0x10000 + ((u - 0xD800) << 10) + (units[i + 1] - 0xDC00))); starts.append(i); i += 2
        else:
            cps.append(chr(u)); starts.append(i); i += 1
    starts.append(len(units))
    return ''.join(cps), starts

def norm(s):
    return ''.join('??' if ord(c) > 0xFFFF else '?' if 0xD800 <= ord(c) <= 0xDFFF or c == '\x01' else c for c in s)

def esc(s):
    return ''.join(c if 0x20 <= ord(c) <= 0x7e else '\\u%04X' % ord(c) for c in s)

def run(api, pat, text, pos, endpos, u):
    M = lambda m: 'None' if m is None else '(%d,%d)%s' % (u[m.start()], u[m.end()], 'p' if m.partial else '') + ''.join(
        '[%d,%d]' % (u[m.start(g)], u[m.end(g)]) if m.start(g) >= 0 else '[-]' for g in range(1, len(m.groups()) + 1))
    k = dict(pos=pos, endpos=endpos, timeout=2)
    if api == 'search': return M(pat.search(text, **k))
    if api == 'match': return M(pat.match(text, **k))
    if api == 'full': return M(pat.fullmatch(text, **k))
    if api == 'psearch': return M(pat.search(text, partial=True, **k))
    if api == 'pmatch': return M(pat.match(text, partial=True, **k))
    if api == 'pfull': return M(pat.fullmatch(text, partial=True, **k))
    if api == 'findall': return ' '.join(M(m) for m in pat.finditer(text, **k))
    if api == 'overlap': return ' '.join(M(m) for m in pat.finditer(text, overlapped=True, **k))
    if api == 'pfindall': return ' '.join(M(m) for m in pat.finditer(text, partial=True, **k))
    if api == 'count': return str(len(list(pat.finditer(text, **k))))
    if api == 'sub': return norm(pat.sub(r'<\g<0>>', text, **k))

out, n, diff = [], 0, 0
cache = {}
for line in open(sys.argv[1], encoding='utf-8'):
    f = line.rstrip('\n').split('\t')
    problems, src, _, bl, api, got, want, hsrc, hsub = f
    b, ln = map(int, bl.split(','))
    source = ''.join(chr(int(x, 16)) for x in hsrc.split())
    units = [int(x, 16) for x in hsub.split()]
    text, starts = cut_string(units, b, b + ln)
    cp = {u: k for k, u in enumerate(starts)}
    if source not in cache:
        try: cache[source] = regex.compile(source)
        except Exception as ex: cache[source] = ex
    pat = cache[source]
    if isinstance(pat, Exception): ans = 'COMPILE'
    else:
        try: ans = esc(run(api, pat, text, cp[b], cp[b + ln], starts))
        except TimeoutError: ans = 'TIMEOUT'
        except Exception as ex: ans = 'EXC:' + type(ex).__name__
    ph_units = list(units)
    if 0 < b < len(units) and 0xD800 <= units[b - 1] <= 0xDBFF and 0xDC00 <= units[b] <= 0xDFFF: ph_units[b - 1] = 1
    e = b + ln
    if 0 < e < len(units) and 0xD800 <= units[e - 1] <= 0xDBFF and 0xDC00 <= units[e] <= 0xDFFF: ph_units[e] = 1
    ptext, pstarts = cut_string(ph_units, b, e)
    pcp = {u: k for k, u in enumerate(pstarts)}
    try: pans = esc(run(api, pat, ptext, pcp[b], pcp[e], pstarts)) if not isinstance(pat, Exception) else 'COMPILE'
    except Exception as ex: pans = 'EXC:' + type(ex).__name__
    got = got[4:]
    n += 1
    if ans != got:
        diff += 1
        out.append('\t'.join(['same-on-placeholder' if ans == pans else 'SURROGATE-SPECIFIC', src, f[2], bl, api, 'port=' + got, 'upstream=' + ans, 'upstream_ph=' + pans]))
open(sys.argv[2], 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('rows=%d differ=%d' % (n, diff))
