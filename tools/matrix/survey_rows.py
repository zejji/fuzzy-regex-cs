"""Rows for the answer key's engine survey: a small sample for every risky cell, plus the witness
rows of the existing rulings.

    python tools/matrix/survey_rows.py --out .scratch/matrix/survey-rows.jsonl [--per-cell 30] [--seed 20260930]

The risky cells (tools/matrix/constructs.json, risky_families) are every single family, every pair
and every triple of {call, fuzzy, lookaround, conditional, verb, partial, reverse}, and capture with
each of them: 7 + 21 + 35 + 7 = 70 cells. A row belongs to its cell when its pattern and options
use at least one construct of each family in the cell; survey.py re-derives the families from the
row, and a generated row whose families do not cover its cell is discarded here, so a cell's count
is never inflated by a row that only claims it.

Patterns are built by wrapping a small atom (a, ab, a+, [ab], ...) once per family of the cell, in a
random order, so nesting depth is 1-3; subjects are 0-6 characters over {a, b, c}. This is a SAMPLE
for measuring where engines agree, not the harness's matrix: that is the harness builder's job.
"""

import argparse
import itertools
import json
import random
import sys
from pathlib import Path

import regex

sys.path.insert(0, str(Path(__file__).resolve().parent))
import survey  # noqa: E402

ATOMS = ["a", "b", "ab", "ba", "a+", "b*", "[ab]", ".", "a?b", "aa", "a*?"]
PATTERN_FAMILIES = ["call", "fuzzy", "lookaround", "conditional", "verb", "capture"]
ROW_FAMILIES = ["partial", "reverse"]


class Builder:
    def __init__(self, rng):
        self.rng = rng
        self.n = 0

    def name(self, prefix):
        self.n += 1
        return f"{prefix}{self.n}"

    def atom(self):
        return self.rng.choice(ATOMS)

    def wrap(self, family, x):
        r, a = self.rng, self.atom
        if family == "call":
            g = self.name("g")
            return r.choice([
                f"(?<{g}>{x})(?&{g})",
                f"(?(DEFINE)(?<{g}>{x}))(?&{g}){a()}",
                f"(?<{g}>a(?&{g})?b|{x})",
                f"(?P<{g}>{x}){a()}(?P>{g})",
                f"(?<{g}>{x}|b(?&{g}))",
            ])
        if family == "fuzzy":
            budget = r.choice(["e<=1", "i<=1", "d<=1", "s<=1", "1<=e<=2", "2i+2d+1s<=2", "e<=2"])
            return r.choice([f"(?:{x}){{{budget}}}", f"(?:{x}{a()}){{{budget}}}"])
        if family == "lookaround":
            look = r.choice(["?=", "?!", "?<=", "?<!"])
            return r.choice([f"({look}{x}){a()}", f"{a()}({look}{x})", f"({look}{x}){x}"])
        if family == "conditional":
            c = self.name("c")
            return r.choice([
                f"(?<{c}>{x})?(?({c}){a()}|{a()})",
                f"(?(?={x}){a()}|{a()})",
                f"(?(?!{x}){a()}|{a()})",
                f"{a()}(?(?<={x}){a()}|{a()})",
            ])
        if family == "verb":
            return r.choice([
                f"(?:{x}(*PRUNE){a()}|{a()})",
                f"(?:{x}(*SKIP){a()}|{a()})",
                f"(?:{x}(*FAIL)|{a()})",
                f"(?:{x}(*SKIP)(*F)|{a()})",
                f"{x}(*PRUNE){a()}",
            ])
        if family == "capture":
            n = self.name("n")
            return r.choice([f"({x})", f"(?<{n}>{x})", f"({x})+", f"(?:({x})|{a()})", f"(?<{n}>{x})*{a()}"])
        raise ValueError(family)


def make_row(rng, cell):
    b = Builder(rng)
    pattern_fams = [f for f in cell if f in PATTERN_FAMILIES]
    rng.shuffle(pattern_fams)
    x = b.atom()
    for f in pattern_fams:
        x = b.wrap(f, x)
    flags = 0
    if "reverse" in cell:
        if rng.random() < 0.5:
            x = "(?r)" + x
        else:
            flags |= survey.REVERSE_BIT
    partial = "partial" in cell
    ops = ["search", "match", "fullmatch"] + ([] if partial else ["finditer"])
    subject = "".join(rng.choice("abc") for _ in range(rng.randint(0, 6)))
    return {"cell": "+".join(cell), "pattern": x, "flags": flags, "subject": subject,
            "operation": rng.choice(ops), "partial": partial}


