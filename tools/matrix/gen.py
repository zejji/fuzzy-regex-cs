"""The complete matrix's row generator: rows built BY CELL, each confirmed by upstream's parser.

A cell is a pair of construct ids (tools/matrix/constructs.json), or a triple of the risky families
(call, fuzzy, lookaround, conditional, verb, partial, reverse). For each cell this builds patterns
in which its constructs really meet: each construct is a WRAPPER round a smaller pattern, so they
nest (a fuzzy section round a call round a lookaround) or sit in sequence reading each other (a
lookaround whose body refers back to a capture, a call placed outside the fuzzy section its group
sits in). Nesting depth is 1-3; subjects are up to 8 characters, drawn from a string the pattern
can match and then edited, cut short or padded past the pattern's width; every operation
(search, match, fullmatch, finditer, partial, pos/endpos) is drawn.

A row counts for a cell only when `tagger.confirmed` finds every construct of the cell in the
row's parse tree, compiled flags and operation. The run fails (exit 1) if any feasible cell has
fewer than --min rows, and lists every infeasible cell with the reason the engine gave.

    python tools/matrix/gen.py --out rows.jsonl [--seed 20260930] [--min 50]
    python tools/matrix/gen.py --out pilot.jsonl --sample 2000     # stratified over every cell
    python tools/matrix/gen.py --out rows.jsonl --extra-triples "call,fuzzy,backref;verb,fuzzy,keep"

Deterministic: the same seed and arguments give byte-identical rows.

Each row carries, besides the oracle's inputs (pattern, flags, namedLists, subject, operation,
partial, pos, endpos): `cell` (what it was built for), `tags` (confirmed ids), `depth`, and
`writtenOut` - the same pattern with every call written out as the body it calls (captures made
non-capturing), for check C2 - with `recursive` saying whether that needed a depth bound.
"""

from __future__ import annotations

import argparse
import itertools
import json
import random
import subprocess
import sys
from pathlib import Path

import regex

sys.path.insert(0, str(Path(__file__).resolve().parent))
import tagger  # noqa: E402

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
ROW_ATTEMPTS_PER_CELL = 40  # attempts per row still needed before a cell is called infeasible

# ----------------------------------------------------------------------------------------------
# Construct ids


def load_constructs(path: Path | None) -> dict:
    """constructs.json from the working tree, else from the answer-key branch (its owner)."""
    if path and path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    local = HERE / "constructs.json"
    if local.exists():
        return json.loads(local.read_text(encoding="utf-8"))
    text = subprocess.run(["git", "show", "matrix/answer-key:tools/matrix/constructs.json"], cwd=REPO,
                          capture_output=True, text=True, encoding="utf-8", check=True).stdout
    return json.loads(text)


# ----------------------------------------------------------------------------------------------
# Pattern trees. A node is a tuple; render() turns it into text, sample() into a string it can
# match (ignoring what lookarounds and verbs demand).

ATOMS = (("a", "a"), ("b", "b"), ("ab", "ab"), ("ba", "ba"), (".", None), ("[ab]", None), ("a+", "aa"),
         ("b?", None), (r"\w", "a"), ("x", "x"), ("a*", "a"), ("(?:ab|a)", "ab"))
FUZZY = ("e<=1", "i<=1", "d<=1", "s<=1", "e<=2", "s<=1,i<=1", "i<=1,d<=1")
FUZZY_MIN = ("1<=e<=2", "1<=e<=1", "1<=d<=2", "1<=s<=1,i<=1", "1<=i<=2")
FUZZY_COST = ("1i+1d<=2", "2i+2d+1s<=4", "1s+2i<=2", "e<=2,1i+2d<=2", "2s+1d<=2")
QUANTS = ("*", "+", "?", "{1,2}", "{0,2}", "{2}", "{1,3}")
LOOKS = {"lookahead": "(?=", "neg-lookahead": "(?!", "lookbehind": "(?<=", "neg-lookbehind": "(?<!"}
SETS = ("[ab]", "[^a]", "[a-c]", "[bx]", "[^ab]")
NAMED_LIST = {"w": ["ab", "b", "ba"]}


