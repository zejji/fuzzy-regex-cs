"""D10 reference: upstream's answer to each row with an empty capture group added at the start of
every lookaround body. Upstream saves and restores the captures around a lookaround only when its
body holds a capture group (_regex.c:13772), so the extra group makes it restore them for a body
that otherwise only calls a group. Numbered calls become named ones first so the renumbering the
extra groups cause cannot move a call. Output: port-answer.cs's shape, groups g1..gN only."""
import json, re, sys, regex
for line in open(sys.argv[1], encoding='utf-8'):
    if not line.strip(): continue
    r = json.loads(line)
    p = re.sub(r'\(\?(\d+)\)', r'(?&g\1)', r['pattern'])
    n = [0]
    def dummy(m):
        n[0] += 1
        return m.group(0) + f'(?P<zz{n[0]}>)'
    p = re.sub(r'\(\?<?[=!]', dummy, p)
    if len(sys.argv) > 2:
        print(json.dumps({**r, 'pattern': p})); continue
    names = sorted({int(x) for x in re.findall(r'\(\?P<g(\d+)>', r['pattern'])})
    try:
        m = regex.compile(p).search(r['subject'], partial=r.get('partial', False), timeout=2)
        if m is None: a = 'None'
        else:
            s, i, d = m.fuzzy_counts
            spans = ''.join(f'({m.start(f"g{k}")},{m.end(f"g{k}")})' for k in names)
            caps = ''.join('[' + ''.join(f'({x},{y})' for x, y in m.spans(f'g{k}')) + ']' for k in names)
            a = f'({m.start()},{m.end()}) {s},{i},{d} {"P" if m.partial else "F"} {spans} {caps}'
    except TimeoutError: a = 'ERR Timeout'
    except Exception as e: a = 'ERR ' + type(e).__name__
    print(a)
