"""D10 grid: random patterns with a group call inside a lookaround, as JSON-lines rows.

The whole run, from this directory, for a stem such as .scratch/grid (measured 2026-09-29 at
seeds 20260929 x 1,500 and 7 x 3,000: 58 rows changed, all capture-list removals):

    python gen-grid.py <seed> <rows> > STEM.jsonl
    python upstream-answer.py STEM.jsonl > STEM.up.txt
    python reference-answer.py STEM.jsonl > STEM.ref.txt          # upstream, empty group in each lookaround
    python reference-answer.py STEM.jsonl --emit > STEM.refpat.jsonl
    dotnet run port-answer.cs -- STEM.jsonl > STEM.after.txt       # and STEM.before.txt with the D10 line removed
    dotnet run port-answer.cs -- STEM.refpat.jsonl --only-g > STEM.portref.txt
    python compare.py STEM
Each row carries the constructs it holds, so the comparison can report rows per construct."""
import json, random, sys
seed, n = int(sys.argv[1]), int(sys.argv[2])
rng = random.Random(seed)
LOOKS = ['(?=', '(?!', '(?<=', '(?<!']

def seq(depth, in_look, need_call=False):
    items = [atom(depth, in_look) for _ in range(rng.randint(1, 3))]
    if need_call:
        items.insert(rng.randrange(len(items) + 1), ('call',))
    return ('seq', items)

def atom(depth, in_look):
    r = rng.random()
    if depth >= 3 or r < 0.35:
        return ('lit', rng.choice(['a', 'b', '.', 'a', 'b']))
    if r < 0.5:
        return ('group', seq(depth + 1, in_look))
    if r < 0.62:
        return ('call',)
    if r < 0.74:
        return ('look', rng.choice(LOOKS), seq(depth + 1, True, need_call=rng.random() < 0.8))
    if r < 0.84:
        return ('alt', [seq(depth + 1, in_look) for _ in range(2)])
    if r < 0.95:
        return ('rep', rng.choice(['?', '*', '+', '??', '*?', '+?', '{1,2}', '{2}']), atom(depth + 1, in_look))
    return ('fuzzy', rng.choice(['e<=1', 's<=1', 'i<=1', 'd<=1']), seq(depth + 1, in_look))

def pattern():
    # A top level that is sure to hold a lookaround with a call inside it.
    top = [atom(0, False) for _ in range(rng.randint(0, 2))]
    look = ('look', rng.choice(LOOKS), seq(1, True, need_call=True))
    if rng.random() < 0.5:
        # The witness's shape: the lookaround as one branch of a repeated alternation, so a body
        # that is thrown away can still be followed by a match.
        look = ('rep', rng.choice(['+?', '*', '+', '{1,3}?']), ('alt', [('seq', [look]), seq(2, False)]))
    top.insert(rng.randrange(len(top) + 1), look)
    if rng.random() < 0.9:
        top.insert(0, ('group', seq(1, False)))
    return ('seq', top)

def render(node, st):
    k = node[0]
    if k == 'seq': return ''.join(render(x, st) for x in node[1])
    if k == 'lit': return node[1]
    if k == 'group':
        st['g'] += 1
        me = st['g']
        name = f'g{me}'
        st['stack'].append(me)
        body = render(node[1], st)
        st['stack'].pop()
        return f'(?P<{name}>{body})'
    if k == 'call':
        st['out_calls'].append(list(st['stack']))
        return f'\x00{len(st["out_calls"]) - 1}\x00'
    if k == 'look':
        st['kinds'].add({'(?=': 'poslookahead', '(?!': 'neglookahead', '(?<=': 'poslookbehind', '(?<!': 'neglookbehind'}[node[1]])
        return node[1] + render(node[2], st) + ')'
    if k == 'alt': return '(?:' + '|'.join(render(x, st) for x in node[1]) + ')'
    if k == 'rep':
        inner = render(node[2], st)
        return '(?:' + inner + ')' + node[1]
    if k == 'fuzzy':
        st['kinds'].add('fuzzy')
        return '(?:' + render(node[2], st) + '){' + node[1] + '}'

rows = []
while len(rows) < n:
    st = {'g': 0, 'stack': [], 'out_calls': [], 'kinds': set()}
    text = render(pattern(), st)
    G = st['g']
    for i, ancestors in enumerate(st['out_calls']):
        c = rng.random()
        # Mostly call a group the call is not inside, so most rows end rather than recurse forever.
        targets = [g for g in range(1, G + 1) if g not in ancestors] if rng.random() < 0.85 else []
        if not targets:
            targets = list(range(1, G + 1))
        if G == 0 or c < 0.05:
            rep, kind = '(?R)', 'recurse'
        elif c < 0.6:
            rep, kind = f'(?{rng.choice(targets)})', 'numbered'
        else:
            rep, kind = f'(?&g{rng.choice(targets)})', 'named'
        st['kinds'].add(kind)
        text = text.replace(f'\x00{i}\x00', rep)
    partial = rng.random() < 0.15
    if partial: st['kinds'].add('partial')
    subject = ''.join(rng.choice('ab') for _ in range(rng.randint(0, 8)))
    rows.append({'pattern': text, 'subject': subject, 'partial': partial, 'kinds': sorted(st['kinds'])})
for r in rows: print(json.dumps(r))