class Tree:
    """Per-pattern state: group names in order, calls, what the wrappers hoisted."""

    def __init__(self, rng: random.Random):
        self.rng = rng
        self.groups: list[str] = []
        self.bodies: dict[str, tuple] = {}
        self.pre: list[tuple] = []
        self.post: list[tuple] = []
        self.whole_recursion = False
        self.named_lists = False

    def group(self, body) -> tuple:
        name = f"g{len(self.groups) + 1}"
        self.groups.append(name)
        node = ("grp", name, body)
        self.bodies[name] = body
        return node

    def atom(self) -> tuple:
        text, sample = self.rng.choice(ATOMS)
        return ("lit", text, sample)

    def atoms(self, n=None) -> tuple:
        n = n or self.rng.randint(1, 2)
        return ("seq", [self.atom() for _ in range(n)])


def _nc(node):
    return node if node[0] in ("nc", "grp", "atomic", "look", "alt") else ("nc", node)


def wrap(cid: str, inner: tuple, t: Tree) -> tuple:
    """`inner` with construct `cid` round it or beside it, so the two interact."""
    r = t.rng
    if cid == "fuzzy":
        return ("fz", inner, r.choice(FUZZY))
    if cid == "fuzzy-min":
        return ("fz", inner, r.choice(FUZZY_MIN))
    if cid == "fuzzy-cost":
        return ("fz", inner, r.choice(FUZZY_COST))
    if cid == "call":
        shape = r.choice(("after", "before", "hoist", "define", "recurse", "recurse", "whole"))
        if shape == "define":
            g = t.group(inner)
            t.pre.append(("define", g))
            return ("call", g[1])
        if shape == "recurse":
            name = f"g{len(t.groups) + 1}"
            call = ("call", name)
            body = r.choice((
                ("alt", [("seq", [t.atom(), call, t.atom()]), inner]),
                ("seq", [inner, ("rep", call, "?")]),
                ("alt", [inner, ("seq", [t.atom(), call])]),
            ))
            t.groups.append(name)
            t.bodies[name] = body
            return ("grp", name, body)
        if shape == "whole":
            t.whole_recursion = True
            return r.choice((("seq", [inner, ("rep", ("call", "R"), "?")]),
                             ("alt", [("seq", [t.atom(), ("call", "R")]), inner])))
        g = t.group(inner)
        if shape == "after":
            return ("seq", [g, ("call", g[1])])
        if shape == "before":
            return ("seq", [("call", g[1]), g])
        (t.post if r.random() < 0.7 else t.pre).append(("call", g[1]))
        return g
    if cid in LOOKS:
        op = LOOKS[cid]
        behind = "<" in op
        shape = r.choice(("body", "body", "beside", "reads-capture"))
        if shape == "body":
            look = ("look", op, inner)
            return ("seq", [t.atom(), look]) if behind else ("seq", [look, t.atoms()])
        if shape == "beside":
            look = ("look", op, t.atom())
            return ("seq", [look, inner]) if behind else ("seq", [inner, look])
        g = t.group(inner)
        look = ("look", op, ("seq", [("lit", ".?", None), ("bref", g[1])]) if not behind else ("bref", g[1]))
        return ("seq", [g, look, t.atom()]) if not behind else ("seq", [g, look])
    if cid == "atomic":
        return ("seq", [("atomic", inner), t.atom()]) if r.random() < 0.7 else ("atomic", inner)
    if cid == "possessive":
        return ("rep", _nc(inner), r.choice(("*", "+", "?", "{1,2}")) + "+")
    if cid == "lazy":
        return ("seq", [("rep", _nc(inner), r.choice(QUANTS[:5]) + "?"), t.atom()])
    if cid == "repeat":
        return ("rep", _nc(inner), r.choice(QUANTS))
    if cid == "verb":
        verb = ("lit", r.choice(("(*PRUNE)", "(*SKIP)", "(*SKIP)", "(*PRUNE)", "(*FAIL)", "(*F)")), "")
        shape = r.choice(("mid", "skipfail", "tail", "failalt"))
        if verb[1] in ("(*FAIL)", "(*F)"):
            shape = "failalt"
        if shape == "mid":
            return ("seq", [inner, verb, t.atom()])
        if shape == "skipfail":
            return ("alt", [("seq", [inner, verb, ("lit", "(*F)", "")]), t.atoms()])
        if shape == "tail":
            return ("seq", [inner, verb])
        return ("alt", [("seq", [t.atom(), verb]), inner])
    if cid == "conditional":
        shape = r.choice(("group-yes", "group-test", "look-test", "look-yes"))
        if shape == "group-yes":
            g = t.group(t.atom())
            return ("seq", [("rep", g, "?"), ("cond", ("grp", g[1]), inner, t.atom())])
        if shape == "group-test":
            g = t.group(inner)
            return ("seq", [("rep", g, "?"), ("cond", ("grp", g[1]), t.atom(), t.atom())])
        op = r.choice(tuple(LOOKS.values()))
        if shape == "look-test":
            return ("cond", ("look", op, inner), t.atoms(), t.atom())
        return ("cond", ("look", op, t.atom()), inner, t.atom())
    if cid == "capture":
        g = t.group(inner)
        return ("rep", g, r.choice(("+", "{1,2}", "*"))) if r.random() < 0.4 else g
    if cid == "branch-reset":
        if r.random() < 0.5:
            return ("breset", [("ugrp", inner), ("ugrp", t.atom())])
        return ("breset", [("seq", [("ugrp", t.atom()), inner]), ("ugrp", t.atom())])
    if cid == "alternation":
        return ("alt", [inner, t.atom()] if r.random() < 0.5 else [t.atom(), inner])
    if cid == "backref":
        if r.random() < 0.5:
            g = t.group(inner)
            return ("seq", [g, ("lit", ".?", None), ("bref", g[1])])
        g = t.group(t.atom())
        return ("seq", [g, inner, ("bref", g[1])])
    if cid == "search-anchor":
        return r.choice((("seq", [("lit", r"\G", ""), inner]), ("alt", [("seq", [("lit", r"\G", ""), inner]), t.atom()]),
                         ("seq", [inner, ("lit", r"\G", "")])))
    if cid == "keep":
        return ("seq", [t.atom(), ("lit", r"\K", ""), inner]) if r.random() < 0.5 else ("seq", [inner, ("lit", r"\K", ""), t.atom()])
    if cid == "anchor":
        a = r.choice(("^", "$", r"\A", r"\Z"))
        lit = ("lit", a, "")
        return ("seq", [lit, inner]) if a in ("^", r"\A") else ("seq", [inner, lit])
    if cid == "word-boundary":
        b = r.choice((r"\b", r"\B", r"\m", r"\M"))
        return ("seq", [("lit", b, ""), inner]) if r.random() < 0.5 else ("seq", [inner, ("lit", b, "")])
    if cid == "set":
        s = ("lit", r.choice(SETS), None)
        return ("seq", [s, inner]) if r.random() < 0.5 else ("seq", [inner, s])
    if cid == "named-list":
        t.named_lists = True
        nl = ("lit", r"\L<w>", None)
        return r.choice((("seq", [nl, inner]), ("seq", [inner, nl]), ("alt", [nl, inner])))
    raise KeyError(cid)


