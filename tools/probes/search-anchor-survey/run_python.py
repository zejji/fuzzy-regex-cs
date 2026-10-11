r"""Non-fuzzy \G survey: mrab regex and PCRE2 (pip pcre2). Run: python run_python.py"""
import pathlib, regex, pcre2
rows = [l.split('\t') for l in pathlib.Path(__file__).with_name('battery.tsv').read_text().splitlines() if l and l[0] != '#']
for rid, p, s, pos, _ in rows:
    if pos == '-':
        m = regex.search('(?r)' + p, s)
        print(f"regex {regex.__version__}\t{rid}\t{m and m.span()}")
        continue
    pos = int(pos)
    m = regex.search(p, s, pos=pos)
    print(f"regex {regex.__version__}\t{rid}\t{m and m.span()}")
    m = pcre2.compile(p).search(s, pos)
    print(f"pcre2 {pcre2.__version__}\t{rid}\t{m and (m.start(), m.end())}")
