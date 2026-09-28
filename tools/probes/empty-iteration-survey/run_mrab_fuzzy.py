"""mrab regex on the fuzzy battery: (?:item){d<=N}, in search/match/fullmatch, plain, (?e) and (?b)."""
import os, regex
HERE = os.path.dirname(os.path.abspath(__file__))
for line in open(os.path.join(HERE, "fuzzy.tsv"), encoding="utf-8"):
    if line.startswith("#") or not line.strip(): continue
    cid, item, subj, n = line.rstrip("\n").split("\t")[:4]
    for flag in ("", "(?e)", "(?b)"):
        pat = f"{flag}(?:{item}){{d<={n}}}"
        cells = []
        for mode in ("search", "match", "fullmatch"):
            m = getattr(regex, mode)(pat, subj)
            cells.append(f"{mode}=" + ("nomatch" if m is None else f"{m.span()}{m.fuzzy_counts}"))
        print(f"regex {regex.__version__}\t{cid}\t{pat} over {subj!r}\t" + "  ".join(cells))