PATTERN_WRAPPERS = ("fuzzy", "fuzzy-min", "fuzzy-cost", "call", "lookahead", "neg-lookahead", "lookbehind",
                    "neg-lookbehind", "atomic", "possessive", "lazy", "repeat", "verb", "conditional", "capture",
                    "branch-reset", "alternation", "backref", "search-anchor", "keep", "anchor", "word-boundary",
                    "set", "named-list")
FLAG_BITS = {k: v for k, v in tagger.FLAG_IDS.items()}


class Recursion(Exception):
    pass


def render(node, t: Tree, inline=None, depth=0, capture=True, number=None) -> str:
    """The pattern text. With `inline` (a depth bound), every call is written out as its body."""
    k = node[0]
    rr = lambda n, cap=capture, d=depth: render(n, t, inline, d, cap, number)
    if k == "lit":
        return node[1]
    if k == "seq":
        return "".join(rr(x) for x in node[1])
    if k == "alt":
        return "(?:" + "|".join(rr(x) for x in node[1]) + ")"
    if k == "grp":
        body = rr(node[2])
        return f"(?P<{node[1]}>{body})" if capture else f"(?:{body})"
    if k == "ugrp":
        return f"({rr(node[1])})" if capture else f"(?:{rr(node[1])})"
    if k == "nc":
        return "(?:" + rr(node[1]) + ")"
    if k == "rep":
        body = rr(node[1])
        if node[1][0] not in ("grp", "ugrp", "nc", "alt", "atomic", "look", "call") and not (
                node[1][0] == "lit" and len(node[1][1]) == 1):
            body = "(?:" + body + ")"
        return body + node[2]
    if k == "fz":
        return "(?:" + rr(node[1]) + "){" + node[2] + "}"
    if k == "look":
        return node[1] + rr(node[2]) + ")"
    if k == "atomic":
        return "(?>" + rr(node[1]) + ")"
    if k == "cond":
        test = node[1]
        head = f"(?({test[1]})" if test[0] == "grp" else "(?" + test[1] + rr(test[2]) + ")"
        return head + rr(node[2]) + "|" + rr(node[3]) + ")"
    if k == "breset":
        if not capture:
            return "(?:" + "|".join(rr(x) for x in node[1]) + ")"
        return "(?|" + "|".join(rr(x) for x in node[1]) + ")"
    if k == "bref":
        style = hash_choice(node[1], 3)
        n = number[node[1]] if number else None
        return (f"(?P={node[1]})", f"\\g<{node[1]}>", f"\\g<{n}>" if n else f"(?P={node[1]})")[style]
    if k == "define":
        return "(?(DEFINE)" + rr(node[1]) + ")"
    if k == "call":
        target = node[1]
        if inline is None:
            if target == "R":
                return "(?R)" if hash_choice("R", 2) == 0 else "(?0)"
            n = number[target] if number else None
            return (f"(?&{target})", f"(?P>{target})", f"(?{n})" if n else f"(?&{target})")[hash_choice(target, 3)]
        if depth >= inline:
            return "(?!)"
        # SHORTCUT: the copy takes the call site's flags, which is right only because every flag
        # here is a whole-pattern flag (the generator writes no scoped `(?i:...)`). A called group
        # keeps its DEFINITION's flags (`(abc)(?i:(?-1))` over 'abcABC' is None in upstream, PCRE2
        # and Perl), so once scoped flags are generated the copy must be wrapped in the definition's
        # flags, e.g. `(?-i:abc)` (matrix triage 2026-09-30; 0 of the 1,509 C2 rows are affected).
        body = t.root if target == "R" else t.bodies[target]
        return "(?:" + render(body, t, inline, depth + 1, False, number) + ")"
    raise KeyError(k)


