"""Check C8: TRE, the second fuzzy engine, on the matrix rows it can be asked about.

    python tools/matrix/c8.py CHUNK.jsonl OUT.jsonl

For each row it writes {"id", "c8"}: "n/a: <reason>" when the row is outside TRE's reach, else
TRE's answer {"matched", "cost", "span", "weights", "best"}, or "ERR ..." when TRE refused the
pattern or WSL failed. All the rows of a chunk go to TRE in one WSL process (a `wsl` start costs
about 200 ms), through tools/probes/tre-fuzzy-check.py's venv.

WHAT TRE'S ANSWER IS WORTH, measured 2026-09-30 with TRE 0.8.0 against upstream regex 2026.9.10.

TRE reports one match per text, chosen by its own rule, and that rule is neither upstream's nor
"the cheapest": `abc` {e<=1} over 'abx abc' is (4,7) cost 0 in TRE and (0,3) cost 1 upstream,
but `a+` {e<=1} over 'baaa' is (0,4) cost 1 in TRE although (1,4) costs 0. Worse, TRE can MISS a
match: `a+b` {s<=1} over 'baaax' is None in TRE, but (1,5) with one substitution matches
(upstream and the reference agree); `a+bc` {e<=1} over 'baaaxc' is None too, against (1,6). Its
automaton seems to keep one path per state, the earlier start, whatever that path has spent. So a
TRE "None" says nothing, and TRE's cost is not a minimum. What survives is that a match TRE does
report is a real alignment within the budget. Hence two one-sided checks:

  existence  TRE found a match, so one exists, and a search (or a match anchored at the start,
             asked of TRE as `^BODY`) must find one too. A row TRE finds nothing on is n/a.
  BESTMATCH  (?b) returns the lowest-cost match (answer key A2 "flag-b"), so its cost can be no
             higher than the cost of the match TRE found.
Spans, groups and every other cost are never compared.

What is refused, and why:
  fullmatch  TRE lets no insertion stand in front of `$`: `^abc$` {e<=1} over 'abcx' is None in
             TRE, (0,4) with one insertion upstream (reference rule 10: a failing anchor in a
             fuzzy section may be passed by an insertion). For the same reason a `$` anywhere in
             the body is refused.
  ^ in a search  TRE lets an insertion follow `^` at the start, so `^ab` {e<=1} over 'xab' is
             (0,3) in TRE; a search may not take an insertion where it began (reference rule 5),
             and upstream answers None. A `match` has no such rule, so there `^` is kept (and
             `(?:ab){e<=1}` match 'xab' is (0,3) with one insertion in both).
  shape      the POSIX ERE core only: literals, `.`, classes, groups, `|` and greedy
             `* + ? {m,n}`; no backslash, no lazy or possessive repeat, no other `(?...)` group, no
             verb; and no minimum in a budget (TRE has none).
  sections   A budget over the whole pattern goes to TRE as its search parameters, cost equation
             included. Other budgets are written into the pattern in TRE's own syntax, `(bc){#1}`
             (at most one substitution), `{+n}`, `{-n}`, `{~n}`, with the rest exact. TRE's
             defaults there are rule 7's: once one kind is named the others are forbidden
             (`a(bc){#1}d` does not match 'abxcd' or 'ad'), and `{~2}` alone allows every kind.
             TRE allows fewer alignments than regex: no insertion at a section's end
             (`a(bc){+1}d` does not match 'abcxd', which reference rule 4 allows), so a match TRE
             reports is still one regex must find. Refused: nested sections, a cost equation in a
             section (TRE reads `{2i+1d<3}` as an error without spaces), a section with no bound.
  flags      IGNORECASE is TRE's ICASE, and FULLCASE agrees with it over ASCII text; ENHANCEMATCH
             and POSIX change which match is reported, never whether one exists; BESTMATCH gets its
             cost bound except under finditer (what (?b) means for later matches is not written
             down); REVERSE only for search (a reversed `match` is anchored at the end, and TRE's
             search is forwards, which existence does not depend on). Any other flag is refused.
  finditer   only whether it finds anything, which is whether a search from the start does.
  text       a newline with `.` in the body (the two engines differ, the probe's gate);
             non-ASCII text or pattern under IGNORECASE.
Budgets are read with the reference matcher's rule 7 (parse_limits: an unnamed kind is forbidden
once any kind is named; a cost equation permits only the kinds it names), and passed to TRE as
per-kind maxima, a total, weights and a maximum cost.
"""

