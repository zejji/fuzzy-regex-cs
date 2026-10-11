"""Which pairs of regex features do the oracle generators actually exercise together?

Tags every row the generators of `tools/record-oracle.py` produce with the constructs its pattern,
flags and operation use, then counts the rows for every PAIR of constructs. A pair with no rows is
a combination no oracle wave can have tested, whatever it reports: the blind spot that let the
failed-call memo ship without a single `\\G` row (docs/VERIFICATION.md rule 11).

Most faults need only two interacting factors to show (Kuhn, Wallace and Gallo, "Software fault
interactions and implications for software testing", IEEE TSE 30(6), 2004), so covering every pair
on purpose is the organised alternative to finding the next combination one review at a time.

Run (a few seconds; no upstream calls, the generators only):

    python tools/interaction-matrix.py                  # all default generators, seeds 7 and 4242
    python tools/interaction-matrix.py --count 2000 --seeds 7,4242,20260927
    python tools/interaction-matrix.py --generator matrix --zeros-only

It prints the zero cells and a per-construct row count, and exits 1 when a pair that is not in
IMPOSSIBLE has no rows, so a wave run can refuse to call itself green.
"""

import argparse
import collections
import importlib.util
import itertools
import random
import re
import sys
from pathlib import Path

_HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("record_oracle", _HERE / "record-oracle.py")
record_oracle = importlib.util.module_from_spec(_spec)
sys.modules["record_oracle"] = record_oracle
_spec.loader.exec_module(record_oracle)

# VERSION0 is regex's default, so every row without V1 is a V0 row and V0 is not a construct.
# regex's flag values (regex 2026.9.10: regex.BESTMATCH == 4096 and so on).
_FLAG_CONSTRUCTS = {
    "flag-b": 4096,
    "flag-e": 32768,
    "flag-r": 1024,
    "flag-i": 2,
    "flag-f": 16384,
    "flag-V1": 256,
    "flag-w": 2048,
    "flag-p": 65536,
}
_INLINE_FLAG_LETTERS = {"b": "flag-b", "e": "flag-e", "r": "flag-r", "i": "flag-i", "f": "flag-f",
                        "w": "flag-w", "p": "flag-p"}

# Constructs found in the pattern text once escapes and set contents are masked. Each is a
# (name, regex) pair over the masked text.
_PATTERN_CONSTRUCTS = (
    ("fuzzy", r"\{[^{}]*(?:[eids]\s*<|<\s*[eids]|\d[ids]\s*\+)|\{\s*[eids]\s*\}"),
    ("fuzzy-min", r"\{\s*\d+\s*<=?\s*[eids]"),
    ("fuzzy-cost", r"\{[^{}]*\d[ids]\s*\+"),
    ("call", r"\(\?(?:R|[+-]?\d+|&\w+|P>\w+)\)"),
    ("lookahead", r"\(\?="),
    ("neg-lookahead", r"\(\?!"),
    ("lookbehind", r"\(\?<="),
    ("neg-lookbehind", r"\(\?<!"),
    ("atomic", r"\(\?>"),
    # `(?` opens a group and `(*` a verb, so a `?` or `*` straight after `(` is no quantifier. Until
    # 2026-09-30 every `(?:` counted as a repeat: 994 of 2,836 generator rows were tagged `repeat`
    # with no repeat in their parse tree (tools/matrix/tagger.py, which checks this tagger).
    # `{1}` and `{1,1}` are no repeat at all: the parser drops them.
    ("possessive", r"(?:(?<!\()[*+?]|(?<!\{1)(?<!\{1,1)\})\+"),
    ("lazy", r"(?:(?<!\()[*+?]|(?<!\{1)(?<!\{1,1)\})\?"),
    # Greedy only: a quantifier followed by `?` is lazy and by `+` possessive.
    ("repeat", r"(?<![(*+?}])[*+?](?![?+])|\{(?!1\}|1,1\})\d+(?:,\d*)?\}(?![?+])"),
    ("verb", r"\(\*[A-Z]"),
    # (?(DEFINE)...) is a definition, not a test.
    ("conditional", r"\(\?\((?!DEFINE\))"),
    ("capture", r"\((?![?*])|\(\?P?<(?![=!])\w+>"),
    ("branch-reset", r"\(\?\|"),
    ("alternation", r"\|"),
    ("anchor", r"[\^$]"),
    ("set", r"\["),
)
# Constructs that are escapes, matched before masking.
_ESCAPE_CONSTRUCTS = (
    ("search-anchor", r"\\G"),
    ("keep", r"\\K"),
    # Escapes, so masking hid them from the pattern constructs until 2026-09-30.
    ("anchor", r"\\[AZ]"),
    ("backref", r"\\[1-9]|\\g<|\(\?P=\w+\)"),
    ("word-boundary", r"\\[bBmM]"),
    ("named-list", r"\\L<"),
)

# Pairs no row can hold because the engine rejects the combination at compile time. Each needs
# a reason; an entry without one is a blind spot being hidden.
IMPOSSIBLE = {
    frozenset({"op-search", "op-match"}): "one operation per row",
    frozenset({"op-search", "op-fullmatch"}): "one operation per row",
    frozenset({"op-match", "op-fullmatch"}): "one operation per row",
    frozenset({"flag-b", "flag-e"}): "regex raises 'BESTMATCH and ENHANCEMATCH are mutually exclusive'",
}


