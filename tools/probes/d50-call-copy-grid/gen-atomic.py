"""D50: a group holding an atomic group, called from a fuzzy section or a lookbehind, against the
call written out. Checks that the copy is not left optimised by the pattern's own pass."""
import json, random, sys
rng = random.Random(int(sys.argv[1]))
ALTS = ['ab|ac', 'a|b', 'ab|a', 'a|ab', 'ba|ca', 'abc|abd|b', 'x|xy|y', 'ss|st', 'ß|ss', 'a*|b', 'ab|ab?c']
FUZZ = ['e<=1', 's<=1', 'i<=1', 'd<=1', 'e<=2']
out = []
for _ in range(int(sys.argv[2])):
    body = '(?>' + rng.choice(ALTS) + ')' + rng.choice(['', 'c', 'b', '(?:' + rng.choice(ALTS) + ')'])
    flags = rng.choice(['', '(?fi)', '(?r)', '(?i)'])
    site = rng.choice(['(?:{c}{t}){{{f}}}', '(?:{t}{c}){{{f}}}', '{t}(?<=x?{c})', '(?:{t}(?<={c})){{{f}}}', '(?<!{c}){t}'])
    t = rng.choice(['', 'a', 'b', 'c'])
    f = rng.choice(FUZZ)
    pat = flags + '(?(DEFINE)(?P<g1>' + body + '))' + site.format(c='(?&g1)', t=t, f=f)
    inline = flags + site.format(c='(?:' + body + ')', t=t, f=f)
    subj = ''.join(rng.choice('abcxyßst') for _ in range(rng.randint(0, 6)))
    out.append({'pattern': pat, 'inline': inline, 'subject': subj, 'kinds': []})
for r in out: print(json.dumps(r))