from __future__ import annotations

import importlib.util
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TRE_MAX = 2**31 - 1
IGNORECASE, FULLCASE, BESTMATCH, ENHANCEMATCH, REVERSE, POSIX = 2, 16384, 4096, 32768, 1024, 65536
ALLOWED_FLAGS = IGNORECASE | FULLCASE | BESTMATCH | ENHANCEMATCH | REVERSE | POSIX


def _load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_ref = _load(HERE.parent / "probes" / "fuzzy-reference-matcher.py", "fuzzy_reference_c8")
_WHOLE = re.compile(r"^\((?:\?:)?(?P<body>.*)\)\{(?P<budget>[^{}]*[a-z][^{}]*)\}$")
_CORE = re.compile(r"^[A-Za-z0-9 \[\]()|.^*+?:,{}-]*$")


def _nests(body: str) -> bool:
    depth = 0
    for ch in body:
        depth += (ch == "(") - (ch == ")")
        if depth < 0:
            return False
    return depth == 0


def _tre_limits(lim) -> str:
    """A section's limits in TRE's inline syntax (see "sections" above)."""
    if any(lim.mins) or lim.max_cost != float("inf") or lim.costs != (1, 1, 1):
        raise ValueError("a minimum or a cost equation in a section")
    inf = float("inf")
    kinds, total = lim.maxs[:3], lim.maxs[3]
    if all(n == inf for n in kinds):  # no kind named: TRE's `{~n}` allows every kind
        if total == inf:
            raise ValueError("a section with no bound")
        return "{~" + str(int(total)) + "}"
    if inf in kinds:  # rule 7 never leaves a kind unbounded once one is named
        raise ValueError("a section with an unbounded kind beside a named one")
    return "{" + "".join(f"{sign}{int(n)}" for sign, n in zip("#+-", kinds)) + (
        "" if total == inf else f"~{int(total)}") + "}"


def _tre_form(node, op: str, in_fuzzy: bool = False) -> str:
    """The reference parser's tree as a TRE pattern, or ValueError for anything TRE cannot say."""
    r = _ref
    if isinstance(node, r.Item):
        if node.text.startswith("\\"):
            raise ValueError("a class shorthand")
        return node.text
    if isinstance(node, r.Seq):
        return "".join(_tre_form(n, op, in_fuzzy) for n in node.parts)
    if isinstance(node, r.Alt):
        return "(" + "|".join(_tre_form(n, op, in_fuzzy) for n in node.branches) + ")"
    if isinstance(node, r.Group):
        return "(" + _tre_form(node.body, op, in_fuzzy) + ")"
    if isinstance(node, r.Repeat):
        if not node.greedy:
            raise ValueError("a lazy repeat")
        hi = "" if node.hi == float("inf") else str(int(node.hi))
        return "(" + _tre_form(node.body, op, in_fuzzy) + ")" + "{" + f"{node.lo},{hi}" + "}"
    if isinstance(node, r.Fuzzy):
        if in_fuzzy:
            raise ValueError("nested sections")
        return "(" + _tre_form(node.body, op, True) + ")" + _tre_limits(node.limits)
    if isinstance(node, r.Anchor) and node.kind == "^" and op == "match":
        return "^"
    raise ValueError(f"{type(node).__name__} has no TRE form here")


def _sections(pattern: str, op: str) -> str:
    """The pattern with its budgets written in TRE's syntax, or ValueError."""
    tree = _ref.Parser(pattern).parse()
    if not _ref.contains(tree, _ref.Fuzzy):
        raise ValueError("no budget")
    return ("^" if op == "match" else "") + _tre_form(tree, op)


