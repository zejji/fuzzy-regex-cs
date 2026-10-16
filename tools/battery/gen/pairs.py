"""Feature-pair rows for the correctness battery: one cell per pair of constructs that meet.

    python tools/battery/gen/pairs.py --seed 202610161 --per-cell 2500 --out rows.jsonl [--cells a,b]

Cells (plan to 1.0, section 3):
  fuzzy x {backref, lookaround, atomic, verb, conditional, keep, reverse, partial}
  keep x each cut construct {atomic, possessive, prune, skip, lookahead, lookbehind}
  backref x repeat
  partial x {lookaround, atomic, verb}            (partial x fuzzy is fuzzy+partial)
  reverse x each cell above                        (named "rev:<cell>"; fuzzy+reverse is its own)

(*COMMIT) is left out: upstream 2026.9.10 refuses it ("unknown verb at position 2", measured
2026-10-16), so neither engine can answer a row with it.

Each construct is a wrapper round a smaller pattern, so the two constructs of a cell nest (either
way round) or sit in sequence with a shared group between them. Bodies are drawn from short atoms
over a, b, c, x, some captured. A row counts for its cell only when tools/matrix/tagger.py, which
parses with upstream's own parser, finds every construct of the cell in the row (the same rule as
the complete matrix), so a cell's row count is a count of real meetings.

Subjects: about a dozen per pattern over "abcx", up to 7 characters, half drawn from strings
upstream matches (so the rows exercise the constructs, not just fail fast) and half not; partial
cells add prefixes of matching strings, where a partial answer is possible. Upstream is asked with
a 50 ms timeout and a pattern that times out is dropped.

Rows are in the matrix harness's shape (tools/matrix/run.py): pattern, flags (always 0: the flags
are inline, because tools/battery/probe.cs ignores the field), namedLists, subject, operation,
partial, cell, tags, id. Deterministic for a given seed and arguments.
"""

from __future__ import annotations

import argparse
import json
import random
import sys
from pathlib import Path

import regex

HERE = Path(__file__).resolve().parent
MATRIX = HERE.parent.parent / "matrix"
sys.path.insert(0, str(MATRIX))
import tagger  # noqa: E402

ATOMS = ["a", "b", "c", "x", "a*", "b?", "a+", "[ab]", ".", "(?:a|b)", "(?:ab|a)", "a*?", "(?:a|)", "a{1,2}",
         "ab", "ba", "b*", "[^c]"]
FUZZY = ["e<=1", "e<=1", "i<=1", "d<=1", "s<=1", "e<=2", "i<=1,d<=1", "s<=1,i<=1"]
REPEAT_Q = ["*", "+", "?", "*?", "+?", "??", "{1,3}", "{0,2}", "{2,}"]

# Which tagger ids prove a construct is present. A tuple means any one of them.
LOOK = ("lookahead", "neg-lookahead", "lookbehind", "neg-lookbehind")
NEEDS = {
    "fuzzy": ("fuzzy",), "backref": ("backref",), "lookaround": LOOK, "atomic": ("atomic",),
    "possessive": ("possessive",), "verb": ("verb",), "prune": ("verb",), "skip": ("verb",),
    "conditional": ("conditional",), "keep": ("keep",), "repeat": ("repeat", "lazy"),
    "lookahead": ("lookahead", "neg-lookahead"), "lookbehind": ("lookbehind", "neg-lookbehind"),
    "reverse": ("flag-r",), "partial": ("partial",),
}
# Text that must also be present, where the tagger's id is coarser than the cell.
NEEDS_TEXT = {"prune": "(*PRUNE)", "skip": "(*SKIP)"}

BASE_CELLS = (
    [("fuzzy", c) for c in ("backref", "lookaround", "atomic", "verb", "conditional", "keep", "reverse", "partial")]
    + [("keep", c) for c in ("atomic", "possessive", "prune", "skip", "lookahead", "lookbehind")]
    + [("backref", "repeat")]
    + [("partial", c) for c in ("lookaround", "atomic", "verb")]
)


def all_cells() -> list[tuple[str, tuple[str, ...]]]:
    """(name, constructs). Row-level constructs (reverse, partial) are applied to the row, not wrapped."""
    out = [("+".join(c), c) for c in BASE_CELLS]
    out += [("rev:" + "+".join(c), c + ("reverse",)) for c in BASE_CELLS if "reverse" not in c]
    return out


