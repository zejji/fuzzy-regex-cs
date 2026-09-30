"""Which constructs does a row REALLY contain? Confirmed by upstream's own parser.

`tools/interaction-matrix.py` tags a row by matching regexes over its pattern text. That is fast, but
text is not structure: `(?(DEFINE)...)` reads as a conditional, a `{` inside a set is masked by hand,
and a pattern the engine rejects is tagged anyway. This module parses the pattern with regex
2026.9.10's parser (`regex._regex_core`, the same code `regex.compile` runs, before optimisation so
nothing is folded away) and tags by node type, so a tag means the construct is in the parse tree.

    confirmed(row)  -> set of construct ids (tools/matrix/constructs.json)
    text_tags(row)  -> interaction-matrix.py's tagger, mapped onto the same ids

`python tools/matrix/tagger.py ROWS.jsonl` prints every row where the two disagree, per id.
"""

from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

import regex
from regex import _regex_core as core

_HERE = Path(__file__).resolve().parent

FLAG_IDS = {
    "flag-b": regex.BESTMATCH,
    "flag-e": regex.ENHANCEMATCH,
    "flag-r": regex.REVERSE,
    "flag-i": regex.IGNORECASE,
    "flag-f": regex.FULLCASE,
    "flag-V1": regex.VERSION1,
    "flag-w": regex.WORD,
    "flag-p": regex.POSIX,
}
OPERATION_IDS = {"search": "op-search", "match": "op-match", "fullmatch": "op-fullmatch", "finditer": "op-finditer"}

_ANCHORS = (core.StartOfString, core.StartOfLine, core.StartOfLineU, core.EndOfLine, core.EndOfLineU,
            core.EndOfString, core.EndOfStringLine, core.EndOfStringLineU)
_WORD_BOUNDARIES = (core.Boundary, core.DefaultBoundary, core.StartOfWord, core.EndOfWord,
                    core.DefaultStartOfWord, core.DefaultEndOfWord)


class NotParsed(Exception):
    pass


def parse(pattern: str, flags: int = 0, named_lists: dict | None = None):
    """Upstream's parse tree of `pattern` and its Info, exactly as `regex._main._compile` builds them."""
    core.DEFAULT_VERSION = regex.DEFAULT_VERSION
    global_flags = flags
    kwargs = {k: v for k, v in (named_lists or {}).items()}
    while True:
        try:
            source = core.Source(pattern)
            info = core.Info(global_flags, source.char_type, kwargs)
            info.guess_encoding = core.UNICODE
            source.ignore_space = bool(info.flags & regex.VERBOSE)
            tree = core._parse_pattern(source, info)
            if not source.at_end():
                raise NotParsed("unbalanced parenthesis")
            return tree, info
        except core._UnscopedFlagSet:
            global_flags = info.global_flags
        except core.error as e:
            raise NotParsed(str(e)) from e


def _nodes(node):
    stack = [node]
    while stack:
        n = stack.pop()
        if isinstance(n, core.RegexBase):
            yield n
            for v in vars(n).values():
                if isinstance(v, (core.RegexBase, list, tuple)):
                    stack.append(v)
        elif isinstance(n, (list, tuple)):
            stack.extend(n)


def _lookaround_id(behind: bool, positive: bool) -> str:
    return ("lookbehind" if behind else "lookahead") if positive else ("neg-lookbehind" if behind else "neg-lookahead")


