"""D10: upstream regex's canonical answer per JSON-lines row, in port-answer.cs's shape."""
import json, sys, regex
out = []
for line in open(sys.argv[1], encoding='utf-8'):
    if not line.strip(): continue
    r = json.loads(line)
    try:
        m = regex.compile(r['pattern']).search(r['subject'], partial=r.get('partial', False), timeout=2)
        if m is None: a = 'None'
        else:
            g = m.re.groups
            s, i, d = m.fuzzy_counts
            spans = ''.join(f'({m.start(k)},{m.end(k)})' for k in range(1, g + 1))
            caps = ''.join('[' + ''.join(f'({x},{y})' for x, y in m.spans(k)) + ']' for k in range(1, g + 1))
            a = f'({m.start()},{m.end()}) {s},{i},{d} {"P" if m.partial else "F"} {spans} {caps}'
    except TimeoutError: a = 'ERR Timeout'
    except Exception as e: a = 'ERR ' + type(e).__name__
    out.append(a)
sys.stdout.write('\n'.join(out) + '\n')