def hash_choice(key: str, n: int) -> int:
    return sum(map(ord, key)) % n


def sample(node, t: Tree, r: random.Random, depth=0) -> str:
    k = node[0]
    s = lambda n: sample(n, t, r, depth)
    if k == "lit":
        if node[2] is not None:
            return node[2]
        text = node[1]
        if text == r"\L<w>":
            return r.choice(NAMED_LIST["w"])
        if text in (".?", "b?"):
            return r.choice(("", "a", "b")) if text == ".?" else r.choice(("", "b"))
        return r.choice("ab") if text in (".", "[ab]", r"\w") else r.choice("abx")
    if k == "seq":
        return "".join(s(x) for x in node[1])
    if k in ("alt", "breset"):
        return s(r.choice(node[1]))
    if k in ("grp",):
        return s(node[2])
    if k in ("ugrp", "nc", "atomic", "fz"):
        return s(node[1])
    if k == "rep":
        q = node[2].rstrip("+?") or node[2]
        lo, hi = {"*": (0, 2), "+": (1, 2), "?": (0, 1)}.get(q[0], (1, 2))
        if q.startswith("{"):
            parts = q[1:-1].split(",")
            lo = int(parts[0]); hi = int(parts[-1]) if parts[-1] else lo + 1
        return "".join(s(node[1]) for _ in range(r.randint(lo, min(hi, 3))))
    if k == "look":
        return ""
    if k == "cond":
        return s(r.choice((node[2], node[3])))
    if k == "bref":
        return s(t.bodies.get(node[1], ("lit", "a", "a")))
    if k == "define":
        return ""
    if k == "call":
        if depth > 2:
            return ""
        body = t.root if node[1] == "R" else t.bodies[node[1]]
        return sample(body, t, r, depth + 1)
    return ""


def group_numbers(text: str) -> dict:
    try:
        return dict(regex.compile(text).groupindex)
    except regex.error:
        return {}


