"""D40 grid: random patterns with group calls in fuzzy sections, conditional tests and lookbehinds,
calling groups whose bodies call other groups, as JSON-lines rows. Each row also carries 'inline',
the same pattern with every call written out as the body it calls (captures made non-capturing),
or null where a call recurses. The whole run, from this directory, for a stem such as .scratch/g:

    python gen-grid.py <seed> <rows> > STEM.jsonl
    python upstream-answer.py STEM.jsonl pattern > STEM.up.txt     # survives upstream's segfaults
    python upstream-answer.py STEM.jsonl inline > STEM.inline.txt
    dotnet run -c Debug port-answer.cs -- STEM.jsonl > STEM.after.txt   # STEM.before.txt: main's port
    dotnet run -c Debug port-answer.cs -- STEM.jsonl --field inline > STEM.portinline.txt
    python compare.py STEM
"""
import json, random, sys
seed, n = int(sys.argv[1]), int(sys.argv[2])
rng = random.Random(seed)
FUZZ = ['e<=1', 's<=1', 'i<=1', 'd<=1', 's<=1', 'e<=1']
LOOKS = ['(?=', '(?!', '(?<=', '(?<!']

def seq(d):
    return ('seq', [atom(d) for _ in range(rng.randint(1, 2))])

def atom(d):
    r = rng.random()
    if d >= 3 or r < 0.3:
        return ('lit', rng.choice(['a', 'b', '.', 'a', 'b', 'a{1,2}', '.*']))
    if r < 0.45:
        return ('group', seq(d + 1))
    if r < 0.6:
        return ('call',)
    if r < 0.72:
        return ('fuzzy', rng.choice(FUZZ), seq(d + 1))
    if r < 0.82:
        return ('look', rng.choice(['(?<=', '(?<!', '(?<=', '(?=', '(?!']), ('seq', [('call',)] + seq(d + 1)[1][:1]))
    if r < 0.92:
        return ('cond', rng.choice(LOOKS), ('seq', [('call',)]), seq(d + 1), seq(d + 1))
    return ('rep', rng.choice(['?', '*', '{1,2}']), atom(d + 1))

def pattern():
    top = [atom(0) for _ in range(rng.randint(0, 2))]
    # Always a group inside a fuzzy section whose body calls another group, and a group outside.
    top.insert(rng.randrange(len(top) + 1), ('fuzzy', rng.choice(FUZZ), ('seq', [('group', ('seq', [('call',)] + seq(2)[1][:rng.randint(0, 1)]))])))
    top.insert(rng.randrange(len(top) + 1), ('group', seq(1)))
    return ('seq', top)

def number(node, st, stack):
    """Numbers the groups in order and records each call's enclosing groups."""
    k = node[0]
    if k == 'group':
        st['g'] += 1
        me = st['g']
        st['bodies'][me] = node[1]
        node = ('group', node[1], me)
        return ('group', number(node[1], st, stack + [me]), me)
    if k == 'call':
        st['calls'].append(stack)
        return ('call', len(st['calls']) - 1)
    if k == 'seq':
        return ('seq', [number(x, st, stack) for x in node[1]])
    if k == 'fuzzy':
        return ('fuzzy', node[1], number(node[2], st, stack))
    if k == 'look':
        return ('look', node[1], number(node[2], st, stack))
    if k == 'cond':
        return ('cond', node[1], number(node[2], st, stack), number(node[3], st, stack), number(node[4], st, stack))
    if k == 'rep':
        return ('rep', node[1], number(node[2], st, stack))
    return node

class Recursion(Exception): pass

def render(node, st, inline, depth=0, capture=True, ctx=()):
    k = node[0]
    if k == 'seq': return ''.join(render(x, st, inline, depth, capture, ctx) for x in node[1])
    if k == 'lit': return node[1]
    if k == 'group':
        body = render(node[1], st, inline, depth, capture, ctx)
        return f'(?P<g{node[2]}>{body})' if capture else f'(?:{body})'
    if k == 'call':
        target = st['target'][node[1]]
        if target is None: return 'b'
        if not inline:
            st['kinds'].update(ctx)
            if st['calls'][node[1]]: st['kinds'].add('nested-call')
            return st['spell'][node[1]]
        if depth >= 8: raise Recursion()
        return '(?:' + render(st['numbered'][target], st, inline, depth + 1, False, ctx) + ')'
    if k == 'fuzzy':
        return '(?:' + render(node[2], st, inline, depth, capture, ctx + ('fuzzy-call',)) + '){' + node[1] + '}'
    if k == 'look':
        c = 'lookbehind-call' if '<' in node[1] else 'lookahead-call'
        return node[1] + render(node[2], st, inline, depth, capture, ctx + (c,)) + ')'
    if k == 'cond':
        return ('(?' + node[1] + render(node[2], st, inline, depth, capture, ctx + ('cond-call',)) + ')'
                + render(node[3], st, inline, depth, capture, ctx) + '|' + render(node[4], st, inline, depth, capture, ctx) + ')')
    if k == 'rep':
        return '(?:' + render(node[2], st, inline, depth, capture, ctx) + ')' + node[1]

def find_bodies(node, out):
    k = node[0]
    if k == 'group':
        out[node[2]] = node[1]
        find_bodies(node[1], out)
    elif k == 'seq':
        for x in node[1]: find_bodies(x, out)
    elif k in ('fuzzy', 'look', 'rep'):
        find_bodies(node[2], out)
    elif k == 'cond':
        for x in node[2:]: find_bodies(x, out)

rows = []
while len(rows) < n:
    st = {'g': 0, 'bodies': {}, 'calls': [], 'kinds': set()}
    tree = number(pattern(), st, [])
    G = st['g']
    if G == 0: continue
    st['numbered'] = {}
    find_bodies(tree, st['numbered'])
    st['target'], st['spell'] = [], []
    for ancestors in st['calls']:
        # Mostly a call inside a group calls a later-numbered one, so the calls cannot cycle and most
        # rows end rather than recurse; one in twenty may call any group.
        targets = [g for g in range(max(ancestors, default=0) + 1, G + 1)] if rng.random() < 0.95 else []
        if not targets and rng.random() < 0.95:
            # No later group to call: a literal instead, so the row still ends.
            st['target'].append(None); st['spell'].append('b'); continue
        if not targets: targets = list(range(1, G + 1))
        t = rng.choice(targets)
        st['target'].append(t)
        st['spell'].append(rng.choice([f'(?&g{t})', f'(?P>g{t})']))
    text = render(tree, st, False)
    try:
        inline = render(tree, st, True)
    except Recursion:
        inline = None
    subject = ''.join(rng.choice('aab') for _ in range(rng.randint(0, 10)))
    rows.append({'pattern': text, 'inline': inline, 'subject': subject, 'kinds': sorted(st['kinds'])})
for r in rows: print(json.dumps(r))