def cells():
    fams = survey.RISKY
    out = [(f,) for f in fams]
    out += list(itertools.combinations(fams, 2)) + list(itertools.combinations(fams, 3))
    out += [("capture", f) for f in fams]
    return out


# Witness rows of existing rulings and register rows, measured by name in Part A section 5.
WITNESSES = [
    ("D51", r"(?(DEFINE)(?<c>a))(?&c)b", "ab", "match", False, 0),
    ("D51", r"(?<c>a)(?&c)b", "aab", "search", False, 0),
    ("D10", r"(?=(?&g))(?<g>a)?b|(?(DEFINE)(?<h>x))", "ab", "search", False, 0),
    ("D10", r"(?!(?&g)c)(?:(?<g>a))?", "ab", "search", False, 0),
    ("D10-plain", r"(?=(a))?b|(?!(a)c)a", "ab", "search", False, 0),
    ("D23", r"(?m)(?<=$(*SKIP)|a*)", "ab", "match", False, 0),
    ("D23", r"(?m)(?<=$(*SKIP)|a*)", "ab", "search", False, 0),
    ("D23", r"(?<=$(*SKIP)|a*)", "ab", "search", False, 0),
    ("D18", r"a\b\B", "a", "search", True, 0),
    ("D19", r"(?:a\B)+", "a", "fullmatch", True, 0),
    ("D39", r"(?:(?=(?:a|ab)$)a){2,}b", "a", "match", True, 0),
    ("D36", r"(x)?(?(?=.*z)a|a)b(?(1)y)", "ac", "match", True, 0),
    ("D28", r"(\A)x", ":", "search", True, 0),
    ("D28", r"\Gx", ":", "search", True, 0),
    ("L47-atomic-verb", r"(?>a(*PRUNE)b)?a", "ac", "search", False, 0),
    ("L47-atomic-verb", r"(?:(?>a(*SKIP)x)|a)c", "ac", "search", False, 0),
    ("L45-skip", r"aa(*SKIP)x(*PRUNE)y|a", "aaxz", "search", False, 0),
    ("L45-skip", r"aa(*SKIP)b(*PRUNE)(*F)|a", "aab", "search", False, 0),
    ("L46-verb-alt", r"(?:a(*PRUNE)x|a)b", "ab", "search", False, 0),
    ("neg-look-verb", r"(?!a(*PRUNE)b)a", "ab", "search", False, 0),
    ("neg-look-verb", r"(?!a(*SKIP)x|a)a", "ab", "search", False, 0),
    ("neg-look-verb", r"a(?!(*FAIL))", "a", "search", False, 0),
    ("look-capture", r"(?=(a))a\1", "aa", "search", False, 0),
    ("look-capture", r"(?!(a)b)a(\1)?", "aa", "search", False, 0),
    ("call-restore", r"(a)(?1)", "aa", "search", False, 0),
    ("call-restore", r"(a|b)(?1)\1", "aba", "search", False, 0),
    ("call-restore", r"(a|b)(?1)\1", "abb", "search", False, 0),
    ("call-restore", r"(?<x>a(?<y>b)?)(?&x)", "aba", "search", False, 0),
    ("call-backref", r"(?<x>(?<y>a|b)\k<y>)(?&x)", "aabb", "search", False, 0),
    ("recursion", r"\((?:[^()]|(?R))*\)", "(a(b))", "search", False, 0),
    ("recursion", r"(?<p>a(?&p)?b)", "aabb", "fullmatch", False, 0),
    ("recursion-left", r"(?P<g1>(?:ab)?(?&g1)?)", "abab", "search", False, 0),
    ("cond-look", r"(?(?=a)ab|b)", "ab", "search", False, 0),
    ("cond-lookbehind", r"a(?(?<=a)b|c)", "ab", "search", False, 0),
    ("cond-group-in-call", r"(?<g>(a)?(?(2)b|c))(?&g)", "abc", "search", False, 0),
    ("partial-look", r"a(?=bc)", "ab", "search", True, 0),
    ("partial-look", r"a(?!bc)", "ab", "search", True, 0),
    ("partial-look", r"(?<=a)b", "a", "search", True, 0),
    ("partial-fullmatch", r"(\S??)\.", ".a", "fullmatch", True, 0),
    ("partial-boundary", r"aa\B", "aa", "match", True, 0),
    ("partial-verb", r"a(*SKIP)bc|a", "ab", "search", True, 0),
    ("partial-verb", r"a(*PRUNE)bc", "ab", "search", True, 0),
    ("partial-call", r"(a(?1)?b)", "aab", "match", True, 0),
    ("partial-cond", r"(a)?(?(1)bc|d)", "ab", "match", True, 0),
    ("reverse-look", r"(?r)(?<=a)b", "ab", "search", False, 0),
    ("reverse-look", r"(?r)a(?=b)", "ab", "search", False, 0),
    ("reverse-capture", r"(?r)(a)+", "aa", "search", False, 0),
    ("reverse-backref", r"(?r)\1(a)", "aa", "search", False, 0),
    ("reverse-cond", r"(?r)(?(1)b|a)(a)?", "aab", "search", False, 0),
    ("finditer-empty", r"a*", "baa", "finditer", False, 0),
    ("finditer-empty", r"a|", "ba", "finditer", False, 0),
    ("finditer-empty", r"(?=a)|a", "aa", "finditer", False, 0),
]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--per-cell", type=int, default=30)
    ap.add_argument("--seed", default="20260930")
    a = ap.parse_args(argv)
    rows = []
    for cell in cells():
        rng = random.Random(f"{a.seed}:{'+'.join(cell)}")
        got, tries = 0, 0
        while got < a.per_cell and tries < a.per_cell * 50:
            tries += 1
            row = make_row(rng, cell)
            try:
                regex.compile(row["pattern"], row["flags"])
            except (regex.error, OverflowError, RecursionError):
                continue
            # Prefer a subject upstream finds something in (70%), so most rows ask about a match
            # rather than about an absence every engine agrees on trivially.
            if rng.random() < 0.7:
                for _ in range(8):
                    s = "".join(rng.choice("abc") for _ in range(rng.randint(1, 6)))
                    try:
                        if regex.search(row["pattern"], s, row["flags"], timeout=0.5):
                            row["subject"] = s
                            break
                    except (TimeoutError, RecursionError, MemoryError):
                        break
            fams = survey.families_of(survey.tags_of(row))
            if not set(cell) <= fams:
                continue
            row["id"] = f"{row['cell']}#{got}"
            rows.append(row)
            got += 1
        if got < a.per_cell:
            print(f"cell {'+'.join(cell)}: only {got} rows", file=sys.stderr)
    # The fuzzy core that TRE can be asked: one whole-pattern budget, a POSIX ERE body, search only.
    rng = random.Random(f"{a.seed}:fuzzy-core")
    core_atoms = ["a", "b", "ab", "ba", "abc", "a+", "b*", "[ab]", "(ab|ba)", "(a|bc)", "cab"]
    for k in range(40):
        body = "".join(rng.choice(core_atoms) for _ in range(rng.randint(1, 3)))
        budget = rng.choice(["e<=1", "i<=1", "d<=1", "s<=1", "e<=2", "i<=1,d<=1", "s<=1,d<=1"])
        subject = "".join(rng.choice("abc") for _ in range(rng.randint(0, 7)))
        rows.append({"id": f"fuzzy-core#{k}", "cell": "fuzzy-core", "pattern": f"(?:{body}){{{budget}}}",
                     "flags": 0, "subject": subject, "operation": "search", "partial": False})
    for k, (tag, p, s, op, partial, flags) in enumerate(WITNESSES):
        rows.append({"id": f"w{k}:{tag}", "cell": "witness", "pattern": p, "flags": flags, "subject": s,
                     "operation": op, "partial": partial})
    a.out.parent.mkdir(parents=True, exist_ok=True)
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
    print(f"{len(rows)} rows in {len(cells())} cells plus {len(WITNESSES)} witnesses -> {a.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