def calls_recurse(t: Tree) -> bool:
    if t.whole_recursion:
        return True
    graph = {g: set(_calls_in(b)) for g, b in t.bodies.items()}

    def reach(start):
        seen, stack = set(), list(graph.get(start, ()))
        while stack:
            g = stack.pop()
            if g == start:
                return True
            if g in seen or g == "R":
                continue
            seen.add(g)
            stack.extend(graph.get(g, ()))
        return False

    return any(reach(g) for g in graph) or any("R" in v for v in graph.values())


def _calls_in(node):
    if not isinstance(node, tuple):
        return
    if node[0] == "call":
        yield node[1]
    for x in node[1:]:
        if isinstance(x, tuple):
            yield from _calls_in(x)
        elif isinstance(x, list):
            for y in x:
                yield from _calls_in(y)


def _has(node, kinds) -> bool:
    if not isinstance(node, tuple):
        return False
    if node[0] in kinds or (node[0] == "cond" and node[1][0] == "grp" and "cond-grp" in kinds):
        return True
    return any(_has(x, kinds) if isinstance(x, tuple) else any(_has(y, kinds) for y in x)
               for x in node[1:] if isinstance(x, (tuple, list)))


# ----------------------------------------------------------------------------------------------
# Rows


OPS = ("search", "match", "fullmatch", "finditer")
OP_WEIGHTS = (40, 20, 20, 20)


def edit(s: str, r: random.Random, n: int) -> str:
    for _ in range(n):
        choice = r.randrange(3)
        i = r.randint(0, len(s))
        if choice == 0 or not s:
            s = s[:i] + r.choice("abx") + s[i:]
        elif choice == 1:
            s = s[:max(i - 1, 0)] + s[i:] if i else s[1:]
        else:
            i = min(i, len(s) - 1)
            s = s[:i] + r.choice("abx") + s[i + 1:]
    return s


def make_row(cell: tuple[str, ...], r: random.Random, families: dict) -> dict | None:
    t = Tree(r)
    flags = 0
    op = None
    partial = slice_ = False
    wrappers = []
    for cid in cell:
        if cid in FLAG_BITS:
            flags |= FLAG_BITS[cid]
            if cid == "flag-f":
                flags |= regex.IGNORECASE
        elif cid.startswith("op-"):
            op = cid[3:]
        elif cid == "partial":
            partial = True
        elif cid == "slice":
            slice_ = True
        else:
            wrappers.append(cid)
    # A fuzzy mode flag with nothing fuzzy asks nothing of it.
    if flags & (regex.BESTMATCH | regex.ENHANCEMATCH) and not any(w.startswith("fuzzy") for w in wrappers) and r.random() < 0.8:
        wrappers.append("fuzzy")
    # Depth 1-3: pad a short list with one more wrapper now and then.
    if len(wrappers) < 3 and r.random() < 0.35:
        wrappers.append(r.choice(PATTERN_WRAPPERS[:18]))
    if not wrappers:
        wrappers.append(r.choice(("capture", "repeat", "alternation", "fuzzy", "set")))
    r.shuffle(wrappers)
    # Nested (each wraps the last), or two chains in sequence.
    base = t.atoms()
    if len(wrappers) > 1 and r.random() < 0.3:
        cut = r.randint(1, len(wrappers) - 1)
        a, b = base, t.atoms()
        for w in wrappers[:cut]:
            a = wrap(w, a, t)
        for w in wrappers[cut:]:
            b = wrap(w, b, t)
        node = ("seq", [a, b])
        depth = max(cut, len(wrappers) - cut)
    else:
        node = base
        for w in wrappers:
            node = wrap(w, node, t)
        depth = len(wrappers)
    root = ("seq", t.pre + [node] + t.post)
    t.root = root
    text = render(root, t)
    numbers = group_numbers(text) if t.groups else {}
    if numbers:
        text = render(root, t, number=numbers)
    for extra in ("flag-i", "flag-r", "flag-V1", "flag-w"):
        if not flags & FLAG_BITS[extra] and r.random() < 0.04:
            flags |= FLAG_BITS[extra]
    named = dict(NAMED_LIST) if t.named_lists else {}
    try:
        compiled = regex.compile(text, flags, **named)
    except (regex.error, ValueError, OverflowError, RecursionError) as e:
        return {"error": str(e).split(" at position")[0]}

    # The subject: a string the pattern can match, then edited (for fuzzy), cut (for partial) or
    # padded past the pattern's width.
    s = sample(root, t, r)
    if r.random() < 0.5:
        s = edit(s, r, r.randint(1, 2))
    if partial and s and r.random() < 0.7:
        s = s[: r.randint(0, max(len(s) - 1, 0))]
    if r.random() < 0.3:
        s = "".join(r.choice("abx ") for _ in range(r.randint(1, 3))) + s
    if r.random() < 0.2:
        s = s + "".join(r.choice("abx") for _ in range(r.randint(1, 4)))
    s = s[:12] if r.random() < 0.2 else s[:8]
    if op is None:
        op = r.choices(OPS, OP_WEIGHTS)[0]
    if not partial and r.random() < 0.1:
        partial = True
    row = {"pattern": text, "flags": flags, "namedLists": named, "subject": s, "operation": op}
    if partial:
        row["partial"] = True
    if slice_ or r.random() < 0.08:
        pos = r.randint(0, len(s))
        endpos = r.randint(pos, len(s))
        row["pos"], row["endpos"] = pos, endpos
    row["depth"] = depth
    # C2's written-out forms. A call body that reads a group (a backreference or a group test) reads
    # the CALLED instance's captures, which a non-capturing copy does not have, so none is made.
    if t.groups and any(True for _ in _calls_in(root)) or t.whole_recursion:
        bodies_read = any(_has(b, ("bref", "cond-grp")) for b in t.bodies.values()) or (
            t.whole_recursion and _has(root, ("bref", "cond-grp")))
        recursive = calls_recurse(t)
        row["recursive"] = recursive
        if bodies_read:
            row["writtenOut"] = None
        else:
            try:
                depths = (2, 3, 4, 5, 6) if recursive else (8,)
                row["writtenOut"] = {str(d): render(root, t, inline=d, number=numbers) for d in depths}
                if not recursive and "(?!)" in row["writtenOut"]["8"]:
                    row["writtenOut"] = None
            except (KeyError, RecursionError):
                row["writtenOut"] = None
    return row