class Builder:
    def __init__(self, r: random.Random):
        self.r = r
        self.groups = 0

    def pick(self, xs):
        return self.r.choice(xs)

    def new_group(self) -> str:
        self.groups += 1
        return f"g{self.groups}"

    def atom(self) -> str:
        a = self.pick(ATOMS)
        if self.r.random() < 0.25:
            self.groups += 1  # an unnamed capture still takes a number; named ones count too
            return "(" + a + ")"
        return a

    def body(self, lo=1, hi=2) -> str:
        return "".join(self.atom() for _ in range(self.r.randint(lo, hi)))

    def maybe(self, p=0.5) -> str:
        return self.body(1, 1) if self.r.random() < p else ""

    # Each wrapper takes the inner pattern and returns a pattern holding the construct round it.
    def wrap(self, construct: str, inner: str) -> str:
        r = self.r
        if construct == "fuzzy":
            return f"(?:{inner}){{{self.pick(FUZZY)}}}"
        if construct == "backref":
            g = self.new_group()
            return self.pick([
                f"(?P<{g}>{inner}){self.maybe()}(?P={g})",
                f"(?P<{g}>{self.body(1, 1)}){inner}(?P={g})",
                f"(?:(?P<{g}>{inner})|{self.body(1, 1)}){self.maybe()}(?P={g})",
                f"(?P<{g}>{self.body(1, 1)})(?:{inner}(?P={g}))" + self.pick(REPEAT_Q),
            ])
        if construct in ("lookaround", "lookahead", "lookbehind"):
            kinds = {"lookaround": ["(?=", "(?!", "(?<=", "(?<!"], "lookahead": ["(?=", "(?!"],
                     "lookbehind": ["(?<=", "(?<!"]}[construct]
            k = self.pick(kinds)
            look = f"{k}{inner})"
            return self.pick([f"{self.maybe()}{look}{self.body(1, 2)}", f"{self.body(1, 2)}{look}{self.maybe()}",
                              f"(?:{look}{self.body(1, 1)})" + self.pick(REPEAT_Q)])
        if construct == "atomic":
            return self.pick([f"(?>{inner}){self.body(1, 2)}", f"{self.maybe()}(?>{inner}|{self.body(1, 1)}){self.maybe()}"])
        if construct == "possessive":
            q = self.pick(["*+", "++", "?+", "{1,2}+", "{0,3}+"])
            return f"{self.maybe()}(?:{inner}){q}{self.body(1, 2)}"
        if construct in ("verb", "prune", "skip"):
            verbs = {"verb": ["(*PRUNE)", "(*SKIP)", "(*F)", "(*SKIP)(*F)", "(*PRUNE)(*F)"],
                     "prune": ["(*PRUNE)"], "skip": ["(*SKIP)"]}[construct]
            v = self.pick(verbs)
            return self.pick([f"{inner}{v}{self.body(1, 2)}", f"(?:{inner}{v}{self.maybe()}|{self.body(1, 2)})",
                              f"{self.body(1, 1)}{v}{inner}", f"(?:{self.body(1, 1)}{v}|{inner}){self.maybe()}"])
        if construct == "conditional":
            g = self.new_group()
            return self.pick([
                f"(?P<{g}>{self.body(1, 1)})?(?({g}){inner}|{self.body(1, 1)})",
                f"(?P<{g}>{self.body(1, 1)})?(?({g}){self.body(1, 1)}|{inner})",
                f"(?(?={self.body(1, 1)}){inner}|{self.body(1, 1)})",
                f"(?(?!{self.body(1, 1)}){inner}|{self.body(1, 1)})",
                f"(?:(?P<{g}>{inner})|{self.body(1, 1)})(?({g}){self.body(1, 1)}|{self.body(1, 1)})",
            ])
        if construct == "keep":
            return self.pick([f"{self.body(1, 1)}\\K{inner}", f"{inner}\\K{self.body(1, 1)}",
                              f"(?:{self.body(1, 1)}\\K)?{inner}", f"(?:{inner}\\K|{self.body(1, 1)})"])
        if construct == "repeat":
            return f"(?:{inner})" + self.pick(REPEAT_Q) + self.maybe(0.6)
        raise ValueError(construct)

    def pattern(self, constructs: tuple[str, ...]) -> str:
        wrapped = [c for c in constructs if c not in ("reverse", "partial")]
        if len(wrapped) == 1:
            # fuzzy+reverse, fuzzy+partial, partial+X: one wrapper, sometimes twice, and a second
            # construct-free wrapper so it is not always the whole pattern.
            p = self.wrap(wrapped[0], self.body())
            if self.r.random() < 0.3:
                p = self.wrap(wrapped[0], p)
        elif self.r.random() < 0.7:
            first, second = wrapped if self.r.random() < 0.5 else wrapped[::-1]
            p = self.wrap(second, self.wrap(first, self.body()))
        else:
            # Side by side: the second construct follows the first and the bodies between them may
            # read the first one's groups.
            first, second = wrapped if self.r.random() < 0.5 else wrapped[::-1]
            p = self.wrap(first, self.body()) + self.maybe() + self.wrap(second, self.body())
        p = self.maybe(0.3) + p + self.maybe(0.3)
        if "reverse" in constructs:
            p = "(?r)" + p
        return p