def pattern_ids(pattern: str, flags: int = 0, named_lists: dict | None = None) -> set[str]:
    tree, info = parse(pattern, flags, named_lists)
    ids: set[str] = set()
    for n in _nodes(tree):
        t = type(n)
        if t is core.Fuzzy:
            ids.add("fuzzy")
            c = n.constraints
            if any(isinstance(c.get(k), tuple) and (c[k][0] or 0) > 0 for k in "eids"):
                ids.add("fuzzy-min")
            cost = c.get("cost") or {}
            # A cost equation is written with an explicit weight: {2i+1d<=3}. The plain {e<=n}
            # form stores a unit cost of its own, so a weight other than 1, or a cost max not
            # equal to e's max, is what distinguishes the written equation.
            e_max = c.get("e", (0, None))[1] if isinstance(c.get("e"), tuple) else None
            if cost and (any(cost.get(k, 1) not in (0, 1) for k in "ids") or cost.get("max") != e_max):
                ids.add("fuzzy-cost")
        elif t is core.CallGroup:
            ids.add("call")
        elif t is core.LookAround:
            ids.add(_lookaround_id(n.behind, n.positive))
        elif t is core.LookAroundConditional:
            # The test IS a lookaround, run for its answer: count both.
            ids.add("conditional")
            ids.add(_lookaround_id(n.behind, n.positive))
        elif t is core.Conditional:
            # (?(DEFINE)...) parses as a conditional on a group named DEFINE that never matches,
            # which is a definition, not a test. interaction-matrix.py's text tagger counted it.
            if n.group != "DEFINE":
                ids.add("conditional")
        elif t is core.Atomic:
            ids.add("atomic")
        elif t is core.PossessiveRepeat:
            ids.add("possessive")
        elif t is core.LazyRepeat:
            ids.add("lazy")
        elif t is core.GreedyRepeat:
            ids.add("repeat")
        elif t in (core.Prune, core.Skip, core.Failure):
            ids.add("verb")
        elif t is core.Group:
            ids.add("capture")
        elif t is core.Branch:
            ids.add("alternation")
        elif t is core.RefGroup:
            ids.add("backref")
        elif t is core.SearchAnchor:
            ids.add("search-anchor")
        elif t is core.Keep:
            ids.add("keep")
        elif isinstance(n, _ANCHORS):
            ids.add("anchor")
        elif isinstance(n, _WORD_BOUNDARIES):
            ids.add("word-boundary")
        elif isinstance(n, core.SetBase):
            ids.add("set")
        elif t is core.StringSet:
            ids.add("named-list")
    # A branch reset has no node of its own: it is a Branch whose alternatives reuse group numbers.
    # The parser's Info records each group's name and number; the text tells us it was (?|.
    masked = _masked(pattern)
    if "alternation" in ids and "(?|" in masked:
        ids.add("branch-reset")
    # The parser reduces a one-member set to its member: `[[:digit:]]` is a Property node and `[^a]`
    # a negated Character. It still went through parse_set, so a bracket in the masked text of a
    # pattern that parsed is a set.
    if "[" in masked:
        ids.add("set")
    return ids


def _masked(pattern: str) -> str:
    return _text_tagger()._masked(pattern)


def confirmed(row: dict) -> set[str]:
    """The construct ids of a row: its pattern's parse, the compiled flags, and the operation."""
    named = row.get("namedLists") or {}
    flags = row.get("flags") or 0
    ids = pattern_ids(row["pattern"], flags, named)
    compiled = regex.compile(row["pattern"], flags, **named)
    ids |= {k for k, bit in FLAG_IDS.items() if compiled.flags & bit}
    # FULLCASE does nothing without IGNORECASE (regex README "Full case-folding"), so it only counts
    # when the pattern is also case-insensitive.
    if "flag-f" in ids and "flag-i" not in ids:
        ids.discard("flag-f")
    ids.add(OPERATION_IDS[row["operation"]])
    if row.get("partial"):
        ids.add("partial")
    if row.get("pos") is not None or row.get("endpos") is not None:
        ids.add("slice")
    return ids


_TEXT = None


def _text_tagger():
    global _TEXT
    if _TEXT is None:
        spec = importlib.util.spec_from_file_location("interaction_matrix", _HERE.parent / "interaction-matrix.py")
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        _TEXT = mod
    return _TEXT


def text_tags(row: dict) -> set[str]:
    tags = _text_tagger().constructs(row)
    # Ids the text tagger has that the canonical list does not, and the reverse mapping.
    return {t for t in tags if t not in ("fuzzy-literal",)}


def main(argv: list[str]) -> int:
    rows = [json.loads(line) for line in open(argv[0], encoding="utf-8") if line.strip()]
    wrong: dict[str, list] = {}
    for row in rows:
        try:
            parsed = confirmed(row)
        except Exception:  # noqa: BLE001 - a row the engine rejects has no constructs
            continue
        text = text_tags(row)
        for tag in (text ^ parsed):
            wrong.setdefault(("text-only " if tag in text else "parser-only ") + tag, []).append(row["pattern"])
    for key, pats in sorted(wrong.items()):
        print(f"{len(pats):6}  {key}   e.g. {ascii(pats[0])}")
    return 1 if wrong else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
