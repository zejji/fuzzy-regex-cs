"""D40: upstream regex's answer per JSON-lines row, in port-answer.cs's shape, for the row's
'pattern' or 'inline' field. Upstream segfaults on some rows, so a child process answers them and
this supervisor restarts it past a row that kills it ('ERR Segfault') or hangs for 20 s
('ERR Hang'). Usage: python upstream-answer.py ROWS.jsonl pattern|inline > OUT.txt"""
import json, queue, subprocess, sys, threading
if len(sys.argv) > 3 and sys.argv[3] == '--child':
    import regex
    rows = [json.loads(l) for l in open(sys.argv[1], encoding='utf-8') if l.strip()]
    for k in range(int(sys.argv[4]), len(rows)):
        r = rows[k]
        p = r[sys.argv[2]]
        try:
            if p is None: raise ValueError('no pattern')
            m = regex.compile(p).search(r['subject'], timeout=2)
            if m is None: a = 'None'
            else:
                names = sorted(int(x[1:]) for x in m.re.groupindex if x.startswith('g'))
                s, i, d = m.fuzzy_counts
                spans = ''.join(f'({m.start(f"g{g}")},{m.end(f"g{g}")})' for g in names)
                caps = ''.join('[' + ''.join(f'({x},{y})' for x, y in m.spans(f'g{g}')) + ']' for g in names)
                a = f'({m.start()},{m.end()}) {s},{i},{d} {spans} {caps}'
        except TimeoutError: a = 'ERR Timeout'
        except Exception as e: a = 'ERR ' + type(e).__name__
        sys.stdout.write(f'{k}\t{a}\n'); sys.stdout.flush()
    sys.exit(0)
rows = sum(1 for l in open(sys.argv[1], encoding='utf-8') if l.strip())
out, start = [], 0
while start < rows:
    child = subprocess.Popen([sys.executable, __file__, sys.argv[1], sys.argv[2], '--child', str(start)],
                             stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                             text=True, encoding='utf-8')
    q = queue.Queue()
    threading.Thread(target=lambda: ([q.put(l) for l in child.stdout], q.put(None)), daemon=True).start()
    while True:
        try: line = q.get(timeout=20)
        except queue.Empty:
            child.kill(); out.append('ERR Hang'); start = len(out); break
        if line is None:
            child.wait()
            if len(out) < rows: out.append('ERR Segfault')
            start = len(out); break
        out.append(line.rstrip('\n').split('\t', 1)[1])
    child.wait()
sys.stdout.write('\n'.join(out) + '\n')