def dialect(row: dict) -> dict | str:
    """The TRE job for a row (without its number), or the reason it is refused."""
    op, flags, subject = row["operation"], row["flags"], row["subject"]
    if op not in ("search", "match", "finditer"):
        return f"operation {op} (fullmatch needs an insertion before $, which TRE refuses)"
    if row.get("partial") or row.get("namedLists") or row.get("pos") is not None:
        return "partial, named list or slice"
    if flags & ~ALLOWED_FLAGS or (flags & FULLCASE and not flags & IGNORECASE):
        return f"flags {flags}"
    if flags & REVERSE and op != "search":
        return "REVERSE on an anchored operation"
    pattern = re.sub(r"\(\?P<\w+>", "(", row["pattern"])
    whole = _WHOLE.match(pattern)
    if whole is None or not _nests(whole.group("body")) or "{" in re.sub(r"\{[0-9,]*\}", "", whole.group("body")):
        # Not one budget over everything: write the budgets into the pattern ("sections").
        # The structure is checked by _tre_form on the reference's parse; only what a parse cannot
        # show is checked here: a backslash (an escaped `.` would parse to the literal and be
        # written back as TRE's any-character) and the text gates.
        if "\\" in pattern:
            return "sections: a backslash"
        if "\n" in subject and "." in pattern:
            return "a newline in the text with ."
        if flags & IGNORECASE and not (subject.isascii() and pattern.isascii()):
            return "non-ASCII under IGNORECASE"
        try:
            body = _sections(pattern, op)
        except (ValueError, IndexError, StopIteration) as e:
            return f"sections: {str(e)[:50]}"
        return {"body": body, "subject": subject, "fuzzyness": {"maxerr": 0}, "icase": bool(flags & IGNORECASE),
                "weights": [1, 1, 1], "best": bool(flags & BESTMATCH) and op != "finditer"}
    body = whole.group("body")
    # Tested on the whole pattern: the budget regex has already taken the opening "(" off the body,
    # so `(?x:a b c){e<=1}` would leave "?x:a b c" (blind review, 2026-09-30).
    if (not _CORE.match(body) or re.search(r"\(\?(?!:)", pattern) or re.search(r"[*+?}][?+]", body)
            or "(*" in body):
        return "the body leaves the POSIX ERE core"
    if re.search(r"\{(?![0-9]+(?:,[0-9]*)?\})", body):
        return "a brace in the body that is not a bounded repeat (a second budget)"
    if "$" in body:
        return "a $ (TRE lets no insertion stand in front of it)"
    if "^" in body and op != "match":
        return "a ^ in a search (TRE lets an insertion follow it where the search began)"
    if "\n" in subject and any(ch in body for ch in ".^"):
        return "a newline in the text with . or ^"
    if flags & IGNORECASE and not (subject.isascii() and body.isascii()):
        return "non-ASCII under IGNORECASE"
    try:
        limits = _ref.parse_limits(whole.group("budget").split(","))
    except (ValueError, StopIteration):
        return "a budget the reference cannot read"
    if any(limits.mins):
        return "a minimum, which TRE has no form for"
    cap = lambda n: TRE_MAX if n == float("inf") else int(n)  # noqa: E731
    fz = {"maxsub": cap(limits.maxs[0]), "maxins": cap(limits.maxs[1]), "maxdel": cap(limits.maxs[2]),
          "maxerr": cap(limits.maxs[3]), "subcost": limits.costs[0] or 1, "inscost": limits.costs[1] or 1,
          "delcost": limits.costs[2] or 1, "maxcost": cap(limits.max_cost)}
    body = body.replace("(?:", "(")
    # A `match` anchors the whole body: `^ab|cd` would anchor only the first alternative.
    return {"body": ("^(" + body + ")" if op == "match" else body), "subject": subject, "fuzzyness": fz,
            "icase": bool(flags & IGNORECASE), "weights": list(limits.costs),
            "best": bool(flags & BESTMATCH) and op != "finditer"}