def cells_of(constructs: dict, extra_triples=()) -> list[tuple]:
    ids = [c["id"] for c in constructs["constructs"]]
    impossible = {frozenset(x["pair"]) for x in constructs["impossible"]}
    pairs = [p for p in itertools.combinations(ids, 2) if frozenset(p) not in impossible]
    risky = constructs["risky_families"]
    triples = [("family",) + t for t in itertools.combinations(risky, 3)]
    triples += [("ids",) + tuple(x) for x in extra_triples]
    return pairs, triples


def family_members(constructs: dict) -> dict:
    fam = {}
    for c in constructs["constructs"]:
        fam.setdefault(c["family"], []).append(c["id"])
    return fam


def covers(tags: set, cell: tuple, fam: dict) -> bool:
    if cell[0] == "family":
        return all(any(i in tags for i in fam[f]) for f in cell[1:])
    if cell[0] == "ids":
        return all(i in tags for i in cell[1:])
    return all(i in tags for i in cell)


def cell_name(cell: tuple) -> str:
    return "+".join(cell[1:]) if cell[0] in ("family", "ids") else "+".join(cell)


def target_ids(cell: tuple, fam: dict, r: random.Random) -> tuple:
    if cell[0] == "family":
        # One id of each family; a non-pattern family has one member (partial, reverse).
        return tuple(r.choice(fam[f]) for f in cell[1:])
    if cell[0] == "ids":
        return cell[1:]
    return cell


def generate(constructs: dict, seed: int, minimum: int, extra_triples=()):
    pairs, triples = cells_of(constructs, extra_triples)
    cells = pairs + triples
    fam = family_members(constructs)
    counts = {c: 0 for c in cells}
    errors: dict = {}
    rows = []
    r = random.Random(seed)
    for cell in cells:
        attempts = 0
        while counts[cell] < minimum and attempts < ROW_ATTEMPTS_PER_CELL * minimum:
            attempts += 1
            ids = target_ids(cell, fam, r)
            row = make_row(ids, r, fam)
            if row is None:
                continue
            if "error" in row:
                errors.setdefault(cell, {}).setdefault(row["error"], 0)
                errors[cell][row["error"]] += 1
                continue
            try:
                tags = tagger.confirmed(row)
            except Exception as e:  # noqa: BLE001
                errors.setdefault(cell, {}).setdefault("tagger: " + str(e), 0)
                errors[cell]["tagger: " + str(e)] += 1
                continue
            if not covers(tags, cell, fam):
                missing = [i for i in ids if i not in tags]
                key = "not confirmed: " + ",".join(missing)
                errors.setdefault(cell, {}).setdefault(key, 0)
                errors[cell][key] += 1
                continue
            row["cell"] = cell_name(cell)
            row["tags"] = sorted(tags)
            row["id"] = len(rows)
            rows.append(row)
            for c in cells:
                if covers(tags, c, fam):
                    counts[c] += 1
    return rows, cells, counts, errors, fam