def _masked(pattern: str) -> str:
    """The pattern with every escape and every set's contents replaced by 'x'."""
    out, i, depth = [], 0, 0
    while i < len(pattern):
        c = pattern[i]
        if c == "\\" and i + 1 < len(pattern):
            if not depth:
                out.append("x")
            i += 2
            # The argument of \p{..}, \N{..}, \x{..}, \g<..> and \L<..> belongs to the escape, so a
            # `^` or `+` inside it is not syntax: `\p{^Nd}` is no anchor, `\p{Ll}+` no possessive.
            if i < len(pattern) and pattern[i] in "{<" and pattern[i - 1] in "pPNxgLu":
                close = pattern.find("}" if pattern[i] == "{" else ">", i)
                if close > 0:
                    i = close + 1
            continue
        if depth:
            if c == "[":
                depth += 1
            elif c == "]":
                depth -= 1
                if depth == 0:
                    out.append("]")
            i += 1
            continue
        if c == "[":
            depth = 1
            out.append("[")
            j = i + 1
            if j < len(pattern) and pattern[j] == "^":
                j += 1
            if j < len(pattern) and pattern[j] == "]":
                j += 1
            i = j
            continue
        out.append(c)
        i += 1
    return "".join(out)


def _has_alternation(masked: str) -> bool:
    """Whether a `|` separates alternatives, rather than a conditional's yes and no branches.

    A conditional `(?(test)yes|no)` holds one `|` of its own; a second one at the same level is an
    alternation in the no branch.
    """
    conditional, bars = [], []
    for i, c in enumerate(masked):
        if c == "(":
            conditional.append(masked.startswith("(?(", i))
            bars.append(0)
        elif c == ")" and conditional:
            conditional.pop()
            bars.pop()
        elif c == "|":
            if conditional and conditional[-1] and bars[-1] == 0:
                bars[-1] = 1
                continue
            return True
    return False


def constructs(row: dict) -> set[str]:
    pattern = row["pattern"]
    tags = {name for name, rx in _ESCAPE_CONSTRUCTS if re.search(rx, pattern)}
    masked = _masked(pattern)
    # A fuzzy constraint's own text ({e<=3,1i+1d<=2}) holds `+` and `<`, which are no quantifier.
    unfuzzed = re.sub(r"\{[^{}]*<[^{}]*\}", "{}", masked)
    tags |= {name for name, rx in _PATTERN_CONSTRUCTS
             if re.search(rx, masked if name.startswith("fuzzy") or name == "set" else unfuzzed)}
    if "alternation" in tags and not _has_alternation(masked):
        tags.discard("alternation")
    flags = row.get("flags") or 0
    tags |= {name for name, bit in _FLAG_CONSTRUCTS.items() if flags & bit}
    for scoped in re.findall(r"\(\?([a-zA-Z0-9-]+)[:)]", masked):
        if scoped.startswith("V1"):
            tags.add("flag-V1")
        tags |= {_INLINE_FLAG_LETTERS[c] for c in scoped.split("-")[0] if c in _INLINE_FLAG_LETTERS}
    # Version 1 folds case fully whenever it ignores case (regex README, "Case-insensitive
    # matching"); the compiled pattern carries FULLCASE.
    if {"flag-V1", "flag-i"} <= tags:
        tags.add("flag-f")
    tags.add("op-" + row["operation"])
    if row.get("partial"):
        tags.add("partial")
    if row.get("pos") or row.get("endpos") is not None:
        tags.add("slice")
    return tags


def _rows(generators, seeds, count):
    for seed in seeds:
        for name in generators:
            yield from record_oracle._generate(name, random.Random(f"{seed}:{name}"), count)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    default = [g for g in record_oracle.GENERATORS if g not in record_oracle.LONG_BASES]
    parser.add_argument("--generator", default=",".join(default))
    parser.add_argument("--seeds", default="7,4242")
    parser.add_argument("--count", type=int, default=300, help="rows per generator per seed")
    parser.add_argument("--zeros-only", action="store_true")
    args = parser.parse_args(argv)

    generators = [g.strip() for g in args.generator.split(",") if g.strip()]
    seeds = [int(s) for s in args.seeds.split(",")]
    single, pairs, total = collections.Counter(), collections.Counter(), 0
    for row in _rows(generators, seeds, args.count):
        tags = constructs(row)
        total += 1
        single.update(tags)
        pairs.update(frozenset(p) for p in itertools.combinations(sorted(tags), 2))

    names = sorted({n for n, _ in _PATTERN_CONSTRUCTS} | {n for n, _ in _ESCAPE_CONSTRUCTS}
                   | set(_FLAG_CONSTRUCTS) | {"op-search", "op-match", "op-fullmatch", "op-finditer", "partial", "slice"})
    zeros = [p for p in map(frozenset, itertools.combinations(names, 2))
             if pairs[p] == 0 and p not in IMPOSSIBLE]
    print(f"{total} rows, {len(names)} constructs, {len(names) * (len(names) - 1) // 2} pairs, "
          f"{len(zeros)} with no rows")
    if not args.zeros_only:
        for name in names:
            print(f"  {single[name]:>7}  {name}")
    for pair in sorted(zeros, key=sorted):
        print("  ZERO " + " + ".join(sorted(pair)))
    return 1 if zeros else 0


if __name__ == "__main__":
    sys.exit(main())
