"""D50 group-0 grid: one (?R) under a lookaround, a fuzzy section or both, always optional, so the
pattern ends. 'inline' writes the call out to depth 4 with the innermost call made to fail."""
import json, random, sys
seed, n = int(sys.argv[1]), int(sys.argv[2])
rng = random.Random(seed)
LITS = ['a', 'b', 'SS', 'ss', 'ß', 'st', 'ﬆ', 'ff', 'ﬀ', '(?:ab|ac)', '(?:ss|st)', '(?:a|b)', '(?:ß|x)', '(?:ﬀ|ff)', '(?>a|ab)', '.']
FUZZ = ['e<=1', 's<=1', 'i<=1', 'd<=1']
def lit(): return ''.join(rng.choice(LITS) for _ in range(rng.randint(1, 2)))
def wrap(core):
    k = rng.random()
    if k < 0.3: return '(?<=' + core + ')'
    if k < 0.5: return '(?=' + core + ')'
    if k < 0.75: return '(?:' + core + '){' + rng.choice(FUZZ) + '}'
    return '(?:(?<=' + core + ')){' + rng.choice(FUZZ) + '}' if rng.random() < 0.5 else '(?<=(?:' + core + '){' + rng.choice(FUZZ) + '})'
rows = []
while len(rows) < n:
    flags = rng.choice(['(?fi)', '(?fi)', '(?i)', '', '(?fir)', '(?r)'])
    call = '(?:' + lit() + '(?R)|' + lit() + ')' if rng.random() < 0.5 else '(?:(?R))?'
    body = lit() + wrap(call) + (lit() if rng.random() < 0.5 else '')
    body = rng.choice(['', lit() + '|']) + body
    pat = flags + body
    inner = '(?!)'
    for _ in range(4): inner = '(?:' + body.replace('(?R)', inner) + ')'
    inline = flags + body.replace('(?R)', inner)
    subject = ''.join(rng.choice(['a', 'b', 'ß', 's', 'S', 't', 'ﬆ', 'ﬀ', 'f', 'x', 'c']) for _ in range(rng.randint(0, 8)))
    rows.append({'pattern': pat, 'inline': inline, 'subject': subject, 'kinds': []})
for r in rows: print(json.dumps(r))
