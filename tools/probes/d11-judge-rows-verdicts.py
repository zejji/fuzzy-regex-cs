"""Writes one d11-brute-judge.py verdict per row (continuations of up to 3 characters):
    python d11-judge-rows-verdicts.py rows.jsonl tools/probes/d11-brute-judge.py out.txt
"""
import json, sys, importlib.util, os
spec = importlib.util.spec_from_file_location("j", sys.argv[2])
j = importlib.util.module_from_spec(spec); spec.loader.exec_module(j)
out = open(sys.argv[3], "w", encoding="utf-8", newline="\n")
for line in open(sys.argv[1], encoding="utf-8"):
    r = json.loads(line)
    try:
        v, w = j.judge(r["operation"], r["pattern"], r["subject"], max_len=3)
        s = j.show(v)
    except Exception as e:
        s = "ERR:" + type(e).__name__
    out.write(s + "\n"); out.flush()