def subjects(r: random.Random, compiled, partial: bool) -> list[str] | None:
    """About a dozen subjects, half matching; None if upstream times out on any of them."""
    hits, misses = [], []
    for _ in range(60):
        s = "".join(r.choice("aabbcx") for _ in range(r.randint(0, 7)))
        if s in hits or s in misses:
            continue
        try:
            m = compiled.search(s, timeout=0.05)
        except TimeoutError:
            return None
        (hits if m else misses).append(s)
        if len(hits) >= 7 and len(misses) >= 5:
            break
    out = hits[:7] + misses[:5]
    if partial:
        for s in hits[:4]:
            if len(s) > 1:
                out.append(s[: r.randint(1, len(s) - 1)])
    return list(dict.fromkeys(out))


def present(constructs: tuple[str, ...], tags: set[str], pattern: str) -> bool:
    for c in constructs:
        if not any(t in tags for t in NEEDS[c]):
            return False
        if c in NEEDS_TEXT and NEEDS_TEXT[c] not in pattern:
            return False
    return True


def generate(seed: int, per_cell: int, only: set[str] | None):
    rows = []
    for name, constructs in all_cells():
        if only and name not in only:
            continue
        # One generator per cell, seeded from the run's seed and the cell's name, so adding or
        # dropping a cell does not change the others' rows.
        r = random.Random(f"{seed}:{name}")
        made, tries, patterns = 0, 0, set()
        partial = "partial" in constructs
        while made < per_cell and tries < per_cell * 40:
            tries += 1
            b = Builder(r)
            p = b.pattern(constructs)
            if p in patterns:
                continue
            try:
                compiled = regex.compile(p)
            except (regex.error, OverflowError, RecursionError):
                continue
            subs = subjects(r, compiled, partial)
            if not subs:
                continue
            probe_row = {"pattern": p, "flags": 0, "namedLists": {}, "subject": subs[0],
                         "operation": "search", "partial": partial}
            try:
                tags = tagger.confirmed(probe_row)
            except Exception:  # noqa: BLE001 - the tagger refuses what its parser cannot read
                continue
            if not present(constructs, tags, p):
                continue
            patterns.add(p)
            for s in subs:
                op = r.choice(["search", "search", "match", "fullmatch", "finditer"])
                row = {"pattern": p, "flags": 0, "namedLists": {}, "subject": s, "operation": op}
                if partial:
                    row["partial"] = True
                row_tags = (tags - {t for t in tags if t.startswith("op-")}) | {tagger.OPERATION_IDS[op]}
                row.update({"cell": name, "tags": sorted(row_tags), "depth": 2})
                rows.append(row)
                made += 1
                if made >= per_cell:
                    break
        yield name, made, tries, rows
        rows = []


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--seed", type=int, required=True)
    ap.add_argument("--per-cell", type=int, default=2500)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--cells", default="", help="comma-separated cell names (default: all)")
    ap.add_argument("--counts", type=Path, default=None)
    args = ap.parse_args(argv)
    only = set(args.cells.split(",")) if args.cells else None
    n = 0
    counts = []
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        for name, made, tries, rows in generate(args.seed, args.per_cell, only):
            for row in rows:
                row["id"] = n
                n += 1
                f.write(json.dumps(row, ensure_ascii=True) + "\n")
            counts.append((name, made, tries))
            print(f"{name}: {made} rows ({tries} pattern attempts)", file=sys.stderr, flush=True)
    short = [c for c in counts if c[1] < args.per_cell]
    if args.counts:
        args.counts.write_text("".join(f"{a},{b},{c}\n" for a, b, c in counts), encoding="utf-8")
    if short:
        print("cells below --per-cell: " + ", ".join(f"{a} {b}" for a, b, _ in short), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
