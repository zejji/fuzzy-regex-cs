"""Read a survey.py output directory: why engines did not answer, and every disagreement.

    python tools/matrix/survey_report.py <survey-dir> [--reasons] [--cell CELL] [--disagreements N]
"""

import argparse
import collections
import json
import sys
from pathlib import Path


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("dir", type=Path)
    ap.add_argument("--reasons", action="store_true")
    ap.add_argument("--cell")
    ap.add_argument("--disagreements", type=int, default=0)
    ap.add_argument("--verdict", default="disagree,disagree-existence,disagree-groups")
    a = ap.parse_args(argv)
    sys.stdout.reconfigure(encoding="utf-8")
    results = collections.defaultdict(dict)
    for line in open(a.dir / "results.jsonl", encoding="utf-8"):
        r = json.loads(line)
        results[r["id"]][r["engine"]] = r
    rows = [json.loads(line) for line in open(a.dir / "rows.jsonl", encoding="utf-8")]
    if a.cell:
        rows = [r for r in rows if r.get("cell") == a.cell or a.cell in r.get("cell", "")]
    if a.reasons:
        why = collections.Counter()
        for r in rows:
            for e, res in results[r["id"]].items():
                if res["status"] in ("n/a", "error", "timeout", "memory", "crash"):
                    text = res.get("reason") or res.get("error") or ""
                    why[(e, res["status"], text[:90])] += 1
        for (e, s, t), n in why.most_common(60):
            print(f"{n:>5} {e:7} {s:8} {t}")
    verdicts = set(a.verdict.split(","))
    shown = 0
    for r in rows:
        judge_split = any(j["differ"] for j in r.get("judges", {}).values())
        wanted = r.get("verdict") in verdicts or ("judge" in verdicts and judge_split)
        if not wanted or shown >= a.disagreements:
            continue
        shown += 1
        flags = f" flags={r['flags']}" if r.get("flags") else ""
        part = " partial" if r.get("partial") else ""
        print(f"\n{r['id']} [{r['verdict']}] {r['operation']}{part}{flags} {r['pattern']!r} over {r['subject']!r}"
              + "".join(f"  {j}-differs:{','.join(v['differ'])}" for j, v in r.get("judges", {}).items() if v["differ"]))
        for e, res in results[r["id"]].items():
            if res["status"] == "n/a":
                continue
            detail = res.get("matches") if res["status"] == "matches" else [res.get("span"), res.get("groups")]
            if res["status"] == "matches":
                detail = [m["span"] for m in detail]
            extra = res.get("error", "") or (f"cost={res['cost']}" if "cost" in res else "")
            extra += f" fc={res['fuzzy_counts']}" if res.get("fuzzy_counts") and any(res["fuzzy_counts"]) else ""
            print(f"    {e:7} {res['status']:8} {detail} {extra}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