def ask(jobs: list[dict]) -> dict:
    """{number: TRE's answer}, one WSL process for all of them."""
    probe = _load(HERE.parent / "probes" / "tre-fuzzy-check.py", "tre_fuzzy_check")
    here = Path(__file__).resolve()
    inside = "/mnt/" + here.drive[0].lower() + here.as_posix()[2:]
    import subprocess  # noqa: PLC0415
    done = subprocess.run(["wsl", "-d", "Ubuntu", "--", probe.WSL_PYTHON, inside, "--in-wsl"],
                          input=json.dumps(jobs), capture_output=True, text=True, encoding="utf-8",
                          check=False, timeout=600)
    if done.returncode == 139:
        # TRE itself segfaults on some patterns: measured 2026-09-30 on a bounded repeat round a
        # section, `(((ab)){~2}){1,2}` and `(((([ab]x)){#0+1-0})){2,2}`. Halve the batch until the
        # job that kills it answers "error" (unanswered, so the row is n/a).
        if len(jobs) == 1:
            return {jobs[0]["number"]: {"number": jobs[0]["number"], "error": "TRE crashed (segfault)"}}
        half = len(jobs) // 2
        return {**ask(jobs[:half]), **ask(jobs[half:])}
    if done.returncode != 0:
        raise RuntimeError(f"WSL side failed ({done.returncode}): {done.stderr[-500:]}")
    return {a["number"]: a for a in json.loads(done.stdout)}


def in_wsl() -> int:
    import tre  # noqa: PLC0415 - only inside WSL
    out = []
    for job in json.load(sys.stdin):
        try:
            m = tre.compile(job["body"], tre.EXTENDED | (tre.ICASE if job["icase"] else 0)).search(
                job["subject"], tre.Fuzzyness(**job["fuzzyness"]))
            out.append({"number": job["number"], "matched": m is not None, "cost": None if m is None else m.cost,
                        "span": None if m is None else list(m.groups()[0])})
        except Exception as e:  # noqa: BLE001 - an engine that refuses the pattern is an answer
            out.append({"number": job["number"], "error": f"{type(e).__name__}: {e}"})
    print(json.dumps(out))
    return 0


def main(rows_path: str, out_path: str) -> int:
    rows = [json.loads(line) for line in open(rows_path, encoding="utf-8") if line.strip()]
    jobs, answers = [], {}
    for n, row in enumerate(rows):
        job = dialect(row)
        if isinstance(job, str):
            answers[n] = "n/a: " + job
        else:
            jobs.append({"number": n, **job})
    try:
        tre_answers = ask(jobs) if jobs else {}
    except Exception as e:  # noqa: BLE001 - the whole chunk goes unanswered, never judged
        tre_answers = {j["number"]: {"error": f"WSL {type(e).__name__}"} for j in jobs}
    for job in jobs:
        a = tre_answers.get(job["number"], {"error": "no answer"})
        answers[job["number"]] = ("ERR " + a["error"][:80] if "error" in a else
                                  {"matched": a["matched"], "cost": a["cost"], "span": a["span"],
                                   "weights": job["weights"], "best": job["best"]})
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        for n, row in enumerate(rows):
            f.write(json.dumps({"id": row.get("id", n), "c8": answers[n]}) + "\n")
    return 0


def verdict(row: dict, base: str | None, tre: object) -> tuple:
    """(status, kind, port, TRE) for check C8, from the port's base answer."""
    if tre is None or isinstance(tre, str):
        return ("n/a", tre[:60] if isinstance(tre, str) else "", None, None)
    if not tre["matched"]:
        return ("n/a", "TRE found no match (TRE can miss one)", base, "TRE None")
    if base is None or not (base == "None" or base.startswith("(") or base.startswith("[")):
        return ("n/a", "base unanswered", base, None)
    first = base[1:-1].split(" ; ")[0] if base.startswith("[") else base
    other = f"TRE cost {tre['cost']} at {tuple(tre['span'])}"
    if first in ("None", ""):
        return ("fail", "no match where TRE found one", "None", other)
    counts = [int(n) for n in first.split(" ")[1].split(",")]
    cost = sum(c * (w or 1) for c, w in zip(counts, tre["weights"]))
    if tre["best"] and cost > tre["cost"]:
        return ("fail", "BESTMATCH costs more than a match TRE found", first, other)
    return ("pass", "", first, other)


if __name__ == "__main__":
    if "--in-wsl" in sys.argv:
        sys.exit(in_wsl())
    sys.exit(main(sys.argv[1], sys.argv[2]))
