r"""Grid for ledger entry 50 (known defect D8): an insertion in front of a failing lookaround.

Draws random patterns with a lookaround inside a fuzzy section, answers each with the reference
matcher (fuzzy-reference-matcher.py, rule 10), with upstream (python `regex`) and with this port
(port-probe.cs on a Debug build), and reports:

- every row where the port and the reference disagree (must be none);
- per construct, how many rows the grid drew, and how many of those rule 10 moves (the reference
  answers differently with LOOKAROUND_INSERTION = False: the rows the fix moves);
- of the moved rows, how many upstream answers as the reference does with rule 10 and rule 3 (the
  ledger entry 42 deletion, which upstream also lacks) both off; the rest are listed.

Run from the repo root:
    git show 8dda4d9:tools/probes/port-probe.cs > .scratch/probes/port-probe.cs
    python tools/probes/lookaround-insertion-grid.py [--seed N] [--rows N]
"""

import argparse
import importlib.util
import json
import pathlib
import random
import re
import subprocess
import sys

import regex

ROOT = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("ref", ROOT / "tools/probes/fuzzy-reference-matcher.py")
ref = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ref)
ref.EMPTY_DELETION_ITERATIONS = "needed"

LOOKS = ["(?=", "(?!", "(?<=", "(?<!"]
CONSTRAINTS = ["{i<=1}", "{i<=2}", "{e<=1}", "{e<=2}", "{s<=1}", "{d<=1}", "{i<=1,d<=1}", "{i<=1,s<=1}"]


def look_body(rng, ahead):
    body = rng.choice(["a", "b", "c", "x", "[ab]", "ab", "cx", "a|c"])
    if ahead and rng.random() < 0.25:
        body = "(" + body + ")"  # a capture inside a lookahead
    return body


def atom(rng):
    r = rng.random()
    if r < 0.35:
        kind = rng.choice(LOOKS)
        return kind + look_body(rng, kind in ("(?=", "(?!")) + ")"
    lit = rng.choice(["a", "b", "c", "[ab]", "."])
    if rng.random() < 0.15:
        lit += "?"
    return lit


def pattern(rng):
    parts = [atom(rng) for _ in range(rng.randint(1, 4))]
    if not any(p.startswith("(?") for p in parts):
        parts.insert(rng.randrange(len(parts) + 1), rng.choice(LOOKS) + "c)")
    body = "".join(parts)
    if rng.random() < 0.2:
        body += "|" + "".join(atom(rng) for _ in range(rng.randint(1, 2)))
    p = "(?:" + body + ")" + rng.choice(CONSTRAINTS)
    if rng.random() < 0.3:
        p = rng.choice(["a", "b", "c"]) + p
    if rng.random() < 0.3:
        p += rng.choice(["a", "b", "c"])
    return p


def ref_answer(op, p, s):
    m = getattr(ref, op)(p, s)
    if m is None:
        return "None"
    g1 = m.groups[0] if m.groups else None
    return (m.span, tuple(m.fuzzy_counts), g1)


def upstream_answer(op, p, s):
    m = getattr(regex, op)(p, s)
    if m is None:
        return "None"
    g1 = m.span(1) if m.re.groups else None
    return (m.span(), tuple(m.fuzzy_counts), g1)


PORT_LINE = re.compile(
    r"^\((\d+),(\d+)\) FuzzyCounts \{ Substitutions = (\d+), Insertions = (\d+), Deletions = (\d+)[^}]*\}(?: g1=(.*))?$"
)


def port_answer(text):
    if text == "None":
        return "None"
    m = PORT_LINE.match(text)
    if not m:
        return text
    g1 = None
    if m.group(6) is not None:
        spans = re.findall(r"\((\d+),(\d+)\)", m.group(6))
        g1 = (int(spans[-1][0]), int(spans[-1][1])) if spans else (-1, -1)
    return ((int(m.group(1)), int(m.group(2))), (int(m.group(3)), int(m.group(4)), int(m.group(5))), g1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=50)
    ap.add_argument("--rows", type=int, default=3000)
    args = ap.parse_args()
    rng = random.Random(args.seed)

    rows = []
    seen = set()
    while len(rows) < args.rows:
        p = pattern(rng)
        s = "".join(rng.choice("abcx") for _ in range(rng.randint(0, 5)))
        op = rng.choice(["search", "search", "match", "fullmatch"])
        if (op, p, s) in seen:
            continue
        seen.add((op, p, s))
        rows.append((op, p, s))

    scratch = ROOT / ".scratch"
    scratch.mkdir(exist_ok=True)
    jsonl = scratch / "lookaround-grid.jsonl"
    jsonl.write_text("".join(json.dumps({"pattern": p, "subject": s, "operation": op}) + "\n" for op, p, s in rows),
                     encoding="utf-8")
    out = subprocess.run(["dotnet", "run", "-c", "Debug", str(scratch / "probes/port-probe.cs"), "--", str(jsonl)],
                         capture_output=True, text=True, encoding="utf-8", cwd=ROOT)
    lines = [ln for ln in out.stdout.splitlines() if ln.count("\t") >= 4]
    if len(lines) != len(rows):
        sys.exit(f"port-probe gave {len(lines)} lines for {len(rows)} rows:\n{out.stdout[-2000:]}\n{out.stderr[-2000:]}")

    drawn = {k: 0 for k in LOOKS}
    moved = {k: 0 for k in LOOKS}
    wrong = []
    unexplained = []
    for (op, p, s), line in zip(rows, lines):
        expected = ref_answer(op, p, s)
        ours = port_answer(line.split("\t")[3])
        theirs = upstream_answer(op, p, s)
        kinds = [k for k in LOOKS if k in p]  # "(?=" is not a substring of "(?<="
        if ours != expected:
            wrong.append((op, p, s, expected, ours, theirs))
        ref.LOOKAROUND_INSERTION = False
        without = ref_answer(op, p, s)
        if without != expected:
            for k in kinds:
                moved[k] += 1
            ref.DELETE_AFTER_EXACT = False
            if ref_answer(op, p, s) != theirs:
                unexplained.append((op, p, s, expected, theirs))
            ref.DELETE_AFTER_EXACT = True
        ref.LOOKAROUND_INSERTION = True
        for k in kinds:
            drawn[k] += 1

    print(f"seed {args.seed}: {len(rows)} rows; port vs reference: {len(wrong)} disagree")
    for k in LOOKS:
        print(f"  {k:5} drawn {drawn[k]:5}  moved by rule 10 {moved[k]:4}")
    print(f"  moved rows where upstream is not the reference without rules 10 and 3: {len(unexplained)}")
    for w in wrong[:20]:
        print("  DISAGREE", w)
    for u in unexplained[:10]:
        print("  UNEXPLAINED", u)


if __name__ == "__main__":
    main()
