r"""Grid for ledger entry 51 (known defect D9): a section's minimum met by a trailing insertion.

Draws random patterns whose fuzzy sections have minimum error counts (e, i, d and s minimums,
mixed ones, and sections nested inside sections), answers each with the reference matcher
(fuzzy-reference-matcher.py, rule 6), with upstream (python `regex`) and with this port
(port-probe.cs on a Debug build), and reports:

- every row where the port and the reference disagree (must be none);
- per construct, how many rows the grid drew, and how many of those rule 6 moves (the reference
  answers differently with MINIMUM_AFTER_TRAILING_INSERTIONS = False: the rows the fix moves);
- of the moved rows, how many upstream answers as the reference does with rules 6, 3 and 10 all
  off; the rest are listed.

The reference has no flags, so two kinds of row are checked another way:

- (?r): the port's answer to "(?r)" + P over T must be the reference's answer to P reversed over T
  reversed, with the span mirrored. Only group-free patterns are drawn for these, so reversing the
  pattern is reversing its items and the characters of its strings.
- (?b) and (?e), on match and fullmatch only: every path the reference can take from position 0 is
  enumerated, and the port's error total must be the fewest any of them has (None when none).

Run from the repo root:
    git show 8dda4d9:tools/probes/port-probe.cs > .scratch/probes/port-probe.cs
    python tools/probes/fuzzy-minimum-grid.py [--seed N] [--rows N]
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

MINIMUMS = {
    "e": ["{1<=e<=1}", "{1<=e<=2}", "{2<=e<=2}", "{2<=e<=3}"],
    "i": ["{1<=i<=1}", "{1<=i<=2}", "{2<=i<=2}", "{1<=i<=2,s<=1}"],
    "d": ["{1<=d<=1}", "{1<=d<=2}", "{1<=d<=1,i<=1}"],
    "s": ["{1<=s<=1}", "{1<=s<=2}", "{1<=s<=1,i<=1}"],
    "mixed": ["{1<=i<=1,1<=d<=1}", "{1<=e<=2,i<=1}", "{1<=i<=1,1<=s<=1}"],
}
PLAIN = ["{e<=1}", "{i<=1}", "{e<=2}"]
KINDS = list(MINIMUMS) + ["nested", "(?r)", "(?b)", "(?e)"]


class Atom:
    """One pattern item: text, or a section around a list of atoms."""

    def __init__(self, text=None, body=None, constraint=None):
        self.text, self.body, self.constraint = text, body, constraint

    def render(self):
        if self.body is None:
            return self.text
        return "(?:" + "".join(a.render() for a in self.body) + ")" + self.constraint

    def reversed(self):
        if self.body is None:
            return Atom(self.text[::-1] if self.text.isalpha() else self.text)
        return Atom(body=[a.reversed() for a in reversed(self.body)], constraint=self.constraint)


def leaf(rng, groups):
    r = rng.random()
    if groups and r < 0.08:
        return Atom("(a)")
    return Atom(rng.choice(["a", "b", "c", "[ab]", ".", "ab", "abc", "a?", "b*"]))


def constraint(rng):
    kind = rng.choice(list(MINIMUMS))
    return kind, rng.choice(MINIMUMS[kind])


def pattern(rng, groups):
    """A list of atoms holding one section with a minimum, which may hold or sit inside another."""
    kinds = set()
    kind, c = constraint(rng)
    kinds.add(kind)
    body = [leaf(rng, groups) for _ in range(rng.randint(1, 3))]
    section = Atom(body=body, constraint=c)
    if rng.random() < 0.3:
        kinds.add("nested")
        if rng.random() < 0.5:
            inner_kind, inner_c = constraint(rng) if rng.random() < 0.6 else (None, rng.choice(PLAIN))
            if inner_kind:
                kinds.add(inner_kind)
            body.insert(rng.randrange(len(body) + 1), Atom(body=[leaf(rng, groups)], constraint=inner_c))
        else:
            section = Atom(body=[section, leaf(rng, groups)], constraint=rng.choice(PLAIN))
    atoms = [section]
    if rng.random() < 0.4:
        atoms.insert(0, Atom(rng.choice(["a", "b", "c"])))
    if rng.random() < 0.6:
        atoms.append(Atom(rng.choice(["a", "b", "c"])))
    text = "".join(a.render() for a in atoms)
    if groups and "(a)" in text and rng.random() < 0.5:
        atoms.append(Atom("\\1"))
    return atoms, kinds


def render(atoms):
    return "".join(a.render() for a in atoms)


def ref_answer(op, p, s):
    m = getattr(ref, op)(p, s)
    if m is None:
        return "None"
    return (m.span, tuple(m.fuzzy_counts))


def fewest_errors(op, p, s, cap=20000):
    """The fewest errors any reference path from 0 has, 'None' without one, or '?' past the cap."""
    tree, ngroups, referenced = ref.compile_pattern(p)
    st = ref.State(0, ((-1, -1),) * (ngroups + 1), (0, 0, 0), None, None, (), -1)
    best = None
    try:
        for n, final in enumerate(ref.run(tree, st, ref.Ctx(s, referenced), lambda x: iter([x]))):
            if n > cap:
                return "?"
            if op == "fullmatch" and final.pos != len(s):
                continue
            total = sum(final.totals)
            best = total if best is None else min(best, total)
    except ref.Prune:
        pass
    return "None" if best is None else best


def upstream_answer(op, p, s):
    m = getattr(regex, op)(p, s)
    if m is None:
        return "None"
    return (m.span(), tuple(m.fuzzy_counts))


PORT_LINE = re.compile(r"^\((\d+),(\d+)\) FuzzyCounts \{ Substitutions = (\d+), Insertions = (\d+), Deletions = (\d+)")


def port_answer(text):
    if text == "None":
        return "None"
    m = PORT_LINE.match(text)
    if not m:
        return text
    return ((int(m.group(1)), int(m.group(2))), (int(m.group(3)), int(m.group(4)), int(m.group(5))))


def draw(rng, count):
    rows, seen = [], set()
    while len(rows) < count:
        flag = rng.choice(["", "", "", "", "(?r)", "(?b)", "(?e)"])
        atoms, kinds = pattern(rng, groups=flag == "")
        s = "".join(rng.choice("abcx") for _ in range(rng.randint(0, 5)))
        if flag in ("(?b)", "(?e)"):
            op = rng.choice(["match", "fullmatch"])
        else:
            op = rng.choice(["search", "search", "match", "fullmatch"])
        key = (flag, op, render(atoms), s)
        if key in seen:
            continue
        seen.add(key)
        if flag:
            kinds = kinds | {flag}
        rows.append((flag, op, atoms, s, kinds))
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=51)
    ap.add_argument("--rows", type=int, default=3000)
    args = ap.parse_args()
    rows = draw(random.Random(args.seed), args.rows)

    scratch = ROOT / ".scratch"
    scratch.mkdir(exist_ok=True)
    jsonl = scratch / "minimum-grid.jsonl"
    jsonl.write_text(
        "".join(json.dumps({"pattern": f + render(a), "subject": s, "operation": op}) + "\n" for f, op, a, s, _ in rows),
        encoding="utf-8",
    )
    out = subprocess.run(["dotnet", "run", "-c", "Debug", str(scratch / "probes/port-probe.cs"), "--", str(jsonl)],
                         capture_output=True, text=True, encoding="utf-8", cwd=ROOT)
    lines = [ln for ln in out.stdout.splitlines() if ln.count("\t") >= 4]
    if len(lines) != len(rows):
        sys.exit(f"port-probe gave {len(lines)} lines for {len(rows)} rows:\n{out.stdout[-2000:]}\n{out.stderr[-2000:]}")

    drawn = {k: 0 for k in KINDS}
    moved = {k: 0 for k in KINDS}
    wrong, unexplained, skipped = [], [], 0
    for (flag, op, atoms, s, kinds), line in zip(rows, lines):
        p = render(atoms)
        ours = port_answer(line.split("\t")[3])
        if flag == "(?r)":
            rp, rs = render([a.reversed() for a in reversed(atoms)]), s[::-1]
            got = ref_answer(op, rp, rs)
            expected = got if got == "None" else ((len(s) - got[0][1], len(s) - got[0][0]), got[1])
            ref.MINIMUM_AFTER_TRAILING_INSERTIONS = False
            without = ref_answer(op, rp, rs)
            ref.MINIMUM_AFTER_TRAILING_INSERTIONS = True
            ok = ours == expected
        elif flag:
            expected = fewest_errors(op, p, s)
            if expected == "?":
                skipped += 1
                continue
            ok = (ours == "None") if expected == "None" else (ours != "None" and sum(ours[1]) == expected)
            ref.MINIMUM_AFTER_TRAILING_INSERTIONS = False
            without = fewest_errors(op, p, s)
            ref.MINIMUM_AFTER_TRAILING_INSERTIONS = True
        else:
            expected = ref_answer(op, p, s)
            ok = ours == expected
            ref.MINIMUM_AFTER_TRAILING_INSERTIONS = False
            without = ref_answer(op, p, s)
            if without != expected:
                ref.DELETE_AFTER_EXACT = False
                ref.LOOKAROUND_INSERTION = False
                theirs = upstream_answer(op, p, s)
                if ref_answer(op, p, s) != theirs:
                    unexplained.append((op, p, s, expected, theirs))
                ref.DELETE_AFTER_EXACT = True
                ref.LOOKAROUND_INSERTION = True
            ref.MINIMUM_AFTER_TRAILING_INSERTIONS = True
        if not ok:
            wrong.append((flag, op, p, s, expected, ours))
        for k in kinds:
            drawn[k] += 1
            if without != expected:
                moved[k] += 1

    print(f"seed {args.seed}: {len(rows)} rows ({skipped} (?b)/(?e) rows past the path cap); "
          f"port vs reference: {len(wrong)} disagree")
    for k in KINDS:
        print(f"  {k:7} drawn {drawn[k]:5}  moved by rule 6 {moved[k]:4}")
    print(f"  moved rows where upstream is not the reference without rules 6, 3 and 10: {len(unexplained)}")
    for w in wrong[:20]:
        print("  DISAGREE", w)
    for u in unexplained[:10]:
        print("  UNEXPLAINED", u)


if __name__ == "__main__":
    main()