def stratified_sample(rows, cells, fam, n, seed):
    """n rows spread over every cell: each cell's rows are picked in turn, fewest-covered first."""
    r = random.Random(seed)
    by_cell = {c: [i for i, row in enumerate(rows) if covers(set(row["tags"]), c, fam)] for c in cells}
    chosen, have = [], {c: 0 for c in cells}
    picked = set()
    while len(chosen) < n:
        progress = False
        for c in sorted(cells, key=lambda c: (have[c], cell_name(c))):
            if len(chosen) >= n:
                break
            options = [i for i in by_cell[c] if i not in picked]
            if not options:
                continue
            i = r.choice(options)
            picked.add(i)
            chosen.append(i)
            progress = True
            for c2 in cells:
                if covers(set(rows[i]["tags"]), c2, fam):
                    have[c2] += 1
            break
        if not progress:
            break
    return [rows[i] for i in sorted(chosen)], have


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--seed", type=int, default=20260930)
    ap.add_argument("--min", type=int, default=50)
    ap.add_argument("--sample", type=int, default=0, help="write a stratified sample of this many rows")
    ap.add_argument("--constructs", type=Path, default=None)
    ap.add_argument("--extra-triples", default="", help="id triples, ';'-separated, each ','-separated")
    ap.add_argument("--counts", type=Path, default=None, help="write per-cell counts CSV here")
    args = ap.parse_args(argv)

    constructs = load_constructs(args.constructs)
    extra = [tuple(x.split(",")) for x in args.extra_triples.split(";") if x.strip()]
    rows, cells, counts, errors, fam = generate(constructs, args.seed, args.min, extra)

    infeasible = [c for c in cells if counts[c] == 0]
    short = [c for c in cells if 0 < counts[c] < args.min]
    out_rows = rows
    have = counts
    if args.sample:
        out_rows, have = stratified_sample(rows, cells, fam, args.sample, args.seed)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        for row in out_rows:
            f.write(json.dumps(row, ensure_ascii=True) + "\n")
    counts_path = args.counts or args.out.with_suffix(".cells.csv")
    with open(counts_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("cell,kind,rows_full,rows_written,status,reason\n")
        for c in cells:
            status = "infeasible" if c in infeasible else "short" if c in short else "ok"
            reason = ""
            if c in errors and status != "ok":
                reason = max(errors[c].items(), key=lambda kv: kv[1])[0].replace(",", ";").replace("\n", " ")
            kind = "triple" if c[0] in ("family", "ids") else "pair"
            f.write(f"{cell_name(c)},{kind},{counts[c]},{have[c]},{status},{reason}\n")
    feasible = [c for c in cells if c not in infeasible]
    print(f"{len(rows)} rows generated (seed {args.seed}); wrote {len(out_rows)} to {args.out}")
    print(f"cells: {len(cells)} ({sum(1 for c in cells if c[0] not in ('family', 'ids'))} pairs, "
          f"{sum(1 for c in cells if c[0] in ('family', 'ids'))} triples); feasible {len(feasible)}, "
          f"infeasible {len(infeasible)}; min rows per feasible cell {min(counts[c] for c in feasible)}")
    for c in infeasible:
        reason = max(errors.get(c, {"no attempt": 1}).items(), key=lambda kv: kv[1])[0]
        print(f"  INFEASIBLE {cell_name(c)}: {reason}")
    for c in short:
        reason = max(errors.get(c, {"?": 1}).items(), key=lambda kv: kv[1])[0]
        print(f"  SHORT {cell_name(c)}: {counts[c]} rows; most common rejection: {reason}")
    return 1 if short else 0


if __name__ == "__main__":
    sys.exit(main())
