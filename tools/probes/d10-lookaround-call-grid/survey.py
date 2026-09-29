"""D10 survey: captures left by a group call inside a lookaround. regex, PCRE2, Perl."""
import json, subprocess, sys, regex, pcre2
CASES = [
 ("W1 neg lookahead, call", r'(a)(?:(?!.(?1))|.)+?b', 'aaab'),
 ("W1 control, direct group", r'(a)(?:(?!.(a))|.)+?b', 'aaab'),
 ("W1 control, same group by name", r'(?P<x>a)(?:(?!.(?P<x>a))|.)+?b', 'aaab'),
 ("W2 pos lookahead backtracked, call", r'(a)(?:(?=.(?1))x|.)+?b', 'aaab'),
 ("W2 control, same group by name", r'(?P<x>a)(?:(?=.(?P<x>a))x|.)+?b', 'aaab'),
 ("neg lookbehind, call", r'(a)(?:(?<!(?1)).|.)+?b', 'aaab'),
 ("pos lookbehind backtracked, call", r'(a)(?:(?<=(?1))x|.)+?b', 'aaab'),
 ("pos lookahead kept, call", r'(a)(?=.(?1))', 'aa'),
 ("(?R) in neg lookahead", r'a(b)?(?!(?R))', 'aba'),
 ("(?&name) neg lookahead", r'(?<n>a)(?:(?!.(?&n))|.)+?b', 'aaab'),
 ("nested call neg lookahead", r'(a)(?<t>(?1))?(?:(?!.(?&t))|.)+?b', 'aaab'),
 ("fuzzy", r'(?:(a)(?:(?!.(?1))|.)+?b){e<=1}', 'aaab'),
 ("fuzzy inside lookahead", r'(a)(?:(?!.(?:(?1)){e<=1})|.)+?b', 'aaxb'),
]
def rx(p, s, partial=False):
    m = regex.search(p, s, partial=partial)
    if not m: return None
    g = m.re.groups
    return {"span": m.span(), "spans": [m.span(i) for i in range(1, g+1)],
            "caps": [m.spans(i) for i in range(1, g+1)]}
def pc(p, s):
    if '{e' in p or '?P<x>' in p: return 'n/a'
    try:
        m = pcre2.search(p, s)
    except Exception as e: return f'err {e}'
    if not m: return None
    g = m.re.groups if hasattr(m.re,'groups') else len(m.groups())
    return {"span": m.span(), "spans": [m.span(i) for i in range(1, len(m.groups())+1)]}
def pl(p, s):
    if '{e' in p: return 'n/a'
    p2 = p.replace('?P<', '?<')
    code = r'my($p,$s)=@ARGV; if($s=~/$p/){print "($-[0], $+[0]) ", join(" ", map { defined $-[$_] ? "($-[$_], $+[$_])" : "None" } 1..$#-), "\n"} else {print "None\n"}'
    return subprocess.run(['perl', '-e', code, p2, s], capture_output=True, text=True).stdout.strip()
print('regex', regex.__version__, 'pcre2 (pip)', pcre2.__name__)
for name, p, s in CASES:
    print(f'## {name}: {p!r} over {s!r}')
    print('  regex :', rx(p, s))
    print('  pcre2 :', pc(p, s))
    print('  perl  :', pl(p, s))
print('## partial: (a)(?:(?!.(?1))|.)+?bc over aaab')
print('  regex :', rx(r'(a)(?:(?!.(?1))|.)+?bc', 'aaab', True))
