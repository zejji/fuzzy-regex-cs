"""D11 amendment 2: judges a JSON-lines rows file with d11-brute-judge.py, one line per row.

Also prints upstream's own partial answer, and the group 1 spans of the completion the judge found
(wg1), which is what a partial's group 1 should hold where the completion fixes it.

    python tools/probes/d11-judge-rows.py rows.jsonl
"""
import json, sys, importlib.util, regex
spec = importlib.util.spec_from_file_location("j", __import__("os").path.join(__import__("os").path.dirname(__file__), "d11-brute-judge.py"))
j = importlib.util.module_from_spec(spec); spec.loader.exec_module(j)
for line in open(sys.argv[1], encoding="utf-8"):
    r = json.loads(line)
    v, w = j.judge(r["operation"], r["pattern"], r["subject"])
    try:
        m = getattr(regex.compile(r["pattern"]), r["operation"])(r["subject"], partial=True)
        up = "None" if m is None else f"{'P' if m.partial else 'F'}{m.span()}" + (f" g1={m.spans(1)}" if m.re.groups else "")
    except Exception as e:
        up = type(e).__name__
    cg = ""
    if w is not None and regex.compile(r["pattern"]).groups:
        mm = getattr(regex.compile(r["pattern"]), r["operation"])(r["subject"]+w)
        cg = f" wg1={mm.spans(1)}" if mm else ""
    print(f'{r["operation"]}\t{json.dumps(r["pattern"])}\t{json.dumps(r["subject"])}\tjudge={j.show(v)} w={w!r}{cg}\tupstream={up}')
